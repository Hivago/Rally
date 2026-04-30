using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Infrastructure.Persistence;

namespace RallyAPI.Users.Infrastructure.Services;

public sealed class RiderPayoutQueryService : IRiderPayoutQueryService
{
    private readonly UsersDbContext _context;

    public RiderPayoutQueryService(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<RiderPayoutSummary> GetSummaryAsync(
        DateTime nextAutoRunAtUtc,
        CancellationToken ct = default)
    {
        DateTime cycleEndUtc = GetCurrentCycleEndUtc(DateTime.UtcNow);
        DateTime cycleStartUtc = cycleEndUtc.AddDays(-7);

        int pendingCount = await _context.RiderPayoutLedger
            .CountAsync(p => p.Status == RiderPayoutStatus.Pending, ct);

        decimal pendingAmount = await _context.RiderPayoutLedger
            .Where(p => p.Status == RiderPayoutStatus.Pending)
            .SumAsync(p => (decimal?)p.NetPayable, ct) ?? 0m;

        decimal failedAmount = await _context.RiderPayoutLedger
            .Where(p => p.Status == RiderPayoutStatus.Failed)
            .SumAsync(p => (decimal?)p.NetPayable, ct) ?? 0m;

        int onHoldCount = await _context.RiderPayoutLedger
            .CountAsync(p => p.Status == RiderPayoutStatus.OnHold, ct);

        decimal onHoldAmount = await _context.RiderPayoutLedger
            .Where(p => p.Status == RiderPayoutStatus.OnHold)
            .SumAsync(p => (decimal?)p.NetPayable, ct) ?? 0m;

        decimal totalEarned = await _context.RiderPayoutLedger
            .Where(p => p.Status == RiderPayoutStatus.Paid
                && p.CycleStartUtc == cycleStartUtc
                && p.CycleEndUtc == cycleEndUtc)
            .SumAsync(p => (decimal?)p.NetPayable, ct) ?? 0m;

        var lastRun = await _context.RiderPayoutLedger
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new
            {
                AtUtc = g.Key,
                RiderCount = g.Count(),
                TotalAmount = g.Sum(p => p.NetPayable),
                TotalPaid = g.Where(p => p.Status == RiderPayoutStatus.Paid).Sum(p => p.NetPayable)
            })
            .OrderByDescending(x => x.AtUtc)
            .FirstOrDefaultAsync(ct);

        RiderLastAutoRunInfo? lastAutoRun = lastRun is null
            ? null
            : new RiderLastAutoRunInfo(lastRun.AtUtc, lastRun.RiderCount, lastRun.TotalAmount, lastRun.TotalPaid);

        return new RiderPayoutSummary(
            pendingCount,
            pendingAmount,
            failedAmount,
            onHoldCount,
            onHoldAmount,
            totalEarned,
            nextAutoRunAtUtc,
            lastAutoRun);
    }

    public async Task<RiderPayoutsPagedResult> GetPayoutsAsync(
        RiderPayoutsFilter filter,
        CancellationToken ct = default)
    {
        int page = filter.Page < 1 ? 1 : filter.Page;
        int pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var query = _context.RiderPayoutLedger.AsNoTracking().AsQueryable();

        if (filter.FromUtc.HasValue)
            query = query.Where(p => p.CycleStartUtc >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(p => p.CycleEndUtc < filter.ToUtc.Value);

        if (filter.RiderId.HasValue)
            query = query.Where(p => p.RiderId == filter.RiderId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status)
            && Enum.TryParse<RiderPayoutStatus>(filter.Status, ignoreCase: true, out var status))
        {
            query = query.Where(p => p.Status == status);
        }

        int total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(p => p.CycleEndUtc)
            .ThenBy(p => p.RiderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                _context.Riders.AsNoTracking(),
                payout => payout.RiderId,
                rider => rider.Id,
                (payout, rider) => new RiderPayoutRow(
                    payout.Id,
                    payout.RiderId,
                    rider.Name,
                    rider.Phone.Value,
                    payout.DeliveryCount,
                    payout.BaseFare,
                    payout.SurgeFare,
                    payout.Tips,
                    payout.NetPayable,
                    payout.Status.ToString(),
                    payout.StatusNote,
                    payout.CycleStartUtc,
                    payout.CycleEndUtc))
            .ToListAsync(ct);

        return new RiderPayoutsPagedResult(rows, total, page, pageSize);
    }

    private static DateTime GetCurrentCycleEndUtc(DateTime nowUtc)
    {
        int daysSinceMonday = ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        DateTime cycleEnd = nowUtc.Date.AddDays(-daysSinceMonday).AddMinutes(30);
        return nowUtc < cycleEnd ? cycleEnd.AddDays(-7) : cycleEnd;
    }
}
