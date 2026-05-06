using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

/// <summary>
/// Body for POST /partner/order/issue — raise a grievance with the LSP.
/// </summary>
public sealed class ProRoutingIssueRequest
{
    [JsonPropertyName("order")]
    public required ProRoutingIssueOrderRef Order { get; init; }

    [JsonPropertyName("issue")]
    public required ProRoutingIssueCategory Issue { get; init; }

    [JsonPropertyName("description")]
    public required ProRoutingIssueDescription Description { get; init; }
}

public sealed class ProRoutingIssueOrderRef
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

public sealed class ProRoutingIssueCategory
{
    [JsonPropertyName("category")]
    public required string Category { get; init; }

    [JsonPropertyName("sub_category")]
    public required string SubCategory { get; init; }
}

public sealed class ProRoutingIssueDescription
{
    [JsonPropertyName("line1")]
    public required string Line1 { get; init; }

    [JsonPropertyName("line2")]
    public string? Line2 { get; init; }
}

public sealed class ProRoutingIssueResponse
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("issue")]
    public ProRoutingIssueResponseIssue? Issue { get; init; }

    [JsonIgnore]
    public bool IsSuccess => Status == 1 && Issue is not null;
}

public sealed class ProRoutingIssueResponseIssue
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("resolution")]
    public ProRoutingIssueResolution? Resolution { get; init; }
}

public sealed class ProRoutingIssueResolution
{
    [JsonPropertyName("short_desc")]
    public string? ShortDesc { get; init; }

    [JsonPropertyName("long_desc")]
    public string? LongDesc { get; init; }

    [JsonPropertyName("action_triggered")]
    public string? ActionTriggered { get; init; }   // "REFUND" / "NO-ACTION"

    [JsonPropertyName("refund_amount")]
    public decimal? RefundAmount { get; init; }
}

/// <summary>
/// Body for POST /partner/order/issue_status — poll the status of an issue.
/// </summary>
public sealed class ProRoutingIssueStatusRequest
{
    [JsonPropertyName("issue")]
    public required ProRoutingIssueOrderRef Issue { get; init; }
}

/// <summary>
/// Body for POST /partner/order/issue_close.
/// </summary>
public sealed class ProRoutingIssueCloseRequest
{
    [JsonPropertyName("issue")]
    public required ProRoutingIssueCloseDetails Issue { get; init; }
}

public sealed class ProRoutingIssueCloseDetails
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("rating")]
    public required string Rating { get; init; }

    [JsonPropertyName("refund_by_lsp")]
    public bool RefundByLsp { get; init; }

    [JsonPropertyName("refund_to_client")]
    public bool RefundToClient { get; init; }
}

public sealed class ProRoutingIssueCloseResponse
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonIgnore]
    public bool IsSuccess => Status == 1;
}
