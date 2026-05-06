using System.Globalization;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Catalog.Application.Abstractions;

namespace RallyAPI.Catalog.Infrastructure.Services;

/// <summary>
/// Google Cloud Vision implementation. Uses DOCUMENT_TEXT_DETECTION which is tuned for dense
/// printed text (menus, receipts, scanned PDFs) and preserves line layout better than the
/// generic TEXT_DETECTION feature.
///
/// If credentials aren't configured the service throws InvalidOperationException with a
/// clear message — surfaced to the API caller as a 500 with code "Catalog.Ocr.NotConfigured".
/// The rest of the API still boots; only the OCR endpoint is affected.
/// </summary>
public sealed class GoogleVisionMenuOcrService : IMenuOcrService
{
    // Matches a price at the end of a line: "299", "299.00", "₹ 299", "Rs. 299", "INR 299".
    // Tolerant of the symbol being separated by a space and trailing whitespace.
    private static readonly Regex PriceRegex = new(
        @"(?:₹|rs\.?|inr)?\s*([0-9]{1,4}(?:\.[0-9]{1,2})?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly GoogleVisionOptions _options;
    private readonly ILogger<GoogleVisionMenuOcrService> _logger;
    private readonly Lazy<ImageAnnotatorClient> _client;

    public GoogleVisionMenuOcrService(
        IOptions<GoogleVisionOptions> options,
        ILogger<GoogleVisionMenuOcrService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<ImageAnnotatorClient>(BuildClient);
    }

    public async Task<MenuOcrResult> ExtractAsync(Stream image, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "Google Vision credentials are not configured. Set the GoogleVision:CredentialsJson configuration value.");
        }

        var client = _client.Value;
        var visionImage = await Image.FromStreamAsync(image);

        ct.ThrowIfCancellationRequested();
        var response = await client.DetectDocumentTextAsync(visionImage);
        if (response is null || string.IsNullOrWhiteSpace(response.Text))
        {
            return new MenuOcrResult(string.Empty, Array.Empty<MenuOcrSuggestedItem>());
        }

        var rawText = response.Text;
        var suggestions = ExtractSuggestions(rawText);
        return new MenuOcrResult(rawText, suggestions);
    }

    private ImageAnnotatorClient BuildClient()
    {
        try
        {
            var credential = GoogleCredential
                .FromJson(_options.CredentialsJson)
                .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

            return new ImageAnnotatorClientBuilder
            {
                Credential = credential
            }.Build();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to construct Google Vision client from inline credentials JSON.");
            throw;
        }
    }

    /// <summary>
    /// Best-effort line-by-line parse: pull a trailing price from each non-empty line; the rest
    /// of the line becomes the item name. Lines without a price are dropped (likely headers/notes).
    /// Ops still reviews the output before pasting into the Excel template, so we err on the
    /// side of keeping more candidates rather than fewer.
    /// </summary>
    private static IReadOnlyList<MenuOcrSuggestedItem> ExtractSuggestions(string rawText)
    {
        var suggestions = new List<MenuOcrSuggestedItem>();
        var lines = rawText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length < 3) continue;

            var match = PriceRegex.Match(line);
            if (!match.Success) continue;

            if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                continue;

            // Skip implausible prices (a phone-number digit blob caught accidentally).
            if (price < 10 || price > 9999) continue;

            var name = line[..match.Index].Trim().TrimEnd('.', '-', '·', '…').Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length < 2) continue;

            suggestions.Add(new MenuOcrSuggestedItem(
                Name: name,
                BasePrice: price,
                Description: null));
        }

        return suggestions;
    }
}
