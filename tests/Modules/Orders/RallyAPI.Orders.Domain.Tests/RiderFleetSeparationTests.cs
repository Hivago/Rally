using FluentAssertions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.Orders.Domain.ValueObjects;
using Xunit;

namespace RallyAPI.Orders.Domain.Tests;

/// <summary>
/// Own-fleet riders (Rally riders with an account and an internal id) and third-party/3PL
/// riders (ProRouting agents, external, no Rally account) must never be mistaken for one
/// another. The rule the whole system rests on:
///
///   own fleet  => DeliveryInfo.RiderId is the rider's internal id
///   3PL        => DeliveryInfo.RiderId is ALWAYS null; the rider is name/phone only
///
/// That null is load-bearing: every own-rider query (earnings, stats, order auth, active
/// deliveries) matches on RiderId, so a 3PL agent carrying an id would leak into rider
/// payouts and could unlock another rider's order. Conversely, AssignedAt — not RiderId —
/// is the "a rider is assigned" signal, because 3PL riders legitimately have no id.
///
/// Regression origin: 3PL assignment used to throw (null id coerced to Guid.Empty), which
/// the event pipeline swallowed, stranding real orders at ReadyForPickup while the 3PL
/// rider actually delivered them (ORD-20260716-00303).
/// </summary>
public class RiderFleetSeparationTests
{
    #region Test Helpers

    private static Order CreateDeliveryOrderReadyForPickup()
    {
        var order = CreatePaidDeliveryOrder();
        order.Confirm();
        order.StartPreparing();
        order.MarkReadyForPickup();
        return order;
    }

    private static Order CreatePaidDeliveryOrder()
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

