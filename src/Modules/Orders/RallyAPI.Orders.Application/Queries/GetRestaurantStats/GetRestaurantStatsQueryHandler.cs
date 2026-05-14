using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetRestaurantStats;

public sealed class GetRestaurantStatsQueryHandler
    : IRequestHandler<GetRestaurantStatsQuery, Result<RestaurantStatsResponse>>
{
    private readonly IRestaurantStatsService _stats;

    public GetRestaurantStatsQueryHandler(IRestaurantStatsService stats)
    {
        _stats = stats;
    }

    public async Task<Result<RestaurantStatsResponse>> Handle(
        GetRestaurantStatsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fromUtc = request.Range switch
        {
            StatsRange.Today => now.Date,
            StatsRange.Last7Days => now.AddDays(-7),
            StatsRange.Last30Days => now.AddDays(-30),
            _ => now.Date
        };

        var snapshot = await _stats.GetStatsAsync(request.RestaurantId, fromUtc, cancellationToken);

        return Result.Success(new RestaurantStatsResponse(
            request.Range.ToString(),
            fromUtc,
            snapshot.OrdersTotal,
            snapshot.OrdersDelivered,
            snapshot.OrdersCancelled,
            snapshot.OrdersActive,
            snapshot.GrossRevenue,
            snapshot.AverageOrderValue,
            snapshot.ActiveByStatus));
    }
}
