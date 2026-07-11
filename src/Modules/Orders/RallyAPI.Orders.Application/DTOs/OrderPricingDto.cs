namespace RallyAPI.Orders.Application.DTOs;

/// <summary>
/// Order pricing breakdown data transfer object.
/// </summary>
public sealed record OrderPricingDto
{
    public decimal SubTotal { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal Tax { get; init; }
    public decimal Discount { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal ServiceFee { get; init; }
    public decimal Tip { get; init; }

    /// <summary>Flat platform fee (delivery AND pickup orders).</summary>
    public decimal PlatformFee { get; init; }

    /// <summary>GST on delivery fee + platform fee (18%). Distinct from Tax (food GST).</summary>
    public decimal ServiceGst { get; init; }

    public decimal Total { get; init; }
    public string Currency { get; init; } = "INR";
    public string? DiscountCode { get; init; }
    public string? DiscountDescription { get; init; }

    // Formatted strings for display
    public string SubTotalDisplay { get; init; } = string.Empty;
    public string DeliveryFeeDisplay { get; init; } = string.Empty;
    public string TaxDisplay { get; init; } = string.Empty;
    public string PlatformFeeDisplay { get; init; } = string.Empty;
    public string ServiceGstDisplay { get; init; } = string.Empty;
    public string DiscountDisplay { get; init; } = string.Empty;
    public string TotalDisplay { get; init; } = string.Empty;
}