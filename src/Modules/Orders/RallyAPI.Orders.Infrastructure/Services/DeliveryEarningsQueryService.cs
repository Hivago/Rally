using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Orders;

namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class DeliveryEarningsQueryService : IDeliveryEarningsQueryService
{
    private readonly OrdersDbContext _context;

    public DeliveryEarningsQueryService(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RiderEarningsSummary>> GetEarningsByCycleAsync(
        DateTimeOffset cycleStart,
        DateTimeOffset cycleEnd,
        CancellationToken ct = default)
    {
        DateTime startUtc = cycleStart.UtcDateTime;
        DateTime endUtc = cycleEnd.UtcDateTime;

        return await _context.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Delivered
                && o.UpdatedAt >= startUtc
                && o.UpdatedAt < endUtc
                && o.DeliveryInfo != null
                && o.DeliveryInfo.RiderId.HasValue)
            .GroupBy(o => o.DeliveryInfo!.RiderId!.Value)
            .Select(g => new RiderEarningsSummary(
                g.Key,
                g.Count(),
                g.Sum(o => o.Pricing.DeliveryFee.Amount)))
            .ToListAsync(ct);
    }
}
