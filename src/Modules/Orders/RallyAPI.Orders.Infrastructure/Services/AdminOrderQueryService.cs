using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Orders;

namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class AdminOrderQueryService : IAdminOrderQueryService
{
    private static readonly OrderStatus[] ActiveStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup,
        OrderStatus.PickedUp
    ];

    private static readonly OrderStatus[] FailedStatuses =
    [
        OrderStatus.Rejected,
        OrderStatus.Cancelled,
        OrderStatus.Failed,
        OrderStatus.Refunding,
        OrderStatus.Refunded
    ];

    private readonly OrdersDbContext _context;

    public AdminOrderQueryService(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<AdminOrdersPagedResult> SearchAsync(
        AdminOrdersFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var query = BuildBaseQuery(filter);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AdminOrderRow(
                o.Id,
                o.OrderNumber.Value,
                o.CustomerName,
                o.RestaurantName,
                o.DeliveryInfo != null ? o.DeliveryInfo.RiderName : null,
                o.Status.ToString(),
                o.PaymentStatus.ToString(),
                o.FulfillmentType.ToString(),
                o.IsEscalated,
                o.Items.Count,
                o.Pricing.Total.Amount,
                o.Pricing.Total.Currency,
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        // Tab counts ignore the Tab filter so the UI can render badge counts on every tab.
        // Search + date filters DO apply so the counts match the visible result set.
        var counts = await GetTabCountsAsync(filter, cancellationToken);

        return new AdminOrdersPagedResult(items, total, page, pageSize, counts);
    }

    public async Task<AdminOrderDetail?> GetDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.DeliveryInfo)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return null;

        // Delay = how long the order has been in its current non-terminal state.
        // For the escalation modal, the most useful "delay" is age since payment for orders
        // still awaiting restaurant action.
        int? delayMinutes = order.Status switch
        {
            OrderStatus.Paid       => (int)(DateTime.UtcNow - order.CreatedAt).TotalMinutes,
            OrderStatus.Confirmed  => order.ConfirmedAt is { } t ? (int)(DateTime.UtcNow - t).TotalMinutes : null,
            OrderStatus.Preparing  => order.PreparingAt is { } t ? (int)(DateTime.UtcNow - t).TotalMinutes : null,
            OrderStatus.ReadyForPickup => order.ReadyAt is { } t ? (int)(DateTime.UtcNow - t).TotalMinutes : null,
            _ => null
        };

        return new AdminOrderDetail(
            order.Id,
            order.OrderNumber.Value,
            order.Status.ToString(),
            order.Status.GetDisplayName(),
            order.PaymentStatus.ToString(),
            order.FulfillmentType.ToString(),
            order.IsEscalated,
            order.EscalatedAt,
            order.EscalationReason,
            delayMinutes,
            order.CustomerId,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.RestaurantId,
            order.RestaurantName,
            order.RestaurantPhone,
            order.DeliveryInfo?.RiderId,
            order.DeliveryInfo?.RiderName,
            order.DeliveryInfo?.RiderPhone,
            order.DeliveryInfo?.DeliveryAddress?.GetFormattedAddress(),
            order.Pricing.SubTotal.Amount,
            order.Pricing.DeliveryFee.Amount,
            order.Pricing.Tax.Amount,
            order.Pricing.Discount.Amount,
            order.Pricing.Total.Amount,
            order.Pricing.Total.Currency,
            order.Items.Count,
            order.Items.Select(i => new AdminOrderItem(
                i.ItemName,
                i.Quantity,
                i.UnitPrice.Amount,
                i.TotalPrice.Amount,
                i.SpecialInstructions)).ToList(),
            order.CancellationReason?.ToString(),
            order.CancellationNotes,
            order.RejectionReason,
            order.SpecialInstructions,
            order.CreatedAt,
            order.ConfirmedAt,
            order.PreparingAt,
            order.ReadyAt,
            order.PickedUpAt,
            order.DeliveredAt,
            order.CancelledAt);
    }

    public async IAsyncEnumerable<AdminOrderExportRow> StreamForExportAsync(
        AdminOrdersFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = BuildBaseQuery(filter)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminOrderExportRow(
                o.OrderNumber.Value,
                o.CreatedAt,
                o.Status.ToString(),
                o.PaymentStatus.ToString(),
                o.FulfillmentType.ToString(),
                o.CustomerName,
                o.CustomerPhone,
                o.RestaurantName,
                o.DeliveryInfo != null ? o.DeliveryInfo.RiderName : null,
                o.Items.Count,
                o.Pricing.Total.Amount,
                o.Pricing.Total.Currency,
                o.IsEscalated,
                o.CancellationReason != null ? o.CancellationReason.ToString() : null));

        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return row;
        }
    }

    private IQueryable<Domain.Entities.Order> BuildBaseQuery(AdminOrdersFilter filter)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();

        // Tab filter
        query = filter.Tab switch
        {
            AdminOrdersTab.Active => query.Where(o => ActiveStatuses.Contains(o.Status)),
            AdminOrdersTab.Escalated => query.Where(o => o.IsEscalated),
            AdminOrdersTab.Failed => query.Where(o => FailedStatuses.Contains(o.Status)),
            _ => query
        };

        if (filter.FromUtc.HasValue)
            query = query.Where(o => o.CreatedAt >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(o => o.CreatedAt < filter.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var raw = filter.Search.Trim();
            var term = raw.ToLower();

            // OrderNumber is a value object with HasConversion. Its underlying column is text,
            // but neither EF.Property<string> nor `.Value.ToLower().Contains` translate cleanly
            // (the value converter intercepts the parameter). For order numbers we therefore
            // do an exact equality match via the value-object factory; for the other two we
            // do a translatable substring match on the plain string columns.
            if (raw.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase))
            {
                var exact = Domain.ValueObjects.OrderNumber.From(raw.ToUpper());
                query = query.Where(o =>
                    o.OrderNumber == exact ||
                    o.CustomerName.ToLower().Contains(term) ||
                    o.RestaurantName.ToLower().Contains(term));
            }
            else
            {
                query = query.Where(o =>
                    o.CustomerName.ToLower().Contains(term) ||
                    o.RestaurantName.ToLower().Contains(term));
            }
        }

        return query;
    }

    private async Task<AdminOrderTabCounts> GetTabCountsAsync(
        AdminOrdersFilter filter,
        CancellationToken cancellationToken)
    {
        // Counts use the same date + search filters but ignore the Tab filter.
        var baseFilter = filter with { Tab = AdminOrdersTab.All };
        var baseQuery = BuildBaseQuery(baseFilter);

        var all = await baseQuery.CountAsync(cancellationToken);
        var active = await baseQuery.CountAsync(o => ActiveStatuses.Contains(o.Status), cancellationToken);
        var escalated = await baseQuery.CountAsync(o => o.IsEscalated, cancellationToken);
        var failed = await baseQuery.CountAsync(o => FailedStatuses.Contains(o.Status), cancellationToken);

        return new AdminOrderTabCounts(all, active, escalated, failed);
    }
}
