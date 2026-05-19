using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Bridge: DeliveryFailedEvent (domain) -> DeliveryFailedIntegrationEvent (cross-module).
/// Lets the Orders module react to delivery failure (refund flow, customer notification, etc.).
/// </summary>
public sealed class DeliveryFailedEventBridge : INotificationHandler<DeliveryFailedEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DeliveryFailedEventBridge> _logger;

    public DeliveryFailedEventBridge(
        IPublisher publisher,
        ILogger<DeliveryFailedEventBridge> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(DeliveryFailedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new DeliveryFailedIntegrationEvent(
            notification.DeliveryRequestId,
            notification.OrderId,
            notification.Reason.ToString(),
            notification.Notes,
            notification.FailedAt);

        await _publisher.Publish(integrationEvent, cancellationToken);

        _logger.LogInformation(
            "Published DeliveryFailedIntegrationEvent for Order {OrderId} (reason: {Reason})",
            notification.OrderId, notification.Reason);
    }
}
