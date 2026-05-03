using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Orders;

namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class RiderOrderStatsService : IRiderOrderStatsService
{
    private readonly OrdersDbContext _context;

    public RiderOrderStatsService(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<RiderDeliveryStats> GetDeliveryStatsAsync(
        Guid riderId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.Orders
            .AsNoTracking()
            .Where(o => o.DeliveryInfo != null && o.DeliveryInfo.RiderId == riderId)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int total = 0, completed = 0, cancelled = 0, ongoing = 0;
        foreach (var row in rows)
        {
            total += row.Count;
            switch (row.Status)
            {
                case OrderStatus.Delivered:
                    completed += row.Count;
                    break;
                case OrderStatus.Cancelled:
                    cancelled += row.Count;
                    break;
                case OrderStatus.PickedUp:
                    ongoing += row.Count;
                    break;
            }
        }

        return new RiderDeliveryStats(total, completed, cancelled, ongoing);
    }
}
