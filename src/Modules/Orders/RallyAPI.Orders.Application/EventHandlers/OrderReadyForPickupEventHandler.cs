using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Bridge handler: OrderReadyForPickup domain event -> OrderReadyForPickupIntegrationEvent.
/// Notifies the Delivery module to trigger immediate rider dispatch.
/// </summary>
public sealed class OrderReadyForPickupEventHandler : INotificationHandler<OrderReadyForPickupEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<OrderReadyForPickupEventHandler> _logger;

    public OrderReadyForPickupEventHandler(
        IOrderRepository orderRepository,
        IPublisher publisher,
        ILogger<OrderReadyForPickupEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _publisher = publisher;
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

        await _publisher.Publish(integrationEvent, cancellationToken);

        _logger.LogInformation("Published OrderReadyForPickupIntegrationEvent for Order {OrderNumber}", order.OrderNumber.Value);
    }
}
