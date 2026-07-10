using System.ComponentModel.DataAnnotations;

namespace RallyAPI.Integrations.ProRouting;

/// <summary>
/// Configuration options for ProRouting API integration.
/// </summary>
public sealed class ProRoutingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "ProRouting";

    /// <summary>
    /// Base URL for the ProRouting API.
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://preprod.logistics-buyer.mp2.in";

    /// <summary>
    /// API key for authentication (x-pro-api-key header).
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Default order category. Options: "F&B", "Grocery"
    /// </summary>
    public string DefaultOrderCategory { get; set; } = "F&B";

    /// <summary>
    /// Default search category. 
    /// Options: "Immediate Delivery", "Standard Delivery", "Same Day Delivery", "Next Day Delivery"
    /// </summary>
    public string DefaultSearchCategory { get; set; } = "Immediate Delivery";

    /// <summary>
    /// Default order weight in kilograms when not specified.
    /// </summary>
    [Range(0.1, 100)]
    public decimal DefaultOrderWeight { get; set; } = 2;

    /// <summary>
    /// Number of retry attempts for transient failures.
    /// </summary>
    [Range(0, 5)]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Enable or disable the provider (useful for testing/fallback).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cancellation reason code sent to ProRouting on order cancel.
    /// ProRouting's cancel API expects a coded reason id, not free text.
    /// "010" = "Buyer wants to modify details" — the correct buyer-side,
    /// no-fault code for our automated re-dispatch / rollback cancels
    /// (using "005 Merchant rejected" would falsely blame the restaurant).
    /// Configurable in case the code table changes.
    /// </summary>
    public string DefaultCancellationReasonId { get; set; } = "010";
}