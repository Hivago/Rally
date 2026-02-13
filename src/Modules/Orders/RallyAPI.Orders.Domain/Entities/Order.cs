using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.Orders.Domain.ValueObjects;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.Entities;

/// <summary>
/// Order Aggregate Root - the central entity for order management.
/// All modifications go through this class to maintain invariants.
/// </summary>
public sealed class Order : AggregateRoot
{
    // Identity
    public OrderNumber OrderNumber { get; private set; }

    // Participants
    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; }
    public string? CustomerPhone { get; private set; }
    public string? CustomerEmail { get; private set; }

    public Guid RestaurantId { get; private set; }
    public string RestaurantName { get; private set; }
    public string? RestaurantPhone { get; private set; }

    // Status
    public OrderStatus Status { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }

    // Items
    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // Pricing
    public OrderPricing Pricing { get; private set; }

    // Delivery
    public DeliveryInfo DeliveryInfo { get; private set; }

    // Payment Reference
    public string? PaymentId { get; private set; }
    public string? PaymentTransactionId { get; private set; }

    // Delivery Quote Reference (from Delivery Module)
    public string? DeliveryQuoteId { get; private set; }

    // Timestamps
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public DateTime? PreparingAt { get; private set; }
    public DateTime? ReadyAt { get; private set; }
    public DateTime? PickedUpAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Rejection/Cancellation
    public CancellationReason? CancellationReason { get; private set; }
    public string? CancellationNotes { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? CancelledBy { get; private set; }

    // Instructions & Notes
    public string? SpecialInstructions { get; private set; }
    public string? InternalNotes { get; private set; }

    // Metadata (flexible JSON for future needs)
    public string? Metadata { get; private set; }

    // EF Core constructor
    private Order() { }

    private Order(
        OrderNumber orderNumber,
        Guid customerId,
        string customerName,
        string? customerPhone,
        string? customerEmail,
        Guid restaurantId,
        string restaurantName,
        string? restaurantPhone,
        DeliveryInfo deliveryInfo,
        OrderPricing pricing,
        string? paymentId,
        string? paymentTransactionId,
        string? deliveryQuoteId,
        string? specialInstructions)
    {
        Id = Guid.NewGuid();
        OrderNumber = orderNumber;

        CustomerId = customerId;
        CustomerName = customerName;
        CustomerPhone = customerPhone;
        CustomerEmail = customerEmail;

        RestaurantId = restaurantId;
        RestaurantName = restaurantName;
        RestaurantPhone = restaurantPhone;

        // Order starts as PAID (customer already paid)
        Status = OrderStatus.Paid;
        PaymentStatus = PaymentStatus.Paid;

        DeliveryInfo = deliveryInfo;
        Pricing = pricing;

        PaymentId = paymentId;
        PaymentTransactionId = paymentTransactionId;
        DeliveryQuoteId = deliveryQuoteId;

        SpecialInstructions = specialInstructions;

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    #region Factory Methods

    /// <summary>
    /// Creates a new paid order.
    /// Called AFTER payment is successful.
    /// </summary>
    public static Order CreatePaidOrder(
        OrderNumber orderNumber,
        Guid customerId,
        string customerName,
        Guid restaurantId,
        string restaurantName,
        DeliveryInfo deliveryInfo,
        OrderPricing pricing,
        string paymentId,
        string? paymentTransactionId = null,
        string? deliveryQuoteId = null,
        string? customerPhone = null,
        string? customerEmail = null,
        string? restaurantPhone = null,
        string? specialInstructions = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID is required", nameof(customerId));

        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required", nameof(customerName));

        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant ID is required", nameof(restaurantId));

        if (string.IsNullOrWhiteSpace(restaurantName))
            throw new ArgumentException("Restaurant name is required", nameof(restaurantName));

        if (deliveryInfo is null)
            throw new ArgumentNullException(nameof(deliveryInfo));

        if (pricing is null)
            throw new ArgumentNullException(nameof(pricing));

        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("Payment ID is required", nameof(paymentId));

        var order = new Order(
            orderNumber,
            customerId,
            customerName.Trim(),
            customerPhone?.Trim(),
            customerEmail?.Trim(),
            restaurantId,
            restaurantName.Trim(),
            restaurantPhone?.Trim(),
            deliveryInfo,
            pricing,
            paymentId,
            paymentTransactionId,
            deliveryQuoteId,
            specialInstructions?.Trim());

        return order;
    }

    #endregion

    #region Item Management

    /// <summary>
    /// Adds an item to the order. Only during creation.
    /// </summary>
    public void AddItem(OrderItem item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        _items.Add(item);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds multiple items to the order.
    /// </summary>
    public void AddItems(IEnumerable<OrderItem> items)
    {
        foreach (var item in items)
        {
            if (item is null) continue;
            _items.Add(item);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Status Transitions

    /// <summary>
    /// Restaurant confirms/accepts the order.
    /// </summary>
    public void Confirm()
    {
        EnsureValidTransition(OrderStatus.Confirmed);

        Status = OrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderConfirmedEvent(Id, OrderNumber.Value, RestaurantId, CustomerId));
    }

    /// <summary>
    /// Restaurant rejects the order.
    /// </summary>
    public void Reject(string? reason = null)
    {
        if (!Status.CanBeRejected())
            throw new InvalidOperationException($"Cannot reject order in {Status} status");

        Status = OrderStatus.Rejected;
        RejectionReason = reason?.Trim();
        RejectedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderRejectedEvent(Id, OrderNumber.Value, RestaurantId, CustomerId, reason));
    }

    /// <summary>
    /// Marks order as being prepared.
    /// </summary>
    public void StartPreparing()
    {
        EnsureValidTransition(OrderStatus.Preparing);

        Status = OrderStatus.Preparing;
        PreparingAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderPreparingEvent(Id, OrderNumber.Value));
    }

    /// <summary>
    /// Marks order as ready for pickup.
    /// </summary>
    public void MarkReadyForPickup()
    {
        EnsureValidTransition(OrderStatus.ReadyForPickup);

        Status = OrderStatus.ReadyForPickup;
        ReadyAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderReadyForPickupEvent(Id, OrderNumber.Value, RestaurantId));
    }

    /// <summary>
    /// Marks order as picked up by rider.
    /// </summary>
    public void MarkPickedUp()
    {
        EnsureValidTransition(OrderStatus.PickedUp);

        Status = OrderStatus.PickedUp;
        PickedUpAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        DeliveryInfo.MarkPickedUp();

        AddDomainEvent(new OrderPickedUpEvent(Id, OrderNumber.Value, DeliveryInfo.RiderId));
    }

    /// <summary>
    /// Marks order as delivered.
    /// </summary>
    public void MarkDelivered()
    {
        EnsureValidTransition(OrderStatus.Delivered);

        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        DeliveryInfo.MarkDelivered();

        AddDomainEvent(new OrderDeliveredEvent(Id, OrderNumber.Value, CustomerId, DeliveryInfo.RiderId));
    }

    /// <summary>
    /// Assigns a rider to the order.
    /// </summary>
    public void AssignRider(Guid? riderId, string? riderName = null, string? riderPhone = null, string? trackingUrl = null)
    {
        // 3PL might not have a Guid RiderId, so we use empty for domestic consistency if needed
        // but DeliveryInfo.AssignRider requires it. If null/empty, we handle gracefully.
        DeliveryInfo.AssignRider(riderId ?? Guid.Empty, riderName, riderPhone);
        
        if (!string.IsNullOrWhiteSpace(trackingUrl))
        {
            DeliveryInfo.SetTrackingUrl(trackingUrl);
        }
        
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new RiderAssignedEvent(Id, OrderNumber.Value, riderId ?? Guid.Empty));
    }

    /// <summary>
    /// Cancels the order.
    /// </summary>
    public void Cancel(CancellationReason reason, Guid? cancelledBy = null, string? notes = null)
    {
        if (!Status.CanBeCancelled())
            throw new InvalidOperationException($"Cannot cancel order in {Status} status");

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        CancelledBy = cancelledBy;
        CancellationNotes = notes?.Trim();
        CancelledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderCancelledEvent(Id, OrderNumber.Value, reason, cancelledBy));
    }

    /// <summary>
    /// Marks order as failed.
    /// </summary>
    public void MarkFailed(string? reason = null)
    {
        Status = OrderStatus.Failed;
        CancellationNotes = reason?.Trim();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderFailedEvent(Id, OrderNumber.Value, reason));
    }

    /// <summary>
    /// Marks refund as initiated.
    /// </summary>
    public void InitiateRefund()
    {
        if (!Status.RequiresRefund())
            throw new InvalidOperationException($"Refund not applicable for {Status} status");

        Status = OrderStatus.Refunding;
        PaymentStatus = PaymentStatus.RefundInitiated;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new RefundInitiatedEvent(Id, OrderNumber.Value, Pricing.Total.Amount));
    }

    /// <summary>
    /// Marks refund as completed.
    /// </summary>
    public void CompleteRefund()
    {
        Status = OrderStatus.Refunded;
        PaymentStatus = PaymentStatus.Refunded;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new RefundCompletedEvent(Id, OrderNumber.Value, Pricing.Total.Amount));
    }

    private void EnsureValidTransition(OrderStatus targetStatus)
    {
        var validTransitions = GetValidTransitions();

        if (!validTransitions.Contains(targetStatus))
            throw new InvalidOperationException(
                $"Invalid status transition from {Status} to {targetStatus}");
    }

    /// <summary>
    /// Gets valid status transitions from current status.
    /// </summary>
    public IReadOnlyList<OrderStatus> GetValidTransitions()
    {
        return Status switch
        {
            OrderStatus.Paid => new[] { OrderStatus.Confirmed, OrderStatus.Rejected, OrderStatus.Cancelled },
            OrderStatus.Confirmed => new[] { OrderStatus.Preparing, OrderStatus.Cancelled },
            OrderStatus.Preparing => new[] { OrderStatus.ReadyForPickup },
            OrderStatus.ReadyForPickup => new[] { OrderStatus.PickedUp },
            OrderStatus.PickedUp => new[] { OrderStatus.Delivered, OrderStatus.Failed },
            _ => Array.Empty<OrderStatus>()
        };
    }

    #endregion

    #region Rider Management (Updated by Delivery Module via Events)

    /// <summary>
    /// Updates rider info (called when Delivery Module assigns rider).
    /// </summary>
    public void UpdateRiderInfo(Guid? riderId, string? riderName = null, string? riderPhone = null)
    {
        if (Status.IsTerminal())
            throw new InvalidOperationException("Cannot update rider for completed order");

        DeliveryInfo.AssignRider(riderId ?? Guid.Empty, riderName, riderPhone);
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Notes & Metadata

    /// <summary>
    /// Updates internal notes (for staff).
    /// </summary>
    public void UpdateInternalNotes(string? notes)
    {
        InternalNotes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets metadata JSON.
    /// </summary>
    public void SetMetadata(string? metadata)
    {
        Metadata = metadata;
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Finalization

    /// <summary>
    /// Finalizes the order after all items are set.
    /// Raises OrderPaidEvent (order is ready for restaurant).
    /// </summary>
    public void FinalizeOrder()
    {
        if (!_items.Any())
            throw new InvalidOperationException("Cannot finalize order without items");

        if (Pricing.Total.Amount <= 0)
            throw new InvalidOperationException("Order total must be greater than zero");

        AddDomainEvent(new OrderPaidEvent(
            Id,
            OrderNumber.Value,
            CustomerId,
            RestaurantId,
            Pricing.Total.Amount,
            _items.Count));
    }

    #endregion
}