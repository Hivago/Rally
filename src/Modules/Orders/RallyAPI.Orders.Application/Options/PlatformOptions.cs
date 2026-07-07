namespace RallyAPI.Orders.Application.Options;

/// <summary>
/// Platform-level identifiers printed on customer-facing documents.
/// Bound from appsettings.json section "Platform".
/// </summary>
public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>
    /// Rally's own FSSAI food-licence number, printed on the customer bill/label
    /// alongside the restaurant's (as an aggregator/marketplace). Nullable until issued.
    /// </summary>
    public string? FssaiNumber { get; set; }
}
