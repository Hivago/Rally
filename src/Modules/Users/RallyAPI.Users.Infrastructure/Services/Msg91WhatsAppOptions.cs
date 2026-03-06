// ============================================================================
// FILE: Users.Infrastructure/Services/Msg91WhatsAppOptions.cs
// PURPOSE: Configuration binding for MSG91 WhatsApp Template API
// REPLACES: ExotelOptions.cs
// ============================================================================

namespace RallyAPI.Users.Infrastructure.Services;

public class Msg91WhatsAppOptions
{
    public const string SectionName = "Msg91WhatsApp";

    /// <summary>
    /// MSG91 authentication key (Dashboard → API → Authkey)
    /// </summary>
    public string AuthKey { get; set; } = string.Empty;

    /// <summary>
    /// Your integrated WhatsApp Business number (e.g., "919920920600")
    /// </summary>
    public string IntegratedNumber { get; set; } = string.Empty;

    /// <summary>
    /// Approved WhatsApp template name (e.g., "rally_login_verify")
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Template namespace — found in MSG91 dashboard under template details.
    /// This is your WhatsApp Business Account (WABA) namespace.
    /// </summary>
    public string TemplateNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Template language code (e.g., "en" for English)
    /// </summary>
    public string TemplateLanguage { get; set; } = "en";

    /// <summary>
    /// OTP expiry in minutes — passed as {{2}} variable in the template.
    /// Must match what OtpService uses for Redis TTL.
    /// </summary>
    public int OtpExpiryMinutes { get; set; } = 5;

    /// <summary>
    /// MSG91 WhatsApp API base URL
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.msg91.com/api/v5/whatsapp/whatsapp-outbound-message/bulk/";
}