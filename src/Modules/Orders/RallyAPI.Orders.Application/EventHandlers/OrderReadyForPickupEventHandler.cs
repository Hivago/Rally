using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Bridge handler: OrderReadyForPickup domain event -> OrderReadyForPickupIntegrationEvent.
/// Notifies the Delivery module to trigger immediate rider dispatch.
///
/// The integration event is written to the transactional outbox rather than published
/// in-process: the OutboxProcessor then delivers it with retries to the Delivery consumer.
/// An in-process publish that threw (or whose long dispatch was cancelled when the request
/// thread ended) was swallowed by the domain-event interceptor, so the rider was never
/// dispatched with no retry. The dispatch recovery service is a further backstop.
/// </summary>
public sealed class OrderReadyForPickupEventHandler : INotificationHandler<OrderReadyForPickupEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<OrderReadyForPickupEventHandler> _logger;

    public OrderReadyForPickupEventHandler(
        IOrderRepository orderRepository,
        IOutboxWriter outbox,
        ILogger<OrderReadyForPickupEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task Handle(OrderReadyForPickupEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Bridging OrderReadyForPickupEvent for Order {OrderId} to Integration Event",
            notification.OrderId);

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogError("Order {OrderId} not found while bridging to ready-for-pickup event", notification.OrderId);
            return;
        }

        var integrationEvent = new OrderReadyForPickupIntegrationEvent(
            orderId: order.Id,
            orderNumber: order.OrderNumber.Value,
            restaurantId: order.RestaurantId
        );

        await _outbox.WriteAsync(integrationEvent, cancellationToken);

        _logger.LogInformation("Enqueued OrderReadyForPickupIntegrationEvent to outbox for Order {OrderNumber}", order.OrderNumber.Value);
    }
}
