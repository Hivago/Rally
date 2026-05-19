using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Events;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Bridge: DeliveryPickedUpEvent (domain) -> DeliveryPickedUpIntegrationEvent (cross-module).
/// Lets the Orders module advance status to PickedUp.
/// </summary>
public sealed class DeliveryPickedUpEventBridge : INotificationHandler<DeliveryPickedUpEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DeliveryPickedUpEventBridge> _logger;

    public DeliveryPickedUpEventBridge(
        IPublisher publisher,
        ILogger<DeliveryPickedUpEventBridge> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(DeliveryPickedUpEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new DeliveryPickedUpIntegrationEvent(
            notification.DeliveryRequestId,
            notification.OrderId,
            notification.PickedUpAt);

        await _publisher.Publish(integrationEvent, cancellationToken);

        _logger.LogInformation(
            "Published DeliveryPickedUpIntegrationEvent for Order {OrderId}",
            notification.OrderId);
    }
}
