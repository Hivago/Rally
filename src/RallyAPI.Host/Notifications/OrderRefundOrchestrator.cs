using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RallyAPI.Host.Hubs;
using RallyAPI.Orders.Application.Commands.RefundPayment;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Triggers PayU refund automatically when an order is cancelled or rejected,
/// then pushes a follow-up SignalR notification to the customer with the actual
/// refund outcome (RefundInitiated / RefundFailed).
///
/// The initial Cancelled/Rejected push (from OrderStatusSignalRHandler) is kept
/// vague on timing — "We're processing your refund." The firm 5–7 business day
/// promise lives here, only after PayU has actually accepted the refund request.
///
/// Idempotency: RefundPaymentCommand internally checks Payment.Status.IsRefundable()
/// (== Paid) and rejects already-refunded payments, so a duplicate event firing
/// twice will not double-refund and will not spam the customer with extra pushes.
/// </summary>
public sealed class OrderRefundOrchestrator :
    INotificationHandler<OrderCancelledEvent>,
    INotificationHandler<OrderRejectedEvent>
{
    private readonly ISender _sender;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IOrderRepository _orders;
    private readonly ILogger<OrderRefundOrchestrator> _logger;

    public OrderRefundOrchestrator(
        ISender sender,
        IHubContext<NotificationHub> hub,
        IOrderRepository orders,
        ILogger<OrderRefundOrchestrator> logger)
    {
        _sender = sender;
        _hub = hub;
        _orders = orders;
        _logger = logger;
    }

    public async Task Handle(OrderCancelledEvent notification, CancellationToken ct)
    {
        // Skip when the cancellation reason explicitly means no payment was taken
        // (e.g., PaymentTimeout, PaymentFailed). No refund means no follow-up push —
        // OrderStatusSignalRHandler's initial message already explained no refund.
        if (!notification.Reason.IsRefundable())
        {
            _logger.LogDebug(
                "Order {OrderId} cancelled with non-refundable reason {Reason}; skipping refund",
                notification.OrderId, notification.Reason);
            return;
        }

        // Need to look up the order to get CustomerId — OrderCancelledEvent doesn't carry it.
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null)
        {
            _logger.LogWarning(
                "OrderRefundOrchestrator: order {OrderId} not found, skipping refund",
                notification.OrderId);
            return;
        }

        await TryRefundAsync(notification.OrderId, order.CustomerId, $"cancel:{notification.Reason}", ct);
    }

    public async Task Handle(OrderRejectedEvent notification, CancellationToken ct)
    {
        // Restaurant rejection always implies a paid order needing refund.
        // OrderRejectedEvent carries CustomerId directly, no DB lookup needed.
        await TryRefundAsync(notification.OrderId, notification.CustomerId, "reject", ct);
    }

    private async Task TryRefundAsync(Guid orderId, Guid customerId, string trigger, CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(
                new RefundPaymentCommand(orderId, Amount: null), ct);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Auto-refund initiated for order {OrderId} (trigger: {Trigger}). RequestId: {RequestId}",
                    orderId, trigger, result.Value.RefundRequestId);

                await PushRefundUpdateAsync(customerId, orderId, new
                {
                    orderId,
                    status          = "RefundInitiated",
                    refundRequestId = result.Value.RefundRequestId,
                    message         = "Your refund has been initiated and will reflect in your account within 5–7 business days."
                }, ct);
                return;
            }

            // Expected non-fatal: payment not in Paid state (e.g., never charged,
            // already refunded). No customer push needed — initial cancel message
            // already conveys what happened.
            if (result.Error.Code is "Payment.NotFound" or "Payment.NotRefundable")
            {
                _logger.LogInformation(
                    "Auto-refund skipped for order {OrderId} (trigger: {Trigger}): {Code} - {Message}",
                    orderId, trigger, result.Error.Code, result.Error.Message);
                return;
            }

            // PayU API failed or other unexpected refund error — needs admin attention.
            _logger.LogError(
                "Auto-refund FAILED for order {OrderId} (trigger: {Trigger}): {Code} - {Message}. " +
                "Admin must retry refund manually via the admin refund endpoint.",
                orderId, trigger, result.Error.Code, result.Error.Message);

            await PushRefundUpdateAsync(customerId, orderId, new
            {
                orderId,
                status  = "RefundFailed",
                message = "We hit an issue processing your refund. Our team has been notified and will resolve it shortly."
            }, ct);
        }
        catch (Exception ex)
        {
            // Never let a refund failure break the cancellation/rejection flow.
            // The order is already cancelled in DB; the initial SignalR push has fired.
            // Admin will see the order in Cancelled state with payment still Paid
            // and can manually trigger a refund.
            _logger.LogError(ex,
                "Auto-refund threw for order {OrderId} (trigger: {Trigger}). " +
                "Order state preserved; admin must retry refund manually",
                orderId, trigger);

            await PushRefundUpdateAsync(customerId, orderId, new
            {
                orderId,
                status  = "RefundFailed",
                message = "We hit an issue processing your refund. Our team has been notified and will resolve it shortly."
            }, ct);
        }
    }

    private async Task PushRefundUpdateAsync(Guid customerId, Guid orderId, object payload, CancellationToken ct)
    {
        try
        {
            await _hub.Clients.Group($"customer_{customerId}").SendAsync("OrderStatusUpdate", payload, ct);
        }
        catch (Exception ex)
        {
            // SignalR push failure must not break the refund flow. Log and move on.
            _logger.LogWarning(ex,
                "Failed to push refund SignalR update for order {OrderId} to customer {CustomerId}",
                orderId, customerId);
        }
    }
}
