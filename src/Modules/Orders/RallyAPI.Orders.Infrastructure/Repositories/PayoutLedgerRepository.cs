using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;

namespace RallyAPI.Orders.Infrastructure.Repositories;

public class PayoutLedgerRepository : IPayoutLedgerRepository
{
    private readonly OrdersDbContext _context;

    public PayoutLedgerRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<PayoutLedger?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PayoutLedgers.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<PayoutLedger?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.PayoutLedgers.FirstOrDefaultAsync(l => l.OrderId == orderId, ct);

    public async Task AddAsync(PayoutLedger entry, CancellationToken ct = default)
        => await _context.PayoutLedgers.AddAsync(entry, ct);

    public async Task<IReadOnlyList<PayoutLedger>> GetPendingByOwnerIdAsync(
        Guid ownerId, CancellationToken ct = default)
    {
        return await _context.PayoutLedgers
            .Where(l => l.OwnerId == ownerId && l.Status == PayoutLedgerStatus.Pending)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PayoutLedger>> GetByPayoutIdAsync(
        Guid payoutId, CancellationToken ct = default)
    {
        return await _context.PayoutLedgers
            .Where(l => l.PayoutId == payoutId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PayoutLedger>> GetByOwnerIdAsync(
        Guid ownerId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        return await _context.PayoutLedgers
            .Where(l => l.OwnerId == ownerId)
            .OrderByDescending(l => l.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetOwnerIdsWithPendingEntriesAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        return await _context.PayoutLedgers
            .Where(l => l.Status == PayoutLedgerStatus.Pending
                     && l.CreatedAt >= fromUtc
                     && l.CreatedAt < toUtc)
            .Select(l => l.OwnerId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PayoutLedger>> GetByOwnerIdAndDateRangeAsync(
        Guid ownerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        return await _context.PayoutLedgers
            .Where(l => l.OwnerId == ownerId
                     && l.CreatedAt >= fromUtc
                     && l.CreatedAt < toUtc)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);
    }
}
