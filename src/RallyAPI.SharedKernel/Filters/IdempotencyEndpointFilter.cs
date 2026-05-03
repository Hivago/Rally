using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RallyAPI.SharedKernel.Constants;
using RallyAPI.SharedKernel.Infrastructure;

namespace RallyAPI.SharedKernel.Filters;

public sealed class IdempotencyEndpointFilter : IEndpointFilter
{
    private readonly RedisIdempotencyService _idempotencyService;

    public IdempotencyEndpointFilter(RedisIdempotencyService idempotencyService)
    {
        _idempotencyService = idempotencyService;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        // 1. Read header
        if (!request.Headers.TryGetValue(HttpHeaders.IdempotencyKey, out var idempotencyKeyValues))
        {
             context.HttpContext.Response.StatusCode = 400;
             return Microsoft.AspNetCore.Http.Results.Problem("Idempotency-Key header is required.", statusCode: 400);
        }

        var idempotencyKey = idempotencyKeyValues.ToString();
        var userId = context.HttpContext.User.FindFirst("sub")?.Value ?? "anonymous"; // or extract via claims
        var redisKey = $"idempotency:{userId}:{idempotencyKey}";

        // 2. Compute Payload Hash
        request.EnableBuffering();
        using var memoryStream = new MemoryStream();
        if (request.Body.CanSeek) { request.Body.Seek(0, SeekOrigin.Begin); }
        await request.Body.CopyToAsync(memoryStream);
        if (request.Body.CanSeek) { request.Body.Seek(0, SeekOrigin.Begin); } // Reset for next middleware
        
        var rawBody = Encoding.UTF8.GetString(memoryStream.ToArray());
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawBody + userId));
        var payloadHash = "sha256:" + Convert.ToHexString(hashBytes).ToLowerInvariant();

        // 3. Check Redis cache
        var cached = await _idempotencyService.GetCachedResponseAsync(redisKey);
        if (cached != null)
        {
            if (cached.Status == "in-flight")
            {
                // 425 Too Early
                context.HttpContext.Response.StatusCode = 425;
                context.HttpContext.Response.Headers["Retry-After"] = "2";
                return Microsoft.AspNetCore.Http.Results.Problem("A request with this Idempotency-Key is currently processing.", statusCode: 425);
            }

            // Must be completed
            if (cached.PayloadHash != payloadHash)
            {
                context.HttpContext.Response.StatusCode = 409;
                return Microsoft.AspNetCore.Http.Results.Problem("Payload mismatch for the given Idempotency-Key.", statusCode: 409);
            }

            // Return cached response via dynamic formulation
            context.HttpContext.Response.StatusCode = cached.StatusCode;
            foreach (var header in cached.ResponseHeaders)
            {
                context.HttpContext.Response.Headers[header.Key] = header.Value;
            }
            context.HttpContext.Response.Headers[HttpHeaders.IdempotentReplay] = "true";
            
            // Bypass execution and write raw body back
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(cached.ResponseBody);
            return Microsoft.AspNetCore.Http.Results.Empty; 
        }

        // 4. Not cached. Attempt to acquire lock.
        var lockAcquired = await _idempotencyService.AcquireLockAsync(redisKey, payloadHash, TimeSpan.FromHours(24));
        if (!lockAcquired)
        {
            // Another thread just acquired it? It's in flight. 
            context.HttpContext.Response.StatusCode = 425;
            context.HttpContext.Response.Headers["Retry-After"] = "2";
            return Microsoft.AspNetCore.Http.Results.Problem("A request with this Idempotency-Key is currently processing.", statusCode: 425);
        }

        // 5. Intercept the response to store it in cache.
        var originalBodyStream = context.HttpContext.Response.Body;
        using var responseBody = new MemoryStream();
        context.HttpContext.Response.Body = responseBody;

        try
        {
            // Execute the endpoint
            var result = await next(context);

            // We must force the result to be processed so headers/status are populated before caching.
            // When intercepting streams, we write the result if it's an IResult
            if (result is IResult iResult)
            {
                await iResult.ExecuteAsync(context.HttpContext);
                result = Microsoft.AspNetCore.Http.Results.Empty; // Prevent returning the result again from Filter
            }

            // Now read the response stream
            context.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBodyText = await new StreamReader(context.HttpContext.Response.Body).ReadToEndAsync();
            context.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);

            // Cache it
            var headersToCache = new Dictionary<string, string>();
            foreach (var header in context.HttpContext.Response.Headers)
            {
                headersToCache[header.Key] = header.Value.ToString();
            }

            await _idempotencyService.CacheResponseAsync(
                redisKey, 
                payloadHash, 
                context.HttpContext.Response.StatusCode, 
                headersToCache, 
                responseBodyText, 
                TimeSpan.FromHours(24)
            );

            // Copy back to original stream
            await responseBody.CopyToAsync(originalBodyStream);

            return result;
        }
        catch
        {
            await _idempotencyService.ReleaseLockAsync(redisKey);
            throw;
        }
        finally
        {
            context.HttpContext.Response.Body = originalBodyStream;
        }
    }
}
