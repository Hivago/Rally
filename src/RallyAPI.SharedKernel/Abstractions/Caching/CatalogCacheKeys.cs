namespace RallyAPI.SharedKernel.Abstractions.Caching;

/// <summary>
/// Cache keys for the customer-facing catalog read paths. Lives in SharedKernel
/// because writes that must invalidate them span two modules: menu edits happen
/// in Catalog, restaurant profile/availability changes happen in Users.
/// </summary>
public static class CatalogCacheKeys
{
    /// <summary>
    /// The full list of active restaurant summaries (distance not populated —
    /// it is requester-specific and computed after the cache read).
    /// </summary>
    public const string ActiveRestaurants = "catalog:restaurants:active";

    /// <summary>The fully-built menu response for one restaurant.</summary>
    public static string Menu(Guid restaurantId) => $"catalog:menu:{restaurantId}";
}
