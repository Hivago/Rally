using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RallyAPI.Host.Hubs;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Pushes order lifecycle events to the restaurant's SignalR group
/// (`restaurant_{restaurantId}`) so the restaurant dashboard sees real-time
/// updates without having to poll.
///
/// Mirrors AdminOrderFeedHandler. Replaces the previous NewOrderSignalRHandler
/// which was incorrectly bound to OrderConfirmedEvent (firing AFTER the
/// restaurant had already accepted) — the new-order alert now correctly fires
/// on OrderPaidEvent (the moment the customer pays).
///
/// Channels:
///   - "NewOrderReceived"   — emitted on OrderPaidEvent (incoming order alert)
///   - "OrderStatusUpdate"  — emitted on every other lifecycle event
/// </summary>
public sealed class RestaurantOrderFeedHandler :
    INotificationHandler<OrderPaidEvent>,
    INotificationHandler<OrderConfirmedEvent>,
    INotificationHandler<OrderPreparingEvent>,
    INotificationHandler<OrderReadyForPickupEvent>,
    INotificationHandler<RiderAssignedEvent>,
    INotificationHandler<OrderPickedUpEvent>,
    INotificationHandler<OrderDeliveredEvent>,
    INotificationHandler<OrderCancelledEvent>,
    INotificationHandler<OrderRejectedEvent>
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IOrderRepository _orders;
    private readonly ILogger<RestaurantOrderFeedHandler> _logger;

    public RestaurantOrderFeedHandler(
        IHubContext<NotificationHub> hub,
        IOrderRepository orders,
        ILogger<RestaurantOrderFeedHandler> logger)
    {
        _hub = hub;
        _orders = orders;
        _logger = logger;
    }

    // ── Events that carry RestaurantId directly ────────────────────────────

    public Task Handle(OrderPaidEvent notification, CancellationToken ct) =>
        PushAsync(notification.RestaurantId, "NewOrderReceived", new
        {
            orderId     = notification.OrderId,
            orderNumber = notification.OrderNumber,
            customerId  = notification.CustomerId,
            itemCount   = notification.ItemCount,
            totalAmount = notification.Amount,
            placedAt    = notification.OccurredAt
        }, ct);

    public Task Handle(OrderConfirmedEvent notification, CancellationToken ct) =>
        PushStatusAsync(notification.RestaurantId, notification.OrderId, notification.OrderNumber,
            "Confirmed", "You accepted this order.", ct);

    public Task Handle(OrderRejectedEvent notification, CancellationToken ct) =>
        PushStatusAsync(notification.RestaurantId, notification.OrderId, notification.OrderNumber,
            "Rejected", "Order rejected.", ct);

    public Task Handle(OrderReadyForPickupEvent notification, CancellationToken ct) =>
        PushStatusAsync(notification.RestaurantId, notification.OrderId, notification.OrderNumber,
            "ReadyForPickup", "Order marked ready for pickup.", ct);

    // ── Events without RestaurantId — load order to resolve ───────────────

    public Task Handle(OrderPreparingEvent notification, CancellationToken ct) =>
        LookupAndPushStatusAsync(notification.OrderId, notification.OrderNumber,
            "Preparing", "Order moved to preparing.", ct);

    public Task Handle(RiderAssignedEvent notification, CancellationToken ct) =>
        LookupAndPushStatusAsync(notification.OrderId, notification.OrderNumber,
            "RiderAssigned", "A rider is on the way to pick up.", ct);

    public Task Handle(OrderPickedUpEvent notification, CancellationToken ct) =>
        LookupAndPushStatusAsync(notification.OrderId, notification.OrderNumber,
            "PickedUp", "Rider has picked up the order.", ct);

    public Task Handle(OrderDeliveredEvent notification, CancellationToken ct) =>
        LookupAndPushStatusAsync(notification.OrderId, notification.OrderNumber,
            "Delivered", "Order delivered.", ct);

    public async Task Handle(OrderCancelledEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await PushAsync(order.RestaurantId, "OrderStatusUpdate", new
        {
            orderId     = notification.OrderId,
            orderNumber = notification.OrderNumber,
            status      = "Cancelled",
            reason      = notification.Reason.ToString(),
            initiator   = notification.Reason.GetInitiator(),
            message     = $"Order cancelled ({notification.Reason.GetInitiator().ToLowerInvariant()}). Stop preparation if started."
        }, ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task LookupAndPushStatusAsync(
        Guid orderId, string orderNumber, string status, string message, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null) { LogMissing(orderId); return; }

        await PushStatusAsync(order.RestaurantId, orderId, orderNumber, status, message, ct);
    }

    private Task PushStatusAsync(
        Guid restaurantId, Guid orderId, string orderNumber,
        string status, string message, CancellationToken ct) =>
        PushAsync(restaurantId, "OrderStatusUpdate", new
        {
            orderId,
            orderNumber,
            status,
            message
        }, ct);

    private async Task PushAsync(Guid restaurantId, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            await _hub.Clients.Group($"restaurant_{restaurantId}").SendAsync(eventName, payload, ct);
        }
        catch (Exception ex)
        {
            // SignalR push failures must never break the order pipeline.
            _logger.LogWarning(ex,
                "Failed to push {EventName} to restaurant {RestaurantId}",
                eventName, restaurantId);
        }
    }

    private void LogMissing(Guid orderId) =>
        _logger.LogWarning(
            "RestaurantOrderFeedHandler: order {OrderId} not found, skipping push",
            orderId);
}
