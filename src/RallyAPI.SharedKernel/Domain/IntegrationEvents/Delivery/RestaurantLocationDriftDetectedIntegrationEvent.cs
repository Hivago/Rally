using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Delivery;

/// <summary>
/// Published by the Delivery module's pin-drift sweep when the last N own-fleet
/// pickups at a restaurant all landed more than the drift threshold away from the
/// stored pin. Consumed by Users module to flag the restaurant and enqueue an
/// admin_review_queue entry — Delivery cannot write to Restaurant/Users tables directly.
/// </summary>
public sealed class RestaurantLocationDriftDetectedIntegrationEvent : BaseDomainEvent
{
    public Guid RestaurantId { get; }
    public double CurrentLatitude { get; }
    public double CurrentLongitude { get; }
    public double SuggestedLatitude { get; }
    public double SuggestedLongitude { get; }
    public decimal AverageDriftMeters { get; }
    public int SampleSize { get; }
    public DateTimeOffset DetectedAt { get; }

    public RestaurantLocationDriftDetectedIntegrationEvent(
        Guid restaurantId,
        double currentLatitude,
        double currentLongitude,
        double suggestedLatitude,
        double suggestedLongitude,
        decimal averageDriftMeters,
        int sampleSize,
        DateTimeOffset detectedAt)
    {
        RestaurantId = restaurantId;
        CurrentLatitude = currentLatitude;
        CurrentLongitude = currentLongitude;
        SuggestedLatitude = suggestedLatitude;
        SuggestedLongitude = suggestedLongitude;
        AverageDriftMeters = averageDriftMeters;
        SampleSize = sampleSize;
        DetectedAt = detectedAt;
    }
}
