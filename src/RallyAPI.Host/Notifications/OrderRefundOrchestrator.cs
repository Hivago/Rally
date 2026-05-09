using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Commands.RefundPayment;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Triggers PayU refund automatically when an order is cancelled or rejected.
/// Lives in Host so it can dispatch RefundPaymentCommand via MediatR without
/// the Orders module taking a dependency on its own commands.
///
/// Idempotency: RefundPaymentCommand internally checks Payment.Status.IsRefundable()
/// (== Paid) and rejects already-refunded payments, so a duplicate event firing
/// twice will not double-refund.
/// </summary>
public sealed class OrderRefundOrchestrator :
    INotificationHandler<OrderCancelledEvent>,
    INotificationHandler<OrderRejectedEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<OrderRefundOrchestrator> _logger;

    public OrderRefundOrchestrator(
        ISender sender,
        ILogger<OrderRefundOrchestrator> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Handle(OrderCancelledEvent notification, CancellationToken ct)
    {
        // Skip when the cancellation reason explicitly means no payment was taken
        // (e.g., PaymentTimeout, PaymentFailed). Avoids noise in refund logs.
        if (!notification.Reason.IsRefundable())
        {
            _logger.LogDebug(
                "Order {OrderId} cancelled with non-refundable reason {Reason}; skipping refund",
                notification.OrderId, notification.Reason);
            return;
        }

        await TryRefundAsync(notification.OrderId, $"cancel:{notification.Reason}", ct);
    }

    public async Task Handle(OrderRejectedEvent notification, CancellationToken ct)
    {
        // Restaurant rejection always implies a paid order needing refund.
        await TryRefundAsync(notification.OrderId, "reject", ct);
    }

    private async Task TryRefundAsync(Guid orderId, string trigger, CancellationToken ct)
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
                return;
            }

            // Expected non-fatal: payment not in Paid state (e.g., never charged,
            // already refunded). RefundPaymentCommand returns Payment.NotRefundable
            // or Payment.NotFound — both safe to swallow.
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
        }
        catch (Exception ex)
        {
            // Never let a refund failure break the cancellation/rejection flow.
            // The order is already cancelled in DB; the SignalR push has fired.
            // Admin will see the order in Cancelled state with payment still Paid
            // and can manually trigger a refund.
            _logger.LogError(ex,
                "Auto-refund threw for order {OrderId} (trigger: {Trigger}). " +
                "Order state preserved; admin must retry refund manually",
                orderId, trigger);
        }
    }
}
