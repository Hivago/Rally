namespace RallyAPI.Delivery.Application.DTOs;

public sealed record DeliveryQuoteDto
{
    public Guid Id { get; init; }

    /// <summary>"Delivery" or "Pickup". Pickup quotes carry no delivery fee / distance / ETA.</summary>
    public string FulfillmentType { get; init; } = "Delivery";

    /// <summary>Food subtotal (echo of the request's OrderAmount) so the UI can show the full bill.</summary>
    public decimal ItemTotal { get; init; }

    /// <summary>Delivery fee only (no platform fee / GST). Always 0 for pickup.</summary>
    public decimal DeliveryFee { get; init; }

    /// <summary>Flat platform fee charged to the customer.</summary>
    public decimal PlatformFee { get; init; }

    /// <summary>GST on (delivery fee + platform fee) — 18%. Distinct from <see cref="FoodGst"/>.</summary>
    public decimal Gst { get; init; }

    /// <summary>GST on the food subtotal (ItemTotal) — 5%. Restaurant-service GST the platform collects.</summary>
    public decimal FoodGst { get; init; }

    /// <summary>Everything the customer pays on top of the food = DeliveryFee + PlatformFee + Gst + FoodGst.</summary>
    public decimal TotalPayable { get; init; }

    /// <summary>The full amount the customer pays = ItemTotal + TotalPayable.</summary>
    public decimal GrandTotal { get; init; }

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