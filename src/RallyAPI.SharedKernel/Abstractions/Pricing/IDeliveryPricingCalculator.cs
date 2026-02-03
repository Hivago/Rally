namespace RallyAPI.SharedKernel.Abstractions.Pricing;

/// <summary>
/// Service for calculating own fleet delivery prices.
/// Implemented by Pricing module, consumed by Delivery module.
/// </summary>
public interface IDeliveryPricingCalculator
{
    /// <summary>
    /// Calculates delivery fee for own fleet.
    /// Applies base fee, distance rate, and any surge pricing.
    /// </summary>
    /// <param name="request">Pricing request with locations and order details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Calculated price with breakdown</returns>
    Task<DeliveryPriceResult> CalculateAsync(
        DeliveryPriceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Calculates estimated delivery time based on distance.
    /// </summary>
    /// <param name="distanceKm">Distance in kilometers</param>
    /// <returns>Estimated minutes</returns>
    int EstimateDeliveryMinutes(decimal distanceKm);
}
