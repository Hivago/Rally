//using RallyAPI.SharedKernel.Domain;

//namespace RallyAPI.SharedKernel.IntegrationEvents.Orders;

///// <summary>
///// Published when restaurant confirms an order.
///// Consumed by Delivery module to create DeliveryRequest.
///// </summary>
//public sealed class OrderConfirmedIntegrationEvent : BaseDomainEvent
//{
//    public Guid OrderId { get; }
//    public string OrderNumber { get; }
//    public Guid RestaurantId { get; }
//    public Guid CustomerId { get; }

//    public OrderConfirmedIntegrationEvent(
//        Guid orderId,
//        string orderNumber,
//        Guid restaurantId,
//        Guid customerId)
//    {
//        OrderId = orderId;
//        OrderNumber = orderNumber;
//        RestaurantId = restaurantId;
//        CustomerId = customerId;
//    }
//}


using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Orders;

/// <summary>
/// Raised when restaurant confirms an order.
/// Contains ALL data needed by Admin to dispatch rider manually.
/// </summary>
public sealed class OrderConfirmedIntegrationEvent : BaseDomainEvent
{
    #region Order Identifiers

    public Guid OrderId { get; }
    public string OrderNumber { get; }
    public Guid RestaurantId { get; }
    public Guid CustomerId { get; }

    #endregion

    #region Pickup Details (Restaurant) - Admin forwards to Rider

    public string RestaurantName { get; }
    public string RestaurantPhone { get; }
    public string PickupAddress { get; }
    public double PickupLatitude { get; }
    public double PickupLongitude { get; }
    public string PickupPincode { get; }

    #endregion

    #region Drop Details (Customer) - Admin forwards to Rider

    public string CustomerName { get; }
    public string CustomerPhone { get; }
    public string DropAddress { get; }
    public double DropLatitude { get; }
    public double DropLongitude { get; }
    public string DropPincode { get; }

    #endregion

    #region Order Details

    public int ItemCount { get; }
    public decimal TotalAmount { get; }
    public string? DeliveryInstructions { get; }
    public Guid? QuoteId { get; }
    public DateTime ConfirmedAt { get; }

    #endregion

    public OrderConfirmedIntegrationEvent(
        // Identifiers
        Guid orderId,
        string orderNumber,
        Guid restaurantId,
        Guid customerId,
        // Pickup (Restaurant)
        string restaurantName,
        string restaurantPhone,
        string pickupAddress,
        double pickupLatitude,
        double pickupLongitude,
        string pickupPincode,
        // Drop (Customer)
        string customerName,
        string customerPhone,
        string dropAddress,
        double dropLatitude,
        double dropLongitude,
        string dropPincode,
        // Order Details
        int itemCount,
        decimal totalAmount,
        string? deliveryInstructions,
        Guid? quoteId,
        DateTime confirmedAt)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        RestaurantId = restaurantId;
        CustomerId = customerId;

        RestaurantName = restaurantName;
        RestaurantPhone = restaurantPhone;
        PickupAddress = pickupAddress;
        PickupLatitude = pickupLatitude;
        PickupLongitude = pickupLongitude;
        PickupPincode = pickupPincode;

        CustomerName = customerName;
        CustomerPhone = customerPhone;
        DropAddress = dropAddress;
        DropLatitude = dropLatitude;
        DropLongitude = dropLongitude;
        DropPincode = dropPincode;

        ItemCount = itemCount;
        TotalAmount = totalAmount;
        DeliveryInstructions = deliveryInstructions;
        QuoteId = quoteId;
        ConfirmedAt = confirmedAt;
    }
}