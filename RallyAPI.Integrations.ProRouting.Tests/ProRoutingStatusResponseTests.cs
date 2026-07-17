using System.Text.Json;
using FluentAssertions;
using RallyAPI.Integrations.ProRouting.Models;
using Xunit;

namespace RallyAPI.Integrations.ProRouting.Tests;

/// <summary>
/// Pins the shape of POST partner/order/status against a real captured response.
///
/// Why this exists: the status response was previously deserialized into ProRoutingWebhookPayload,
/// which looks similar but is NOT the same contract — it reads "order_id"/"state"/"agent" at the
/// root, while the status response nests everything under "order" and calls the agent "rider".
/// Every field silently came back null/empty, GetTaskStatusAsync still reported Success, and the
/// missed-webhook reconcile therefore never healed anything.
/// </summary>
public class ProRoutingStatusResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Verbatim response from client-api.prorouting.in for task mfnb_fx6fsryz (2026-07-17) —
    // the staging order our sweep cancelled while this rider was completing it.
    private const string RealDeliveredResponse = """
    {
      "status": 1,
      "order": {
        "id": "mfnb_fx6fsryz",
        "client_order_id": "ba2097ed-0017-40c1-b174-c99753c4c317",
        "state": "Order-delivered",
        "lsp": { "id": "ondc.shiprocket.in", "name": "Shiprocket", "item_id": "1001" },
        "price": 61.0,
        "fees": { "lsp": 58.0, "platform": 3.0, "total_with_tax": 71.98 },
        "distance": 0.59,
        "rider": {
          "name": "SUNIL DINESH",
          "phone": "8976033457",
          "last_location": { "lat": 19.17, "lng": 72.98, "updated_at": "2026-07-17 17:05:08" }
        },
        "pickedup_at": "2026-07-17 17:02:32",
        "delivered_at": "2026-07-17 17:05:15",
        "tracking_url": "https://shiprocket.co/tracking/6a5a115324afba174a4b4e38"
      }
    }
    """;

    private const string RealErrorResponse = """
    {"status":0,"message":"Invalid JSON Payload, path: order"}
    """;

    [Fact]
    public void Deserialize_RealDeliveredResponse_ShouldReadStateAndRider()
    {
        var payload = JsonSerializer.Deserialize<ProRoutingStatusResponse>(RealDeliveredResponse, JsonOptions);

        payload.Should().NotBeNull();
        payload!.Status.Should().Be(1);
        payload.Order.Should().NotBeNull();
        payload.Order!.Id.Should().Be("mfnb_fx6fsryz");
        payload.Order.State.Should().Be("Order-delivered");
        payload.Order.Rider!.Name.Should().Be("SUNIL DINESH");
        payload.Order.Rider.Phone.Should().Be("8976033457");
        payload.Order.TrackingUrl.Should().Be("https://shiprocket.co/tracking/6a5a115324afba174a4b4e38");
    }

    [Fact]
    public void Deserialize_ErrorResponse_ShouldExposeNonSuccessStatus()
    {
        // ProRouting returns this with HTTP 200, so the envelope status is the only failure signal.
        var payload = JsonSerializer.Deserialize<ProRoutingStatusResponse>(RealErrorResponse, JsonOptions);

        payload.Should().NotBeNull();
        payload!.Status.Should().NotBe(1);
        payload.Order.Should().BeNull();
        payload.Message.Should().Contain("Invalid JSON Payload");
    }

    [Fact]
    public void Deserialize_StatusResponseIntoWebhookPayload_ShouldLoseEverything()
    {
        // Documents the original bug so nobody "simplifies" the two models back together:
        // the webhook contract reads the status response as an empty order.
        var wrong = JsonSerializer.Deserialize<ProRoutingWebhookPayload>(RealDeliveredResponse, JsonOptions);

        wrong!.State.Should().BeEmpty("the status response has no root-level 'state' — it is under 'order'");
        wrong.OrderId.Should().BeEmpty("the status response uses 'order.id', not root 'order_id'");
        wrong.Agent.Should().BeNull("the status response calls the agent 'rider'");
    }
}
