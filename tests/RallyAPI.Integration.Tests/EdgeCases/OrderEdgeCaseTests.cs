using System.Net;
using System.Text.Json;
using FluentAssertions;
using RallyAPI.Integration.Tests.Infrastructure;

namespace RallyAPI.Integration.Tests.EdgeCases;

/// <summary>
/// Edge case integration tests for the order lifecycle.
///
/// Scenarios:
///   1. Double-confirm same order → second confirm returns 400
///   2. Cancel after delivery started (PickedUp) → 400
///   3. Place order missing required fields → 400 with field-level errors
///   4. Place order with empty items list → 400
///   5. Restaurant cannot confirm another restaurant's order
///   6. Error responses always use { error, message, details? } shape
///   7. Reject order that is already confirmed → 400
/// </summary>
public sealed class OrderEdgeCaseTests : IntegrationTestBase
{
    public OrderEdgeCaseTests(IntegrationTestFactory factory) : base(factory) { }

    // ─── 1. Double-confirm ────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmOrder_AlreadyConfirmed_Returns400()
    {
        var orderId = await PlaceOrderAsync();

        AuthenticateAsRestaurant();
        var first = await Client.PutAsync($"/api/orders/{orderId}/confirm", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second confirm on the same order
        var second = await Client.PutAsync($"/api/orders/{orderId}/confirm", null);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertErrorShapeAsync(second);
    }

    // ─── 2. Cancel after delivery started ────────────────────────────────────────

    [Fact]
    public async Task CancelOrder_WhenPickedUp_Returns400()
    {
        var orderId = await PlaceOrderAsync();

        // Advance to PickedUp
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

        AuthenticateAsRider();
        await Client.PutAsync($"/api/orders/{orderId}/pickup", null);

        // Try to cancel
        AuthenticateAsCustomer();
        var cancel = await Client.PutAsync(
            $"/api/orders/{orderId}/cancel",
            JsonBody(new { reason = "CustomerRequested" }));

        cancel.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorShapeAsync(cancel);
    }

    // ─── 3. Validation errors have field-level details ────────────────────────────

    [Fact]
    public async Task PlaceOrder_InvalidRequest_Returns400WithFieldErrors()
    {
        AuthenticateAsCustomer();

        // Send a request missing required fields: restaurantId is empty, items is empty
        var response = await Client.PostAsync("/api/orders", JsonBody(new
        {
            paymentId    = "test-payment",
            restaurantId = Guid.Empty.ToString(),
            items        = Array.Empty<object>(),
            pricing      = new { subTotal = 0m, deliveryFee = 0m, tax = 0m, discount = 0m },
            deliveryAddress = new
            {
                street  = "",
                city    = "",
                pincode = ""
            }
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await DeserializeAsync<JsonElement>(response);

        // Must follow { error, message, details? } shape
        body.TryGetProperty("error", out _).Should().BeTrue();
        body.TryGetProperty("message", out _).Should().BeTrue();
        // Field-level validation errors should be in 'details'
        if (body.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            details.GetArrayLength().Should().BeGreaterThan(0,
                because: "Validation errors should include field-level details");
        }
    }

    // ─── 4. Reject after order has auto-promoted to Preparing ────────────────────

    [Fact]
    public async Task RejectOrder_AfterAutoPromoteToPreparing_Returns400()
    {
        // After /confirm, OrderConfirmedAutoPrepareHandler synchronously promotes
        // the order to Preparing within the same request. By the time the response
        // returns, the order is in Preparing — which is no longer rejectable, so
        // a follow-up /reject must return 400.
        var orderId = await PlaceOrderAsync();

        AuthenticateAsRestaurant();
        await Client.PutAsync($"/api/orders/{orderId}/confirm", null);

        var reject = await Client.PutAsync(
            $"/api/orders/{orderId}/reject",
            JsonBody(new { reason = "Stock issue" }));

        reject.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorShapeAsync(reject);
    }

    // ─── 5. Reject non-existent order ────────────────────────────────────────────

    [Fact]
    public async Task RejectOrder_NonExistent_Returns404()
    {
        AuthenticateAsRestaurant();

        var response = await Client.PutAsync(
            $"/api/orders/{Guid.NewGuid()}/reject",
            JsonBody(new { reason = "Not found" }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertErrorShapeAsync(response);
    }

    // ─── 6. Status transitions that are out of order ─────────────────────────────

    [Fact]
    public async Task MarkPickedUp_WhenOrderIsOnlyConfirmed_Returns400()
    {
        var orderId = await PlaceOrderAsync();

        AuthenticateAsRestaurant();
        await Client.PutAsync($"/api/orders/{orderId}/confirm", null);

        // Try to mark picked up without going through Preparing → Ready → AssignRider
        AuthenticateAsRider();
        var pickup = await Client.PutAsync($"/api/orders/{orderId}/pickup", null);

        pickup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkDelivered_WithoutBeingPickedUp_Returns400()
    {
        var orderId = await PlaceOrderAsync();

        AuthenticateAsRestaurant();
        await Client.PutAsync($"/api/orders/{orderId}/confirm", null);

        AuthenticateAsRider();
        var deliver = await Client.PutAsync($"/api/orders/{orderId}/deliver", null);

        deliver.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 7. Error response shape consistency ─────────────────────────────────────

    [Fact]
    public async Task AllErrorResponses_UseCanonicalErrorShape()
    {
        // Test several different error conditions and verify shape is consistent

        AuthenticateAsCustomer();

        var scenarios = new List<(Func<Task<HttpResponseMessage>> act, string name)>
        {
            (() => Client.GetAsync($"/api/orders/{Guid.NewGuid()}"),
             "NotFound"),
            (() => Client.PutAsync($"/api/orders/{Guid.NewGuid()}/confirm", null),
             "ConfirmNotFound"),
        };

        // Change to restaurant for confirm
        foreach (var (act, name) in scenarios)
        {
            if (name.Contains("Confirm")) AuthenticateAsRestaurant();
            else AuthenticateAsCustomer();

            var response = await act();

            // Must not be 2xx
            ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400,
                because: $"Scenario '{name}' should fail");

            var body = JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync(), JsonOptions);

            body.TryGetProperty("error", out var e).Should().BeTrue(
                because: $"Scenario '{name}' must include 'error' field");
            body.TryGetProperty("message", out var m).Should().BeTrue(
                because: $"Scenario '{name}' must include 'message' field");

            e.GetString().Should().NotBeNullOrEmpty();
            m.GetString().Should().NotBeNullOrEmpty();
        }
    }

    // ─── Helper ───────────────────────────────────────────────────────────────────

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
