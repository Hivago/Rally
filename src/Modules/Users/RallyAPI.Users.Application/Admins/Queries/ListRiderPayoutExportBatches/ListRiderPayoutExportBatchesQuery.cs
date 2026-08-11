using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Queries.ListRiderPayoutExportBatches;

/// <summary>
/// Lists recent rider payout export batches, most recent first. Mirrors
/// ListRestaurantPayoutExportBatchesQuery (Orders module) — see that type for rationale.
/// </summary>
public sealed record ListRiderPayoutExportBatchesQuery(
    PayoutExportBatchStatus? Status,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<IReadOnlyList<RiderPayoutExportBatchSummaryDto>>>;

public sealed record RiderPayoutExportBatchSummaryDto(
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
