using System.Text.Json;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Abstractions.Caching;
using StackExchange.Redis;

namespace RallyAPI.SharedKernel.Infrastructure;

/// <summary>
/// Redis-backed <see cref="ICacheService"/>. Every operation fails open: if Redis
/// is down or slow, reads behave like a miss (caller falls through to the DB) and
/// writes/evictions are dropped with a warning. TTLs are short (seconds), so a
/// dropped eviction self-heals on expiry.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var value = await _redis.GetDatabase().StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for {CacheKey}; treating as miss", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _redis.GetDatabase().StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for {CacheKey}; value not cached", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            // Safe to drop: entries carry short TTLs and expire on their own.
            _logger.LogWarning(ex, "Cache EVICT failed for {CacheKey}", key);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<Task<T>> factory,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        // No stampede lock: keys here expire on short TTLs, so at worst a handful
        // of concurrent requests rebuild the same value for one beat.
        var value = await factory();

        if (value is not null)
            await SetAsync(key, value, ttl, ct);

        return value;
    }
}
