using Microsoft.Extensions.Options;
using RallyAPI.Pricing.Domain.Abstractions;
using RallyAPI.Pricing.Domain.Enums;
using RallyAPI.Pricing.Domain.ValueObjects;
using RallyAPI.Pricing.Infrastructure.Options;

namespace RallyAPI.Pricing.Infrastructure.Rules;

/// <summary>
/// Adds a per-km surcharge for delivery distances beyond the base distance.
/// Formula: (distanceKm - BaseDistanceKm) * PerKmRate when distanceKm > BaseDistanceKm.
/// </summary>
public sealed class PerKmDistanceRule : IPricingRule
{
    public string RuleName => "PerKmDistance";
    public int Priority => 3;
    public bool IsEnabled => true;

    private readonly PerKmPricingOptions _options;

    public PerKmDistanceRule(IOptions<PerKmPricingOptions> options)
        => _options = options.Value;

    public Task<bool> AppliesAsync(PricingContext context, CancellationToken ct = default)
        => Task.FromResult(context.DistanceKm > _options.BaseDistanceKm);

    public Task<PriceModification?> CalculateAsync(PricingContext context, CancellationToken ct = default)
    {
        var extraKm = (decimal)(context.DistanceKm - _options.BaseDistanceKm);
        var extraFee = Math.Round(extraKm * _options.PerKmRate, 2);
        var description = $"+{extraKm:F1} km beyond {_options.BaseDistanceKm} km base @ ₹{_options.PerKmRate}/km";

        return Task.FromResult<PriceModification?>(
            new PriceModification(RuleName, description, extraFee, ModificationType.Flat, Priority));
    }
}
