namespace RallyAPI.Orders.Infrastructure.Services;

public sealed class OpsAlertDiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>
    /// Discord incoming-webhook URL for the critical ops-alerts channel (order escalations, etc).
    /// Set via the Discord__OpsAlertsWebhookUrl environment variable — never commit a real URL.
    /// Empty/unset means notifications are silently skipped (never blocks order processing).
    /// </summary>
    public string OpsAlertsWebhookUrl { get; set; } = string.Empty;
}
