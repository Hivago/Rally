using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Bridge handler: OrderConfirmed domain event -> OrderConfirmedIntegrationEvent.
/// This crosses the module boundary to the Delivery module.
/// </summary>
public sealed class OrderConfirmedEventHandler : INotificationHandler<OrderConfirmedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<OrderConfirmedEventHandler> _logger;

    public OrderConfirmedEventHandler(
        IOrderRepository orderRepository,
        IPublisher publisher,
        ILogger<OrderConfirmedEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _publisher = publisher;
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

        var integrationEvent = new OrderConfirmedIntegrationEvent(
            orderId: order.Id,
            orderNumber: order.OrderNumber.Value,
            restaurantId: order.RestaurantId,
            customerId: order.CustomerId,
            // Pickup
            restaurantName: order.RestaurantName,
            restaurantPhone: order.RestaurantPhone ?? string.Empty,
            pickupAddress: order.DeliveryInfo.PickupAddress ?? string.Empty,
            pickupLatitude: order.DeliveryInfo.PickupLocation.Latitude,
            pickupLongitude: order.DeliveryInfo.PickupLocation.Longitude,
            pickupPincode: order.DeliveryInfo.PickupPincode,
            // Drop
            customerName: order.CustomerName,
            customerPhone: order.CustomerPhone ?? string.Empty,
            dropAddress: order.DeliveryInfo.DeliveryAddress.FullAddress,
            dropLatitude: order.DeliveryInfo.DeliveryAddress.Latitude,
            dropLongitude: order.DeliveryInfo.DeliveryAddress.Longitude,
            dropPincode: order.DeliveryInfo.DeliveryAddress.Pincode,
            // Details
            itemCount: order.Items.Count,
            totalAmount: order.Pricing.TotalAmount.Amount,
            deliveryInstructions: order.SpecialInstructions,
            quoteId: quoteId,
            confirmedAt: order.ConfirmedAt ?? DateTime.UtcNow
        );

        await _publisher.Publish(integrationEvent, cancellationToken);
        
        _logger.LogInformation("Published OrderConfirmedIntegrationEvent for Order {OrderNumber}", order.OrderNumber.Value);
    }
}
