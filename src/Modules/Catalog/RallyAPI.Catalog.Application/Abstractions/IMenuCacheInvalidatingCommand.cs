namespace RallyAPI.Catalog.Application.Abstractions;

/// <summary>
/// Marker for commands whose success makes the cached menu response for
/// <see cref="RestaurantId"/> stale. <c>MenuCacheInvalidationBehavior</c> evicts
/// the cache entry after the handler runs, so the restaurant dashboard sees its
/// own edits immediately instead of waiting out the TTL.
/// </summary>
public interface IMenuCacheInvalidatingCommand
{
    Guid RestaurantId { get; }
}
