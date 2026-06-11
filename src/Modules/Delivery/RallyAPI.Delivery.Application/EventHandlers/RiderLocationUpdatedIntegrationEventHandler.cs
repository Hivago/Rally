using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.IntegrationEvents.Riders;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// When an own-fleet rider posts a GPS location, forward it to the customer
/// tracking the order — but only if the rider currently has an active delivery.
/// Acts as the gatekeeper: no active delivery (idle/online rider) → no-op.
/// </summary>
public sealed class RiderLocationUpdatedIntegrationEventHandler
    : INotificationHandler<RiderLocationUpdatedIntegrationEvent>
{
    private readonly IDeliveryRequestRepository _deliveryRequestRepository;
    private readonly ICustomerNotificationService _customerNotifications;
    private readonly ILogger<RiderLocationUpdatedIntegrationEventHandler> _logger;

    public RiderLocationUpdatedIntegrationEventHandler(
        IDeliveryRequestRepository deliveryRequestRepository,
        ICustomerNotificationService customerNotifications,
        ILogger<RiderLocationUpdatedIntegrationEventHandler> logger)
    {
        _deliveryRequestRepository = deliveryRequestRepository;
        _customerNotifications = customerNotifications;
        _logger = logger;
    }

    public async Task Handle(
        RiderLocationUpdatedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        var delivery = await _deliveryRequestRepository.GetActiveByRiderAsync(
            notification.RiderId, cancellationToken);

        if (delivery is null)
            return; // Rider has no active delivery — nothing to forward.

        // Persist the live position so a reconnecting customer sees the last
        // known location (parity with the 3PL track-callback path).
        delivery.UpdateLiveLocation(
            notification.Latitude, notification.Longitude, notification.UpdatedAt);
        await _deliveryRequestRepository.UpdateAsync(delivery, cancellationToken);

        if (delivery.CustomerId is not Guid customerId)
        {
            _logger.LogWarning(
                "Active delivery {DeliveryId} has no CustomerId; cannot push rider location",
                delivery.Id);
            return;
        }

        await _customerNotifications.SendRiderLocationAsync(
            customerId,
            new RiderLocationUpdate(
                delivery.OrderId,
                delivery.Id,
                notification.Latitude,
                notification.Longitude,
                notification.UpdatedAt),
            cancellationToken);
    }
}
