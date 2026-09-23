using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Bridge handler: OrderCancelled domain event -> OrderCancelledIntegrationEvent.
/// This crosses the module boundary to the Delivery module so a cancelled order's
/// DeliveryRequest gets cancelled and its rider released. Before this handler existed,
/// cancelling an order only notified the customer/restaurant/admin and triggered a
/// refund — the Delivery module never learned about it, leaving the DeliveryRequest
/// stuck at whatever status it was in and the rider permanently pinned to a dead order.
///
/// Written to the transactional outbox (not published in-process) for the same reason
/// as OrderConfirmedEventHandler: a Delivery DB blip must not silently drop the event.
/// </summary>
public sealed class OrderCancelledEventHandler : INotificationHandler<OrderCancelledEvent>
{
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<OrderCancelledEventHandler> _logger;

    public OrderCancelledEventHandler(
        IOutboxWriter outbox,
        ILogger<OrderCancelledEventHandler> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    public async Task Handle(OrderCancelledEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Bridging OrderCancelledEvent for Order {OrderId} to Integration Event",
            notification.OrderId);

        var integrationEvent = new OrderCancelledIntegrationEvent(
            orderId: notification.OrderId,
            orderNumber: notification.OrderNumber,
            reason: notification.Reason.ToString());

        await _outbox.WriteAsync(integrationEvent, cancellationToken);

        _logger.LogInformation(
            "Enqueued OrderCancelledIntegrationEvent to outbox for Order {OrderNumber}",
            notification.OrderNumber);
    }
}
