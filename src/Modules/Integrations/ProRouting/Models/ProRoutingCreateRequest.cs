using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

public sealed class ProRoutingCreateRequest
{
    [JsonPropertyName("client_order_id")]
    public required string ClientOrderId { get; init; }

    [JsonPropertyName("retail_order_id")]
    public string? RetailOrderId { get; init; }

    [JsonPropertyName("pickup")]
    public required ProRoutingPickupDetails Pickup { get; init; }

    [JsonPropertyName("drop")]
    public required ProRoutingDropDetails Drop { get; init; }

    [JsonPropertyName("customer_promised_time")]
    public string? CustomerPromisedTime { get; init; }

    [JsonPropertyName("callback_url")]
    public required string CallbackUrl { get; init; }

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

    [JsonPropertyName("order_items")]
    public List<ProRoutingOrderItem> OrderItems { get; init; } = [];

    [JsonPropertyName("order_ready")]
    public bool OrderReady { get; init; } = true;

    [JsonPropertyName("select_criteria")]
    public required ProRoutingSelectCriteria SelectCriteria { get; init; }

    [JsonPropertyName("note1")]
    public string? Note1 { get; init; }
}

public sealed class ProRoutingPickupDetails
{
    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lng")]
    public double Lng { get; init; }

    [JsonPropertyName("pincode")]
    public required string Pincode { get; init; }

    [JsonPropertyName("phone")]
    public required string Phone { get; init; }

    [JsonPropertyName("address")]
    public required ProRoutingAddress Address { get; init; }

    [JsonPropertyName("store_id")]
    public string? StoreId { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("otp")]
    public string? Otp { get; init; }

    [JsonPropertyName("rto_otp")]
    public string? RtoOtp { get; init; }
}

public sealed class ProRoutingDropDetails
{
    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lng")]
    public double Lng { get; init; }

    [JsonPropertyName("pincode")]
    public required string Pincode { get; init; }

    [JsonPropertyName("phone")]
    public required string Phone { get; init; }

    [JsonPropertyName("address")]
    public required ProRoutingAddress Address { get; init; }

    [JsonPropertyName("otp")]
    public string? Otp { get; init; }

    [JsonPropertyName("pin")]
    public string? Pin { get; init; }
}

public sealed class ProRoutingAddress
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("line1")]
    public required string Line1 { get; init; }

    [JsonPropertyName("line2")]
    public string? Line2 { get; init; }

    [JsonPropertyName("city")]
    public required string City { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }
}

public sealed class ProRoutingOrderItem
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("qty")]
    public int Qty { get; init; }

    [JsonPropertyName("price")]
    public decimal Price { get; init; }
}

public sealed class ProRoutingSelectCriteria
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "fastest_agent";

    [JsonPropertyName("max_amount")]
    public decimal? MaxAmount { get; init; }

    [JsonPropertyName("max_sla")]
    public int? MaxSla { get; init; }

    [JsonPropertyName("lsp_id")]
    public string? LspId { get; init; }

    [JsonPropertyName("item_id")]
    public string? ItemId { get; init; }
}