using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetDashboardStats;

/// <summary>
/// The five-number top bar of the Live Operations Dashboard (admin panel Page 1).
/// One round trip → one card row.
/// </summary>
public sealed record GetDashboardStatsQuery(Guid RequestedByAdminId)
    : IRequest<Result<DashboardStatsResponse>>;

public sealed record DashboardStatsResponse(
    int ActiveOrders,
    int TodayOrders,
    int OnlineRiders,
    int PendingKyc,
    int ActiveRestaurants);
