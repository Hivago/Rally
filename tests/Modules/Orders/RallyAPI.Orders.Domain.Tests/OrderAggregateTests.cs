using FluentAssertions;
using RallyAPI.Orders.Domain.Entities;
using Xunit;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.Orders.Domain.ValueObjects;

namespace RallyAPI.Orders.Domain.Tests;

public class OrderAggregateTests
{
    #region Test Helpers

    private static Order CreatePendingOrder(
        Guid? customerId = null,
        Guid? restaurantId = null)
    {
        var deliveryAddress = Address.Create(
            street: "123 MG Road",
            city: "Bengaluru",
            pincode: "560001",
            latitude: 12.9716,
            longitude: 77.5946);

        var deliveryInfo = DeliveryInfo.Create(
            pickupLatitude: 12.9352,
            pickupLongitude: 77.6245,
            pickupPincode: "560095",
            deliveryAddress: deliveryAddress,
            pickupAddress: "Restaurant Street");

        var pricing = OrderPricing.CreateSimple(
            subTotal: 250m,
            deliveryFee: 40m);

        return Order.CreatePendingOrder(
            orderNumber: OrderNumber.Create(dailySequence: 1),
            customerId: customerId ?? Guid.NewGuid(),
            customerName: "Ravi Kumar",
            restaurantId: restaurantId ?? Guid.NewGuid(),
            restaurantName: "Biryani House",
            deliveryInfo: deliveryInfo,
            pricing: pricing);
    }

