// File: src/RallyAPI.SharedKernel/Abstractions/Restaurants/RestaurantListFilter.cs

namespace RallyAPI.SharedKernel.Abstractions.Restaurants;

/// <summary>
/// Filter / sort / page parameters for the customer-facing restaurant browse list.
/// </summary>
public sealed record RestaurantListFilter
{
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? RadiusKm { get; init; }

    /// <summary>Free-text keyword matched against restaurant name and cuisines (case-insensitive).</summary>
    public string? Search { get; init; }

    /// <summary>Restaurant matches if any of its cuisines overlap this list (case-insensitive).</summary>
    public IReadOnlyList<string>? Cuisines { get; init; }

    public bool? PureVeg { get; init; }
    public bool? VeganFriendly { get; init; }
    public bool? JainOptions { get; init; }
    public bool? OpenNow { get; init; }

    /// <summary>Only include restaurants whose AvgPrepTimeMins ≤ this value.</summary>
    public int? MaxPrepTimeMins { get; init; }

    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }

    /// <summary>True = only pickup-enabled. False = only delivery-only. Null = both.</summary>
    public bool? SupportsPickup { get; init; }

    /// <summary>One of: distance, cost_asc, cost_desc, prep_time, newest, relevance.</summary>
    public string? Sort { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record PagedRestaurantList(
    IReadOnlyList<RestaurantSummary> Items,
    int TotalCount,
    int Page,
    int PageSize);
