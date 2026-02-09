using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Application.Commands.TriggerDispatch;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Handles OrderReadyForPickupIntegrationEvent - Triggers dispatch if not started
/// </summary>
public class OrderReadyForPickupIntegrationEventHandler : INotificationHandler<OrderReadyForPickupIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly IDeliveryRequestRepository _deliveryRequestRepository;
    private readonly ILogger<OrderReadyForPickupIntegrationEventHandler> _logger;

    public OrderReadyForPickupIntegrationEventHandler(
        IMediator mediator,
        IDeliveryRequestRepository deliveryRequestRepository,
        ILogger<OrderReadyForPickupIntegrationEventHandler> logger)
    {
        _mediator = mediator;
        _deliveryRequestRepository = deliveryRequestRepository;
        _logger = logger;
    }

    public async Task Handle(
        OrderReadyForPickupIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "🍔 Handling OrderReadyForPickupIntegrationEvent for Order {OrderId}",
            notification.OrderId);

        try
        {
            var deliveryRequest = await _deliveryRequestRepository
                .GetByOrderIdAsync(notification.OrderId, cancellationToken);

            if (deliveryRequest is null)
            {
                _logger.LogError(
                    "❌ No delivery request found for Order {OrderId}",
                    notification.OrderId);
                return;
            }

            if (deliveryRequest.ShouldTriggerImmediateDispatch())
            {
                _logger.LogInformation(
                    "🚀 Food ready! Triggering dispatch for Order {OrderId}",
                    notification.OrderId);

                var command = new TriggerDispatchCommand
                {
                    DeliveryRequestId = deliveryRequest.Id
                };

                var result = await _mediator.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogError(
                        "❌ Failed to trigger dispatch: {Error}",
                        result.Error);
                }
            }
            else
            {
                _logger.LogInformation(
                    "ℹ️ Dispatch already in progress for Order {OrderId}",
                    notification.OrderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Error handling OrderReadyForPickupIntegrationEvent");
            throw;
        }
    }
}