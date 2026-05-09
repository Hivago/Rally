using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RallyAPI.Host.Hubs;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Pushes order status changes to the customer's SignalR group.
/// Handles every terminal-or-near-terminal lifecycle event so the customer
/// app never gets stuck on a stale "Order Placed" view.
/// Lives in Host to avoid a circular dependency on IHubContext{NotificationHub}.
/// </summary>
public sealed class OrderStatusSignalRHandler :
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

    public async Task Handle(OrderCancelledEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null) { LogMissing(notification.OrderId); return; }

        await PushToCustomer(order.CustomerId, new
        {
            orderId       = order.Id,
            orderNumber   = order.OrderNumber.Value,
            status        = "Cancelled",
            reason        = notification.Reason.ToString(),
            initiator     = notification.Reason.GetInitiator(),
            isRefundable  = notification.Reason.IsRefundable(),
            message       = BuildCancelMessage(notification.Reason)
        }, ct);
    }

    public async Task Handle(OrderRejectedEvent notification, CancellationToken ct)
    {
        // Initial cancellation push — does NOT promise a specific refund timeline.
        // OrderRefundOrchestrator pushes a follow-up `RefundInitiated` event with the
        // 5–7 day promise once PayU has actually accepted the refund.
        var detail = string.IsNullOrWhiteSpace(notification.Reason)
            ? "The restaurant could not accept your order."
            : $"The restaurant could not accept your order: {notification.Reason}.";

        await PushToCustomer(notification.CustomerId, new
        {
            orderId      = notification.OrderId,
            orderNumber  = notification.OrderNumber,
            status       = "Rejected",
            reason       = notification.Reason,
            initiator    = "Restaurant",
            isRefundable = true,
            message      = $"{detail} We're processing your refund."
        }, ct);
    }

    // Initial cancellation messages — describe what happened and (where applicable)
    // that a refund is being processed. The firm "refund initiated, X business days"
    // promise lives in OrderRefundOrchestrator and only fires after PayU confirms.
    private static string BuildCancelMessage(CancellationReason reason) => reason switch
    {
        CancellationReason.CustomerRequested    => "Your order was cancelled at your request. We're processing your refund.",
        CancellationReason.RestaurantUnavailable => "The restaurant could not confirm your order in time. We're processing your refund.",
        CancellationReason.ItemsOutOfStock      => "Some items are out of stock. We're processing your refund.",
        CancellationReason.RestaurantClosed     => "The restaurant is closed. We're processing your refund.",
        CancellationReason.NoRidersAvailable    => "We could not find a delivery partner. We're processing your refund.",
        CancellationReason.PaymentFailed        => "Your payment could not be processed.",
        CancellationReason.PaymentTimeout       => "Payment was not completed in time, so the order was cancelled.",
        CancellationReason.DeliveryAddressIssue => "There was an issue with the delivery address. We're processing your refund.",
        CancellationReason.Timeout              => "The order timed out before it could be fulfilled. We're processing your refund.",
        CancellationReason.SystemError          => "A system error cancelled your order. We're processing your refund.",
        CancellationReason.FraudSuspected       => "Your order was cancelled.",
        _                                        => "Your order was cancelled."
    };

    private Task PushToCustomer(Guid customerId, object payload, CancellationToken ct) =>
        _hub.Clients.Group($"customer_{customerId}").SendAsync("OrderStatusUpdate", payload, ct);

    private void LogMissing(Guid orderId) =>
        _logger.LogWarning("OrderStatusSignalRHandler: order {OrderId} not found, skipping push", orderId);
}
