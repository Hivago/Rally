using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Utilities.Payouts;

namespace RallyAPI.Orders.Application.Commands.GenerateRestaurantPayoutExport;

public sealed class GenerateRestaurantPayoutExportCommandHandler
    : IRequestHandler<GenerateRestaurantPayoutExportCommand, Result<RestaurantPayoutExportResult>>
{
    private readonly IPayoutRepository _payoutRepository;
    private readonly IRestaurantPayoutExportBatchRepository _batchRepository;
    private readonly IRestaurantQueryService _restaurantQueryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateRestaurantPayoutExportCommandHandler> _logger;

    public GenerateRestaurantPayoutExportCommandHandler(
        IPayoutRepository payoutRepository,
        IRestaurantPayoutExportBatchRepository batchRepository,
        IRestaurantQueryService restaurantQueryService,
        IUnitOfWork unitOfWork,
        ILogger<GenerateRestaurantPayoutExportCommandHandler> logger)
    {
        _payoutRepository = payoutRepository;
        _batchRepository = batchRepository;
        _restaurantQueryService = restaurantQueryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<RestaurantPayoutExportResult>> Handle(
        GenerateRestaurantPayoutExportCommand request,
        CancellationToken ct)
    {
        var pendingPayouts = await _payoutRepository.GetPendingByPeriodAsync(
            request.PeriodStart, request.PeriodEnd, ct);

        if (pendingPayouts.Count == 0)
            return Result.Failure<RestaurantPayoutExportResult>(
                Error.NotFound($"No Pending restaurant payouts for period {request.PeriodStart}-{request.PeriodEnd}."));

        var ownerIds = pendingPayouts.Select(p => p.OwnerId).Distinct().ToList();
        var bankDetailsByOwner = await _restaurantQueryService.GetOwnerBankDetailsAsync(ownerIds, ct);

        var rows = new List<IciciExportRow>();
        var included = new List<Payout>();
        var excluded = new List<ExcludedRestaurantPayoutDto>();
        var narration = $"Rally payout {request.PeriodStart:ddMMMyyyy}-{request.PeriodEnd:ddMMMyyyy}";

        foreach (var payout in pendingPayouts)
        {
            // A misconfigured commission (flat fee exceeding the order amount) can drive
            // NetPayoutAmount to zero or negative — never wire that to a bank file. Exclude
            // it for admin review instead of crashing or transferring a nonsense amount.
            if (payout.NetPayoutAmount <= 0)
            {
                excluded.Add(new ExcludedRestaurantPayoutDto(
                    payout.Id, payout.OwnerId, payout.NetPayoutAmount,
                    "Net payout amount is zero or negative — check commission configuration for this owner."));
                continue;
            }

            bankDetailsByOwner.TryGetValue(payout.OwnerId, out var bank);

            if (string.IsNullOrWhiteSpace(bank?.BankAccountNumber)
                || string.IsNullOrWhiteSpace(bank.BankIfscCode)
                || string.IsNullOrWhiteSpace(bank.BankAccountName))
            {
                excluded.Add(new ExcludedRestaurantPayoutDto(
                    payout.Id, payout.OwnerId, payout.NetPayoutAmount,
                    "Missing or incomplete bank details (account number, IFSC, or account holder name)."));
                continue;
            }

            rows.Add(new IciciExportRow(
                BeneficiaryName: bank.BankAccountName,
                AccountNumber: bank.BankAccountNumber,
                IfscCode: bank.BankIfscCode,
                Amount: payout.NetPayoutAmount,
                Narration: narration));
            included.Add(payout);
        }

        if (rows.Count == 0)
            return Result.Failure<RestaurantPayoutExportResult>(
                Error.Validation("Every Pending payout for this period is missing bank details — nothing to export."));

        var fileBytes = IciciBulkTransferExcelWriter.Write(rows, "Restaurant Payouts");
        var fileHash = Convert.ToHexString(SHA256.HashData(fileBytes));
        var controlSum = included.Sum(p => p.NetPayoutAmount);

        var batch = RestaurantPayoutExportBatch.Create(
            request.PeriodStart, request.PeriodEnd, included.Count, controlSum,
            request.RequestedByAdminId, fileHash);

        await _batchRepository.AddAsync(batch, ct);

        foreach (var payout in included)
        {
            payout.MarkProcessing(batch.Id);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated restaurant payout export batch {BatchId} for period {Start}-{End}: {RowCount} rows, control-sum={ControlSum}, {ExcludedCount} excluded",
            batch.Id, request.PeriodStart, request.PeriodEnd, rows.Count, controlSum, excluded.Count);

        var fileName = $"restaurant-payouts-{request.PeriodStart:yyyyMMdd}-{request.PeriodEnd:yyyyMMdd}.xlsx";

        return Result.Success(new RestaurantPayoutExportResult(
            batch.Id, fileBytes, fileName, rows.Count, controlSum, excluded, batch.GeneratedAtUtc));
    }
}
