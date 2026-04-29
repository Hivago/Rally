using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Orders;

namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class LiveOrderFeedService : ILiveOrderFeedService
{
    // Active states shown in the live feed. Pending (unpaid) and terminal states are excluded.
    private static readonly OrderStatus[] LiveStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup,
        OrderStatus.PickedUp
    ];

    private readonly OrdersDbContext _context;

    public LiveOrderFeedService(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LiveOrderRow>> GetLiveOrdersAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 200;
        if (limit > 500) limit = 500;

        // Project directly to avoid hydrating entities. Item count is computed from the
        // navigation collection so a single SQL statement covers everything.
        var rows = await _context.Orders
            .AsNoTracking()
            .Where(o => LiveStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .Select(o => new LiveOrderRow(
                o.Id,
                o.OrderNumber.Value,
                o.CustomerName,
                o.RestaurantName,
                o.DeliveryInfo != null ? o.DeliveryInfo.RiderName : null,
                o.Status.ToString(),
                o.FulfillmentType.ToString(),
                o.Items.Count,
                o.Pricing.Total.Amount,
                o.Pricing.Total.Currency,
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        return rows;
    }
}
