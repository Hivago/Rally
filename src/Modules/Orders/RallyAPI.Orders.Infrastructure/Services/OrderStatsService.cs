using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Orders;

namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class OrderStatsService : IOrderStatsService
{
    private static readonly OrderStatus[] ActiveStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup,
        OrderStatus.PickedUp
    ];

    // Statuses that represent realized (or in-flight) revenue.
    // Excludes Pending (unpaid), Cancelled, Rejected, Failed, Refunding, Refunded.
    private static readonly OrderStatus[] RevenueStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup,
        OrderStatus.PickedUp,
        OrderStatus.Delivered
    ];

    private readonly OrdersDbContext _context;

    public OrderStatsService(OrdersDbContext context)
    {
        _context = context;
    }

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
        => _context.Orders.CountAsync(cancellationToken);

    public Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
        => _context.Orders.CountAsync(o => ActiveStatuses.Contains(o.Status), cancellationToken);

    public Task<int> GetTodayCountAsync(CancellationToken cancellationToken = default)
    {
        var startOfDay = DateTime.UtcNow.Date;
        return _context.Orders.CountAsync(o => o.CreatedAt >= startOfDay, cancellationToken);
    }

    public Task<int> GetCountSinceAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
        => _context.Orders.CountAsync(o => o.CreatedAt >= fromUtc, cancellationToken);

    public async Task<decimal> GetRevenueSinceAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var sum = await _context.Orders
            .Where(o => o.CreatedAt >= fromUtc && RevenueStatuses.Contains(o.Status))
            .SumAsync(o => (decimal?)o.Pricing.Total.Amount, cancellationToken);
        return sum ?? 0m;
    }

    public async Task<decimal> GetCommissionSinceAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var sum = await _context.PayoutLedgers
            .Where(l => l.CreatedAt >= fromUtc)
            .SumAsync(l => (decimal?)l.CommissionAmount, cancellationToken);
        return sum ?? 0m;
    }

    public async Task<decimal> GetLifetimeCommissionAsync(CancellationToken cancellationToken = default)
    {
        var sum = await _context.PayoutLedgers
            .SumAsync(l => (decimal?)l.CommissionAmount, cancellationToken);
        return sum ?? 0m;
    }

    public async Task<decimal> GetLifetimeRevenueAsync(CancellationToken cancellationToken = default)
    {
        var sum = await _context.Orders
            .Where(o => RevenueStatuses.Contains(o.Status))
            .SumAsync(o => (decimal?)o.Pricing.Total.Amount, cancellationToken);
        return sum ?? 0m;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetOrdersByStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var grouped = await _context.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(x => x.Status.ToString(), x => x.Count);
    }

    public Task<int> GetPendingLedgerEntriesCountAsync(CancellationToken cancellationToken = default)
        => _context.PayoutLedgers
            .CountAsync(l => l.Status == PayoutLedgerStatus.Pending, cancellationToken);

    public async Task<decimal> GetUnpaidPayoutAmountAsync(CancellationToken cancellationToken = default)
    {
        var sum = await _context.PayoutLedgers
            .Where(l => l.Status != PayoutLedgerStatus.PaidOut)
            .SumAsync(l => (decimal?)l.NetAmount, cancellationToken);
        return sum ?? 0m;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetOrderCountsByRestaurantAsync(
        IReadOnlyCollection<Guid> restaurantIds,
        CancellationToken cancellationToken = default)
    {
        if (restaurantIds.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await _context.Orders
            .Where(o => restaurantIds.Contains(o.RestaurantId))
            .GroupBy(o => o.RestaurantId)
            .Select(g => new { RestaurantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.RestaurantId, x => x.Count);
    }
}
