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

    // Fulfillment
    public FulfillmentType FulfillmentType { get; private set; }

    // Items
    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // Pricing
    public OrderPricing Pricing { get; private set; }

    // Delivery (null for pickup orders)
    public DeliveryInfo? DeliveryInfo { get; private set; }

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

    public bool IsEscalated { get; private set; }
    public DateTime? EscalatedAt { get; private set; }
    public string? EscalationReason { get; private set; }

    private Order(
        OrderNumber orderNumber,
        Guid customerId,
        string customerName,
        string? customerPhone,
        string? customerEmail,
        Guid restaurantId,
        string restaurantName,
        string? restaurantPhone,
        FulfillmentType fulfillmentType,
        DeliveryInfo? deliveryInfo,
        OrderPricing pricing,
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

        // Order starts as PENDING — only webhook can transition to Paid
        Status = OrderStatus.Pending;
        PaymentStatus = PaymentStatus.Pending;

        FulfillmentType = fulfillmentType;
        DeliveryInfo = deliveryInfo;
        Pricing = pricing;

        DeliveryQuoteId = deliveryQuoteId;

        SpecialInstructions = specialInstructions;

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    #region Factory Methods

    /// <summary>
    /// Creates a new pending order awaiting payment.
    /// Only the PayU webhook (after hash verification) can transition to Paid.
    /// </summary>
    public static Order CreatePendingOrder(
        OrderNumber orderNumber,
        Guid customerId,
        string customerName,
        Guid restaurantId,
        string restaurantName,
        OrderPricing pricing,
        FulfillmentType fulfillmentType = FulfillmentType.Delivery,
        DeliveryInfo? deliveryInfo = null,
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

        if (fulfillmentType == FulfillmentType.Delivery && deliveryInfo is null)
            throw new ArgumentNullException(nameof(deliveryInfo), "Delivery info is required for delivery orders");

        if (pricing is null)
            throw new ArgumentNullException(nameof(pricing));

        var order = new Order(
            orderNumber,
            customerId,
            customerName.Trim(),
            customerPhone?.Trim(),
            customerEmail?.Trim(),
            restaurantId,
            restaurantName.Trim(),
            restaurantPhone?.Trim(),
            fulfillmentType,
            deliveryInfo,
            pricing,
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
    /// Confirms payment and transitions order from Pending to Paid.
    /// Called ONLY by the PayU webhook handler (after hash verification) or
    /// the VerifyPayment handler (as a delayed-webhook safety net).
    /// </summary>
    public void ConfirmPayment(string paymentId, string? paymentTransactionId)
    {
        if (Status == OrderStatus.Paid)
            return; // Idempotent

        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm payment for order in {Status} status");

        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("Payment ID is required", nameof(paymentId));

        Status = OrderStatus.Paid;
        PaymentStatus = PaymentStatus.Paid;
        PaymentId = paymentId;
        PaymentTransactionId = paymentTransactionId;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderPaidEvent(
            Id, OrderNumber.Value, CustomerId, RestaurantId,
            Pricing.Total.Amount, _items.Count));
    }

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
    public void Reject(string reason)
    {
        if (!Status.CanBeRejected())
            throw new InvalidOperationException($"Cannot reject order in {Status} status");

        Status = OrderStatus.Rejected;
        RejectionReason = reason.Trim();
        RejectedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderRejectedEvent(Id, OrderNumber.Value, RestaurantId, CustomerId, reason));
    }

    /// <summary>
    /// Marks order as being prepared.
    /// Accepts both Confirmed → Preparing (chain-restaurant 2-step path)
    /// and Paid → Preparing (collapsed single-step accept flow).
    /// When called directly from Paid, ConfirmedAt is set and OrderConfirmedEvent
    /// is also emitted so downstream consumers (delivery dispatch, customer
    /// "confirmed" push, admin feed) fire exactly as they did before the collapse.
    /// </summary>
    public void StartPreparing()
    {
        EnsureValidTransition(OrderStatus.Preparing);

        var skippedConfirmed = Status == OrderStatus.Paid;

        Status = OrderStatus.Preparing;
        PreparingAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        if (skippedConfirmed)
        {
            ConfirmedAt = DateTime.UtcNow;
            AddDomainEvent(new OrderConfirmedEvent(Id, OrderNumber.Value, RestaurantId, CustomerId));
        }

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
    /// Marks order as picked up by rider. Only valid for delivery orders.
    /// </summary>
    public void MarkPickedUp()
    {
        if (FulfillmentType == FulfillmentType.Pickup)
            throw new InvalidOperationException("Pickup orders do not have a rider pickup step. Use MarkDelivered instead.");

        EnsureValidTransition(OrderStatus.PickedUp);

        if (DeliveryInfo is null || !DeliveryInfo.RiderId.HasValue || DeliveryInfo.RiderId == Guid.Empty)
            throw new InvalidOperationException("Cannot mark order as picked up: no rider has been assigned.");

        Status = OrderStatus.PickedUp;
        PickedUpAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        DeliveryInfo.MarkPickedUp();

        AddDomainEvent(new OrderPickedUpEvent(Id, OrderNumber.Value, DeliveryInfo.RiderId));
    }

    /// <summary>
    /// Marks order as delivered (by rider) or collected (by customer for pickup orders).
    /// </summary>
    public void MarkDelivered()
    {
        EnsureValidTransition(OrderStatus.Delivered);

        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        DeliveryInfo?.MarkDelivered();

        AddDomainEvent(new OrderDeliveredEvent(Id, OrderNumber.Value, CustomerId, DeliveryInfo?.RiderId));
    }

    /// <summary>
    /// Assigns a rider to the order. Only valid for delivery orders.
    /// </summary>
    public void AssignRider(Guid? riderId, string? riderName = null, string? riderPhone = null, string? trackingUrl = null)
    {
        if (FulfillmentType == FulfillmentType.Pickup)
            throw new InvalidOperationException("Cannot assign rider to a pickup order");

        if (DeliveryInfo is null)
            throw new InvalidOperationException("Cannot assign rider: no delivery info");

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
    /// <param name="allowAnyActive">
    /// When true, allows cancellation from any non-terminal state (Preparing, ReadyForPickup,
    /// PickedUp). Reserved for admin-overridden cancellations. When false (default),
    /// only the normal allowlist (Pending/Paid/Confirmed) applies.
    /// </param>
    public void Cancel(
        CancellationReason reason,
        Guid? cancelledBy = null,
        string? notes = null,
        bool allowAnyActive = false)
    {
        var canCancel = allowAnyActive
            ? !Status.IsTerminal()
            : Status.CanBeCancelled();

        if (!canCancel)
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
    /// Pickup orders skip PickedUp and go directly ReadyForPickup → Delivered.
    /// </summary>
    public IReadOnlyList<OrderStatus> GetValidTransitions()
    {
        return Status switch
        {
            OrderStatus.Pending => new[] { OrderStatus.Paid, OrderStatus.Cancelled },
            // Paid → Preparing is the collapsed single-step accept flow used today.
            // Paid → Confirmed is kept for future chain-restaurant 2-step flow that
            // needs an explicit accept-then-prepare gate (e.g., Domino's-style queues).
            OrderStatus.Paid => new[] { OrderStatus.Preparing, OrderStatus.Confirmed, OrderStatus.Rejected, OrderStatus.Cancelled },
            OrderStatus.Confirmed => new[] { OrderStatus.Preparing, OrderStatus.Cancelled },
            OrderStatus.Preparing => new[] { OrderStatus.ReadyForPickup },
            OrderStatus.ReadyForPickup => FulfillmentType == FulfillmentType.Pickup
                ? new[] { OrderStatus.Delivered }
                : new[] { OrderStatus.PickedUp },
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

        if (FulfillmentType == FulfillmentType.Pickup || DeliveryInfo is null)
            throw new InvalidOperationException("Cannot update rider for a pickup order");

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
    /// Validates the order after all items are set.
    /// Called during order creation to ensure order is valid before persisting.
    /// </summary>
    public void ValidateOrder()
    {
        if (!_items.Any())
            throw new InvalidOperationException("Cannot create order without items");

        if (Pricing.Total.Amount <= 0)
            throw new InvalidOperationException("Order total must be greater than zero");
    }

    #endregion

    /// <summary>
    /// Stage 1: Escalate order to admin when restaurant doesn't confirm in time.
    /// Flags the order and raises a domain event for admin notification.
    /// </summary>
    public void EscalateToAdmin(string reason)
    {
        if (Status != OrderStatus.Paid)
            return; // Only escalate orders waiting for restaurant confirmation

        if (IsEscalated)
            return; // Already escalated, don't raise duplicate events

        IsEscalated = true;
        EscalatedAt = DateTime.UtcNow;
        EscalationReason = reason;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new Events.OrderEscalatedToAdminEvent(
            Id,
            OrderNumber.Value,  // adjust if OrderNumber is a string, not a value object
            RestaurantId,
            reason,
            EscalatedAt.Value));
    }
}