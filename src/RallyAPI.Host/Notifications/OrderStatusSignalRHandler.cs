using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RallyAPI.Host.Hubs;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Pushes order status changes to the customer's SignalR group.
/// Handles all 5 lifecycle events: Confirmed, RiderAssigned, PickedUp, Delivered, Failed.
/// Lives in Host to avoid a circular dependency on IHubContext{NotificationHub}.
/// </summary>
public sealed class OrderStatusSignalRHandler :
    INotificationHandler<OrderConfirmedEvent>,
    INotificationHandler<RiderAssignedEvent>,
    INotificationHandler<OrderPickedUpEvent>,
    INotificationHandler<OrderDeliveredEvent>,
    INotificationHandler<OrderFailedEvent>
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IOrderRepository _orders;
    private readonly ILogger<OrderStatusSignalRHandler> _logger;

    public OrderStatusSignalRHandler(
        IHubContext<NotificationHub> hub,
        IOrderRepository orders,
        ILogger<OrderStatusSignalRHandler> logger)
    {
        _hub = hub;
        _orders = orders;
        _logger = logger;
    }

    public async Task Handle(OrderConfirmedEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await PushToCustomer(order.CustomerId, new
        {
            orderId     = order.Id,
            orderNumber = order.OrderNumber.Value,
            status      = "Confirmed",
            message     = $"{order.RestaurantName} confirmed your order"
        }, ct);
    }

    public async Task Handle(RiderAssignedEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        var riderName = order.DeliveryInfo?.RiderName ?? "Your rider";

        await PushToCustomer(order.CustomerId, new
        {
            orderId     = order.Id,
            orderNumber = order.OrderNumber.Value,
            status      = "RiderAssigned",
            message     = $"{riderName} is picking up your order"
        }, ct);
    }

    public async Task Handle(OrderPickedUpEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await PushToCustomer(order.CustomerId, new
        {
            orderId     = order.Id,
            orderNumber = order.OrderNumber.Value,
            status      = "PickedUp",
            message     = "Your order is on the way!"
        }, ct);
    }

    public async Task Handle(OrderDeliveredEvent notification, CancellationToken ct)
    {
        await PushToCustomer(notification.CustomerId, new
        {
            orderId     = notification.OrderId,
            orderNumber = notification.OrderNumber,
            status      = "Delivered",
            message     = "Order delivered!"
        }, ct);
    }

    public async Task Handle(OrderFailedEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await PushToCustomer(order.CustomerId, new
        {
            orderId     = order.Id,
            orderNumber = order.OrderNumber.Value,
            status      = "Failed",
            message     = "There was a delivery issue with your order"
        }, ct);
    }

    private Task PushToCustomer(Guid customerId, object payload, CancellationToken ct) =>
        _hub.Clients.Group($"customer_{customerId}").SendAsync("OrderStatusUpdate", payload, ct);

    private void LogMissing(Guid orderId) =>
        _logger.LogWarning("OrderStatusSignalRHandler: order {OrderId} not found, skipping push", orderId);
}
