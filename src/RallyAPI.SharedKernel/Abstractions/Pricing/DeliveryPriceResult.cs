namespace RallyAPI.SharedKernel.Abstractions.Pricing;

/// <summary>
/// Result of delivery price calculation for own fleet.
/// </summary>
public sealed record DeliveryPriceResult
{
    private DeliveryPriceResult() { }

    /// <summary>
    /// Whether the calculation was successful.
    /// </summary>
    public bool IsSuccess { get; private init; }

    /// <summary>
    /// Unique quote identifier for tracking.
    /// </summary>
    public string? QuoteId { get; private init; }

    /// <summary>
    /// Base delivery fee before surges.
    /// </summary>
    public decimal BaseFee { get; private init; }

    /// <summary>
    /// Final DELIVERY fee including all surges. This is the rider-earnings basis and the 3PL
    /// order amount — it never includes the platform fee or GST (those are applied in the
    /// Delivery module's GetQuote handler).
    /// </summary>
    public decimal FinalFee { get; private init; }

    /// <summary>
    /// Distance from restaurant to customer in km.
    /// </summary>
    public decimal DistanceKm { get; private init; }

    /// <summary>
    /// Estimated delivery time in minutes.
    /// </summary>
    public int EstimatedMinutes { get; private init; }

    /// <summary>
    /// Surge multiplier applied (1.0 = no surge).
    /// </summary>
    public decimal SurgeMultiplier { get; private init; }

    /// <summary>
    /// Human-readable surge reason if applicable.
    /// </summary>
    public string? SurgeReason { get; private init; }

    /// <summary>
    /// When this quote expires.
    /// </summary>
    public DateTime ExpiresAt { get; private init; }

    /// <summary>
    /// Price breakdown for transparency.
    /// </summary>
    public IReadOnlyList<PriceComponent> Breakdown { get; private init; } = [];

    /// <summary>
    /// Error message if calculation failed.
    /// </summary>
    public string? ErrorMessage { get; private init; }

    public static DeliveryPriceResult Success(
        string quoteId,
        decimal baseFee,
        decimal finalFee,
        decimal distanceKm,
        int estimatedMinutes,
        decimal surgeMultiplier,
        string? surgeReason,
        DateTime expiresAt,
        IReadOnlyList<PriceComponent> breakdown)
    {
        return new DeliveryPriceResult
        {
            IsSuccess = true,
            QuoteId = quoteId,
            BaseFee = baseFee,
            FinalFee = finalFee,
            DistanceKm = distanceKm,
            EstimatedMinutes = estimatedMinutes,
            SurgeMultiplier = surgeMultiplier,
            SurgeReason = surgeReason,
            ExpiresAt = expiresAt,
            Breakdown = breakdown
        };
    }

    public static DeliveryPriceResult Failure(string errorMessage)
    {
        return new DeliveryPriceResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Breakdown = []
        };
    }
}

/// <summary>
/// Individual component of the delivery price.
/// </summary>
public sealed record PriceComponent(
    string Name,
    string Description,
    decimal Amount);