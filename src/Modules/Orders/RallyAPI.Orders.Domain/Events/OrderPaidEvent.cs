using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.Events;

public sealed class OrderPaidEvent : BaseDomainEvent
{
    public Guid OrderId { get; }
    public string OrderNumber { get; }
    public Guid CustomerId { get; }
    public Guid RestaurantId { get; }
    public string RestaurantName { get; }
    public decimal Amount { get; }
    public int ItemCount { get; }

    public OrderPaidEvent(
        Guid orderId,
        string orderNumber,
        Guid customerId,
        Guid restaurantId,
        string restaurantName,
        decimal amount,
        int itemCount)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        CustomerId = customerId;
        RestaurantId = restaurantId;
        RestaurantName = restaurantName;
        Amount = amount;
        ItemCount = itemCount;
    }
}