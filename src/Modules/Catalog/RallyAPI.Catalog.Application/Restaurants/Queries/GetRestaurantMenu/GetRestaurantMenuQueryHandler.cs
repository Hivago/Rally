// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/GetRestaurantMenu/GetRestaurantMenuQueryHandler.cs

using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Abstractions.Caching;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Results;
using System.Linq;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurantMenu;

internal sealed class GetRestaurantMenuQueryHandler
    : IRequestHandler<GetRestaurantMenuQuery, Result<RestaurantMenuResponse>>
{
    // Menu edits evict explicitly (MenuCacheInvalidationBehavior), so the TTL only
    // bounds staleness for restaurant-level fields baked into the response
    // (IsAcceptingOrders — also evicted by SetAvailability — and prep time).
    private static readonly TimeSpan MenuTtl = TimeSpan.FromSeconds(60);

    private readonly IRestaurantQueryService _restaurantQueryService;
    private readonly IMenuRepository _menuRepository;
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly ICacheService _cache;

    public GetRestaurantMenuQueryHandler(
        IRestaurantQueryService restaurantQueryService,
        IMenuRepository menuRepository,
        IMenuItemRepository menuItemRepository,
        ICacheService cache)
    {
        _restaurantQueryService = restaurantQueryService;
        _menuRepository = menuRepository;
        _menuItemRepository = menuItemRepository;
        _cache = cache;
    }

    public async Task<Result<RestaurantMenuResponse>> Handle(
        GetRestaurantMenuQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = CatalogCacheKeys.Menu(request.RestaurantId);

        var cached = await _cache.GetAsync<RestaurantMenuResponse>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // Get restaurant details from Users module
        var restaurant = await _restaurantQueryService.GetByIdAsync(
            request.RestaurantId, cancellationToken);

        if (restaurant is null)
            return Result.Failure<RestaurantMenuResponse>(
                Error.NotFound("Restaurant.NotFound", request.RestaurantId));

        // Get menus from Catalog module
        var menus = await _menuRepository.GetByRestaurantIdAsync(
            request.RestaurantId, cancellationToken);

        // Get all items for this restaurant (includes options via Include)
        var allItems = await _menuItemRepository.GetByRestaurantIdAsync(
            request.RestaurantId, cancellationToken);

        // Group items by menu. Unavailable items are still returned so clients
        // can render them as "out of stock" (Swiggy/Zomato style) instead of
        // hiding them, and so the restaurant dashboard sees its own full list.
        var itemsByMenu = allItems
            .GroupBy(i => i.MenuId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var menuResponses = menus
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new MenuWithItemsResponse(
                m.Id,
                m.Name,
                m.Description,
                m.DisplayOrder,
                itemsByMenu.TryGetValue(m.Id, out var items)
                    ? items.OrderBy(i => i.DisplayOrder).Select(i => new MenuItemResponse(
                        i.Id,
                        i.Name,
                        i.Description,
                        i.BasePrice,
                        i.ImageUrl,
                        i.IsAvailable,
                        i.IsVegetarian,
                        i.PreparationTimeMinutes,
                        i.Options.Where(o => o.OptionGroupId == null).Select(o => new MenuItemOptionResponse(
                            o.Id,
                            o.Name,
                            o.Type.ToString(),
                            o.AdditionalPrice,
                            o.IsDefault
                        )).ToList(),
                        i.OptionGroups.OrderBy(g => g.DisplayOrder).Select(g => new OptionGroupResponse(
                            g.Id,
                            g.GroupName,
                            g.IsRequired,
                            g.MinSelections,
                            g.MaxSelections,
                            g.DisplayOrder,
                            g.Options.Select(o => new MenuItemOptionResponse(
                                o.Id,
                                o.Name,
                                o.Type.ToString(),
                                o.AdditionalPrice,
                                o.IsDefault
                            )).ToList()
                        )).ToList(),
                        i.Tags
                    )).ToList()
                    : new List<MenuItemResponse>()
            )).ToList();

        var response = new RestaurantMenuResponse(
            restaurant.Id,
            restaurant.Name,
            restaurant.IsAcceptingOrders,
            restaurant.AcceptsPickup,
            restaurant.AvgPrepTimeMins,
            menuResponses);

        // Only successful responses are cached — a NotFound must never stick.
        await _cache.SetAsync(cacheKey, response, MenuTtl, cancellationToken);

        return response;
    }
}