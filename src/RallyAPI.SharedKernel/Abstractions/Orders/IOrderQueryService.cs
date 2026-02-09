namespace RallyAPI.SharedKernel.Abstractions.Orders;

/// <summary>
/// Query service for cross-module order data access
/// </summary>
public interface IOrderQueryService
{
    Task<OrderDeliveryDetails?> GetOrderDeliveryDetailsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for order delivery details - used by Delivery module
/// </summary>
public record OrderDeliveryDetails(
    Guid OrderId,
    string OrderNumber,
    Guid? QuoteId,

    // Pickup (Restaurant)
    double PickupLatitude,
    double PickupLongitude,
    string PickupPincode,
    string PickupAddress,
    string RestaurantName,
    string RestaurantPhone,

    // Drop (Customer)
    double DropLatitude,
    double DropLongitude,
    string DropPincode,
    string DropAddress,
    string CustomerName,
    string CustomerPhone,

    // Order info
    int ItemCount
);