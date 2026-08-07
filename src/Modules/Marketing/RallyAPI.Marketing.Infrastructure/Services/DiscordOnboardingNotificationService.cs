using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Infrastructure.Services;

/// <summary>
/// Posts a Discord embed to the onboarding-alerts webhook whenever a restaurant submits the
/// public onboarding form. Best-effort only — a failure here (missing config, Discord outage,
/// network error) is logged and swallowed, never surfaced to the applicant or the caller.
/// </summary>
public sealed class DiscordOnboardingNotificationService : IOnboardingNotificationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const int BrandColor = 0xD72B1F; // matches the onboarding page's --brand color

    private readonly HttpClient _httpClient;
    private readonly DiscordNotificationOptions _options;
    private readonly ILogger<DiscordOnboardingNotificationService> _logger;

    public DiscordOnboardingNotificationService(
        HttpClient httpClient,
        IOptions<DiscordNotificationOptions> options,
        ILogger<DiscordOnboardingNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyNewApplicationAsync(
        RestaurantOnboardingApplication application,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.OnboardingWebhookUrl))
        {
            _logger.LogDebug("Discord onboarding webhook not configured — skipping notification.");
            return;
        }

        var payload = new DiscordWebhookPayload(
            Embeds:
            [
                new DiscordEmbed(
                    Title: "New restaurant onboarding application",
                    Description: $"**{application.RestaurantName}** applied to join Hivago.",
                    Color: BrandColor,
                    Fields:
                    [
                        new DiscordEmbedField("Owner", application.OwnerName, true),
                        new DiscordEmbedField("City", application.City, true),
                        new DiscordEmbedField("Cuisine", application.CuisineType ?? "—", true),
                        new DiscordEmbedField("Phone", application.Phone, true),
                        new DiscordEmbedField("Email", application.Email, true),
                        new DiscordEmbedField("Application ID", application.Id.ToString(), false),
                    ])
            ]);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _options.OnboardingWebhookUrl, payload, SerializerOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Discord onboarding notification failed with {StatusCode}: {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Discord onboarding notification threw for application {ApplicationId}", application.Id);
        }
    }

    private sealed record DiscordWebhookPayload(IReadOnlyList<DiscordEmbed> Embeds);

    private sealed record DiscordEmbed(string Title, string Description, int Color, IReadOnlyList<DiscordEmbedField> Fields);

    private sealed record DiscordEmbedField(string Name, string Value, bool Inline);
}
