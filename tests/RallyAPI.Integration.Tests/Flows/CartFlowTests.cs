using System.Net;
using System.Text.Json;
using FluentAssertions;
using RallyAPI.Integration.Tests.Infrastructure;

namespace RallyAPI.Integration.Tests.Flows;

/// <summary>
/// Cart flow integration tests.
///
/// Flows tested:
///   1. Add item → cart returned with correct item
///   2. Add same item again → quantity merged
///   3. Update item quantity
///   4. Remove item → cart without item
///   5. Add from different restaurant → 409 Conflict with RestaurantConflictDto
///   6. Replace cart (replaceCart=true) → old cart replaced
///   7. Clear cart → 204 NoContent
///   8. Get cart when empty → 204 NoContent
/// </summary>
public sealed class CartFlowTests : IntegrationTestBase
{
    private static readonly Guid MenuItemId1 = Guid.NewGuid();
    private static readonly Guid MenuItemId2 = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    public CartFlowTests(IntegrationTestFactory factory) : base(factory) { }

    [Fact]
    public async Task AddCartItem_WhenCartEmpty_ReturnsCartWithItem()
    {
        AuthenticateAsCustomer();

        var response = await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await DeserializeAsync<JsonElement>(response);
        cart.GetProperty("customerId").GetString().Should().Be(CustomerId.ToString());
        cart.GetProperty("restaurantId").GetString().Should().Be(RestaurantId.ToString());
        cart.GetProperty("itemCount").GetInt32().Should().Be(1);

        var items = cart.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("name").GetString().Should().Be("Butter Chicken");
        items[0].GetProperty("quantity").GetInt32().Should().Be(1);
        items[0].GetProperty("unitPrice").GetDecimal().Should().Be(200m);
    }

    [Fact]
    public async Task AddCartItem_SameItemTwice_MergesQuantity()
    {
        AuthenticateAsCustomer();

        await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 1)));

        var response = await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 2)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await DeserializeAsync<JsonElement>(response);
        cart.GetProperty("itemCount").GetInt32().Should().Be(3);
        var items = cart.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("quantity").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task AddCartItem_DifferentRestaurant_Returns409Conflict()
    {
        AuthenticateAsCustomer();

        // First item from RestaurantId
        await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 1)));

        // Second item from a different restaurant (replaceCart=false)
        var response = await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId2, "Paneer Tikka", 180m, 1,
                restaurantId: OtherRestaurantId, restaurantName: "Other Place")));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await DeserializeAsync<JsonElement>(response);
        // Standard error shape: { error, message }
        error.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
        error.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AddCartItem_DifferentRestaurant_ReplaceCartTrue_ReplacesCart()
    {
        AuthenticateAsCustomer();

        // First: add item from RestaurantId
        await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 2)));

        // Replace with item from OtherRestaurantId
        var response = await Client.PostAsync(
            "/api/cart/items?replaceCart=true",
            JsonBody(AddItemRequest(MenuItemId2, "Paneer Tikka", 180m, 1,
                restaurantId: OtherRestaurantId, restaurantName: "Other Place")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await DeserializeAsync<JsonElement>(response);
        cart.GetProperty("restaurantId").GetString().Should().Be(OtherRestaurantId.ToString());
        cart.GetProperty("itemCount").GetInt32().Should().Be(1);
        cart.GetProperty("items")[0].GetProperty("name").GetString().Should().Be("Paneer Tikka");
    }

    [Fact]
    public async Task UpdateCartItem_ChangesQuantity()
    {
        AuthenticateAsCustomer();

        var addResponse = await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 1)));

        var cart   = await DeserializeAsync<JsonElement>(addResponse);
        var itemId = cart.GetProperty("items")[0].GetProperty("id").GetString();

        var updateResponse = await Client.PutAsync(
            $"/api/cart/items/{itemId}",
            JsonBody(new { quantity = 3 }));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await DeserializeAsync<JsonElement>(updateResponse);
        updated.GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task RemoveCartItem_LastItem_Returns204()
    {
        AuthenticateAsCustomer();

        var addResponse = await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 1)));

        var cart   = await DeserializeAsync<JsonElement>(addResponse);
        var itemId = cart.GetProperty("items")[0].GetProperty("id").GetString();

        var removeResponse = await Client.DeleteAsync($"/api/cart/items/{itemId}");

        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveCartItem_NotLastItem_ReturnsUpdatedCart()
    {
        AuthenticateAsCustomer();

        await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 1)));

        var addSecond = await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId2, "Naan", 40m, 2)));

        var cart      = await DeserializeAsync<JsonElement>(addSecond);
        var firstItem = cart.GetProperty("items").EnumerateArray()
                            .First(i => i.GetProperty("menuItemId").GetString() == MenuItemId1.ToString());
        var itemId = firstItem.GetProperty("id").GetString();

        var removeResponse = await Client.DeleteAsync($"/api/cart/items/{itemId}");

        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await DeserializeAsync<JsonElement>(removeResponse);
        updated.GetProperty("items").GetArrayLength().Should().Be(1);
        updated.GetProperty("items")[0].GetProperty("name").GetString().Should().Be("Naan");
    }

    [Fact]
    public async Task ClearCart_Returns204()
    {
        AuthenticateAsCustomer();

        await Client.PostAsync(
            "/api/cart/items?replaceCart=false",
            JsonBody(AddItemRequest(MenuItemId1, "Butter Chicken", 200m, 1)));

        var clearResponse = await Client.DeleteAsync("/api/cart");
        clearResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify cart is gone
        var getResponse = await Client.GetAsync("/api/cart");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCart_WhenEmpty_Returns204()
    {
        AuthenticateAsCustomer();

        var response = await Client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CartEndpoints_Unauthenticated_Return401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private static object AddItemRequest(
        Guid menuItemId,
        string name,
        decimal unitPrice,
        int quantity,
        Guid? restaurantId   = null,
        string? restaurantName = null) => new
    {
        restaurantId   = (restaurantId   ?? RestaurantId).ToString(),
        restaurantName = restaurantName ?? "Test Restaurant",
        menuItemId     = menuItemId.ToString(),
        name,
        unitPrice,
        quantity
    };
}
