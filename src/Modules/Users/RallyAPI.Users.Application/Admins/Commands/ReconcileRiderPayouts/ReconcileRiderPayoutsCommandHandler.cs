using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Utilities.Payouts;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Commands.ReconcileRiderPayouts;

public sealed class ReconcileRiderPayoutsCommandHandler
    : IRequestHandler<ReconcileRiderPayoutsCommand, Result<ReconcileRiderPayoutsResult>>
{
    private readonly IRiderPayoutLedgerRepository _ledgerRepository;
    private readonly IRiderPayoutExportBatchRepository _batchRepository;
    private readonly IRiderRepository _riderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReconcileRiderPayoutsCommandHandler> _logger;

    public ReconcileRiderPayoutsCommandHandler(
        IRiderPayoutLedgerRepository ledgerRepository,
        IRiderPayoutExportBatchRepository batchRepository,
        IRiderRepository riderRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReconcileRiderPayoutsCommandHandler> logger)
    {
        _ledgerRepository = ledgerRepository;
        _batchRepository = batchRepository;
        _riderRepository = riderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ReconcileRiderPayoutsResult>> Handle(
        ReconcileRiderPayoutsCommand request,
        CancellationToken ct)
    {
        var batch = await _batchRepository.GetByIdAsync(request.ExportBatchId, ct);
        if (batch is null)
            return Result.Failure<ReconcileRiderPayoutsResult>(
                Error.NotFound("RiderPayoutExportBatch", request.ExportBatchId));

        IReadOnlyList<IciciReconciliationRow> rows;
        try
        {
            rows = IciciReconciliationParser.Parse(request.FileStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse ICICI reconciliation report for batch {BatchId}", request.ExportBatchId);
            return Result.Failure<ReconcileRiderPayoutsResult>(
                Error.Validation("The uploaded file could not be parsed as an ICICI Consolidated Status Report.", ex.Message));
        }

        if (rows.Count == 0)
            return Result.Failure<ReconcileRiderPayoutsResult>(
                Error.Validation("The uploaded file has no data rows."));

        var batchLedgers = await _ledgerRepository.GetByExportBatchIdAsync(request.ExportBatchId, ct);
        if (batchLedgers.Count == 0)
            return Result.Failure<ReconcileRiderPayoutsResult>(
                Error.Validation($"No payouts are stamped with export batch {request.ExportBatchId}."));

        // RiderPayoutLedger doesn't store bank details (export reads them live) — re-fetch
        // live so we can build the same (account, IFSC, amount) match key used at export time.
        var riderIds = batchLedgers.Select(p => p.RiderId).Distinct().ToList();
        var bankDetailsByRider = await _riderRepository.GetBankDetailsByIdsAsync(riderIds, ct);

        var processingByKey = batchLedgers
            .Where(p => p.Status == RiderPayoutStatus.Processing)
            .Where(p => bankDetailsByRider.ContainsKey(p.RiderId))
            .ToLookup(p =>
            {
                var bank = bankDetailsByRider[p.RiderId];
                return (bank.BankAccountNumber ?? string.Empty, bank.BankIfscCode ?? string.Empty, p.NetPayable);
            });

        var resolvedByKey = batchLedgers
            .Where(p => p.Status is RiderPayoutStatus.Paid or RiderPayoutStatus.Failed)
            .Where(p => bankDetailsByRider.ContainsKey(p.RiderId))
            .ToLookup(p =>
            {
                var bank = bankDetailsByRider[p.RiderId];
                return (bank.BankAccountNumber ?? string.Empty, bank.BankIfscCode ?? string.Empty, p.NetPayable);
            });

        int paid = 0, failed = 0, alreadyResolved = 0;
        var unresolved = new List<UnresolvedRiderReconciliationRowDto>();

        foreach (var row in rows)
        {
            var key = (row.AccountNumber, row.IfscCode, row.Amount);
            var candidates = processingByKey[key].ToList();

            if (candidates.Count == 0)
            {
                if (resolvedByKey[key].Any())
                {
                    alreadyResolved++;
                    continue;
                }

                unresolved.Add(new UnresolvedRiderReconciliationRowDto(
                    row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                    "No matching Processing payout found in this batch for this account/IFSC/amount."));
                continue;
            }

            if (candidates.Count > 1)
            {
                unresolved.Add(new UnresolvedRiderReconciliationRowDto(
                    row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                    $"Ambiguous — {candidates.Count} Processing payouts in this batch share this account/IFSC/amount. Resolve manually."));
                continue;
            }

            var payout = candidates[0];

            if (row.IsFailed)
            {
                payout.MarkFailed(row.RejectionReason ?? row.Status);
                _ledgerRepository.Update(payout);
                failed++;
                _logger.LogInformation(
                    "Reconcile: rider payout {PayoutId} marked Failed ({Reason})",
                    payout.Id, row.RejectionReason ?? row.Status);
                continue;
            }

            if (row.IsSuccess)
            {
                var utr = row.Utr!.Trim();
                if (utr.Length < 10 || !utr.All(char.IsLetterOrDigit))
                {
                    unresolved.Add(new UnresolvedRiderReconciliationRowDto(
                        row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                        $"STATUS is Success but UTR '{utr}' does not look like a valid bank UTR — not marked Paid."));
                    continue;
                }

                if (await _ledgerRepository.ExistsWithTransactionReferenceAsync(utr, ct))
                {
                    unresolved.Add(new UnresolvedRiderReconciliationRowDto(
                        row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                        $"UTR {utr} is already recorded against another payout — possible duplicate/replay. Skipped."));
                    continue;
                }

                payout.MarkPaid(utr);
                _ledgerRepository.Update(payout);
                paid++;
                _logger.LogWarning(
                    "Reconcile: rider payout {PayoutId} (rider {RiderId}) marked Paid, UTR {Utr}, amount {Amount}, by admin {AdminId}",
                    payout.Id, payout.RiderId, utr, payout.NetPayable, request.ReconciledByAdminId);
                continue;
            }

            unresolved.Add(new UnresolvedRiderReconciliationRowDto(
                row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                $"Unrecognized or in-flight bank status '{row.Status}' — left Processing."));
        }

        var stillProcessing = batchLedgers.Any(p => p.Status == RiderPayoutStatus.Processing);
        var fullyReconciled = false;

        if (!stillProcessing && batch.Status == PayoutExportBatchStatus.Generated)
        {
            var fileHash = Convert.ToHexString(SHA256.HashData(request.FileBytes));
            batch.MarkReconciled(request.ReconciledByAdminId, fileHash);
            _batchRepository.Update(batch);
            fullyReconciled = true;
        }
        else if (batch.Status == PayoutExportBatchStatus.Reconciled)
        {
            fullyReconciled = true;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Reconciled rider payout batch {BatchId}: {RowCount} rows in file, {Paid} marked Paid, {Failed} marked Failed, {AlreadyResolved} already resolved, {UnresolvedCount} unresolved, fully reconciled={FullyReconciled}",
            batch.Id, rows.Count, paid, failed, alreadyResolved, unresolved.Count, fullyReconciled);

        return Result.Success(new ReconcileRiderPayoutsResult(
            batch.Id, rows.Count, paid, failed, alreadyResolved, unresolved, fullyReconciled));
    }
}
