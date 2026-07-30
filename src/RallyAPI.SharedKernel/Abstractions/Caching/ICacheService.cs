namespace RallyAPI.SharedKernel.Abstractions.Caching;

/// <summary>
/// Distributed read-through cache for hot, mostly-static read paths (restaurant
/// browse, menus). Implementations MUST fail open: a cache outage degrades to
/// hitting the database, never to failing the request.
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value, or default when absent or on cache failure.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns the cached value if present; otherwise runs <paramref name="factory"/>,
    /// caches the result for <paramref name="ttl"/>, and returns it.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<Task<T>> factory,
        CancellationToken ct = default);
}
