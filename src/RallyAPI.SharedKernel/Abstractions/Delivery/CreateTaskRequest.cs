namespace RallyAPI.SharedKernel.Abstractions.Delivery;

/// <summary>
/// Request to create/book a delivery task with a third-party provider.
/// </summary>
public sealed record CreateTaskRequest
{
    /// <summary>
    /// Our internal order ID.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// Our order number for reference.
    /// </summary>
    public required string OrderNumber { get; init; }

    /// <summary>
    /// Our delivery request ID.
    /// </summary>
    public required Guid DeliveryRequestId { get; init; }

    // ─── Pickup Details ───

    public required double PickupLatitude { get; init; }
    public required double PickupLongitude { get; init; }
    public required string PickupPincode { get; init; }
    public required string PickupAddressLine1 { get; init; }
    public string? PickupAddressLine2 { get; init; }
    public required string PickupCity { get; init; }
    public required string PickupState { get; init; }
    public required string PickupContactName { get; init; }
    public required string PickupContactPhone { get; init; }

    /// <summary>
    /// Restaurant/store ID for provider reference.
    /// </summary>
    public string? StoreId { get; init; }

    // ─── Drop Details ───

    public required double DropLatitude { get; init; }
    public required double DropLongitude { get; init; }
    public required string DropPincode { get; init; }
    public required string DropAddressLine1 { get; init; }
    public string? DropAddressLine2 { get; init; }
    public required string DropCity { get; init; }
    public required string DropState { get; init; }
    public required string DropContactName { get; init; }
    public required string DropContactPhone { get; init; }

    // ─── Order Details ───

    public required decimal OrderAmount { get; init; }
    public decimal CodAmount { get; init; } = 0; // We don't do COD
    public decimal OrderWeight { get; init; } = 2; // Default 2kg for F&B

    /// <summary>
    /// 6-digit OTP shown to the rider at the restaurant. Provider stores
    /// this and validates against rider input on pickup.
    /// </summary>
    public string? PickupCode { get; init; }

    /// <summary>
    /// 4-digit OTP shared with the customer for delivery confirmation.
    /// </summary>
    public string? DropCode { get; init; }

    /// <summary>
    /// Order category — "F&amp;B" / "Grocery" / "Pharma" — required by
    /// some 3PL providers (e.g. ProRouting). Defaults to F&amp;B.
    /// </summary>
    public string OrderCategory { get; init; } = "F&B";

    /// <summary>
    /// Order items for provider reference.
    /// </summary>
    public IReadOnlyList<TaskOrderItem> OrderItems { get; init; } = [];

    /// <summary>
    /// Whether the food is already ready for pickup.
    /// </summary>
    public bool IsOrderReady { get; init; }

    /// <summary>
    /// Customer promised delivery time.
    /// </summary>
    public DateTime? CustomerPromisedTime { get; init; }

    /// <summary>
    /// Special instructions for delivery.
    /// </summary>
    public string? Notes { get; init; }

    // ─── Selection Criteria ───

    /// <summary>
    /// Selection mode: "fastest_agent", "selected_lsp", "estimated_price"
    /// </summary>
    public string SelectionMode { get; init; } = "fastest_agent";

    /// <summary>
    /// If using "selected_lsp" mode, specify which LSP.
    /// </summary>
    public string? SelectedLspId { get; init; }
    public string? SelectedItemId { get; init; }

    /// <summary>
    /// Maximum amount willing to pay.
    /// </summary>
    public decimal? MaxAmount { get; init; }

    /// <summary>
    /// Maximum SLA in minutes.
    /// </summary>
    public int? MaxSlaMins { get; init; }

    /// <summary>
    /// Webhook URL for status callbacks.
    /// </summary>
    public required string CallbackUrl { get; init; }
}

/// <summary>
/// Individual order item for task creation.
/// </summary>
public sealed record TaskOrderItem(
    string Name,
    int Quantity,
    decimal Price);