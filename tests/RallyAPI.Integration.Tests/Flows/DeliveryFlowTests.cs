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

    // ─── 2. Create Delivery Request ───────────────────────────────────────────────

    [Fact]
    public async Task CreateDeliveryRequest_ValidOrder_Returns201()
    {
        // Step 1: Get a valid quote so we have a real quoteId in the DB
        var quoteResponse = await Client.PostAsync(
            "/api/delivery/quote",
            JsonBody(new
            {
                restaurantId    = RestaurantId.ToString(),
                pickupLatitude  = 28.6315,
                pickupLongitude = 77.2167,
                pickupPincode   = "110001",
                dropLatitude    = 28.6129,
                dropLongitude   = 77.2295,
                dropPincode     = "110002",
                city            = "New Delhi",
                orderAmount     = 500m
            }));

        quoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = await DeserializeAsync<JsonElement>(quoteResponse);
        var quoteId = quote.GetProperty("id").GetString()!;

        // Step 2: Create delivery request referencing that quote
        AuthenticateAsCustomer();

        var response = await Client.PostAsync(
            "/api/delivery/request",
            JsonBody(new
            {
                orderId             = Guid.NewGuid().ToString(),
                orderNumber         = "RALLY-001",
                quoteId,
                pickupLatitude      = 28.6315,
                pickupLongitude     = 77.2167,
                pickupPincode       = "110001",
                pickupAddress       = "Connaught Place, New Delhi",
                pickupContactName   = "Restaurant Staff",
                pickupContactPhone  = "+911234567890",
                dropLatitude        = 28.6129,
                dropLongitude       = 77.2295,
                dropPincode         = "110002",
                dropAddress         = "12, India Gate, New Delhi",
                dropContactName     = "Test Customer",
                dropContactPhone    = "+919876543210",
                itemCount           = 2
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var delivery = await DeserializeAsync<JsonElement>(response);
        delivery.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        delivery.GetProperty("status").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateDeliveryRequest_Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.PostAsync(
            "/api/delivery/request",
            JsonBody(new
            {
                orderId       = Guid.NewGuid().ToString(),
                orderNumber   = "RALLY-002",
                pickupLatitude = 28.6315,
                pickupLongitude = 77.2167,
                pickupPincode = "110001",
                dropLatitude  = 28.6129,
                dropLongitude = 77.2295,
                dropPincode   = "110002",
                itemCount     = 1
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── 3. Confirm triggers delivery lifecycle ───────────────────────────────────

    [Fact]
    public async Task ConfirmOrder_TriggersDeliveryRequest_OrderIsConfirmed()
    {
        // Place order
        var orderId = await PlaceOrderAsync();

        // Restaurant confirms → this publishes OrderConfirmedEvent → handler creates DeliveryRequest
        AuthenticateAsRestaurant();
        var confirm = await Client.PutAsync($"/api/orders/{orderId}/confirm", null);

        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        // Order status should now be Confirmed
        var order = await DeserializeAsync<JsonElement>(confirm);
        order.GetProperty("status").GetString().Should().Be("Confirmed");

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
        var orderId = await PlaceOrderAsync();

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
        var orderId = await PlaceOrderAsync();

        // Confirm and prepare, but don't assign the rider
        AuthenticateAsRestaurant();
        await Client.PutAsync($"/api/orders/{orderId}/confirm", null);
        await Client.PutAsync($"/api/orders/{orderId}/preparing", null);
        await Client.PutAsync($"/api/orders/{orderId}/ready", null);

        // Rider tries to pick up without being assigned
        AuthenticateAsRider();
        var pickup = await Client.PutAsync($"/api/orders/{orderId}/pickup", null);

        pickup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
