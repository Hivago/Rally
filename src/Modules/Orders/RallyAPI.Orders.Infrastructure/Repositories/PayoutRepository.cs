using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;

namespace RallyAPI.Orders.Infrastructure.Repositories;

public class PayoutRepository : IPayoutRepository
{
    private readonly OrdersDbContext _context;

    public PayoutRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<Payout?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Payouts.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Payout payout, CancellationToken ct = default)
        => await _context.Payouts.AddAsync(payout, ct);

    public void Update(Payout payout)
        => _context.Payouts.Update(payout);

    public async Task<IReadOnlyList<Payout>> GetByOwnerIdAsync(
        Guid ownerId, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        return await _context.Payouts
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.PeriodEnd)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Payout>> GetByStatusAsync(
        PayoutStatus status, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        return await _context.Payouts
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<Payout?> GetCurrentPeriodPayoutAsync(
        Guid ownerId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default)
    {
        return await _context.Payouts
            .FirstOrDefaultAsync(p => p.OwnerId == ownerId
                                   && p.PeriodStart == periodStart
                                   && p.PeriodEnd == periodEnd, ct);
    }
}
