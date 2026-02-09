using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Domain.Events;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Entities;

/// <summary>
/// Aggregate root for delivery requests.
/// </summary>
public sealed class DeliveryRequest : AggregateRoot
{
    private readonly List<RiderOffer> _riderOffers = [];

    private DeliveryRequest() { }

    // Order Reference
    public Guid OrderId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid? QuoteId { get; private set; }

    // Status
    public DeliveryRequestStatus Status { get; private set; }
    public FleetType? FleetType { get; private set; }

    // Pricing
    public decimal QuotedPrice { get; private set; }
    public decimal? ActualPrice { get; private set; }
    public decimal? PriceDifference { get; private set; }

    // Own Fleet Assignment
    public Guid? RiderId { get; private set; }
    public string? RiderName { get; private set; }
    public string? RiderPhone { get; private set; }

    // 3PL Assignment
    public string? ExternalTaskId { get; private set; }
    public string? ExternalTrackingUrl { get; private set; }
    public string? ExternalRiderName { get; private set; }
    public string? ExternalRiderPhone { get; private set; }
    public string? ExternalLspName { get; private set; }

    // Pickup Location
    public double PickupLatitude { get; private set; }
    public double PickupLongitude { get; private set; }
    public string PickupPincode { get; private set; } = string.Empty;
    public string PickupAddress { get; private set; } = string.Empty;
    public string PickupContactName { get; private set; } = string.Empty;
    public string PickupContactPhone { get; private set; } = string.Empty;

    // Drop Location
    public double DropLatitude { get; private set; }
    public double DropLongitude { get; private set; }
    public string DropPincode { get; private set; } = string.Empty;
    public string DropAddress { get; private set; } = string.Empty;
    public string DropContactName { get; private set; } = string.Empty;
    public string DropContactPhone { get; private set; } = string.Empty;

    // Timestamps
    public DateTime CreatedAt { get; private set; }
    public DateTime? DispatchAt { get; private set; }
    public DateTime? SearchingStartedAt { get; private set; }
    public DateTime? AssignedAt { get; private set; }
    public DateTime? ArrivedPickupAt { get; private set; }
    public DateTime? PickedUpAt { get; private set; }
    public DateTime? ArrivedDropAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Failure Info
    public DeliveryFailureReason? FailureReason { get; private set; }
    public string? FailureNotes { get; private set; }
    public string? FailurePhotoUrl { get; private set; }

    // Retry Tracking
    public int OwnFleetAttempts { get; private set; }

    // Distance
    public decimal? DistanceKm { get; private set; }
    public int? EstimatedMinutes { get; private set; }

    // Navigation
    public IReadOnlyCollection<RiderOffer> RiderOffers => _riderOffers.AsReadOnly();

    #region Factory

