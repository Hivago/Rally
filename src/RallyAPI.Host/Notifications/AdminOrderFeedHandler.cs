using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RallyAPI.Host.Hubs;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Pushes order lifecycle events to the admin SignalR group as a unified
/// "AdminOrderFeed" channel. Drives both the live orders feed and the
/// dashboard counters (activeOrders, todayOrders) in the admin panel.
///
/// OrderEscalatedToAdminEvent is intentionally NOT handled here — it is
/// pushed separately as "OrderEscalated" by EscalationSignalRHandler so the
/// admin UI can surface it as a distinct alert.
/// </summary>
public sealed class AdminOrderFeedHandler :
    INotificationHandler<OrderConfirmedEvent>,
    INotificationHandler<RiderAssignedEvent>,
    INotificationHandler<OrderPickedUpEvent>,
    INotificationHandler<OrderDeliveredEvent>,
    INotificationHandler<OrderFailedEvent>,
    INotificationHandler<OrderCancelledEvent>,
    INotificationHandler<OrderRejectedEvent>
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IOrderRepository _orders;
    private readonly ILogger<AdminOrderFeedHandler> _logger;

    public AdminOrderFeedHandler(
        IHubContext<NotificationHub> hub,
        IOrderRepository orders,
        ILogger<AdminOrderFeedHandler> logger)
    {
        _hub = hub;
        _orders = orders;
        _logger = logger;
    }

    public async Task Handle(OrderConfirmedEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await Push("OrderPlaced", new
        {
            orderId        = order.Id,
            orderNumber    = order.OrderNumber.Value,
            restaurantId   = order.RestaurantId,
            restaurantName = order.RestaurantName,
            customerId     = order.CustomerId,
            customerName   = order.CustomerName,
            status         = order.Status.ToString(),
            totalAmount    = order.Pricing.Total.Amount,
            occurredAt     = notification.OccurredAt
        }, ct);
    }

    public async Task Handle(RiderAssignedEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await Push("RiderAssigned", new
        {
            orderId        = order.Id,
            orderNumber    = order.OrderNumber.Value,
            restaurantId   = order.RestaurantId,
            restaurantName = order.RestaurantName,
            riderId        = order.DeliveryInfo?.RiderId,
            riderName      = order.DeliveryInfo?.RiderName,
            status         = order.Status.ToString(),
            totalAmount    = order.Pricing.Total.Amount,
            occurredAt     = notification.OccurredAt
        }, ct);
    }

    public async Task Handle(OrderPickedUpEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await Push("PickedUp", new
        {
            orderId        = order.Id,
            orderNumber    = order.OrderNumber.Value,
            restaurantId   = order.RestaurantId,
            restaurantName = order.RestaurantName,
            riderId        = notification.RiderId,
            status         = order.Status.ToString(),
            totalAmount    = order.Pricing.Total.Amount,
            occurredAt     = notification.OccurredAt
        }, ct);
    }

    public async Task Handle(OrderDeliveredEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await Push("Delivered", new
        {
            orderId        = order.Id,
            orderNumber    = order.OrderNumber.Value,
            restaurantId   = order.RestaurantId,
            restaurantName = order.RestaurantName,
            riderId        = notification.RiderId,
            status         = order.Status.ToString(),
            totalAmount    = order.Pricing.Total.Amount,
            occurredAt     = notification.OccurredAt
        }, ct);
    }

    public async Task Handle(OrderFailedEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await Push("Failed", new
        {
            orderId        = order.Id,
            orderNumber    = order.OrderNumber.Value,
            restaurantId   = order.RestaurantId,
            restaurantName = order.RestaurantName,
            status         = order.Status.ToString(),
            totalAmount    = order.Pricing.Total.Amount,
            reason         = notification.Reason,
            occurredAt     = notification.OccurredAt
        }, ct);
    }

    public async Task Handle(OrderCancelledEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await Push("Cancelled", new
        {
            orderId        = order.Id,
            orderNumber    = order.OrderNumber.Value,
            restaurantId   = order.RestaurantId,
            restaurantName = order.RestaurantName,
            status         = order.Status.ToString(),
            totalAmount    = order.Pricing.Total.Amount,
            reason         = notification.Reason.ToString(),
            cancelledBy    = notification.CancelledBy,
            occurredAt     = notification.OccurredAt
        }, ct);
    }

    public async Task Handle(OrderRejectedEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await Push("Rejected", new
        {
            orderId        = order.Id,
            orderNumber    = order.OrderNumber.Value,
            restaurantId   = order.RestaurantId,
            restaurantName = order.RestaurantName,
            status         = order.Status.ToString(),
            totalAmount    = order.Pricing.Total.Amount,
            reason         = notification.Reason,
            occurredAt     = notification.OccurredAt
        }, ct);
    }

    private Task Push(string eventName, object payload, CancellationToken ct)
    {
        var envelope = new
        {
            @event   = eventName,
            payload
        };

        return _hub.Clients.Group("admin").SendAsync("AdminOrderFeed", envelope, ct);
    }

    private void LogMissing(Guid orderId) =>
        _logger.LogWarning(
            "AdminOrderFeedHandler: order {OrderId} not found, skipping push",
            orderId);
}
