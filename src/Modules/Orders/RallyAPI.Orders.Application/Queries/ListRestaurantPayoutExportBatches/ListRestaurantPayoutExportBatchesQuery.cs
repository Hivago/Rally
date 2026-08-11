using MediatR;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.ListRestaurantPayoutExportBatches;

/// <summary>
/// Lists recent restaurant payout export batches, most recent first. This is the recovery
/// path for an admin who lost the one-time exportBatchId returned by the export response —
/// without it there was previously no way to look a batch back up to reconcile it.
/// </summary>
public sealed record ListRestaurantPayoutExportBatchesQuery(
    PayoutExportBatchStatus? Status,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<IReadOnlyList<RestaurantPayoutExportBatchSummaryDto>>>;

public sealed record RestaurantPayoutExportBatchSummaryDto(
    Guid Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int RowCount,
    decimal ControlSumTotal,
    PayoutExportBatchStatus Status,
    Guid GeneratedByAdminId,
    DateTime GeneratedAtUtc,
    Guid? ReconciledByAdminId,
    DateTime? ReconciledAtUtc,
    int ProcessingCount,
    int PaidCount,
    int FailedCount);
