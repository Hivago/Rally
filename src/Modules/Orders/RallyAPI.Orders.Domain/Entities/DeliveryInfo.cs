using RallyAPI.Orders.Domain.ValueObjects;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.Entities;

/// <summary>
/// Contains all delivery-related information for an order.
/// Separated for clarity and potential future delivery service integration.
/// </summary>
public sealed class DeliveryInfo : BaseEntity
{
    // Pickup (Restaurant)
    public GeoCoordinate PickupLocation { get; private set; }
    public string PickupPincode { get; private set; }
    public string? PickupAddress { get; private set; }
    public string? PickupContactPhone { get; private set; }

    // Drop (Customer)
    public Address DeliveryAddress { get; private set; }

    // Quote from delivery provider
    public string? QuoteId { get; private set; }
    public string? ProviderName { get; private set; }
    public Money? QuotedDeliveryFee { get; private set; }
    public int? EstimatedMinutes { get; private set; }
    public DateTime? QuotedAt { get; private set; }

    // Actual delivery
    public Guid? RiderId { get; private set; }
    public string? RiderName { get; private set; }
    public string? RiderPhone { get; private set; }
    public string? TrackingUrl { get; private set; }

    // Timestamps
    public DateTime? AssignedAt { get; private set; }
    public DateTime? PickedUpAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }

    // Distance
    public double? DistanceKm { get; private set; }

    // EF Core constructor
    private DeliveryInfo() { }

    private DeliveryInfo(
        GeoCoordinate pickupLocation,
        string pickupPincode,
        string? pickupAddress,
        string? pickupContactPhone,
        Address deliveryAddress)
    {
        Id = Guid.NewGuid();
        PickupLocation = pickupLocation;
        PickupPincode = pickupPincode;
        PickupAddress = pickupAddress;
        PickupContactPhone = pickupContactPhone;
        DeliveryAddress = deliveryAddress;

        // Calculate distance
        var dropLocation = GeoCoordinate.Create(deliveryAddress.Latitude, deliveryAddress.Longitude);
        DistanceKm = Math.Round(pickupLocation.DistanceTo(dropLocation), 2);
    }

    public static DeliveryInfo Create(
        double pickupLatitude,
        double pickupLongitude,
        string pickupPincode,
        Address deliveryAddress,
        string? pickupAddress = null,
        string? pickupContactPhone = null)
    {
        var pickupLocation = GeoCoordinate.Create(pickupLatitude, pickupLongitude);

        return new DeliveryInfo(
            pickupLocation,
            pickupPincode,
            pickupAddress,
            pickupContactPhone,
            deliveryAddress);
    }

    /// <summary>
    /// Sets the delivery quote received from provider
    /// </summary>
    public void SetQuote(
        string quoteId,
        string providerName,
        Money deliveryFee,
        int estimatedMinutes)
    {
        QuoteId = quoteId;
        ProviderName = providerName;
        QuotedDeliveryFee = deliveryFee;
        EstimatedMinutes = estimatedMinutes;
        QuotedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Assigns a rider to the delivery.
    /// <paramref name="isOwnFleet"/> is the authoritative fleet discriminator and must come from
    /// the Delivery module's FleetType — never infer it from whether an id happens to be present.
    /// An own-fleet rider always has an internal <paramref name="riderId"/>; a third-party (3PL)
    /// rider never does, and may not even have a name yet (the provider can report an assignment
    /// before it reports the agent). <see cref="RiderId"/> staying null for 3PL is what keeps
    /// own-rider queries — earnings, stats, order auth — from ever matching a 3PL rider.
    /// <see cref="AssignedAt"/> is the canonical "a rider has been assigned" signal for both fleets.
    /// </summary>
    public void AssignRider(Guid? riderId, bool isOwnFleet, string? riderName = null, string? riderPhone = null)
    {
        if (isOwnFleet)
        {
            if (!riderId.HasValue || riderId.Value == Guid.Empty)
                throw new ArgumentException("An own-fleet rider requires an internal rider id", nameof(riderId));

            RiderId = riderId;
        }
        else
        {
            // A 3PL rider has no Rally account, so never keep an id for one even if a caller
            // passes a stray value — that id would make them look like own fleet downstream.
            RiderId = null;
        }

        RiderName = riderName;
        RiderPhone = riderPhone;
        AssignedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets tracking URL from delivery provider
    /// </summary>
    public void SetTrackingUrl(string trackingUrl)
    {
        TrackingUrl = trackingUrl;
    }

    /// <summary>
    /// Marks the order as picked up by rider
    /// </summary>
    public void MarkPickedUp()
    {
        PickedUpAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the order as delivered
    /// </summary>
    public void MarkDelivered()
    {
        DeliveredAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears rider assignment (e.g., rider cancelled)
    /// </summary>
    public void ClearRiderAssignment()
    {
        RiderId = null;
        RiderName = null;
        RiderPhone = null;
        AssignedAt = null;
    }
}