using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Application.DTOs.Requests;

/// <summary>
/// Request model for placing a new order.
/// Pricing is pre-calculated by Cart/Delivery Module.
/// </summary>
public sealed record PlaceOrderRequest
{
    // === FULFILLMENT TYPE (Delivery or Pickup) ===
    /// <summary>
    /// How the customer receives the order. Defaults to Delivery.
    /// </summary>
    public FulfillmentType FulfillmentType { get; init; } = FulfillmentType.Delivery;

    // === PAYMENT INFO (Required - order is post-payment) ===
    public string PaymentId { get; init; } = string.Empty;
    public string? PaymentTransactionId { get; init; }
    public string? DeliveryQuoteId { get; init; }


    public Guid RestaurantId { get; init; }
    public string RestaurantName { get; init; } = string.Empty;
    public string? RestaurantPhone { get; init; }

    // Pickup location (restaurant coordinates)
    public double PickupLatitude { get; init; }
    public double PickupLongitude { get; init; }
    public string PickupPincode { get; init; } = string.Empty;
    public string? PickupAddress { get; init; }

    // Delivery address (required for Delivery, ignored for Pickup)
    public DeliveryAddressRequest? DeliveryAddress { get; init; }

    // Items
    public IReadOnlyList<OrderItemRequest> Items { get; init; } = Array.Empty<OrderItemRequest>();

    // Pre-calculated pricing (from Cart + Delivery Module)
    public OrderPricingRequest Pricing { get; init; } = new();

    // Optional
    public string? SpecialInstructions { get; init; }
}

/// <summary>
/// Delivery address in order request.
/// </summary>
public sealed record DeliveryAddressRequest
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Pincode { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Landmark { get; init; }
    public string? BuildingName { get; init; }
    public string? Floor { get; init; }
    public string? ContactPhone { get; init; }
    public string? Instructions { get; init; }
}

/// <summary>
/// Order item in order request.
/// </summary>
public sealed record OrderItemRequest
{
    public Guid MenuItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? ItemDescription { get; init; }
    public string? ImageUrl { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public string? SpecialInstructions { get; init; }
}

/// <summary>
/// Pre-calculated pricing in order request.
/// </summary>
public sealed record OrderPricingRequest
{
    public decimal SubTotal { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal Tax { get; init; }
    public decimal Discount { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal ServiceFee { get; init; }
    public decimal Tip { get; init; }
    public string? DiscountCode { get; init; }
    public string? DiscountDescription { get; init; }
}