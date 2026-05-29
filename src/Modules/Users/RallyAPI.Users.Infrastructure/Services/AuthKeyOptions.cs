// ============================================================================
// FILE: Users.Infrastructure/Services/AuthKeyOptions.cs
// PURPOSE: Configuration binding for AuthKey.io SMS API.
//          Secondary OTP delivery channel — fallback for MSG91 WhatsApp
//          while Meta Business Verification is pending.
//
// API REFERENCE: https://authkey.io/2fa-api-docs
//   GET https://api.authkey.io/request
//     ?authkey=...&mobile=...&country_code=91
//     &sender=...&pe_id=...&template_id=...
//     &sms=<encoded message>
// ============================================================================

namespace RallyAPI.Users.Infrastructure.Services;

public class AuthKeyOptions
{
    public const string SectionName = "AuthKey";

    /// <summary>
    /// AuthKey.io API key (Dashboard → Settings → API Key)
    /// </summary>
    public string AuthKey { get; set; } = string.Empty;

    /// <summary>
    /// DLT-registered Sender ID (e.g., "RALLYO"). Required for DLT mode in India.
    /// </summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// DLT Principal Entity ID. Required for DLT mode in India.
    /// </summary>
    public string PeId { get; set; } = string.Empty;

    /// <summary>
    /// DLT-registered template ID. Required for DLT mode.
    /// When empty, the service falls back to raw-SMS mode (useful before DLT approval).
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// AuthKey-provisioned SID for the 2FA test endpoint — bypasses DLT.
    /// Used ONLY for development/testing before your DLT entity is approved.
    /// When set, takes precedence over TemplateId and raw-SMS modes.
    /// The pre-provisioned template at AuthKey expects an {#otp#} variable.
    /// Remove this once your production DLT TemplateId is configured.
    /// </summary>
    public string Sid { get; set; } = string.Empty;

    /// <summary>
    /// Country code without "+" prefix. Default "91" for India.
    /// </summary>
    public string CountryCode { get; set; } = "91";

    /// <summary>
    /// OTP expiry in minutes — used to format the message body.
    /// Must match what OtpService uses for Redis TTL.
    /// </summary>
    public int OtpExpiryMinutes { get; set; } = 5;

    /// <summary>
    /// AuthKey.io GET endpoint for transactional SMS / OTP.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.authkey.io/request";
}
