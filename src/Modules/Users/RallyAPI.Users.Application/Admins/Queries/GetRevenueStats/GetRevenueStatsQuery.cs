using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetRevenueStats;

public sealed record GetRevenueStatsQuery(Guid RequestedByAdminId)
    : IRequest<Result<RevenueStatsResponse>>;

public sealed record RevenueStatsResponse(
    string Currency,
    decimal TodayRevenue,
    decimal Last7DaysRevenue,
    decimal Last30DaysRevenue,
    decimal LifetimeRevenue,
    decimal TodayCommission,
    decimal Last7DaysCommission,
    decimal Last30DaysCommission,
    decimal LifetimeCommission,
    int PendingPayoutLedgerEntries,
    decimal UnpaidPayoutAmount);
