using System.Net;
using System.Text.Json;
using FluentAssertions;
using RallyAPI.Integration.Tests.Infrastructure;

namespace RallyAPI.Integration.Tests.Flows;

/// <summary>
/// Order lifecycle integration tests.
///
/// Flows tested:
///   1. Place order → 201 Created, OrderDto returned
///   2. Place order → restaurant confirms → status = Confirmed
///   3. Place order → restaurant rejects → status = Rejected
///   4. Place order → customer cancels → status = Cancelled with reason
///   5. Full lifecycle: Confirmed → Preparing → ReadyForPickup → AssignRider → PickedUp → Delivered
///   6. Error shape: all failures return { error, message } JSON
/// </summary>
public sealed class OrderFlowTests : IntegrationTestBase
{
    public OrderFlowTests(IntegrationTestFactory factory) : base(factory) { }

    // ─── 1. Place Order ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrder_ValidRequest_Returns201WithOrderDto()
    {
        AuthenticateAsCustomer();

        var response = await Client.PostAsync("/api/orders", JsonBody(BuildPlaceOrderRequest()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var order = await DeserializeAsync<JsonElement>(response);
        order.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        order.GetProperty("status").GetString().Should().Be("Paid");
        order.GetProperty("customerId").GetString().Should().Be(CustomerId.ToString());
        order.GetProperty("restaurantId").GetString().Should().Be(RestaurantId.ToString());

        var pricing = order.GetProperty("pricing");
        pricing.GetProperty("subTotal").GetDecimal().Should().Be(200m);
        pricing.GetProperty("deliveryFee").GetDecimal().Should().Be(30m);
    }

    [Fact]
    public async Task PlaceOrder_Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.PostAsync("/api/orders", JsonBody(BuildPlaceOrderRequest()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlaceOrder_MissingPaymentId_Returns400WithErrorShape()
    {
        AuthenticateAsCustomer();

        var body = BuildPlaceOrderRequest(paymentId: "");
        var response = await Client.PostAsync("/api/orders", JsonBody(body));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorShapeAsync(response);
    }

    // ─── 2. Restaurant Confirms ───────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmOrder_ByOwningRestaurant_StatusBecomesConfirmed()
    {
        var orderId = await PlaceOrderAsync();

        AuthenticateAsRestaurant();
        var response = await Client.PutAsync($"/api/orders/{orderId}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await DeserializeAsync<JsonElement>(response);
        order.GetProperty("status").GetString().Should().Be("Confirmed");
        order.GetProperty("id").GetString().Should().Be(orderId.ToString());
    }

    [Fact]
    public async Task ConfirmOrder_ByWrongRestaurant_Returns400()
    {
        var orderId = await PlaceOrderAsync();

        // Authenticate as a DIFFERENT restaurant
        var wrongRestaurantId = Guid.NewGuid();
        AuthenticateAs(Jwt.CreateRestaurantToken(wrongRestaurantId));

        var response = await Client.PutAsync($"/api/orders/{orderId}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorShapeAsync(response);
    }

    [Fact]
    public async Task ConfirmOrder_NonExistentOrder_Returns404()
    {
        AuthenticateAsRestaurant();

        var response = await Client.PutAsync($"/api/orders/{Guid.NewGuid()}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertErrorShapeAsync(response);
    }

    // ─── 3. Restaurant Rejects ────────────────────────────────────────────────────

    [Fact]
    public async Task RejectOrder_ByOwningRestaurant_StatusBecomesRejected()
    {
        var orderId = await PlaceOrderAsync();

        AuthenticateAsRestaurant();
        var response = await Client.PutAsync(
            $"/api/orders/{orderId}/reject",
            JsonBody(new { reason = "Out of stock" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await DeserializeAsync<JsonElement>(response);
        // Reject immediately initiates refund → final status is Refunding
        order.GetProperty("status").GetString().Should().BeOneOf("Rejected", "Refunding");
    }

    // ─── 4. Customer Cancels ──────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOrder_ByCustomer_StatusBecomesCancelled()
    {
        var orderId = await PlaceOrderAsync();

        AuthenticateAsCustomer();
        var response = await Client.PutAsync(
            $"/api/orders/{orderId}/cancel",
            JsonBody(new { reason = "CustomerRequested", notes = "Please process refund" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await DeserializeAsync<JsonElement>(response);
        order.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    // ─── 5. Full delivery lifecycle ───────────────────────────────────────────────

    [Fact]
    public async Task FullOrderLifecycle_PlaceConfirmPrepareReadyAssignPickupDeliver_Succeeds()
    {
        // Step 1: Place order
        var orderId = await PlaceOrderAsync();

        // Step 2: Restaurant confirms
        AuthenticateAsRestaurant();
        var confirm = await Client.PutAsync($"/api/orders/{orderId}/confirm", null);
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeAsync<JsonElement>(confirm))
            .GetProperty("status").GetString().Should().Be("Confirmed");

        // Step 3: Restaurant starts preparing
        var preparing = await Client.PutAsync($"/api/orders/{orderId}/preparing", null);
        preparing.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeAsync<JsonElement>(preparing))
            .GetProperty("status").GetString().Should().Be("Preparing");

        // Step 4: Restaurant marks ready for pickup
        var ready = await Client.PutAsync($"/api/orders/{orderId}/ready", null);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeAsync<JsonElement>(ready))
            .GetProperty("status").GetString().Should().Be("ReadyForPickup");

        // Step 5: Admin assigns rider
        AuthenticateAsAdmin();
        var assign = await Client.PutAsync(
            $"/api/orders/{orderId}/assign-rider",
            JsonBody(new
            {
                riderId    = RiderId.ToString(),
                riderName  = "Test Rider",
                riderPhone = "+919876543210"
            }));
        assign.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 6: Rider marks picked up
        AuthenticateAsRider();
        var pickup = await Client.PutAsync($"/api/orders/{orderId}/pickup", null);
        pickup.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeAsync<JsonElement>(pickup))
            .GetProperty("status").GetString().Should().Be("PickedUp");

        // Step 7: Rider marks delivered
        var deliver = await Client.PutAsync($"/api/orders/{orderId}/deliver", null);
        deliver.StatusCode.Should().Be(HttpStatusCode.OK);

        var final = await DeserializeAsync<JsonElement>(deliver);
        final.GetProperty("status").GetString().Should().Be("Delivered");
    }

    // ─── 6. Query endpoints ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderById_ExistingOrder_ReturnsOrderDto()
    {
        var orderId = await PlaceOrderAsync();

        // Any authenticated user can get by ID
        AuthenticateAsCustomer();
        var response = await Client.GetAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await DeserializeAsync<JsonElement>(response);
        order.GetProperty("id").GetString().Should().Be(orderId.ToString());
    }

    [Fact]
    public async Task GetMyOrders_ReturnsPagedList()
    {
        await PlaceOrderAsync(paymentId: "pay-001");
        await PlaceOrderAsync(paymentId: "pay-002");

        AuthenticateAsCustomer();
        var response = await Client.GetAsync("/api/orders/my-orders?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<JsonElement>(response);
        result.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────────

    /// <summary>Asserts the response body follows the { error, message } error shape.</summary>
    private static async Task AssertErrorShapeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrEmpty();

        var body = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        body.TryGetProperty("error", out var errorProp).Should().BeTrue(
            because: $"Error shape must contain 'error' field. Body: {json}");
        body.TryGetProperty("message", out var messageProp).Should().BeTrue(
            because: $"Error shape must contain 'message' field. Body: {json}");

        errorProp.GetString().Should().NotBeNullOrEmpty();
        messageProp.GetString().Should().NotBeNullOrEmpty();
    }
}
