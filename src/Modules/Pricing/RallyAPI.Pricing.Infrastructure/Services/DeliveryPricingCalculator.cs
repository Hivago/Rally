using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.SharedKernel.Abstractions.Distance;
using RallyAPI.SharedKernel.Abstractions.Pricing;

namespace RallyAPI.Pricing.Infrastructure.Services;

/// <summary>
/// Calculates delivery fees using actual road distance.
/// </summary>
public sealed class DeliveryPricingCalculator : IDeliveryPricingCalculator
{
    private readonly IDistanceCalculator _distanceCalculator;
    private readonly DeliveryPricingOptions _options;
    private readonly ILogger<DeliveryPricingCalculator> _logger;

    public DeliveryPricingCalculator(
        IDistanceCalculator distanceCalculator,
        IOptions<DeliveryPricingOptions> options,
        ILogger<DeliveryPricingCalculator> logger)
    {
        _distanceCalculator = distanceCalculator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DeliveryPriceResult> CalculateAsync(
        DeliveryPriceRequest request,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Calculating delivery price from ({PickupLat}, {PickupLng}) to ({DropLat}, {DropLng})",
            request.PickupLatitude, request.PickupLongitude,
            request.DropLatitude, request.DropLongitude);

        // Get road distance from Google Maps
        var distanceResult = await _distanceCalculator.GetDistanceAsync(
            request.PickupLatitude,
            request.PickupLongitude,
            request.DropLatitude,
            request.DropLongitude,
            ct);

        if (!distanceResult.IsSuccess)
        {
            _logger.LogWarning("Distance calculation failed: {Error}", distanceResult.ErrorMessage);
            return DeliveryPriceResult.Failure(distanceResult.ErrorMessage ?? "Distance calculation failed");
        }

        // Calculate fee
        var distanceKm = distanceResult.DistanceKm;
        var fee = CalculateFee(distanceKm);

        // Build breakdown
        var breakdown = BuildBreakdown(distanceKm, fee);

        // Generate quote
        var quoteId = $"quote_{Guid.NewGuid():N}"[..24];
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.QuoteValidityMinutes);

        _logger.LogInformation(
            "Delivery price calculated: {Distance}km = ₹{Fee}, QuoteId: {QuoteId}",
            distanceKm, fee.FinalFee, quoteId);

        return DeliveryPriceResult.Success(
            quoteId: quoteId,
            baseFee: fee.BaseFee,
            finalFee: fee.FinalFee,
            distanceKm: distanceKm,
            estimatedMinutes: distanceResult.DurationMinutes,
            surgeMultiplier: 1.0m, // TODO: Integrate with surge pricing
            surgeReason: null,
            expiresAt: expiresAt,
            breakdown: breakdown);
    }

    public int EstimateDeliveryMinutes(decimal distanceKm)
    {
        // Average 20 km/h in city traffic
        var minutes = (int)Math.Ceiling((double)distanceKm / 20 * 60);
        return Math.Max(10, minutes); // Minimum 10 minutes
    }

    private (decimal BaseFee, decimal FinalFee) CalculateFee(decimal distanceKm)
    {
        var baseFee = _options.BaseFee;
        var baseDistance = _options.BaseDistanceKm;
        var perKmRate = _options.PerKmRate;

        if (distanceKm <= baseDistance)
        {
            return (baseFee, baseFee);
        }

        var extraKm = distanceKm - baseDistance;
        var extraCharge = Math.Ceiling(extraKm) * perKmRate; // Round up extra km
        var finalFee = baseFee + extraCharge;

        return (baseFee, finalFee);
    }

    private List<PriceComponent> BuildBreakdown(decimal distanceKm, (decimal BaseFee, decimal FinalFee) fee)
    {
        var breakdown = new List<PriceComponent>
        {
            new("Base Fee", $"Covers first {_options.BaseDistanceKm} km", fee.BaseFee)
        };

        if (distanceKm > _options.BaseDistanceKm)
        {
            var extraKm = Math.Ceiling(distanceKm - _options.BaseDistanceKm);
            var extraCharge = extraKm * _options.PerKmRate;

            breakdown.Add(new PriceComponent(
                "Distance Charge",
                $"{extraKm} km × ₹{_options.PerKmRate}",
                extraCharge));
        }

        return breakdown;
    }
}

/// <summary>
/// Configuration for delivery pricing.
/// </summary>
public sealed class DeliveryPricingOptions
{
    public const string SectionName = "Delivery:Pricing";

    /// <summary>
    /// Base delivery fee (covers first X km).
    /// </summary>
    public decimal BaseFee { get; set; } = 30m;

    /// <summary>
    /// Distance covered by base fee.
    /// </summary>
    public decimal BaseDistanceKm { get; set; } = 3m;

    /// <summary>
    /// Rate per km beyond base distance.
    /// </summary>
    public decimal PerKmRate { get; set; } = 10m;

    /// <summary>
    /// How long quotes are valid (minutes).
    /// </summary>
    public int QuoteValidityMinutes { get; set; } = 30;
}