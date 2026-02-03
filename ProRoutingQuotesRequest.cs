using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

public sealed class ProRoutingQuotesRequest
{
    [JsonPropertyName("pickup")]
    public required ProRoutingLocation Pickup { get; init; }

    [JsonPropertyName("drop")]
    public required ProRoutingLocation Drop { get; init; }

    [JsonPropertyName("city")]
    public required string City { get; init; }

    [JsonPropertyName("order_category")]
    public string OrderCategory { get; init; } = "F&B";

    [JsonPropertyName("search_category")]
    public string SearchCategory { get; init; } = "Immediate Delivery";

    [JsonPropertyName("order_amount")]
    public decimal OrderAmount { get; init; }

    [JsonPropertyName("cod_amount")]
    public decimal CodAmount { get; init; } = 0;

    [JsonPropertyName("order_weight")]
    public decimal OrderWeight { get; init; } = 2;
}