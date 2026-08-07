namespace RallyAPI.Marketing.Infrastructure.Services;

public sealed class DiscordNotificationOptions
{
    public const string SectionName = "Discord";

    /// <summary>
    /// Discord incoming-webhook URL for the onboarding-alerts channel.
    /// Set via the Discord__OnboardingWebhookUrl environment variable — never commit a real URL.
    /// Empty/unset means notifications are silently skipped (never blocks a submission).
    /// </summary>
    public string OnboardingWebhookUrl { get; set; } = string.Empty;
}
