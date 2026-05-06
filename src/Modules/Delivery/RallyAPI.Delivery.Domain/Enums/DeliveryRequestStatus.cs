namespace RallyAPI.Delivery.Domain.Enums;

public enum DeliveryRequestStatus
{
    /// <summary>
    /// Initial state - delivery request created.
    /// </summary>
    Created = 10,

    /// <summary>
    /// Waiting for scheduled dispatch time.
    /// </summary>
    PendingDispatch = 15,

    /// <summary>
    /// Searching for own fleet riders.
    /// </summary>
    SearchingOwnFleet = 20,

    /// <summary>
    /// Searching via 3PL provider.
    /// </summary>
    Searching3PL = 25,

    /// <summary>
    /// Rider assigned (own fleet).
    /// </summary>
    RiderAssigned = 30,

    /// <summary>
    /// Rider assigned via 3PL.
    /// </summary>
    Assigned3PL = 31,

    /// <summary>
    /// Rider heading to pickup location.
    /// </summary>
    RiderEnRoutePickup = 35,

    /// <summary>
    /// Rider arrived at restaurant.
    /// </summary>
    RiderArrivedPickup = 40,

    /// <summary>
    /// Order picked up by rider.
    /// </summary>
    PickedUp = 50,

    /// <summary>
    /// Rider heading to drop location.
    /// </summary>
    RiderEnRouteDrop = 55,

    /// <summary>
    /// Rider arrived at customer location.
    /// </summary>
    RiderArrivedDrop = 60,

    /// <summary>
    /// Waiting for customer (customer not available).
    /// </summary>
    WaitingForCustomer = 65,

    /// <summary>
    /// Successfully delivered.
    /// </summary>
    Delivered = 70,

    /// <summary>
    /// RTO (Return To Origin) initiated. Triggered by LSP when delivery fails
    /// (customer unreachable / refused). Not callable from Hivago — only set via
    /// the ProRouting status callback.
    /// </summary>
    RtoInitiated = 75,

    /// <summary>
    /// Order returned to the store and confirmed via RTO OTP.
    /// </summary>
    RtoDelivered = 76,

    /// <summary>
    /// Order disposed by the LSP. Only valid for FoodAndBeverage orders;
    /// Grocery / Pharma must be returned (RtoDelivered) instead.
    /// </summary>
    RtoDisposed = 77,

    /// <summary>
    /// Cancelled before pickup.
    /// </summary>
    Cancelled = 80,

    /// <summary>
    /// Delivery failed.
    /// </summary>
    Failed = 90
}