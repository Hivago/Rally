using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Bridge: DeliveryCompletedEvent (domain) -> DeliveryCompletedIntegrationEvent (cross-module).
/// Lets the Orders module mark the order as Delivered.
/// </summary>
public sealed class DeliveryCompletedEventBridge : INotificationHandler<DeliveryCompletedEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DeliveryCompletedEventBridge> _logger;

    public DeliveryCompletedEventBridge(
        IPublisher publisher,
        ILogger<DeliveryCompletedEventBridge> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(DeliveryCompletedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new DeliveryCompletedIntegrationEvent(
            notification.DeliveryRequestId,
            notification.OrderId,
            notification.DeliveredAt);

        await _publisher.Publish(integrationEvent, cancellationToken);

        _logger.LogInformation(
            "Published DeliveryCompletedIntegrationEvent for Order {OrderId}",
            notification.OrderId);
    }
}
