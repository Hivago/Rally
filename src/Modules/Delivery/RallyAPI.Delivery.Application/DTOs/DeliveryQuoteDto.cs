namespace RallyAPI.Delivery.Application.DTOs;

public sealed record DeliveryQuoteDto
{
    public Guid Id { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal DistanceKm { get; init; }
    public int EstimatedMinutes { get; init; }
    public decimal SurgeMultiplier { get; init; }
    public string? SurgeReason { get; init; }
    public DateTime ExpiresAt { get; init; }
    public IReadOnlyList<PriceBreakdownItem> Breakdown { get; init; } = [];
}

public sealed record PriceBreakdownItem(
    string Name,
    string Description,
    decimal Amount);