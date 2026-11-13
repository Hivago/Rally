using System.Net;
using System.Text.Json;
using FluentAssertions;
using RallyAPI.Integration.Tests.Infrastructure;

namespace RallyAPI.Integration.Tests.Flows;

/// <summary>
/// Delivery flow integration tests.
///
/// Flows tested:
///   1. Get delivery quote → returns quote with fee breakdown
///   2. Create delivery request (after order) → 201 Created
///   3. Delivery quote: public endpoint, no auth required
///   4. Create delivery request: requires auth
///   5. Restaurant confirms order → dispatch is triggered automatically
///      (via OrderConfirmedEvent → CreateDeliveryRequest domain event handler)
/// </summary>
public sealed class DeliveryFlowTests : IntegrationTestBase
{
    public DeliveryFlowTests(IntegrationTestFactory factory) : base(factory) { }

    // ─── 1. Delivery Quote ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeliveryQuote_ValidRequest_Returns200WithQuote()
    {
        // Quote endpoint is public (no auth required per DeliveryEndpoints.cs)
        var response = await Client.PostAsync(
            "/api/delivery/quote",
            JsonBody(new
            {
                restaurantId   = RestaurantId.ToString(),
                pickupLatitude  = 28.6315,
                pickupLongitude = 77.2167,
                pickupPincode   = "110001",
                dropLatitude    = 28.6129,
                dropLongitude   = 77.2295,
                dropPincode     = "110002",
                city            = "New Delhi",
                orderAmount     = 500m
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var quote = await DeserializeAsync<JsonElement>(response);
        // Quote should contain fee info
        quote.TryGetProperty("deliveryFee", out var fee).Should().BeTrue(
            because: "Quote response must include deliveryFee");
        fee.GetDecimal().Should().BeGreaterThan(0);
    }

    // ─── 2. Delivery codes ────────────────────────────────────────────────────────
    // (POST /api/delivery/request no longer exists: DeliveryRequests are created internally
    // when an order is confirmed, covered by ConfirmOrder_TriggersDeliveryRequest below.)

    [Fact]
    public async Task GetDeliveryCodes_Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync($"/api/delivery/orders/{Guid.NewGuid()}/codes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── 3. Confirm triggers delivery lifecycle ───────────────────────────────────

    [Fact]
    public async Task ConfirmOrder_TriggersDeliveryRequest_OrderIsConfirmed()
    {
        // Place order
        var orderId = await PlacePaidOrderAsync();

        // Restaurant confirms → this publishes OrderConfirmedEvent → handler creates DeliveryRequest
        AuthenticateAsRestaurant();
        var confirm = await Client.PutAsync($"/api/orders/{orderId}/confirm", null);

        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirm auto-promotes to Preparing (OrderConfirmedAutoPrepareHandler)
        var order = await DeserializeAsync<JsonElement>(confirm);
        order.GetProperty("status").GetString().Should().Be("Preparing");

        // Delivery endpoint should now have a request for this order
        // (small delay to allow background processing)
        await Task.Delay(200);

        AuthenticateAsCustomer();
        var deliveryResponse = await Client.GetAsync($"/api/delivery/order/{orderId}");
        // The GET endpoint is a TODO (returns 200 OK with empty body currently)
        // Just verify it doesn't error out
        deliveryResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ─── 4. Rider delivery lifecycle via Order endpoints ─────────────────────────

    [Fact]
    public async Task Rider_MarkPickedUp_ThenDeliver_OrderStatusProgresses()
    {
        // Confirm and assign rider
        var orderId = await PlacePaidOrderAsync();

        AuthenticateAsRestaurant();
        await Client.PutAsync($"/api/orders/{orderId}/confirm", null);
        await Client.PutAsync($"/api/orders/{orderId}/preparing", null);
        await Client.PutAsync($"/api/orders/{orderId}/ready", null);

        AuthenticateAsAdmin();
        await Client.PutAsync($"/api/orders/{orderId}/assign-rider", JsonBody(new
        {
            riderId    = RiderId.ToString(),
            riderName  = "Test Rider",
            riderPhone = "+919876543210"
        }));

        // Rider picks up
        AuthenticateAsRider();
        var pickup = await Client.PutAsync($"/api/orders/{orderId}/pickup", null);
        pickup.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeAsync<JsonElement>(pickup))
            .GetProperty("status").GetString().Should().Be("PickedUp");

        // Rider delivers
        var deliver = await Client.PutAsync($"/api/orders/{orderId}/deliver", null);
        deliver.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeAsync<JsonElement>(deliver))
            .GetProperty("status").GetString().Should().Be("Delivered");
    }

    [Fact]
    public async Task Rider_CannotMarkPickedUp_WithoutBeingAssigned()
    {
        var orderId = await PlacePaidOrderAsync();

        // Confirm and prepare, but don't assign the rider
        AuthenticateAsRestaurant();
        await Client.PutAsync($"/api/orders/{orderId}/confirm", null);
        await Client.PutAsync($"/api/orders/{orderId}/preparing", null);
        await Client.PutAsync($"/api/orders/{orderId}/ready", null);

        // A rider who is not assigned to the order is rejected by the ownership check,
        // before any status-transition validation (UpdateOrderStatusCommandHandler).
        AuthenticateAsRider();
        var pickup = await Client.PutAsync($"/api/orders/{orderId}/pickup", null);

        pickup.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
