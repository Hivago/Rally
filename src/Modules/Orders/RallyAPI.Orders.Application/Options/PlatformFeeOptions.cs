namespace RallyAPI.Orders.Application.Options;

/// <summary>
/// Platform fee + its GST, used to price the platform charge on PICKUP orders (which have no
/// delivery quote). Bound to the SAME config section the Delivery quote uses
/// ("Delivery:Quote") so the value is a single source of truth across modules — change it once
/// in Railway and both the delivery quote and pickup bill move together.
/// For DELIVERY orders these amounts come from the stored quote, not this config.
/// </summary>
public sealed class PlatformFeeOptions
{
    public const string SectionName = "Delivery:Quote";

    public decimal CustomerPlatformFee { get; set; } = 10m;
    public decimal PlatformGstPercent { get; set; } = 18m;
}
