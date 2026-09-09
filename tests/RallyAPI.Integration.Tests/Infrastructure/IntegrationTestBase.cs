using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RallyAPI.Integration.Tests.Infrastructure;

/// <summary>
/// Base class for all integration test classes.
/// Provides an authenticated HttpClient, JWT helper, and database reset.
/// </summary>
[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IntegrationTestFactory Factory;
    protected readonly HttpClient Client;
    protected readonly TestJwtHelper Jwt;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Well-known test IDs — stable across tests so JWT userId == order restaurantId, etc.
    protected static readonly Guid CustomerId   = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    protected static readonly Guid RestaurantId = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");
    protected static readonly Guid RiderId      = Guid.Parse("c3c3c3c3-c3c3-c3c3-c3c3-c3c3c3c3c3c3");
    protected static readonly Guid AdminId      = Guid.Parse("d4d4d4d4-d4d4-d4d4-d4d4-d4d4d4d4d4d4");

    protected IntegrationTestBase(IntegrationTestFactory factory)
    {
        Factory = factory;
        Client  = factory.CreateClient();
        Jwt     = new TestJwtHelper(factory.Rsa);
    }

    public virtual async Task InitializeAsync()
    {
        await Factory.InitialiseRespawnerAsync();
        await Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync() => Task.CompletedTask;

    // ─── HTTP helpers ─────────────────────────────────────────────────────────────

    protected void AuthenticateAs(string token)
        => Client.DefaultRequestHeaders.Authorization =
               new AuthenticationHeaderValue("Bearer", token);

    protected void AuthenticateAsCustomer()   => AuthenticateAs(Jwt.CreateCustomerToken(CustomerId));
    protected void AuthenticateAsRestaurant() => AuthenticateAs(Jwt.CreateRestaurantToken(RestaurantId));
    protected void AuthenticateAsRider()      => AuthenticateAs(Jwt.CreateRiderToken(RiderId));
    protected void AuthenticateAsAdmin()      => AuthenticateAs(Jwt.CreateAdminToken(AdminId));

    protected static StringContent JsonBody(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    protected static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from: {json}");
    }

    // ─── Shared request builders ──────────────────────────────────────────────────

    // Pickup/drop coordinates shared between BuildPlaceOrderRequest and
    // GetDeliveryQuoteIdAsync — PlaceOrder recomputes pricing from the quote it's
    // handed, so the quote and the order it's attached to must describe the same trip.
    private const double PickupLatitude = 28.6315;
    private const double PickupLongitude = 77.2167;
    private const string PickupPincode = "110001";
    private const double DropLatitude = 28.6129;
    private const double DropLongitude = 77.2295;
    private const string DropPincode = "110002";

    /// <summary>
    /// Fetches a real delivery quote via POST /api/delivery/quote (own-fleet path —
    /// IRiderQueryService/IDistanceCalculator/IDeliveryPricingCalculator are stubbed
    /// in IntegrationTestFactory) and returns its ID for use as PlaceOrder's
    /// deliveryQuoteId. PlaceOrder rejects delivery orders without one.
    /// </summary>
    protected async Task<string> GetDeliveryQuoteIdAsync(Guid? restaurantId = null, decimal orderAmount = 200m)
    {
        var response = await Client.PostAsync("/api/delivery/quote", JsonBody(new
        {
            restaurantId    = (restaurantId ?? RestaurantId).ToString(),
            pickupLatitude  = PickupLatitude,
            pickupLongitude = PickupLongitude,
            pickupPincode   = PickupPincode,
            dropLatitude    = DropLatitude,
            dropLongitude   = DropLongitude,
            dropPincode     = DropPincode,
            orderAmount
        }));
        response.EnsureSuccessStatusCode();

        var quote = await DeserializeAsync<JsonElement>(response);
        return quote.GetProperty("id").GetString()!;
    }

    /// <summary>Builds a minimal valid PlaceOrderRequest body.</summary>
    protected static object BuildPlaceOrderRequest(
        Guid? restaurantId    = null,
        string paymentId      = "test-payment-001",
        decimal subTotal      = 200m,
        decimal deliveryFee   = 30m,
        string? deliveryQuoteId = null) => new
    {
        paymentId,
        paymentTransactionId = "txn-test-001",
        restaurantId         = (restaurantId ?? RestaurantId).ToString(),
        restaurantName       = "Test Restaurant",
        deliveryQuoteId,
        pickupLatitude       = PickupLatitude,
        pickupLongitude      = PickupLongitude,
        pickupPincode        = PickupPincode,
        pickupAddress        = "Connaught Place, New Delhi",
        deliveryAddress      = new
        {
            street       = "12, India Gate",
            city         = "New Delhi",
            pincode      = DropPincode,
            latitude     = DropLatitude,
            longitude    = DropLongitude,
            contactPhone = "+919876543210"
        },
        items = new[]
        {
            new
            {
                menuItemId   = Guid.NewGuid().ToString(),
                itemName     = "Butter Chicken",
                unitPrice    = subTotal,
                quantity     = 1
            }
        },
        pricing = new
        {
            subTotal,
            deliveryFee,
            tax      = 20m,
            discount = 0m
        }
    };

    /// <summary>
    /// POSTs to /api/orders with a fresh Idempotency-Key attached — the endpoint is
    /// guarded by IdempotencyEndpointFilter and rejects requests missing that header.
    /// A fresh key per call means repeated calls in the same test (e.g. placing 2
    /// orders for a pagination test) aren't deduped/rejected as the same checkout attempt.
    /// </summary>
    protected async Task<HttpResponseMessage> PostOrderAsync(object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonBody(body)
        };
        request.Headers.Add(RallyAPI.SharedKernel.Constants.HttpHeaders.IdempotencyKey, Guid.NewGuid().ToString());

        return await Client.SendAsync(request);
    }

    /// <summary>Places an order and returns its ID. Asserts success.</summary>
    protected async Task<Guid> PlaceOrderAsync(
        Guid? restaurantId  = null,
        string paymentId    = "test-payment-001",
        decimal subTotal    = 200m,
        decimal deliveryFee = 30m)
    {
        AuthenticateAsCustomer();

        var quoteId  = await GetDeliveryQuoteIdAsync(restaurantId, subTotal);
        var body     = BuildPlaceOrderRequest(restaurantId, paymentId, subTotal, deliveryFee, quoteId);
        var response = await PostOrderAsync(body);
        response.EnsureSuccessStatusCode();

        var json   = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        return Guid.Parse(result.GetProperty("id").GetString()!);
    }
}
