namespace RallyAPI.SharedKernel.Abstractions.Reviews;

/// <summary>
/// Read-only proxy to Google's Place Details reviews. Google returns at most 5
/// reviews per place, chosen by their own relevance ranking — not paginated,
/// not necessarily the newest. Callers must treat a null result as
/// "unavailable" (API disabled, place not found, or upstream error), not a
/// hard failure.
/// </summary>
public interface IGoogleReviewsService
{
    Task<PlaceReviewsResult?> GetPlaceReviewsAsync(string placeId, CancellationToken ct = default);
}

public sealed record PlaceReviewsResult(
    double? Rating,
    int? UserRatingCount,
    IReadOnlyList<ReviewSnippet> Reviews);

public sealed record ReviewSnippet(
    string AuthorName,
    string? AuthorPhotoUrl,
    int Rating,
    string Text,
    string RelativeTimeDescription,
    long PublishedAtUnixSeconds);
