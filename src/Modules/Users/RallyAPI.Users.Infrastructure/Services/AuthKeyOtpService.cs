// ============================================================================
// FILE: Users.Infrastructure/Services/AuthKeyOtpService.cs
// PURPOSE: Sends OTP SMS via AuthKey.io transactional SMS API.
//          Implements ISmsService — secondary provider alongside MSG91 WhatsApp.
//
//          Two modes:
//            1. DLT template mode  — when TemplateId is configured.
//                                    Sends OTP as the {#otp#} variable.
//            2. Raw SMS mode       — when TemplateId is empty.
//                                    Sends the full message text as `sms=`.
//                                    Useful before DLT approval clears.
//
// API: GET https://api.authkey.io/request
//        ?authkey=...&mobile=9876543210&country_code=91
//        &sender=RALLYO&pe_id=...&template_id=...&otp=123456
// ============================================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Infrastructure.Services;

public class AuthKeyOtpService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly AuthKeyOptions _options;
    private readonly ILogger<AuthKeyOtpService> _logger;

    public AuthKeyOtpService(
        HttpClient httpClient,
        IOptions<AuthKeyOptions> options,
        ILogger<AuthKeyOtpService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var maskedPhone = MaskPhone(phoneNumber);

        try
        {
            var otp = ExtractOtp(message);
            if (string.IsNullOrEmpty(otp))
            {
                _logger.LogError("Could not extract OTP from message for {Phone}", maskedPhone);
                return false;
            }

            var mobile = NormalizeMobile(phoneNumber);
            var mode = ResolveMode();
            var url = BuildRequestUrl(mobile, otp, message, mode);

            _logger.LogInformation(
                "Sending OTP via AuthKey.io to {Phone} (mode: {Mode})", maskedPhone, mode);

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "AuthKey.io send failed for {Phone}. Status: {Status}, Response: {Body}",
                    maskedPhone, (int)response.StatusCode, body);
                return false;
            }

            if (IsSuccessResponse(body))
            {
                _logger.LogInformation("AuthKey.io OTP sent to {Phone}", maskedPhone);
                return true;
            }

            var reason = ExtractFailureMessage(body);
            _logger.LogError(
                "AuthKey.io send failed for {Phone}. Reason: {Reason}. Raw: {Body}",
                maskedPhone, reason, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending OTP via AuthKey.io to {Phone}", maskedPhone);
            return false;
        }
    }

    /// <summary>
    /// Priority: pre-provisioned SID (test/dev) → DLT template (prod) → raw SMS (fallback).
    /// </summary>
    private string ResolveMode()
    {
        if (!string.IsNullOrWhiteSpace(_options.Sid))
            return "sid-test";
        if (!string.IsNullOrWhiteSpace(_options.TemplateId))
            return "dlt-template";
        return "raw-sms";
    }

    private string BuildRequestUrl(string mobile, string otp, string fullMessage, string mode)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["authkey"] = _options.AuthKey;
        query["mobile"] = mobile;
        query["country_code"] = _options.CountryCode;

        switch (mode)
        {
            case "sid-test":
                // AuthKey-provisioned test SID — bypasses DLT. No sender/pe_id needed.
                query["sid"] = _options.Sid;
                query["otp"] = otp;
                break;

            case "dlt-template":
                if (!string.IsNullOrWhiteSpace(_options.Sender))
                    query["sender"] = _options.Sender;
                query["template_id"] = _options.TemplateId;
                if (!string.IsNullOrWhiteSpace(_options.PeId))
                    query["pe_id"] = _options.PeId;
                query["otp"] = otp;
                break;

            default: // raw-sms
                if (!string.IsNullOrWhiteSpace(_options.Sender))
                    query["sender"] = _options.Sender;
                query["sms"] = fullMessage;
                break;
        }

        return $"{_options.BaseUrl}?{query}";
    }

    /// <summary>
    /// AuthKey returns one of two shapes:
    ///   Success (legacy):  {"LogID":"...","Message":"Submitted Successfully"}
    ///   Error (new):       {"success":{"sms":false},"message":{"sms":"Entity ID Not Found"}}
    /// We must NOT pattern-match the word "success" in the raw body — the error
    /// shape literally contains that key name and would false-positive.
    /// </summary>
    private static bool IsSuccessResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // New shape: { "success": { "sms": true/false }, ... }
            if (root.TryGetProperty("success", out var successElement))
            {
                if (successElement.ValueKind == JsonValueKind.Object
                    && successElement.TryGetProperty("sms", out var smsFlag)
                    && smsFlag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    return smsFlag.GetBoolean();
                }

                if (successElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return successElement.GetBoolean();
            }

            // Legacy shape: { "LogID":"...", "Message":"Submitted Successfully" }
            if (root.TryGetProperty("Message", out var msg)
                && msg.ValueKind == JsonValueKind.String)
            {
                var text = msg.GetString() ?? string.Empty;
                return text.Contains("Submitted", StringComparison.OrdinalIgnoreCase);
            }

            // Anything else — treat as failure so we don't log a false positive.
            return false;
        }
        catch (JsonException)
        {
            // Non-JSON body — only accept if it literally says "Submitted Successfully".
            return body.Contains("Submitted Successfully", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string? ExtractOtp(string message)
    {
        var match = Regex.Match(message, @"\b(\d{4,8})\b");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Pulls a human-readable failure reason out of AuthKey's response shape:
    ///   { "message": { "sms": "Entity ID Not Found" } }  →  "Entity ID Not Found"
    ///   { "Message": "Invalid Authkey" }                 →  "Invalid Authkey"
    /// </summary>
    private static string ExtractFailureMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(empty response)";

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var lower))
            {
                if (lower.ValueKind == JsonValueKind.String)
                    return lower.GetString() ?? "(unknown)";

                if (lower.ValueKind == JsonValueKind.Object
                    && lower.TryGetProperty("sms", out var smsMsg)
                    && smsMsg.ValueKind == JsonValueKind.String)
                {
                    return smsMsg.GetString() ?? "(unknown)";
                }
            }

            if (root.TryGetProperty("Message", out var upper)
                && upper.ValueKind == JsonValueKind.String)
            {
                return upper.GetString() ?? "(unknown)";
            }
        }
        catch (JsonException)
        {
            // ignore — return raw
        }

        return body.Length > 200 ? body[..200] + "…" : body;
    }

    /// <summary>
    /// AuthKey expects the raw 10-digit mobile (country_code is a separate param).
    /// Strip +, country code prefix, spaces, dashes.
    /// </summary>
    private string NormalizeMobile(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.StartsWith(_options.CountryCode) && digits.Length > 10)
            digits = digits[_options.CountryCode.Length..];

        return digits;
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 4)
            return "****";
        return "****" + phone[^4..];
    }
}
