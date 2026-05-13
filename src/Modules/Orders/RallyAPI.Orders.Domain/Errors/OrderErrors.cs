using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Domain.Errors;

/// <summary>
/// Factory for Order-related errors.
/// Centralized for consistency and easy modification.
/// </summary>
public static class OrderErrors
{
    // Not Found Errors
    public static Error NotFound(Guid orderId) => Error.Create(
        "Order.NotFound",
        $"Order with ID '{orderId}' was not found");

    public static Error NotFoundByNumber(string orderNumber) => Error.Create(
        "Order.NotFoundByNumber",
        $"Order with number '{orderNumber}' was not found");

    // Validation Errors
    public static readonly Error EmptyItems = Error.Create(
        "Order.EmptyItems",
        "Order must contain at least one item");

    public static readonly Error InvalidQuantity = Error.Create(
        "Order.InvalidQuantity",
        "Item quantity must be greater than zero");

    public static readonly Error InvalidTotal = Error.Create(
        "Order.InvalidTotal",
        "Order total must be greater than zero");

    public static Error InvalidCustomer(Guid customerId) => Error.Create(
        "Order.InvalidCustomer",
        $"Customer with ID '{customerId}' was not found or is inactive");

    public static Error InvalidRestaurant(Guid restaurantId) => Error.Create(
        "Order.InvalidRestaurant",
        $"Restaurant with ID '{restaurantId}' was not found or is inactive");

    public static Error InvalidMenuItem(Guid menuItemId) => Error.Create(
        "Order.InvalidMenuItem",
        $"Menu item with ID '{menuItemId}' was not found or is unavailable");

    // Status Errors
    public static Error InvalidStatusTransition(string currentStatus, string targetStatus) => Error.Create(
        "Order.InvalidStatusTransition",
        $"Cannot transition from '{currentStatus}' to '{targetStatus}'");

    public static readonly Error AlreadyConfirmed = Error.Create(
        "Order.AlreadyConfirmed",
        "Order has already been confirmed");

    public static readonly Error AlreadyCancelled = Error.Create(
        "Order.AlreadyCancelled",
        "Order has already been cancelled");

    public static readonly Error AlreadyDelivered = Error.Create(
        "Order.AlreadyDelivered",
        "Order has already been delivered");

    public static Error CannotCancelInStatus(string status) => Error.Create(
        "Order.CannotCancel",
        $"Cannot cancel order in '{status}' status");

    public static Error CannotModifyInStatus(string status) => Error.Create(
        "Order.CannotModify",
        $"Cannot modify order in '{status}' status");

    // Restaurant Errors
    public static readonly Error RestaurantClosed = Error.Create(
        "Order.RestaurantClosed",
        "Restaurant is currently closed");

    public static readonly Error RestaurantNotAcceptingOrders = Error.Create(
        "Order.RestaurantNotAcceptingOrders",
        "Restaurant is not accepting orders at this time");

    public static readonly Error RestaurantDoesNotAcceptPickup = Error.Create(
        "Order.RestaurantDoesNotAcceptPickup",
        "This restaurant does not offer pickup.");

    public static Error RestaurantOutsideDeliveryArea(double distance) => Error.Create(
        "Order.OutsideDeliveryArea",
        $"Delivery address is outside restaurant's delivery area ({distance:F1} km)");

    // Delivery Errors
    public static Error DeliveryQuoteFailed(string reason) => Error.Create(
        "Order.DeliveryQuoteFailed",
        $"Failed to get delivery quote: {reason}");

    public static readonly Error NoDeliveryQuote = Error.Create(
        "Order.NoDeliveryQuote",
        "No delivery quote available for this order");

    public static readonly Error DeliveryQuoteExpired = Error.Create(
        "Order.DeliveryQuoteExpired",
        "Delivery quote has expired. Please request a new quote");

    public static readonly Error NoRidersAvailable = Error.Create(
        "Order.NoRidersAvailable",
        "No riders available for delivery at this time");

    // Rider Errors
    public static Error RiderNotFound(Guid riderId) => Error.Create(
        "Order.RiderNotFound",
        $"Rider with ID '{riderId}' was not found");

    public static readonly Error RiderAlreadyAssigned = Error.Create(
        "Order.RiderAlreadyAssigned",
        "A rider is already assigned to this order");

    public static readonly Error NoRiderAssigned = Error.Create(
        "Order.NoRiderAssigned",
        "No rider is assigned to this order");

    // Payment Errors
    public static readonly Error PaymentRequired = Error.Create(
        "Order.PaymentRequired",
        "Payment is required to proceed with this order");

    public static readonly Error PaymentFailed = Error.Create(
        "Order.PaymentFailed",
        "Payment processing failed");

    //public static readonly Error RefundFailed = Error.Create(
    //    "Order.RefundFailed",
    //    "Refund processing failed");

    public static readonly Error AlreadyPaid = Error.Create(
        "Order.AlreadyPaid",
        "Order has already been paid");

    // Authorization Errors
    public static readonly Error Unauthorized = Error.Create(
        "Order.Unauthorized",
        "You are not authorized to perform this action");

    public static Error NotOrderOwner(Guid orderId) => Error.Create(
        "Order.NotOwner",
        $"You are not the owner of order '{orderId}'");

    public static readonly Error NotRestaurantOrder = Error.Create(
        "Order.NotRestaurantOrder",
        "This order does not belong to your restaurant");

    // Timing Errors
    public static readonly Error OrderExpired = Error.Create(
        "Order.Expired",
        "Order has expired due to timeout");

    public static Error ConfirmationTimeout(int minutes) => Error.Create(
        "Order.ConfirmationTimeout",
        $"Order was not confirmed within {minutes} minutes");

    // Concurrency Errors
    public static readonly Error ConcurrencyConflict = Error.Create(
        "Order.ConcurrencyConflict",
        "Order was modified by another process. Please refresh and try again");

    // Generic Errors
    public static Error Unexpected(string message) => Error.Create(
        "Order.Unexpected",
        $"An unexpected error occurred: {message}");

    // Rejection Errors
    public static readonly Error AlreadyRejected = Error.Create(
        "Order.AlreadyRejected",
        "Order has already been rejected");

    public static Error CannotRejectInStatus(string status) => Error.Create(
        "Order.CannotReject",
        $"Cannot reject order in '{status}' status");

    public static readonly Error RejectionReasonRequired = Error.Create(
        "Order.RejectionReasonRequired",
        "Rejection reason is required");

    // Refund Errors
    public static readonly Error RefundNotApplicable = Error.Create(
        "Order.RefundNotApplicable",
        "Refund is not applicable for this order status");


    public static Error RefundFailed(string reason) => Error.Create(
        "Order.RefundFailed",
        $"Refund failed: {reason}");

    // Payment Errors  
    public static readonly Error PaymentIdRequired = Error.Create(
        "Order.PaymentIdRequired",
        "Payment ID is required to create order");

    public static readonly Error OrderNotPaid = Error.Create(
        "Order.NotPaid",
        "Order must be paid before processing");
}