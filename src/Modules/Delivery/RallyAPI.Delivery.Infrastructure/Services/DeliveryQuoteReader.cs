using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.SharedKernel.Abstractions.Delivery;

namespace RallyAPI.Delivery.Infrastructure.Services;

/// <summary>
/// Exposes stored delivery-quote pricing to other modules (Orders) so the order bill can be built
/// from what we quoted, not from the frontend. Reads the DeliveryQuote aggregate by id.
/// </summary>
public sealed class DeliveryQuoteReader : IDeliveryQuoteReader
{
    private readonly IDeliveryQuoteRepository _quotes;

    public DeliveryQuoteReader(IDeliveryQuoteRepository quotes) => _quotes = quotes;

    public async Task<DeliveryQuotePricing?> GetPricingAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await _quotes.GetByIdAsync(quoteId, ct);
        if (quote is null)
            return null;

        return new DeliveryQuotePricing(
            QuoteId: quote.Id,
            DeliveryFee: quote.FinalFee,
            PlatformFee: quote.PlatformFee,
            Gst: quote.GstAmount,
            IsUsed: quote.IsUsed,
            IsExpired: quote.IsExpired);
    }
}
