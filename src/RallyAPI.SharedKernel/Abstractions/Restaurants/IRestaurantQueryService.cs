// File: src/RallyAPI.SharedKernel/Abstractions/Restaurants/IRestaurantQueryService.cs
// Purpose: Cross-module abstraction for querying restaurant data.
//          Implemented by Users module, consumed by Catalog module.

namespace RallyAPI.SharedKernel.Abstractions.Restaurants;

public interface IRestaurantQueryService
{
    /// <summary>
    /// Gets active restaurants, optionally filtered by proximity to a location.
    /// Results are sorted by distance if lat/lng provided, otherwise by name.
    /// </summary>
    Task<IReadOnlyList<RestaurantSummary>> GetActiveRestaurantsAsync(
        double? latitude = null,
        double? longitude = null,
        double? radiusKm = null,
        CancellationToken ct = default);

    /// <summary>
    /// Customer browse endpoint with the full filter / sort / paginate set.
    /// Cheap filters are pushed into SQL; distance / cuisine-list / radius are applied in-memory.
    /// </summary>
    Task<PagedRestaurantList> BrowseAsync(
        RestaurantListFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a single restaurant's details by ID.
    /// </summary>
    Task<RestaurantDetails?> GetByIdAsync(
        Guid restaurantId,
        CancellationToken ct = default);

    /// <summary>
    /// For each given owner id, returns the owner's display name and outlet count.
    /// Used by the admin payouts page so the payout list can show a single label per
    /// owner regardless of how many restaurants they run.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, OwnerPayoutDisplay>> GetOwnerPayoutDisplaysAsync(
        IReadOnlyCollection<Guid> ownerIds,
        CancellationToken ct = default);
}

public sealed record OwnerPayoutDisplay(
    Guid OwnerId,
    string OwnerName,
    int OutletCount,
    string? FirstRestaurantName);