        var order = Order.CreatePendingOrder(
            orderNumber: OrderNumber.Create(dailySequence: 1),
            customerId: Guid.NewGuid(),
            customerName: "Ravi Kumar",
            restaurantId: Guid.NewGuid(),
            restaurantName: "Biryani House",
            pricing: OrderPricing.CreateSimple(subTotal: 250m, deliveryFee: 40m),
            deliveryInfo: deliveryInfo);

        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Chicken Biryani",
            Money.FromDecimal(250m, "INR"), 1));
        order.ConfirmPayment("PAY-001", null);
        return order;
    }

    #endregion

    #region Own-fleet assignment

    [Fact]
    public void AssignRider_WithOwnFleetRider_ShouldStoreInternalRiderId()
    {
        var order = CreateDeliveryOrderReadyForPickup();
        var riderId = Guid.NewGuid();

        order.AssignRider(riderId, isOwnFleet: true, "Suresh", "+919876543210");

        order.DeliveryInfo!.RiderId.Should().Be(riderId);
        order.DeliveryInfo.RiderName.Should().Be("Suresh");
        order.DeliveryInfo.RiderPhone.Should().Be("+919876543210");
        order.DeliveryInfo.AssignedAt.Should().NotBeNull();
    }

    [Fact]
    public void AssignRider_WithOwnFleetButNoRiderId_ShouldThrow()
    {
        var order = CreateDeliveryOrderReadyForPickup();

        var act = () => order.AssignRider(null, isOwnFleet: true, "Suresh", "+919876543210");

        act.Should().Throw<ArgumentException>();
        order.DeliveryInfo!.AssignedAt.Should().BeNull("a failed assignment must not look assigned");
    }

    [Fact]
    public void AssignRider_WithOwnFleetButEmptyRiderId_ShouldThrow()
    {
        // Guard against a caller that forgot to bind the id: Guid.Empty is not a rider.
        // It must be rejected outright, never quietly downgraded to a 3PL-shaped record.
        var order = CreateDeliveryOrderReadyForPickup();

        var act = () => order.AssignRider(Guid.Empty, isOwnFleet: true, "Suresh", "+919876543210");

        act.Should().Throw<ArgumentException>();
        order.DeliveryInfo!.RiderId.Should().BeNull();
        order.DeliveryInfo.AssignedAt.Should().BeNull();
    }

    #endregion

    #region 3PL assignment

    [Fact]
    public void AssignRider_WithThirdPartyRider_ShouldNeverStoreAnInternalRiderId()
    {
        var order = CreateDeliveryOrderReadyForPickup();

        order.AssignRider(null, isOwnFleet: false, "ProRouting Agent", "+919000000001", "https://track.example/abc");

        order.DeliveryInfo!.RiderId.Should().BeNull("a 3PL agent has no Rally account and must never match own-rider queries");
        order.DeliveryInfo.RiderName.Should().Be("ProRouting Agent");
        order.DeliveryInfo.RiderPhone.Should().Be("+919000000001");
        order.DeliveryInfo.TrackingUrl.Should().Be("https://track.example/abc");
        order.DeliveryInfo.AssignedAt.Should().NotBeNull("AssignedAt is the fleet-agnostic 'rider assigned' signal");
    }

    [Fact]
    public void AssignRider_WithThirdPartyRiderAndStrayRiderId_ShouldDiscardTheId()
    {
        // Defence in depth: if a provider payload or a future caller ever supplies an id on a
        // 3PL assignment, keeping it would make an external agent indistinguishable from a
        // Rally rider — they would show up in rider earnings and could pass order auth checks.
        var order = CreateDeliveryOrderReadyForPickup();
        var strayId = Guid.NewGuid();

        order.AssignRider(strayId, isOwnFleet: false, "ProRouting Agent", "+919000000001");

        order.DeliveryInfo!.RiderId.Should().BeNull();
    }

    [Fact]
    public void AssignRider_WithThirdPartyRiderWhoHasNoNameYet_ShouldStillAssign()
    {
        // ProRouting sends "agent-assigned" with the rider block absent (order.Rider?.Name is
        // null) and fills the agent in later. Rejecting that assignment is what stranded orders
        // at ReadyForPickup while the delivery was actually under way.
        var order = CreateDeliveryOrderReadyForPickup();

        var act = () => order.AssignRider(null, isOwnFleet: false, null, null, "https://track.example/abc");

        act.Should().NotThrow();
        order.DeliveryInfo!.AssignedAt.Should().NotBeNull();
        order.DeliveryInfo.RiderId.Should().BeNull();
        order.DeliveryInfo.TrackingUrl.Should().Be("https://track.example/abc");
    }

    #endregion

    #region Pickup progression — the original incident

    [Fact]
    public void MarkPickedUp_AfterThirdPartyAssignment_ShouldSucceed()
    {
        // The original bug: a 3PL rider has no internal id, so an id-based "is a rider
        // assigned?" check refused the pickup and the order stuck at ReadyForPickup.
        var order = CreateDeliveryOrderReadyForPickup();
        order.AssignRider(null, isOwnFleet: false, "ProRouting Agent", "+919000000001");

        order.MarkPickedUp();

        order.Status.Should().Be(OrderStatus.PickedUp);
    }

    [Fact]
    public void MarkPickedUp_AfterOwnFleetAssignment_ShouldSucceed()
    {
        var order = CreateDeliveryOrderReadyForPickup();
        order.AssignRider(Guid.NewGuid(), isOwnFleet: true, "Suresh", "+919876543210");

        order.MarkPickedUp();

        order.Status.Should().Be(OrderStatus.PickedUp);
    }

    [Fact]
    public void MarkPickedUp_WithNoRiderAssigned_ShouldThrow()
    {
        // The loosened check must still reject a genuinely rider-less order.
        var order = CreateDeliveryOrderReadyForPickup();

        var act = () => order.MarkPickedUp();

        act.Should().Throw<InvalidOperationException>();
        order.Status.Should().Be(OrderStatus.ReadyForPickup);
    }

    [Fact]
    public void ThirdPartyDelivery_FullLifecycle_ShouldReachDelivered()
    {
        // End-to-end shape of ORD-20260716-00303: a 3PL rider must carry an order all the way
        // to Delivered, which is what the restaurant dashboard reports.
        var order = CreatePaidDeliveryOrder();

        order.Confirm();
        order.StartPreparing();
        order.MarkReadyForPickup();
        order.AssignRider(null, isOwnFleet: false, "ProRouting Agent", "+919000000001");
        order.MarkPickedUp();
        order.MarkDelivered();

        order.Status.Should().Be(OrderStatus.Delivered);
        order.DeliveredAt.Should().NotBeNull();
        order.DeliveryInfo!.RiderId.Should().BeNull();
    }

    #endregion

    #region Events must not attribute 3PL work to a Rally rider

    [Fact]
    public void MarkPickedUp_WithThirdPartyRider_ShouldRaiseEventWithNullRiderId()
    {
        // Consumers key off this id to credit a Rally rider. A 3PL pickup must credit nobody.
        var order = CreateDeliveryOrderReadyForPickup();
        order.AssignRider(null, isOwnFleet: false, "ProRouting Agent", "+919000000001");

        order.MarkPickedUp();

        order.DomainEvents.OfType<OrderPickedUpEvent>().Should().ContainSingle()
            .Which.RiderId.Should().BeNull();
    }

    [Fact]
    public void MarkPickedUp_WithOwnFleetRider_ShouldRaiseEventWithThatRiderId()
    {
        var order = CreateDeliveryOrderReadyForPickup();
        var riderId = Guid.NewGuid();
        order.AssignRider(riderId, isOwnFleet: true, "Suresh", "+919876543210");

        order.MarkPickedUp();

        order.DomainEvents.OfType<OrderPickedUpEvent>().Should().ContainSingle()
            .Which.RiderId.Should().Be(riderId);
    }

    #endregion

    #region Fleet hand-over

    [Fact]
    public void AssignRider_WhenThirdPartyTakesOverFromOwnFleet_ShouldClearTheOwnRiderId()
    {
        // Dispatch falls back to 3PL after searching own fleet. The previously attached Rally
        // rider must not stay on the order, or they keep accruing someone else's delivery.
        var order = CreateDeliveryOrderReadyForPickup();
        order.AssignRider(Guid.NewGuid(), isOwnFleet: true, "Suresh", "+919876543210");

        order.AssignRider(null, isOwnFleet: false, "ProRouting Agent", "+919000000001");

        order.DeliveryInfo!.RiderId.Should().BeNull();
        order.DeliveryInfo.RiderName.Should().Be("ProRouting Agent");
    }

    [Fact]
    public void AssignRider_WhenOwnFleetTakesOverFromThirdParty_ShouldStoreTheOwnRiderId()
    {
        var order = CreateDeliveryOrderReadyForPickup();
        order.AssignRider(null, isOwnFleet: false, "ProRouting Agent", "+919000000001");
        var riderId = Guid.NewGuid();

        order.AssignRider(riderId, isOwnFleet: true, "Suresh", "+919876543210");

        order.DeliveryInfo!.RiderId.Should().Be(riderId);
        order.DeliveryInfo.RiderName.Should().Be("Suresh");
    }

    #endregion

    #region UpdateRiderInfo (manual admin/restaurant assignment — own fleet only)

    [Fact]
    public void UpdateRiderInfo_WithOwnFleetRider_ShouldStoreInternalRiderId()
    {
        var order = CreateDeliveryOrderReadyForPickup();
        var riderId = Guid.NewGuid();

        order.UpdateRiderInfo(riderId, isOwnFleet: true, "Suresh", "+919876543210");

        order.DeliveryInfo!.RiderId.Should().Be(riderId);
    }

    [Fact]
    public void UpdateRiderInfo_WithOwnFleetButEmptyRiderId_ShouldThrow()
    {
        var order = CreateDeliveryOrderReadyForPickup();

        var act = () => order.UpdateRiderInfo(Guid.Empty, isOwnFleet: true, "Suresh", "+919876543210");

        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
