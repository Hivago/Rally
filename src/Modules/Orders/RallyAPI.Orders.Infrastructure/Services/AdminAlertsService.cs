using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Orders;

namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class AdminAlertsService : IAdminAlertsService
{
    // Orders pending payment longer than this are flagged. Aligns with the
    // hard-cancel threshold so the alert appears just before auto-cancel kicks in.
    private static readonly TimeSpan StuckPaymentAge = TimeSpan.FromMinutes(10);

    private readonly OrdersDbContext _context;

    public AdminAlertsService(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminAlert>> GetActiveAlertsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        var now = DateTime.UtcNow;
        var stuckCutoff = now - StuckPaymentAge;

        // Each query runs sequentially against the same DbContext.
        var escalated = await _context.Orders
            .AsNoTracking()
            .Where(o => o.IsEscalated && o.Status == OrderStatus.Paid)
            .OrderByDescending(o => o.EscalatedAt)
            .Take(limit)
            .Select(o => new AdminAlert(
                "escalated",
                o.Id,
                o.OrderNumber.Value,
                o.EscalationReason ?? "Restaurant has not confirmed in time.",
                "high",
                o.EscalatedAt ?? o.CreatedAt))
            .ToListAsync(cancellationToken);

        var stuckPayments = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < stuckCutoff)
            .OrderBy(o => o.CreatedAt)
            .Take(limit)
            .Select(o => new AdminAlert(
                "stuck_payment",
                o.Id,
                o.OrderNumber.Value,
                "Order is stuck awaiting payment confirmation.",
                "medium",
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        return escalated
            .Concat(stuckPayments)
            .OrderByDescending(a => a.RaisedAt)
            .Take(limit)
            .ToList();
    }
}
