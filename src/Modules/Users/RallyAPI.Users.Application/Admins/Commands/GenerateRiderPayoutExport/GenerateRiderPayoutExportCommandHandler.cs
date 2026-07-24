using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Utilities.Payouts;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Admins.Commands.GenerateRiderPayoutExport;

public sealed class GenerateRiderPayoutExportCommandHandler
    : IRequestHandler<GenerateRiderPayoutExportCommand, Result<RiderPayoutExportResult>>
{
    private readonly IRiderPayoutLedgerRepository _ledgerRepository;
    private readonly IRiderPayoutExportBatchRepository _batchRepository;
    private readonly IRiderRepository _riderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateRiderPayoutExportCommandHandler> _logger;

    public GenerateRiderPayoutExportCommandHandler(
        IRiderPayoutLedgerRepository ledgerRepository,
        IRiderPayoutExportBatchRepository batchRepository,
        IRiderRepository riderRepository,
        IUnitOfWork unitOfWork,
        ILogger<GenerateRiderPayoutExportCommandHandler> logger)
    {
        _ledgerRepository = ledgerRepository;
        _batchRepository = batchRepository;
        _riderRepository = riderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<RiderPayoutExportResult>> Handle(
        GenerateRiderPayoutExportCommand request,
        CancellationToken ct)
    {
        var pendingPayouts = await _ledgerRepository.GetPendingByCycleAsync(
            request.CycleStartUtc, request.CycleEndUtc, ct);

        if (pendingPayouts.Count == 0)
            return Result.Failure<RiderPayoutExportResult>(
                Error.NotFound($"No Pending rider payouts for cycle {request.CycleStartUtc}-{request.CycleEndUtc}."));

        var riderIds = pendingPayouts.Select(p => p.RiderId).Distinct().ToList();
        var bankDetailsByRider = await _riderRepository.GetBankDetailsByIdsAsync(riderIds, ct);

        var rows = new List<IciciExportRow>();
        var included = new List<RiderPayoutLedger>();
        var excluded = new List<ExcludedRiderPayoutDto>();
        var narration = $"Rally payout {request.CycleStartUtc:ddMMMyyyy}-{request.CycleEndUtc:ddMMMyyyy}";

        foreach (var payout in pendingPayouts)
        {
            // A rider with zero deliveries in the cycle (or, once surge/tips ship, some
            // future combination netting to zero) has nothing to pay — never wire a zero
            // amount to a bank file. Exclude for review instead of crashing.
            if (payout.NetPayable <= 0)
            {
                excluded.Add(new ExcludedRiderPayoutDto(
                    payout.Id, payout.RiderId, payout.NetPayable,
                    "Net payable is zero — nothing to transfer for this cycle."));
                continue;
            }

            bankDetailsByRider.TryGetValue(payout.RiderId, out var bank);

            if (string.IsNullOrWhiteSpace(bank?.BankAccountNumber)
                || string.IsNullOrWhiteSpace(bank.BankIfscCode)
                || string.IsNullOrWhiteSpace(bank.BankAccountName))
            {
                excluded.Add(new ExcludedRiderPayoutDto(
                    payout.Id, payout.RiderId, payout.NetPayable,
                    "Missing or incomplete bank details (account number, IFSC, or account holder name)."));
                continue;
            }

            rows.Add(new IciciExportRow(
                BeneficiaryName: bank.BankAccountName,
                AccountNumber: bank.BankAccountNumber,
                IfscCode: bank.BankIfscCode,
                Amount: payout.NetPayable,
                Narration: narration));
            included.Add(payout);
        }

        if (rows.Count == 0)
            return Result.Failure<RiderPayoutExportResult>(
                Error.Validation("Every Pending payout for this cycle is missing bank details — nothing to export."));

        var fileBytes = IciciBulkTransferExcelWriter.Write(rows, "Rider Payouts");
        var fileHash = Convert.ToHexString(SHA256.HashData(fileBytes));
        var controlSum = included.Sum(p => p.NetPayable);

        var batch = RiderPayoutExportBatch.Create(
            DateOnly.FromDateTime(request.CycleStartUtc), DateOnly.FromDateTime(request.CycleEndUtc),
            included.Count, controlSum, request.RequestedByAdminId, fileHash);

        await _batchRepository.AddAsync(batch, ct);

        foreach (var payout in included)
        {
            payout.MarkProcessing(batch.Id);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated rider payout export batch {BatchId} for cycle {Start}-{End}: {RowCount} rows, control-sum={ControlSum}, {ExcludedCount} excluded",
            batch.Id, request.CycleStartUtc, request.CycleEndUtc, rows.Count, controlSum, excluded.Count);

        var fileName = $"rider-payouts-{request.CycleStartUtc:yyyyMMdd}-{request.CycleEndUtc:yyyyMMdd}.xlsx";

        return Result.Success(new RiderPayoutExportResult(
            batch.Id, fileBytes, fileName, rows.Count, controlSum, excluded, batch.GeneratedAtUtc));
    }
}
