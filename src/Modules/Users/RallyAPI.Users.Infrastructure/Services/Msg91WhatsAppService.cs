// ============================================================================
// FILE: Users.Infrastructure/Services/Msg91WhatsAppService.cs
// PURPOSE: Sends messages via MSG91 WhatsApp Template API.
//          Implements ISmsService — drop-in replacement for ExotelSmsService.
//
//          Rally's OtpService generates the OTP, hashes it, stores in Redis,
//          then calls ISmsService.SendAsync() to deliver it.
//          This class handles that delivery via WhatsApp.
//
// API USED:
//   POST https://api.msg91.com/api/v5/whatsapp/whatsapp-outbound-message/bulk/
//
// TEMPLATE ASSUMED:
//   Name: rally_login_verify
//   Body: "Hi! Your Rally login number is {{1}}. Valid for {{2}} minutes.
//          Please do not share this with anyone."
//   {{1}} = OTP value
//   {{2}} = Expiry minutes
// ============================================================================

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Infrastructure.Services;

public class Msg91WhatsAppService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly Msg91WhatsAppOptions _options;
    private readonly ILogger<Msg91WhatsAppService> _logger;

    public Msg91WhatsAppService(
        HttpClient httpClient,
        IOptions<Msg91WhatsAppOptions> options,
        ILogger<Msg91WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends a WhatsApp message using the approved template.
    /// The message parameter contains the OTP (extracted via regex).
    /// </summary>
    public async Task<bool> SendAsync(string phone, string message, CancellationToken cancellationToken = default)
    {
        var maskedPhone = MaskPhone(phone);

        try
        {
            // Extract OTP from the message string.
            // OtpService sends message like: "847293 is your OTP for Rally. Valid for 5 minutes."
            // We need to extract just the OTP digits to pass as {{1}} variable.
            var otp = ExtractOtp(message);
            if (string.IsNullOrEmpty(otp))
            {
                _logger.LogError("Could not extract OTP from message for {Phone}", maskedPhone);
                return false;
            }

            var normalizedPhone = NormalizePhone(phone);

            // Build MSG91 WhatsApp Template API payload
            var payload = new
            {
                integrated_number = _options.IntegratedNumber,
                content_type = "template",
                payload = new
                {
                    messaging_product = "whatsapp",
                    type = "template",
                    template = new
                    {
                        name = _options.TemplateName,
                        language = new
                        {
                            code = _options.TemplateLanguage,
                            policy = "deterministic"
                        },
                        @namespace = _options.TemplateNamespace,
                        to_and_components = new[]
                        {
                            new
                            {
                                to = new[] { normalizedPhone },
                                components = new
                                {
                                    // {{1}} = OTP value
                                    body_1 = new { type = "text", value = otp },
                                    // {{2}} = Expiry minutes
                                    body_2 = new { type = "text", value = _options.OtpExpiryMinutes.ToString() }
                                }
                            }
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // Need to preserve exact property names like "integrated_number"
                // so we use a custom approach
            });

            // Re-serialize with exact property names (snake_case as MSG91 expects)
            var requestBody = BuildRequestBody(normalizedPhone, otp);

            _logger.LogInformation("Sending WhatsApp OTP to {Phone} via MSG91", maskedPhone);

            var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("authkey", _options.AuthKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp OTP sent successfully to {Phone}", maskedPhone);
                return true;
            }

            _logger.LogError(
                "MSG91 WhatsApp send failed for {Phone}. Status: {StatusCode}, Response: {Response}",
                maskedPhone, (int)response.StatusCode, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending WhatsApp OTP to {Phone}", maskedPhone);
            return false;
        }
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    /// <summary>
    /// Builds the exact JSON request body MSG91 expects.
    /// Using manual JSON construction to ensure exact property names (snake_case).
    /// </summary>
    private string BuildRequestBody(string phone, string otp)
    {
        return $$"""
        {
            "integrated_number": "{{_options.IntegratedNumber}}",
            "content_type": "template",
            "payload": {
                "messaging_product": "whatsapp",
                "type": "template",
                "template": {
                    "name": "{{_options.TemplateName}}",
                    "language": {
                        "code": "{{_options.TemplateLanguage}}",
                        "policy": "deterministic"
                    },
                    "namespace": "{{_options.TemplateNamespace}}",
                    "to_and_components": [
                        {
                            "to": ["{{phone}}"],
                            "components": {
                                "body_1": {
                                    "type": "text",
                                    "value": "{{otp}}"
                                },
                                "body_2": {
                                    "type": "text",
                                    "value": "{{_options.OtpExpiryMinutes}}"
                                }
                            }
                        }
                    ]
                }
            }
        }
        """;
    }

    /// <summary>
    /// Extracts the OTP (numeric digits) from the message string.
    /// OtpService formats: "{otp} is your OTP for Rally. Valid for 5 minutes."
    /// We extract the first sequence of 4-8 digits.
    /// </summary>
    private static string? ExtractOtp(string message)
    {
        var match = Regex.Match(message, @"\b(\d{4,8})\b");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Normalizes phone to MSG91 format: 91XXXXXXXXXX (no + prefix).
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

        if (cleaned.StartsWith('+'))
            cleaned = cleaned[1..];

        if (cleaned.Length == 10 && !cleaned.StartsWith("91"))
            cleaned = "91" + cleaned;

        return cleaned;
    }

    /// <summary>
    /// Masks phone number for logging — only shows last 4 digits.
    /// </summary>
    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 4)
            return "****";
        return "****" + phone[^4..];
    }
}