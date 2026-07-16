using FluentAssertions;
using Xunit;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Application.Tests;

/// <summary>
/// Regression tests for the own-fleet rider delivery lifecycle.
///
/// Own-fleet riders (the rider web app) have no "en route to pickup" step —
/// only 3PL/ProRouting callbacks set <see cref="DeliveryRequestStatus.RiderEnRoutePickup"/>.
/// After accepting an offer the request sits at <see cref="DeliveryRequestStatus.RiderAssigned"/>,
/// so every status transition the rider drives must be reachable from there.
///
/// The four rider endpoints under test:
///   1. arrived-pickup → MarkRiderArrivedPickup()
///   2. pickup         → MarkPickedUp()
///   3. arrived-drop   → MarkRiderArrivedDrop()
///   4. delivered      → MarkDelivered()
/// </summary>
public class OwnFleetDeliveryLifecycleTests
{
    private static DeliveryRequest NewAssignedRequest()
    {
        var req = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "TEST-0001",
            quoteId: null,
            quotedPrice: 50m,
            pickupLat: 28.6315, pickupLng: 77.2167, pickupPincode: "110001",
            pickupAddress: "Restaurant", pickupContactName: "Store", pickupContactPhone: "+919999999999",
            dropLat: 28.6129, dropLng: 77.2295, dropPincode: "110002",
            dropAddress: "Customer", dropContactName: "Cust", dropContactPhone: "+918888888888",
            orderCategory: OrderCategory.FoodAndBeverage);

        // Own-fleet accept path: search own fleet → rider accepts → RiderAssigned.
        req.StartSearchingOwnFleet();
        req.AssignOwnFleetRider(Guid.NewGuid(), "Test Rider", "+917777777777");

        req.Status.Should().Be(DeliveryRequestStatus.RiderAssigned);
        return req;
    }

    // ─── 1. arrived-pickup (the reported bug) ──────────────────────────
    [Fact]
    public void MarkRiderArrivedPickup_DirectlyFromRiderAssigned_Succeeds()
    {
        // This is the exact scenario that produced:
        // "Invalid status transition. Current: RiderAssigned, Allowed: RiderEnRoutePickup"
        var req = NewAssignedRequest();

        var act = () => req.MarkRiderArrivedPickup();

        act.Should().NotThrow();
        req.Status.Should().Be(DeliveryRequestStatus.RiderArrivedPickup);
        req.ArrivedPickupAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkRiderArrivedPickup_FromEnRoutePickup_StillSucceeds()
    {
        // 3PL path still works: RiderAssigned → en-route → arrived.
        var req = NewAssignedRequest();
        req.MarkRiderEnRoutePickup();

        var act = () => req.MarkRiderArrivedPickup();

        act.Should().NotThrow();
        req.Status.Should().Be(DeliveryRequestStatus.RiderArrivedPickup);
    }

    // ─── 2. pickup ─────────────────────────────────────────────────────
    [Fact]
    public void MarkPickedUp_FromArrivedPickup_Succeeds()
    {
        var req = NewAssignedRequest();
        req.MarkRiderArrivedPickup();

        var act = () => req.MarkPickedUp();

        act.Should().NotThrow();
        req.Status.Should().Be(DeliveryRequestStatus.PickedUp);
        req.PickedUpAt.Should().NotBeNull();
    }

    // ─── 3. arrived-drop ───────────────────────────────────────────────
    [Fact]
    public void MarkRiderArrivedDrop_FromPickedUp_Succeeds()
    {
        var req = NewAssignedRequest();
        req.MarkRiderArrivedPickup();
        req.MarkPickedUp();

        var act = () => req.MarkRiderArrivedDrop();

        act.Should().NotThrow();
        req.Status.Should().Be(DeliveryRequestStatus.RiderArrivedDrop);
        req.ArrivedDropAt.Should().NotBeNull();
    }

    // ─── 4. delivered ──────────────────────────────────────────────────
    [Fact]
    public void MarkDelivered_FromArrivedDrop_Succeeds()
    {
        var req = NewAssignedRequest();
        req.MarkRiderArrivedPickup();
        req.MarkPickedUp();
        req.MarkRiderArrivedDrop();

        var act = () => req.MarkDelivered();

        act.Should().NotThrow();
        req.Status.Should().Be(DeliveryRequestStatus.Delivered);
        req.DeliveredAt.Should().NotBeNull();
    }

    // ─── Full chain — the whole own-fleet flow, end to end ─────────────
    [Fact]
    public void FullOwnFleetLifecycle_FromAssignedToDelivered_DrivesCleanly()
    {
        var req = NewAssignedRequest();

        req.MarkRiderArrivedPickup();
        req.MarkPickedUp();
        req.MarkRiderArrivedDrop();
        req.MarkDelivered();

        req.Status.Should().Be(DeliveryRequestStatus.Delivered);
    }
}
