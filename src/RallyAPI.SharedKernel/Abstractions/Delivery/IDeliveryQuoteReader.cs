namespace RallyAPI.SharedKernel.Abstractions.Delivery;

/// <summary>
/// Reads a previously-issued delivery quote's authoritative pricing by id. Lets the Orders module
/// build the order bill from what WE quoted (backend-authoritative) instead of trusting the
/// frontend's numbers. Implemented in the Delivery module over the stored DeliveryQuote.
/// </summary>
public interface IDeliveryQuoteReader
{
    Task<DeliveryQuotePricing?> GetPricingAsync(Guid quoteId, CancellationToken ct = default);
}

/// <summary>
/// The customer-facing delivery-side charges captured on a quote.
/// <paramref name="Gst"/> is the total GST on (delivery fee + platform fee).
/// </summary>
public sealed record DeliveryQuotePricing(
    Guid QuoteId,
    decimal DeliveryFee,
    decimal PlatformFee,
    decimal Gst,
    bool IsUsed,
    bool IsExpired)
{
    /// <summary>What the customer pays for delivery = DeliveryFee + PlatformFee + Gst.</summary>
    public decimal Total => DeliveryFee + PlatformFee + Gst;
}
