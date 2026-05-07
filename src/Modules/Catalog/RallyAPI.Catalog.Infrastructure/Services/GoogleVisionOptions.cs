namespace RallyAPI.Catalog.Infrastructure.Services;

/// <summary>
/// Bound from configuration section "GoogleVision".
/// CredentialsJson holds the full service account JSON (Railway-style inline secret) — we never
/// require a file on disk. If empty, the OCR service runs in a "not configured" mode that fails
/// gracefully with a clear error so the rest of the API still boots.
/// </summary>
public sealed class GoogleVisionOptions
{
    public const string SectionName = "GoogleVision";

    public string CredentialsJson { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CredentialsJson);
}
