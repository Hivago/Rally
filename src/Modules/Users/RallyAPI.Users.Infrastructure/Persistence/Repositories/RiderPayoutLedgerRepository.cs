using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Infrastructure.Persistence.Repositories;

public sealed class RiderPayoutLedgerRepository : IRiderPayoutLedgerRepository
{
    private readonly UsersDbContext _context;

    public RiderPayoutLedgerRepository(UsersDbContext context)
    {
        _context = context;
    }

    public Task<RiderPayoutLedger?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.RiderPayoutLedger.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<RiderPayoutLedger?> GetByCycleAsync(
        Guid riderId,
        DateTime cycleStartUtc,
        DateTime cycleEndUtc,
        CancellationToken ct = default)
        => _context.RiderPayoutLedger.FirstOrDefaultAsync(p =>
            p.RiderId == riderId
            && p.CycleStartUtc == cycleStartUtc
            && p.CycleEndUtc == cycleEndUtc,
            ct);

    public async Task<RiderEarningsBreakdown> GetEarningsBreakdownAsync(
        Guid riderId,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var startOfWeekUtc = nowUtc.Date.AddDays(-(int)nowUtc.DayOfWeek);
        var startOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var rows = await _context.RiderPayoutLedger
            .AsNoTracking()
            .Where(p => p.RiderId == riderId)
            .Select(p => new { p.NetPayable, p.Status, p.CycleStartUtc })
            .ToListAsync(ct);

        decimal total = 0m, pending = 0m, thisWeek = 0m, thisMonth = 0m;
        foreach (var r in rows)
        {
            total += r.NetPayable;
            if (r.Status != RiderPayoutStatus.Paid)
                pending += r.NetPayable;
            if (r.CycleStartUtc >= startOfWeekUtc)
                thisWeek += r.NetPayable;
            if (r.CycleStartUtc >= startOfMonthUtc)
                thisMonth += r.NetPayable;
        }

        return new RiderEarningsBreakdown(total, pending, thisWeek, thisMonth);
    }

    public async Task AddAsync(RiderPayoutLedger payout, CancellationToken ct = default)
        => await _context.RiderPayoutLedger.AddAsync(payout, ct);

    public void Update(RiderPayoutLedger payout)
        => _context.RiderPayoutLedger.Update(payout);
}
