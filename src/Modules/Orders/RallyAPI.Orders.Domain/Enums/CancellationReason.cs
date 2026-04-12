namespace RallyAPI.Orders.Domain.Enums;

/// <summary>
/// Standardized cancellation reasons for analytics and processing.
/// Add new reasons as business requirements evolve.
/// </summary>
public enum CancellationReason
{
    /// <summary>Customer requested cancellation</summary>
    CustomerRequested = 0,

    /// <summary>Restaurant cannot fulfill the order</summary>
    RestaurantUnavailable = 10,

    /// <summary>One or more items out of stock</summary>
    ItemsOutOfStock = 20,

    /// <summary>Restaurant is closed</summary>
    RestaurantClosed = 30,

    /// <summary>No riders available</summary>
    NoRidersAvailable = 40,

    /// <summary>Payment failed</summary>
    PaymentFailed = 50,

    /// <summary>Delivery address unreachable</summary>
    DeliveryAddressIssue = 60,

    /// <summary>Order timeout - not confirmed in time</summary>
    Timeout = 70,

    /// <summary>Payment not received within time limit</summary>
    PaymentTimeout = 75,

    /// <summary>System/technical error</summary>
    SystemError = 80,

    /// <summary>Fraud suspected</summary>
    FraudSuspected = 90,

    /// <summary>Other reason (see notes)</summary>
    Other = 100
}

public static class CancellationReasonExtensions
{
    /// <summary>
    /// Determines if customer should be refunded for this cancellation reason
    /// </summary>
    public static bool IsRefundable(this CancellationReason reason) => reason switch
    {
        CancellationReason.CustomerRequested => true,  // Configurable based on timing
        CancellationReason.RestaurantUnavailable => true,
        CancellationReason.ItemsOutOfStock => true,
        CancellationReason.RestaurantClosed => true,
        CancellationReason.NoRidersAvailable => true,
        CancellationReason.PaymentFailed => false,
        CancellationReason.PaymentTimeout => false,  // No payment was made
        CancellationReason.DeliveryAddressIssue => true,
        CancellationReason.Timeout => true,
        CancellationReason.SystemError => true,
        CancellationReason.FraudSuspected => false,
        CancellationReason.Other => true,
        _ => false
    };

    /// <summary>
    /// Determines who initiated the cancellation
    /// </summary>
    public static string GetInitiator(this CancellationReason reason) => reason switch
    {
        CancellationReason.CustomerRequested => "Customer",
        CancellationReason.RestaurantUnavailable => "Restaurant",
        CancellationReason.ItemsOutOfStock => "Restaurant",
        CancellationReason.RestaurantClosed => "System",
        CancellationReason.NoRidersAvailable => "System",
        CancellationReason.PaymentFailed => "System",
        CancellationReason.PaymentTimeout => "System",
        CancellationReason.DeliveryAddressIssue => "Rider",
        CancellationReason.Timeout => "System",
        CancellationReason.SystemError => "System",
        CancellationReason.FraudSuspected => "System",
        CancellationReason.Other => "Unknown",
        _ => "Unknown"
    };
}