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

    /// <summary>GST percentage on (delivery fee + platform fee). e.g. 18 = 18%.</summary>
    public decimal GstPercent { get; set; } = 18m;
}
