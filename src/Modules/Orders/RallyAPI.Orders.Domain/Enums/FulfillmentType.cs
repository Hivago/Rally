namespace RallyAPI.Orders.Domain.Enums;

/// <summary>
/// How the customer receives the order.
/// </summary>
public enum FulfillmentType
{
    /// <summary>Rider delivers to customer address</summary>
    Delivery = 0,

    /// <summary>Customer picks up from restaurant</summary>
    Pickup = 10
}
