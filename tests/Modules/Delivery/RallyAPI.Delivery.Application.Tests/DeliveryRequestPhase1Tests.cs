using FluentAssertions;
using Xunit;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Application.Tests;

/// <summary>
/// Unit tests for Phase 1 additions to DeliveryRequest:
/// - OrderCategory + OTP generation at creation
/// - RTO state machine (InitiateRto / MarkRtoDelivered / MarkRtoDisposed)
/// - Live location updates (UpdateLiveLocation)
/// </summary>
public class DeliveryRequestPhase1Tests
{
    // ─── Test factory ──────────────────────────────────────────────────
    private static DeliveryRequest NewRequest(OrderCategory category = OrderCategory.FoodAndBeverage)
    {
        return DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "TEST-0001",
            quoteId: null,
            quotedPrice: 50m,
            pickupLat: 28.6315, pickupLng: 77.2167, pickupPincode: "110001",
            pickupAddress: "Restaurant", pickupContactName: "Store", pickupContactPhone: "+919999999999",
            dropLat: 28.6129, dropLng: 77.2295, dropPincode: "110002",
            dropAddress: "Customer", dropContactName: "Cust", dropContactPhone: "+918888888888",
            orderCategory: category);
    }

    private static void DriveTo(DeliveryRequest req, DeliveryRequestStatus target)
    {
        // Drive the entity through valid transitions until it reaches `target`.
        req.StartSearching3PL();
        if (target == DeliveryRequestStatus.Searching3PL) return;

        req.Assign3PLRider("EXT-1", "ProRouting", "Rider", "+911", "https://t/abc", 60m);
        if (target == DeliveryRequestStatus.Assigned3PL) return;

        req.MarkRiderEnRoutePickup();
        if (target == DeliveryRequestStatus.RiderEnRoutePickup) return;

        req.MarkRiderArrivedPickup();
        if (target == DeliveryRequestStatus.RiderArrivedPickup) return;

        req.MarkPickedUp();
        if (target == DeliveryRequestStatus.PickedUp) return;

        req.MarkRiderEnRouteDrop();
        if (target == DeliveryRequestStatus.RiderEnRouteDrop) return;

        req.MarkRiderArrivedDrop();
        if (target == DeliveryRequestStatus.RiderArrivedDrop) return;

        req.MarkWaitingForCustomer();
    }

    // ─── OrderCategory + OTP generation ────────────────────────────────
    [Fact]
    public void Create_DefaultOrderCategory_IsFoodAndBeverage()
    {
        var req = NewRequest();
        req.OrderCategory.Should().Be(OrderCategory.FoodAndBeverage);
    }

    [Theory]
    [InlineData(OrderCategory.Grocery)]
    [InlineData(OrderCategory.Pharma)]
    public void Create_ExplicitOrderCategory_IsHonored(OrderCategory category)
    {
        var req = NewRequest(category);
        req.OrderCategory.Should().Be(category);
    }

    [Fact]
    public void Create_GeneratesPickupCode_SixDigitsNumeric()
    {
        var req = NewRequest();
        req.PickupCode.Should().NotBeNull();
        req.PickupCode!.Length.Should().Be(6);
        req.PickupCode.Should().MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public void Create_GeneratesDropCode_FourDigitsNumeric()
    {
        var req = NewRequest();
        req.DropCode.Should().NotBeNull();
        req.DropCode!.Length.Should().Be(4);
        req.DropCode.Should().MatchRegex("^[0-9]{4}$");
    }

    [Fact]
    public void Create_GeneratesUniqueCodesAcrossInstances()
    {
        // Cryptographic random — collisions are statistically negligible.
        var codes = Enumerable.Range(0, 50).Select(_ => NewRequest().PickupCode).ToHashSet();
        codes.Count.Should().BeGreaterThan(45, "6-digit codes should rarely collide across 50 samples");
    }

    // ─── RTO state machine ─────────────────────────────────────────────
    [Fact]
    public void InitiateRto_FromPickedUp_TransitionsToRtoInitiated()
    {
        var req = NewRequest();
        DriveTo(req, DeliveryRequestStatus.PickedUp);

        req.InitiateRto("Customer unreachable");

        req.Status.Should().Be(DeliveryRequestStatus.RtoInitiated);
        req.RtoInitiatedAt.Should().NotBeNull();
        req.FailureNotes.Should().Be("Customer unreachable");
    }

    [Fact]
    public void InitiateRto_FromWaitingForCustomer_IsAllowed()
    {
        var req = NewRequest();
        DriveTo(req, DeliveryRequestStatus.WaitingForCustomer);

        var act = () => req.InitiateRto();

        act.Should().NotThrow();
        req.Status.Should().Be(DeliveryRequestStatus.RtoInitiated);
    }

    [Fact]
    public void InitiateRto_FromCreated_Throws()
    {
        var req = NewRequest();

        var act = () => req.InitiateRto();

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Invalid status transition*");
    }

    [Fact]
    public void MarkRtoDelivered_FromRtoInitiated_TransitionsToRtoDelivered()
    {
        var req = NewRequest();
        DriveTo(req, DeliveryRequestStatus.PickedUp);
        req.InitiateRto();

        req.MarkRtoDelivered();

        req.Status.Should().Be(DeliveryRequestStatus.RtoDelivered);
        req.RtoDeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkRtoDelivered_NotFromRtoInitiated_Throws()
    {
        var req = NewRequest();
        DriveTo(req, DeliveryRequestStatus.PickedUp);

        var act = () => req.MarkRtoDelivered();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRtoDisposed_OnFoodOrder_FromRtoInitiated_Succeeds()
    {
        var req = NewRequest(OrderCategory.FoodAndBeverage);
        DriveTo(req, DeliveryRequestStatus.PickedUp);
        req.InitiateRto();

        req.MarkRtoDisposed();

        req.Status.Should().Be(DeliveryRequestStatus.RtoDisposed);
        req.RtoDisposedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(OrderCategory.Grocery)]
    [InlineData(OrderCategory.Pharma)]
    public void MarkRtoDisposed_OnNonFoodOrder_Throws(OrderCategory category)
    {
        var req = NewRequest(category);
        DriveTo(req, DeliveryRequestStatus.PickedUp);
        req.InitiateRto();

        var act = () => req.MarkRtoDisposed();

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("RTO disposal is only valid for FoodAndBeverage*");
    }

    // ─── Live location updates ─────────────────────────────────────────
    [Fact]
    public void UpdateLiveLocation_WhenFresh_StoresCoordinates()
    {
        var req = NewRequest();
        var t = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);

        req.UpdateLiveLocation(28.6315, 77.2167, t);

        req.LastRiderLatitude.Should().Be(28.6315);
        req.LastRiderLongitude.Should().Be(77.2167);
        req.LastLocationUpdatedAt.Should().Be(t);
    }

    [Fact]
    public void UpdateLiveLocation_WhenStale_IsIgnored()
    {
        var req = NewRequest();
        var newer = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
        var older = newer.AddMinutes(-5);

        req.UpdateLiveLocation(1.0, 2.0, newer);
        req.UpdateLiveLocation(9.0, 9.0, older); // stale → ignored

        req.LastRiderLatitude.Should().Be(1.0);
        req.LastRiderLongitude.Should().Be(2.0);
        req.LastLocationUpdatedAt.Should().Be(newer);
    }
}
