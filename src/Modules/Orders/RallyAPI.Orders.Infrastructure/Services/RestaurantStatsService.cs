using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Orders;

namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class RestaurantStatsService : IRestaurantStatsService
{
    private static readonly OrderStatus[] ActiveStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup,
        OrderStatus.PickedUp
    ];

    private static readonly OrderStatus[] CancelledStatuses =
    [
        OrderStatus.Cancelled,
        OrderStatus.Rejected,
        OrderStatus.Failed
    ];

    private readonly OrdersDbContext _context;

    public RestaurantStatsService(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<RestaurantStatsSnapshot> GetStatsAsync(
        Guid restaurantId,
        DateTime fromUtc,
        CancellationToken cancellationToken = default)
    {
        // Sequential — EF Core does not support concurrent ops on the same context.
        var rangeQuery = _context.Orders
            .Where(o => o.RestaurantId == restaurantId && o.CreatedAt >= fromUtc);

        var ordersTotal = await rangeQuery.CountAsync(cancellationToken);

        var ordersDelivered = await rangeQuery
            .CountAsync(o => o.Status == OrderStatus.Delivered, cancellationToken);

        var ordersCancelled = await rangeQuery
            .CountAsync(o => CancelledStatuses.Contains(o.Status), cancellationToken);

        var grossRevenueNullable = await rangeQuery
            .Where(o => o.Status == OrderStatus.Delivered)
            .SumAsync(o => (decimal?)o.Pricing.Total.Amount, cancellationToken);
        var grossRevenue = grossRevenueNullable ?? 0m;

        var averageOrderValue = ordersDelivered > 0
            ? Math.Round(grossRevenue / ordersDelivered, 2)
            : 0m;

        var ordersActive = await _context.Orders
            .CountAsync(
                o => o.RestaurantId == restaurantId && ActiveStatuses.Contains(o.Status),
                cancellationToken);

        var activeGrouped = await _context.Orders
            .Where(o => o.RestaurantId == restaurantId && ActiveStatuses.Contains(o.Status))
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var activeByStatus = activeGrouped.ToDictionary(x => x.Status.ToString(), x => x.Count);

        return new RestaurantStatsSnapshot(
            ordersTotal,
            ordersDelivered,
            ordersCancelled,
            ordersActive,
            grossRevenue,
            averageOrderValue,
            activeByStatus);
    }
}
