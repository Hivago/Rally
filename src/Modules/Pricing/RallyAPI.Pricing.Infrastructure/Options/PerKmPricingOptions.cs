namespace RallyAPI.Pricing.Infrastructure.Options;

public sealed class PerKmPricingOptions
{
    public const string Section = "PerKmPricing";
    public double BaseDistanceKm { get; init; } = 3.0;
    public decimal PerKmRate { get; init; } = 10.0m;
}
