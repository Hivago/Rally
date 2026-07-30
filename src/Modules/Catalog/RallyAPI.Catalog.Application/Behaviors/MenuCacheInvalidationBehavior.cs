using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Abstractions.Caching;

namespace RallyAPI.Catalog.Application.Behaviors;

/// <summary>
/// Evicts the cached menu response after any command marked
/// <see cref="IMenuCacheInvalidatingCommand"/> completes. Eviction is
/// unconditional — evicting on a failed command is harmless (next read rebuilds),
/// and checking success across the Result/Result&lt;T&gt; shapes isn't worth the
/// complexity.
/// </summary>
internal sealed class MenuCacheInvalidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMenuCacheInvalidatingCommand
{
    private readonly ICacheService _cache;

    public MenuCacheInvalidationBehavior(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        await _cache.RemoveAsync(CatalogCacheKeys.Menu(request.RestaurantId), cancellationToken);

        return response;
    }
}
