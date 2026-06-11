using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Riders.Queries.GetEarningsSummary;

public sealed record GetRiderEarningsSummaryQuery(Guid RiderId)
    : IRequest<Result<RiderEarningsSummaryResponse>>;

/// <summary>
/// Rider-facing earnings snapshot for the app's earnings tab.
/// Amounts are net payable (base + surge + tips) aggregated from the
/// rider's weekly payout cycles.
/// </summary>
public sealed record RiderEarningsSummaryResponse(
    decimal TotalEarned,
    decimal PendingPayout,
    decimal ThisWeek,
    decimal ThisMonth,
    DateTime NextPayoutDateUtc);