    public static DeliveryRequest Create(
        Guid id,
        Guid orderId,
        string orderNumber,
        Guid? quoteId,
        decimal quotedPrice,
        double pickupLat, double pickupLng, string pickupPincode,
        string pickupAddress, string pickupContactName, string pickupContactPhone,
        double dropLat, double dropLng, string dropPincode,
        string dropAddress, string dropContactName, string dropContactPhone,
        DateTime? dispatchAt = null,
        decimal? distanceKm = null,
        int? estimatedMinutes = null)
    {
        var request = new DeliveryRequest
        {
            Id = id,
            OrderId = orderId,
            OrderNumber = orderNumber,
            QuoteId = quoteId,
            QuotedPrice = quotedPrice,
            Status = dispatchAt.HasValue
                ? DeliveryRequestStatus.PendingDispatch
                : DeliveryRequestStatus.Created,
            PickupLatitude = pickupLat,
            PickupLongitude = pickupLng,
            PickupPincode = pickupPincode,
            PickupAddress = pickupAddress,
            PickupContactName = pickupContactName,
            PickupContactPhone = pickupContactPhone,
            DropLatitude = dropLat,
            DropLongitude = dropLng,
            DropPincode = dropPincode,
            DropAddress = dropAddress,
            DropContactName = dropContactName,
            DropContactPhone = dropContactPhone,
            DispatchAt = dispatchAt,
            DistanceKm = distanceKm,
            EstimatedMinutes = estimatedMinutes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        request.AddDomainEvent(new DeliveryRequestCreatedEvent(
            request.Id, orderId, orderNumber));

        return request;
    }

    #endregion

    #region Status Transitions

    public void StartSearchingOwnFleet()
    {
        EnsureStatus(DeliveryRequestStatus.Created, DeliveryRequestStatus.PendingDispatch);

        Status = DeliveryRequestStatus.SearchingOwnFleet;
        FleetType = Enums.FleetType.OwnFleet;
        SearchingStartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartSearching3PL()
    {
        EnsureStatus(DeliveryRequestStatus.SearchingOwnFleet);

        Status = DeliveryRequestStatus.Searching3PL;
        FleetType = Enums.FleetType.ThirdParty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignOwnFleetRider(
        Guid riderId,
        string riderName,
        string riderPhone)
    {
        EnsureStatus(DeliveryRequestStatus.SearchingOwnFleet);

        RiderId = riderId;
        RiderName = riderName;
        RiderPhone = riderPhone;
        Status = DeliveryRequestStatus.RiderAssigned;
        FleetType = Enums.FleetType.OwnFleet;
        AssignedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new DeliveryRiderAssignedEvent(
            Id, OrderId, OrderNumber,
            Enums.FleetType.OwnFleet,
            riderId, riderName, riderPhone, null));
    }

    public void Assign3PLRider(
        string taskId,
        string lspName,
        string? riderName,
        string? riderPhone,
        string? trackingUrl,
        decimal actualPrice)
    {
        EnsureStatus(DeliveryRequestStatus.Searching3PL);

        ExternalTaskId = taskId;
        ExternalLspName = lspName;
        ExternalRiderName = riderName;
        ExternalRiderPhone = riderPhone;
        ExternalTrackingUrl = trackingUrl;
        ActualPrice = actualPrice;
        PriceDifference = actualPrice - QuotedPrice;
        Status = DeliveryRequestStatus.Assigned3PL;
        FleetType = Enums.FleetType.ThirdParty;
        AssignedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new DeliveryRiderAssignedEvent(
            Id, OrderId, OrderNumber,
            Enums.FleetType.ThirdParty,
            null, riderName, riderPhone, trackingUrl));
    }

    public void Update3PLRiderInfo(string? riderName, string? riderPhone, string? trackingUrl)
    {
        ExternalRiderName = riderName ?? ExternalRiderName;
        ExternalRiderPhone = riderPhone ?? ExternalRiderPhone;
        ExternalTrackingUrl = trackingUrl ?? ExternalTrackingUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRiderEnRoutePickup()
    {
        EnsureStatus(DeliveryRequestStatus.RiderAssigned, DeliveryRequestStatus.Assigned3PL);

        Status = DeliveryRequestStatus.RiderEnRoutePickup;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRiderArrivedPickup()
    {
        EnsureStatus(DeliveryRequestStatus.RiderEnRoutePickup);

        Status = DeliveryRequestStatus.RiderArrivedPickup;
        ArrivedPickupAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPickedUp()
    {
        EnsureStatus(DeliveryRequestStatus.RiderArrivedPickup, DeliveryRequestStatus.RiderEnRoutePickup);

        Status = DeliveryRequestStatus.PickedUp;
        PickedUpAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new DeliveryPickedUpEvent(Id, OrderId, PickedUpAt.Value));
    }

    public void MarkRiderEnRouteDrop()
    {
        EnsureStatus(DeliveryRequestStatus.PickedUp);

        Status = DeliveryRequestStatus.RiderEnRouteDrop;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRiderArrivedDrop()
    {
        EnsureStatus(DeliveryRequestStatus.RiderEnRouteDrop, DeliveryRequestStatus.PickedUp);

        Status = DeliveryRequestStatus.RiderArrivedDrop;
        ArrivedDropAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkWaitingForCustomer()
    {
        EnsureStatus(DeliveryRequestStatus.RiderArrivedDrop);

        Status = DeliveryRequestStatus.WaitingForCustomer;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        EnsureStatus(
            DeliveryRequestStatus.RiderArrivedDrop,
            DeliveryRequestStatus.WaitingForCustomer,
            DeliveryRequestStatus.RiderEnRouteDrop);

        Status = DeliveryRequestStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new DeliveryCompletedEvent(Id, OrderId, DeliveredAt.Value));
    }

    public void MarkFailed(DeliveryFailureReason reason, string? notes = null, string? photoUrl = null)
    {
        Status = DeliveryRequestStatus.Failed;
        FailedAt = DateTime.UtcNow;
        FailureReason = reason;
        FailureNotes = notes;
        FailurePhotoUrl = photoUrl;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new DeliveryFailedEvent(Id, OrderId, reason, notes, FailedAt.Value));
    }

    public void Cancel(string? reason = null)
    {
        if (Status >= DeliveryRequestStatus.PickedUp)
            throw new InvalidOperationException("Cannot cancel after pickup");

        Status = DeliveryRequestStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        FailureNotes = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Rider Offers

    public RiderOffer CreateOffer(
        Guid riderId,
        decimal earnings,
        int expiresInSeconds,
        double? riderLat = null,
        double? riderLng = null,
        decimal? distanceToRestaurant = null)
    {
        var offer = RiderOffer.Create(
            Guid.NewGuid(),
            Id,
            riderId,
            earnings,
            expiresInSeconds,
            riderLat,
            riderLng,
            distanceToRestaurant);

        _riderOffers.Add(offer);
        OwnFleetAttempts++;
        UpdatedAt = DateTime.UtcNow;

        return offer;
    }

    public void ExpireAllPendingOffers()
    {
        foreach (var offer in _riderOffers.Where(o => o.IsPending))
        {
            offer.Expire();
        }
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Helpers

    private void EnsureStatus(params DeliveryRequestStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(Status))
        {
            throw new InvalidOperationException(
                $"Invalid status transition. Current: {Status}, Allowed: {string.Join(", ", allowedStatuses)}");
        }
    }

    #endregion

    /// <summary>
    /// Check if immediate dispatch should be triggered
    /// Returns true if dispatch hasn't started yet
    /// </summary>
    public bool ShouldTriggerImmediateDispatch()
    {
        return Status == DeliveryRequestStatus.Created ||
               Status == DeliveryRequestStatus.PendingDispatch;
    }

    /// <summary>
    /// Start searching for riders
    /// </summary>
    public void StartSearching()
    {
        if (!ShouldTriggerImmediateDispatch())
        {
            return; // Already searching or assigned
        }

        Status = DeliveryRequestStatus.SearchingOwnFleet;
        SearchingStartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}