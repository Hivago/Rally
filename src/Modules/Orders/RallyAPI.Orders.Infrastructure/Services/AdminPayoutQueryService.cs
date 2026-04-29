using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Abstractions.Restaurants;

namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class AdminPayoutQueryService : IAdminPayoutQueryService
{
    private readonly OrdersDbContext _context;
    private readonly IRestaurantQueryService _restaurantQueryService;

    public AdminPayoutQueryService(
        OrdersDbContext context,
        IRestaurantQueryService restaurantQueryService)
    {
        _context = context;
        _restaurantQueryService = restaurantQueryService;
    }

    public async Task<RestaurantPayoutSummary> GetRestaurantSummaryAsync(
        DateTime nextAutoRunAtUtc,
        CancellationToken cancellationToken = default)
    {
        // Sequential queries on the single OrdersDbContext.
        var pendingCount = await _context.Payouts.CountAsync(p => p.Status == PayoutStatus.Pending, cancellationToken);

        var pendingAmount = await _context.Payouts
            .Where(p => p.Status == PayoutStatus.Pending)
            .SumAsync(p => (decimal?)p.NetPayoutAmount, cancellationToken) ?? 0m;

        var failedAmount = await _context.Payouts
            .Where(p => p.Status == PayoutStatus.Failed)
            .SumAsync(p => (decimal?)p.NetPayoutAmount, cancellationToken) ?? 0m;

        var onHoldCount = await _context.Payouts.CountAsync(p => p.Status == PayoutStatus.OnHold, cancellationToken);

        var onHoldAmount = await _context.Payouts
            .Where(p => p.Status == PayoutStatus.OnHold)
            .SumAsync(p => (decimal?)p.NetPayoutAmount, cancellationToken) ?? 0m;

        // Platform profit = total commission realized on Paid payouts (lifetime). The "current
        // cycle" wording from spec is ambiguous for restaurants paid weekly; lifetime gives the
        // honest number and the React side can scope to a date range later if needed.
        var platformProfit = await _context.Payouts
            .Where(p => p.Status == PayoutStatus.Paid)
            .SumAsync(p => (decimal?)(p.TotalCommission + p.TotalCommissionGst), cancellationToken) ?? 0m;

        // Last auto-run = most recent batch creation date (the WeeklyPayoutBatchService stamps
        // CreatedAt on every Payout it inserts). Group by date and take the latest day.
        var lastRun = await _context.Payouts
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new
            {
                AtUtc = g.Key,
                RestaurantCount = g.Count(),
                TotalAmount = g.Sum(p => p.NetPayoutAmount),
                TotalPaid = g.Where(p => p.Status == PayoutStatus.Paid).Sum(p => p.NetPayoutAmount)
            })
            .OrderByDescending(x => x.AtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var lastAutoRun = lastRun is null
            ? null
            : new LastAutoRunInfo(lastRun.AtUtc, lastRun.RestaurantCount, lastRun.TotalAmount, lastRun.TotalPaid);

        return new RestaurantPayoutSummary(
            pendingCount,
            pendingAmount,
            failedAmount,
            onHoldCount,
            onHoldAmount,
            platformProfit,
            nextAutoRunAtUtc,
            lastAutoRun);
    }

    public async Task<RestaurantPayoutsPagedResult> GetRestaurantPayoutsAsync(
        RestaurantPayoutsFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var query = _context.Payouts.AsNoTracking().AsQueryable();

        if (filter.FromUtc.HasValue)
            query = query.Where(p => p.CreatedAt >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(p => p.CreatedAt < filter.ToUtc.Value);

        if (filter.OwnerId.HasValue)
            query = query.Where(p => p.OwnerId == filter.OwnerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status)
            && Enum.TryParse<PayoutStatus>(filter.Status, ignoreCase: true, out var status))
        {
            query = query.Where(p => p.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.OwnerId,
                p.OrderCount,
                p.GrossOrderAmount,
                p.NetPayoutAmount,
                p.Status,
                p.Notes,
                p.PeriodStart,
                p.PeriodEnd,
                p.CreatedAt,
                p.PaidAt,
                p.TransactionReference
            })
            .ToListAsync(cancellationToken);

        // Bulk-fetch owner display info to avoid N+1 (cross-module call into Users).
        var ownerIds = rows.Select(r => r.OwnerId).Distinct().ToList();
        var displays = await _restaurantQueryService.GetOwnerPayoutDisplaysAsync(ownerIds, cancellationToken);

        var items = rows
            .Select(r => new RestaurantPayoutRow(
                r.Id,
                r.OwnerId,
                FormatDisplay(r.OwnerId, displays),
                r.OrderCount,
                r.GrossOrderAmount,
                r.NetPayoutAmount,
                r.Status.ToString(),
                r.Notes,
                r.PeriodStart,
                r.PeriodEnd,
                r.CreatedAt,
                r.PaidAt,
                r.TransactionReference))
            .ToList();

        return new RestaurantPayoutsPagedResult(items, total, page, pageSize);
    }

    private static string FormatDisplay(
        Guid ownerId,
        IReadOnlyDictionary<Guid, OwnerPayoutDisplay> displays)
    {
        if (!displays.TryGetValue(ownerId, out var d))
            return ownerId.ToString("N")[..8];

        if (d.OutletCount == 1 && !string.IsNullOrWhiteSpace(d.FirstRestaurantName))
            return d.FirstRestaurantName;

        if (d.OutletCount > 1)
            return $"{d.OwnerName} ({d.OutletCount} outlets)";

        return d.OwnerName;
    }
}
