using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

/// <summary>
/// Body for POST /partner/order/update — pushes pickup/drop OTPs and marks
/// the order ready for the rider to pick up.
/// </summary>
public sealed class ProRoutingUpdateRequest
{
    [JsonPropertyName("order")]
    public required ProRoutingUpdateOrder Order { get; init; }
}

public sealed class ProRoutingUpdateOrder
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("pickup_code")]
    public required string PickupCode { get; init; }

    [JsonPropertyName("drop_code")]
    public string? DropCode { get; init; }

    [JsonPropertyName("customer_promised_time")]
    public string? CustomerPromisedTime { get; init; }

    [JsonPropertyName("order_ready")]
    public bool OrderReady { get; init; } = true;
}

public sealed class ProRoutingUpdateResponse
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("order")]
    public ProRoutingUpdateResponseOrder? Order { get; init; }

    [JsonIgnore]
    public bool IsSuccess => Status == 1;
}

public sealed class ProRoutingUpdateResponseOrder
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }
}
