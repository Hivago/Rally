namespace RallyAPI.Delivery.Application.Commands.GetQuote;

/// <summary>
/// Customer-facing quote add-ons applied uniformly to every quote (own fleet AND 3PL):
/// a flat platform fee and GST on (delivery fee + platform fee). All values are config-bound
/// so they can be tuned from Railway without a redeploy.
/// Bound from appsettings section "Delivery:Quote".
/// </summary>
public sealed class QuotePricingOptions
{
    public const string SectionName = "Delivery:Quote";

    /// <summary>Flat platform fee charged to the customer, on top of the delivery fee.</summary>
    public decimal CustomerPlatformFee { get; set; } = 10m;

    /// <summary>GST percentage on the delivery fee. e.g. 18 = 18%.</summary>
    public decimal DeliveryGstPercent { get; set; } = 18m;

    /// <summary>GST percentage on the platform fee. e.g. 18 = 18%. Kept separate from
    /// <see cref="DeliveryGstPercent"/> so the two can diverge in future without a code change.</summary>
    public decimal PlatformGstPercent { get; set; } = 18m;
}
