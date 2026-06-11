using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Riders;

/// <summary>
/// Raised by the Users module whenever an own-fleet rider posts a GPS location.
/// Consumed by the Delivery module, which checks whether the rider has an active
/// delivery and, if so, persists the live location and pushes it to the customer.
///
/// Published on every location ping; the Delivery handler is the gatekeeper and
/// no-ops when the rider has no active delivery.
/// </summary>
public sealed class RiderLocationUpdatedIntegrationEvent : BaseDomainEvent
{
    public Guid RiderId { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public DateTime UpdatedAt { get; }

    public RiderLocationUpdatedIntegrationEvent(
        Guid riderId,
        double latitude,
        double longitude,
        DateTime updatedAt)
    {
        RiderId = riderId;
        Latitude = latitude;
        Longitude = longitude;
        UpdatedAt = updatedAt;
    }
}
