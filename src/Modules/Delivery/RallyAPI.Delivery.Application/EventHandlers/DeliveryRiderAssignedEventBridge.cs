using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Bridge: DeliveryRiderAssignedEvent (domain) -> DeliveryRiderAssignedIntegrationEvent (cross-module).
/// Lets the Orders module attach rider info to the order.
/// </summary>
public sealed class DeliveryRiderAssignedEventBridge : INotificationHandler<DeliveryRiderAssignedEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DeliveryRiderAssignedEventBridge> _logger;

    public DeliveryRiderAssignedEventBridge(
        IPublisher publisher,
        ILogger<DeliveryRiderAssignedEventBridge> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(DeliveryRiderAssignedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new DeliveryRiderAssignedIntegrationEvent(
            notification.DeliveryRequestId,
            notification.OrderId,
            notification.OrderNumber,
            notification.FleetType == FleetType.OwnFleet,
            notification.RiderId,
            notification.RiderName,
            notification.RiderPhone,
            notification.TrackingUrl);

        await _publisher.Publish(integrationEvent, cancellationToken);

        _logger.LogInformation(
            "Published DeliveryRiderAssignedIntegrationEvent for Order {OrderId}",
            notification.OrderId);
    }
}
