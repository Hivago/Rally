namespace RallyAPI.Orders.Application.Options;

/// <summary>
/// Configurable rules applied when a customer places an order.
/// Bound from appsettings.json section "OrderPlacement".
/// </summary>
public sealed class OrderPlacementOptions
{
    public const string SectionName = "OrderPlacement";

    /// <summary>
    /// Minimum food subtotal (in INR) required to place an order. Default: 150.
    /// Applies to the item subtotal only — delivery fee, tax, and other charges are excluded.
    /// Set to 0 to disable the minimum-order check.
    /// </summary>
    public decimal MinimumOrderValue { get; set; } = 150m;
}