    private static Order CreatePaidOrder(
        Guid? customerId = null,
        Guid? restaurantId = null)
    {
        var order = CreatePendingOrder(customerId, restaurantId);
        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Test Item",
            Money.FromDecimal(250m, "INR"), 1));
        order.ConfirmPayment("PAY-001", null);
        return order;
    }

    #endregion

    #region CreatePendingOrder

    [Fact]
    public void CreatePendingOrder_WithValidData_ShouldHavePendingStatus()
    {
        var order = CreatePendingOrder();

        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);
        order.PaymentId.Should().BeNull();
    }

    [Fact]
    public void CreatePendingOrder_WithEmptyCustomerId_ShouldThrow()
    {
        var act = () => CreatePendingOrder(customerId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*Customer ID*");
    }

    [Fact]
    public void CreatePendingOrder_WithEmptyRestaurantId_ShouldThrow()
    {
        var act = () => CreatePendingOrder(restaurantId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*Restaurant ID*");
    }

    #endregion

    #region ConfirmPayment

    [Fact]
    public void ConfirmPayment_WhenPending_ShouldTransitionToPaid()
    {
        var order = CreatePendingOrder();
        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Biryani",
            Money.FromDecimal(250m, "INR"), 1));

        order.ConfirmPayment("PAY-001", "PAYU-123");

        order.Status.Should().Be(OrderStatus.Paid);
        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
        order.PaymentId.Should().Be("PAY-001");
        order.PaymentTransactionId.Should().Be("PAYU-123");
    }

    [Fact]
    public void ConfirmPayment_WhenPending_ShouldRaiseOrderPaidEvent()
    {
        var order = CreatePendingOrder();
        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Biryani",
            Money.FromDecimal(250m, "INR"), 1));

        order.ConfirmPayment("PAY-001", null);

        order.DomainEvents.Should().ContainSingle(e => e is OrderPaidEvent);
    }

    [Fact]
    public void ConfirmPayment_WhenAlreadyPaid_ShouldBeIdempotent()
    {
        var order = CreatePaidOrder();

        order.ConfirmPayment("PAY-002", null);

        order.Status.Should().Be(OrderStatus.Paid);
        order.PaymentId.Should().Be("PAY-001"); // Original payment ID preserved
    }

    [Fact]
    public void ConfirmPayment_WhenConfirmed_ShouldThrow()
    {
        var order = CreatePaidOrder();
        order.Confirm();

        var act = () => order.ConfirmPayment("PAY-002", null);

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Confirm

    [Fact]
    public void Confirm_WhenOrderIsPaid_ShouldTransitionToConfirmed()
    {
        var order = CreatePaidOrder();

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public void Confirm_WhenOrderIsPaid_ShouldRaiseDomainEvent()
    {
        var order = CreatePaidOrder();

        order.Confirm();

        order.DomainEvents.Should().ContainSingle(e => e is OrderConfirmedEvent);
    }

    [Fact]
    public void Confirm_WhenOrderIsAlreadyConfirmed_ShouldThrow()
    {
        var order = CreatePaidOrder();
        order.Confirm();

        var act = () => order.Confirm();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Confirm_WhenOrderIsCancelled_ShouldThrow()
    {
        var order = CreatePaidOrder();
        order.Cancel(CancellationReason.CustomerRequested);

        var act = () => order.Confirm();

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Reject

    [Fact]
    public void Reject_WhenOrderIsPaid_ShouldTransitionToRejected()
    {
        var order = CreatePaidOrder();

        order.Reject("Out of stock");

        order.Status.Should().Be(OrderStatus.Rejected);
        order.RejectionReason.Should().Be("Out of stock");
        order.RejectedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reject_WhenOrderIsPaid_ShouldRaiseDomainEvent()
    {
        var order = CreatePaidOrder();

        order.Reject("Kitchen closed");

        order.DomainEvents.Should().ContainSingle(e => e is OrderRejectedEvent);
    }

    [Fact]
    public void Reject_WhenOrderIsConfirmed_ShouldThrow()
    {
        var order = CreatePaidOrder();
        order.Confirm();

        var act = () => order.Reject("Item unavailable");

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Cancel

    [Fact]
    public void Cancel_WhenOrderIsPaid_ShouldTransitionToCancelled()
    {
        var order = CreatePaidOrder();
        var cancelledBy = Guid.NewGuid();

        order.Cancel(CancellationReason.CustomerRequested, cancelledBy, "Changed my mind");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be(CancellationReason.CustomerRequested);
        order.CancelledBy.Should().Be(cancelledBy);
        order.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhenOrderIsDelivered_ShouldThrow()
    {
        var order = CreatePaidOrder();
        var riderId = Guid.NewGuid();
        order.Confirm();
        order.StartPreparing();
        order.MarkReadyForPickup();
        order.AssignRider(riderId);
        order.MarkPickedUp();
        order.MarkDelivered();

        var act = () => order.Cancel(CancellationReason.CustomerRequested);

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Status Transitions

    [Fact]
    public void FullLifecycle_HappyPath_ShouldTransitionThroughAllStatuses()
    {
        var order = CreatePaidOrder();
        var riderId = Guid.NewGuid();

        order.Status.Should().Be(OrderStatus.Paid);

        order.Confirm();
        order.Status.Should().Be(OrderStatus.Confirmed);

        order.StartPreparing();
        order.Status.Should().Be(OrderStatus.Preparing);

        order.MarkReadyForPickup();
        order.Status.Should().Be(OrderStatus.ReadyForPickup);

        order.AssignRider(riderId, "Suresh", "+919876543210");
        order.MarkPickedUp();
        order.Status.Should().Be(OrderStatus.PickedUp);

        order.MarkDelivered();
        order.Status.Should().Be(OrderStatus.Delivered);
        order.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void GetValidTransitions_WhenPaid_ShouldReturnConfirmedRejectedCancelled()
    {
        var order = CreatePaidOrder();

        var transitions = order.GetValidTransitions();

        transitions.Should().BeEquivalentTo(new[]
        {
            OrderStatus.Confirmed,
            OrderStatus.Rejected,
            OrderStatus.Cancelled
        });
    }

    [Fact]
    public void MarkPickedUp_WhenNotReadyForPickup_ShouldThrow()
    {
        var order = CreatePaidOrder();
        order.Confirm();
        order.StartPreparing();

        var act = () => order.MarkPickedUp();

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region EscalateToAdmin

    [Fact]
    public void EscalateToAdmin_WhenOrderIsPaid_ShouldSetEscalatedFlag()
    {
        var order = CreatePaidOrder();

        order.EscalateToAdmin("Restaurant not responding");

        order.IsEscalated.Should().BeTrue();
        order.EscalationReason.Should().Be("Restaurant not responding");
        order.EscalatedAt.Should().NotBeNull();
    }

    [Fact]
    public void EscalateToAdmin_WhenAlreadyEscalated_ShouldNotRaiseDuplicateEvent()
    {
        var order = CreatePaidOrder();
        order.EscalateToAdmin("First escalation");

        order.EscalateToAdmin("Second escalation");

        order.DomainEvents.OfType<OrderEscalatedToAdminEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void EscalateToAdmin_WhenOrderIsConfirmed_ShouldNotEscalate()
    {
        var order = CreatePaidOrder();
        order.Confirm();

        order.EscalateToAdmin("Too late");

        order.IsEscalated.Should().BeFalse();
    }

    #endregion
}
