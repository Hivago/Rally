using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Utilities.Payouts;

namespace RallyAPI.Orders.Application.Commands.ReconcileRestaurantPayouts;

public sealed class ReconcileRestaurantPayoutsCommandHandler
    : IRequestHandler<ReconcileRestaurantPayoutsCommand, Result<ReconcileRestaurantPayoutsResult>>
{
    private readonly IPayoutRepository _payoutRepository;
    private readonly IRestaurantPayoutExportBatchRepository _batchRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReconcileRestaurantPayoutsCommandHandler> _logger;

    public ReconcileRestaurantPayoutsCommandHandler(
        IPayoutRepository payoutRepository,
        IRestaurantPayoutExportBatchRepository batchRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReconcileRestaurantPayoutsCommandHandler> logger)
    {
        _payoutRepository = payoutRepository;
        _batchRepository = batchRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ReconcileRestaurantPayoutsResult>> Handle(
        ReconcileRestaurantPayoutsCommand request,
        CancellationToken ct)
    {
        var batch = await _batchRepository.GetByIdAsync(request.ExportBatchId, ct);
        if (batch is null)
            return Result.Failure<ReconcileRestaurantPayoutsResult>(
                Error.NotFound("RestaurantPayoutExportBatch", request.ExportBatchId));

        IReadOnlyList<IciciReconciliationRow> rows;
        try
        {
            rows = IciciReconciliationParser.Parse(request.FileStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse ICICI reconciliation report for batch {BatchId}", request.ExportBatchId);
            return Result.Failure<ReconcileRestaurantPayoutsResult>(
                Error.Validation("The uploaded file could not be parsed as an ICICI Consolidated Status Report.", ex.Message));
        }

        if (rows.Count == 0)
            return Result.Failure<ReconcileRestaurantPayoutsResult>(
                Error.Validation("The uploaded file has no data rows."));

        var batchPayouts = await _payoutRepository.GetByExportBatchIdAsync(request.ExportBatchId, ct);
        if (batchPayouts.Count == 0)
            return Result.Failure<ReconcileRestaurantPayoutsResult>(
                Error.Validation($"No payouts are stamped with export batch {request.ExportBatchId}."));

        // Only Processing payouts are eligible to be resolved; anything already Paid/Failed
        // (a prior reconcile attempt) is matched separately below so a re-upload is a no-op.
        var processingByKey = batchPayouts
            .Where(p => p.Status == PayoutStatus.Processing)
            .ToLookup(p => (p.BankAccountNumber!, p.BankIfscCode!, p.NetPayoutAmount));

        var resolvedByKey = batchPayouts
            .Where(p => p.Status is PayoutStatus.Paid or PayoutStatus.Failed)
            .ToLookup(p => (p.BankAccountNumber!, p.BankIfscCode!, p.NetPayoutAmount));

        int paid = 0, failed = 0, alreadyResolved = 0;
        var unresolved = new List<UnresolvedReconciliationRowDto>();

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

                unresolved.Add(new UnresolvedReconciliationRowDto(
                    row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                    "No matching Processing payout found in this batch for this account/IFSC/amount."));
                continue;
            }

            if (candidates.Count > 1)
            {
                unresolved.Add(new UnresolvedReconciliationRowDto(
                    row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                    $"Ambiguous — {candidates.Count} Processing payouts in this batch share this account/IFSC/amount. Resolve manually."));
                continue;
            }

            var payout = candidates[0];

            if (row.IsFailed)
            {
                payout.MarkFailed(row.RejectionReason ?? row.Status);
                _payoutRepository.Update(payout);
                failed++;
                _logger.LogInformation(
                    "Reconcile: restaurant payout {PayoutId} marked Failed ({Reason})",
                    payout.Id, row.RejectionReason ?? row.Status);
                continue;
            }

            if (row.IsSuccess)
            {
                var utr = row.Utr!.Trim();
                if (utr.Length < 10 || !utr.All(char.IsLetterOrDigit))
                {
                    unresolved.Add(new UnresolvedReconciliationRowDto(
                        row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                        $"STATUS is Success but UTR '{utr}' does not look like a valid bank UTR — not marked Paid."));
                    continue;
                }

                if (await _payoutRepository.ExistsWithTransactionReferenceAsync(utr, ct))
                {
                    unresolved.Add(new UnresolvedReconciliationRowDto(
                        row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                        $"UTR {utr} is already recorded against another payout — possible duplicate/replay. Skipped."));
                    continue;
                }

                payout.MarkPaid(utr);
                _payoutRepository.Update(payout);
                paid++;
                _logger.LogWarning(
                    "Reconcile: restaurant payout {PayoutId} (owner {OwnerId}) marked Paid, UTR {Utr}, amount {Amount}, by admin {AdminId}",
                    payout.Id, payout.OwnerId, utr, payout.NetPayoutAmount, request.ReconciledByAdminId);
                continue;
            }

            unresolved.Add(new UnresolvedReconciliationRowDto(
                row.RowNumber, row.BeneficiaryName, row.AccountNumber, row.Amount,
                $"Unrecognized or in-flight bank status '{row.Status}' — left Processing."));
        }

        var stillProcessing = batchPayouts.Any(p => p.Status == PayoutStatus.Processing);
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
            "Reconciled restaurant payout batch {BatchId}: {RowCount} rows in file, {Paid} marked Paid, {Failed} marked Failed, {AlreadyResolved} already resolved, {UnresolvedCount} unresolved, fully reconciled={FullyReconciled}",
            batch.Id, rows.Count, paid, failed, alreadyResolved, unresolved.Count, fullyReconciled);

        return Result.Success(new ReconcileRestaurantPayoutsResult(
            batch.Id, rows.Count, paid, failed, alreadyResolved, unresolved, fullyReconciled));
    }
}
