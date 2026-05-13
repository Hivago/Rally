// File: src/RallyAPI.SharedKernel/Abstractions/Restaurants/RestaurantSummary.cs

namespace RallyAPI.SharedKernel.Abstractions.Restaurants;

/// <summary>
/// Lightweight restaurant info for list/browse views.
/// </summary>
public sealed record RestaurantSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string AddressLine { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required bool IsAcceptingOrders { get; init; }
    public required int AvgPrepTimeMins { get; init; }
    public required TimeOnly OpeningTime { get; init; }
    public required TimeOnly ClosingTime { get; init; }
    public required List<string> CuisineTypes { get; init; }
    public required bool IsPureVeg { get; init; }
    public required bool IsVeganFriendly { get; init; }
    public required bool HasJainOptions { get; init; }
    public required decimal MinOrderAmount { get; init; }
    public string? LogoUrl { get; init; }

    /// <summary>
    /// True when the restaurant accepts customer pickup orders. Default false.
    /// Customer UI uses this to hide/disable the Pickup tab per restaurant.
    /// </summary>
    public bool AcceptsPickup { get; init; }

    /// <summary>
    /// Distance from the queried location in km. Null if no location was provided.
    /// </summary>
    public double? DistanceKm { get; init; }
}
