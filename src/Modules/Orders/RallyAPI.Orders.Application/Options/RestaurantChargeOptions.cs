namespace RallyAPI.Orders.Application.Options;

/// <summary>
/// What the restaurant is charged per delivered order — a ₹35 delivery + ₹15 platform charge
/// (kept as two values for clarity; summed to one commission of ₹50) plus 18% GST. This REPLACES
/// the per-restaurant commission flat fee. Config-bound so it can be tuned from Railway.
/// Section "Delivery:RestaurantCharge".
/// </summary>
public sealed class RestaurantChargeOptions
{
    public const string SectionName = "Delivery:RestaurantCharge";

    public decimal DeliveryFee { get; set; } = 35m;
    public decimal PlatformFee { get; set; } = 15m;
    public decimal GstPercent { get; set; } = 18m;

    /// <summary>The single commission charge (delivery + platform), kept transparent as a sum.</summary>
    public decimal TotalCharge => DeliveryFee + PlatformFee;
}
