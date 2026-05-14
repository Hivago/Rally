using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetRestaurantStats;

/// <summary>
/// Snapshot stats for a single restaurant outlet, scoped by time range.
/// Powers the restaurant dashboard summary card row.
/// </summary>
public sealed record GetRestaurantStatsQuery(
    Guid RestaurantId,
    StatsRange Range)
    : IRequest<Result<RestaurantStatsResponse>>;

public enum StatsRange
{
    Today = 0,
    Last7Days = 1,
    Last30Days = 2
}

public sealed record RestaurantStatsResponse(
    string Range,
    DateTime PeriodStartUtc,
    int OrdersTotal,
    int OrdersDelivered,
    int OrdersCancelled,
    int OrdersActive,
    decimal GrossRevenue,
    decimal AverageOrderValue,
    IReadOnlyDictionary<string, int> ActiveByStatus);
