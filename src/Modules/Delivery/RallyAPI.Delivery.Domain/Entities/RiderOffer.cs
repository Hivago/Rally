using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Entities;

/// <summary>
/// Tracks delivery offers sent to riders.
/// </summary>
public sealed class RiderOffer : BaseEntity
{
    private RiderOffer() { }

    public Guid DeliveryRequestId { get; private set; }
    public Guid RiderId { get; private set; }

    // Offer Details
    public DateTime OfferedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public decimal Earnings { get; private set; }

    // Response
    public RiderOfferStatus Status { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    // Rider Snapshot
    public double? RiderLatitude { get; private set; }
    public double? RiderLongitude { get; private set; }
    public decimal? DistanceToRestaurantKm { get; private set; }

    // Notification Tracking
    public DateTime? NotificationSentAt { get; private set; }
    public bool NotificationDelivered { get; private set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsPending => Status == RiderOfferStatus.Pending && !IsExpired;

    public static RiderOffer Create(
        Guid id,
        Guid deliveryRequestId,
        Guid riderId,
        decimal earnings,
        int expiresInSeconds,
        double? riderLat = null,
        double? riderLng = null,
        decimal? distanceToRestaurant = null)
    {
        var now = DateTime.UtcNow;

        return new RiderOffer
        {
            Id = id,
            DeliveryRequestId = deliveryRequestId,
            RiderId = riderId,
            Earnings = earnings,
            OfferedAt = now,
            ExpiresAt = now.AddSeconds(expiresInSeconds),
            Status = RiderOfferStatus.Pending,
            RiderLatitude = riderLat,
            RiderLongitude = riderLng,
            DistanceToRestaurantKm = distanceToRestaurant
        };
    }

    public void MarkNotificationSent()
    {
        NotificationSentAt = DateTime.UtcNow;
    }

    public void MarkNotificationDelivered()
    {
        NotificationDelivered = true;
    }

    public void Accept()
    {
        if (Status != RiderOfferStatus.Pending)
            throw new InvalidOperationException($"Cannot accept offer in status: {Status}");

        if (IsExpired)
            throw new InvalidOperationException("Offer has expired");

        Status = RiderOfferStatus.Accepted;
        RespondedAt = DateTime.UtcNow;
    }

    public void Reject(string? reason = null)
    {
        if (Status != RiderOfferStatus.Pending)
            throw new InvalidOperationException($"Cannot reject offer in status: {Status}");

        Status = RiderOfferStatus.Rejected;
        RespondedAt = DateTime.UtcNow;
        RejectionReason = reason;
    }

    public void Expire()
    {
        if (Status != RiderOfferStatus.Pending)
            return;

        Status = RiderOfferStatus.Expired;
    }

    public void Cancel()
    {
        if (Status != RiderOfferStatus.Pending)
            return;

        Status = RiderOfferStatus.Cancelled;
    }
}