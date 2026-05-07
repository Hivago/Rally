namespace RallyAPI.Catalog.Application.Abstractions;

/// <summary>
/// OCRs a menu image (photo or PDF page) and returns the raw text plus a best-effort
/// list of suggested items (name + price) the ops team can paste into the Excel template.
/// </summary>
public interface IMenuOcrService
{
    Task<MenuOcrResult> ExtractAsync(Stream image, CancellationToken ct = default);
}

public sealed record MenuOcrResult(
    string RawText,
    IReadOnlyList<MenuOcrSuggestedItem> SuggestedItems);

public sealed record MenuOcrSuggestedItem(
    string Name,
    decimal? BasePrice,
    string? Description);
