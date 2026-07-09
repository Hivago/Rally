using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Bridge handler: OrderConfirmed domain event -> OrderConfirmedIntegrationEvent.
/// This crosses the module boundary to the Delivery module.
///
/// The integration event is written to the transactional outbox rather than published
/// in-process: the OutboxProcessor then delivers it with retries to the (idempotent)
/// Delivery consumer. Previously an in-process publish that threw (e.g. a Delivery DB
/// blip) was swallowed by the domain-event interceptor, permanently losing the delivery
/// request for that order with no retry.
/// </summary>
public sealed class OrderConfirmedEventHandler : INotificationHandler<OrderConfirmedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<OrderConfirmedEventHandler> _logger;

    public OrderConfirmedEventHandler(
        IOrderRepository orderRepository,
        IOutboxWriter outbox,
        ILogger<OrderConfirmedEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task Handle(OrderConfirmedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Bridging OrderConfirmedEvent for Order {OrderId} to Integration Event", 
            notification.OrderId);

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogError("Order {OrderId} not found while bridging to integration event", notification.OrderId);
            return;
        }

        // Parse QuoteId if it exists
        Guid? quoteId = null;
        if (!string.IsNullOrWhiteSpace(order.DeliveryQuoteId) && Guid.TryParse(order.DeliveryQuoteId, out var parsedGuid))
        {
            quoteId = parsedGuid;
        }

        var isPickup = order.FulfillmentType == FulfillmentType.Pickup;

        var integrationEvent = new OrderConfirmedIntegrationEvent(
            orderId: order.Id,
            orderNumber: order.OrderNumber.Value,
            restaurantId: order.RestaurantId,
            customerId: order.CustomerId,
            // Pickup
            restaurantName: order.RestaurantName,
            restaurantPhone: order.RestaurantPhone ?? string.Empty,
            pickupAddress: order.DeliveryInfo?.PickupAddress ?? string.Empty,
            pickupLatitude: order.DeliveryInfo?.PickupLocation.Latitude ?? 0,
            pickupLongitude: order.DeliveryInfo?.PickupLocation.Longitude ?? 0,
            pickupPincode: order.DeliveryInfo?.PickupPincode ?? string.Empty,
            // Drop (empty for pickup orders)
            customerName: order.CustomerName,
            customerPhone: order.CustomerPhone ?? string.Empty,
            dropAddress: order.DeliveryInfo?.DeliveryAddress.FullAddress ?? string.Empty,
            dropLatitude: order.DeliveryInfo?.DeliveryAddress.Latitude ?? 0,
            dropLongitude: order.DeliveryInfo?.DeliveryAddress.Longitude ?? 0,
            dropPincode: order.DeliveryInfo?.DeliveryAddress.Pincode ?? string.Empty,
            // Details
            itemCount: order.Items.Count,
            totalAmount: order.Pricing.Total.Amount,
            deliveryInstructions: order.SpecialInstructions,
            quoteId: quoteId,
            confirmedAt: order.ConfirmedAt ?? DateTime.UtcNow,
            isPickupOrder: isPickup
        );

        await _outbox.WriteAsync(integrationEvent, cancellationToken);

        _logger.LogInformation("Enqueued OrderConfirmedIntegrationEvent to outbox for Order {OrderNumber}", order.OrderNumber.Value);
    }
}
