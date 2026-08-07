using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Orders.Application.Abstractions;

namespace RallyAPI.Orders.Infrastructure.Services;

/// <summary>
/// Posts a Discord embed to the critical ops-alerts webhook for events that need a human's
/// attention (e.g. a restaurant not confirming an order in time). Best-effort only — a failure
/// here (missing config, Discord outage, network error) is logged and swallowed, never allowed
/// to affect order processing.
/// </summary>
public sealed class DiscordOpsAlertNotifier : IOpsAlertNotifier
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const int WarningColor = 0xE67E22;

    private readonly HttpClient _httpClient;
    private readonly OpsAlertDiscordOptions _options;
    private readonly ILogger<DiscordOpsAlertNotifier> _logger;

    public DiscordOpsAlertNotifier(
        HttpClient httpClient,
        IOptions<OpsAlertDiscordOptions> options,
        ILogger<DiscordOpsAlertNotifier> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyOrderEscalatedAsync(
        Guid orderId,
        string orderNumber,
        Guid restaurantId,
        string reason,
        DateTime escalatedAt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.OpsAlertsWebhookUrl))
        {
            _logger.LogDebug("Discord ops-alerts webhook not configured — skipping notification.");
            return;
        }

        var payload = new DiscordWebhookPayload(
            Embeds:
            [
                new DiscordEmbed(
                    Title: "Order escalated — restaurant not confirming",
                    Description: reason,
                    Color: WarningColor,
                    Fields:
                    [
                        new DiscordEmbedField("Order Number", orderNumber, true),
                        new DiscordEmbedField("Order ID", orderId.ToString(), true),
                        new DiscordEmbedField("Restaurant ID", restaurantId.ToString(), true),
                        new DiscordEmbedField("Escalated At (UTC)", escalatedAt.ToString("u"), true),
                    ])
            ]);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _options.OpsAlertsWebhookUrl, payload, SerializerOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Discord ops-alert notification failed with {StatusCode}: {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Discord ops-alert notification threw for order {OrderId}", orderId);
        }
    }

    private sealed record DiscordWebhookPayload(IReadOnlyList<DiscordEmbed> Embeds);

    private sealed record DiscordEmbed(string Title, string Description, int Color, IReadOnlyList<DiscordEmbedField> Fields);

    private sealed record DiscordEmbedField(string Name, string Value, bool Inline);
}
