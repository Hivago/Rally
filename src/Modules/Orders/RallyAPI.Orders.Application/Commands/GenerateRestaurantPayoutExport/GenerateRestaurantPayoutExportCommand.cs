using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.GenerateRestaurantPayoutExport;

/// <summary>
/// Generates the weekly ICICI bulk-transfer file for restaurant payouts. Only Pending
/// payouts for the exact period are eligible; each included payout flips to Processing
/// atomically with the batch record, so it can never be picked up by a later export.
/// </summary>
public sealed record GenerateRestaurantPayoutExportCommand(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Guid RequestedByAdminId) : IRequest<Result<RestaurantPayoutExportResult>>;

public sealed record RestaurantPayoutExportResult(
    Guid ExportBatchId,
    byte[] FileContent,
    string FileName,
    int RowCount,
    decimal ControlSumTotal,
    IReadOnlyList<ExcludedRestaurantPayoutDto> Excluded,
    DateTime GeneratedAtUtc);

/// <summary>A Pending payout that could not be included in the export because its owner has no usable bank details.</summary>
public sealed record ExcludedRestaurantPayoutDto(
    Guid PayoutId,
    Guid OwnerId,
    decimal NetPayoutAmount,
    string Reason);
