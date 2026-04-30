using System.Text.Json;
using StackExchange.Redis;

namespace RallyAPI.SharedKernel.Infrastructure;

public sealed class IdempotencyCacheModel
{
    public string Status { get; set; } = string.Empty; // "in-flight", "completed"
    public string PayloadHash { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();
    public string ResponseBody { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RedisIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisIdempotencyService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<IdempotencyCacheModel?> GetCachedResponseAsync(string key)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<IdempotencyCacheModel>(value.ToString()!);
    }

    public async Task<bool> AcquireLockAsync(string key, string payloadHash, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        
        var model = new IdempotencyCacheModel
        {
            Status = "in-flight",
            PayloadHash = payloadHash,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(model);
        
        // SET NX ensures we only write if it doesn't already exist.
        return await db.StringSetAsync(key, json, ttl, When.NotExists);
    }

    public async Task CacheResponseAsync(string key, string payloadHash, int statusCode, Dictionary<string, string> headers, string body, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        
        var model = new IdempotencyCacheModel
        {
            Status = "completed",
            PayloadHash = payloadHash,
            StatusCode = statusCode,
            ResponseHeaders = headers,
            ResponseBody = body,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(model);
        
        // Overwrite the in-flight lock with the completed payload
        await db.StringSetAsync(key, json, ttl);
    }
}
