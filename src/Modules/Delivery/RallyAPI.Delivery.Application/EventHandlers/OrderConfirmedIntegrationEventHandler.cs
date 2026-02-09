using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Application.Commands.CreateDeliveryRequest;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Handles OrderConfirmedIntegrationEvent - Creates DeliveryRequest when restaurant confirms
/// </summary>
public class OrderConfirmedIntegrationEventHandler : INotificationHandler<OrderConfirmedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly IOrderQueryService _orderQueryService;
    private readonly ILogger<OrderConfirmedIntegrationEventHandler> _logger;

    public OrderConfirmedIntegrationEventHandler(
        IMediator mediator,
        IOrderQueryService orderQueryService,
        ILogger<OrderConfirmedIntegrationEventHandler> logger)
    {
        _mediator = mediator;
        _orderQueryService = orderQueryService;
        _logger = logger;
    }

    public async Task Handle(
        OrderConfirmedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "📦 Handling OrderConfirmedIntegrationEvent for Order {OrderId} ({OrderNumber})",
            notification.OrderId,
            notification.OrderNumber);

        try
        {
            // Get order details via SharedKernel abstraction (not direct reference!)
            var orderDetails = await _orderQueryService.GetOrderDeliveryDetailsAsync(
                notification.OrderId,
                cancellationToken);

            if (orderDetails is null)
            {
                _logger.LogError(
                    "❌ Order {OrderId} details not found",
                    notification.OrderId);
                return;
            }

            // Create delivery request
            var command = new CreateDeliveryRequestCommand
            {
                OrderId = notification.OrderId,
                OrderNumber = notification.OrderNumber,
                QuoteId = orderDetails.QuoteId ?? Guid.Empty,

                // Pickup (Restaurant)
                PickupLatitude = orderDetails.PickupLatitude,
                PickupLongitude = orderDetails.PickupLongitude,
                PickupPincode = orderDetails.PickupPincode,
                PickupAddress = orderDetails.PickupAddress,
                PickupContactName = orderDetails.RestaurantName,
                PickupContactPhone = orderDetails.RestaurantPhone,

                // Drop (Customer)
                DropLatitude = orderDetails.DropLatitude,
                DropLongitude = orderDetails.DropLongitude,
                DropPincode = orderDetails.DropPincode,
                DropAddress = orderDetails.DropAddress,
                DropContactName = orderDetails.CustomerName,
                DropContactPhone = orderDetails.CustomerPhone
            };

            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError(
                    "❌ Failed to create delivery request for Order {OrderId}: {Error}",
                    notification.OrderId,
                    result.Error);
                return;
            }

            _logger.LogInformation(
                "✅ Delivery request created for Order {OrderId}",
                notification.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Error handling OrderConfirmedIntegrationEvent for Order {OrderId}",
                notification.OrderId);
            throw;
        }
    }
}