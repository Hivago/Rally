// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/SearchMenuItems/SearchMenuItemsQueryHandler.cs

using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.SearchMenuItems;

internal sealed class SearchMenuItemsQueryHandler
    : IRequestHandler<SearchMenuItemsQuery, Result<List<SearchMenuItemResponse>>>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IRestaurantQueryService _restaurantQueryService;

    public SearchMenuItemsQueryHandler(
        IMenuItemRepository menuItemRepository,
        IRestaurantQueryService restaurantQueryService)
    {
        _menuItemRepository = menuItemRepository;
        _restaurantQueryService = restaurantQueryService;
    }

    public async Task<Result<List<SearchMenuItemResponse>>> Handle(
        SearchMenuItemsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length < 2)
            return Result.Failure<List<SearchMenuItemResponse>>(
                Error.Validation("Search.TooShort", "Search query must be at least 2 characters"));


        // Search menu items by name or description (case-insensitive)
        var items = await _menuItemRepository.SearchAsync(
            request.Query, request.MaxResults, cancellationToken);

        if (items.Count == 0)
            return new List<SearchMenuItemResponse>();

        // Get unique restaurant IDs from results
        var restaurantIds = items.Select(i => i.RestaurantId).Distinct().ToList();

        // Fetch restaurant details for all matching restaurants
        var restaurants = await _restaurantQueryService.GetActiveRestaurantsAsync(ct: cancellationToken);
        var restaurantMap = restaurants
            .Where(r => restaurantIds.Contains(r.Id))
            .ToDictionary(r => r.Id);

        var response = items
            .Where(i => restaurantMap.ContainsKey(i.RestaurantId)) // Only items from active restaurants
            .Select(i =>
            {
                var restaurant = restaurantMap[i.RestaurantId];
                return new SearchMenuItemResponse(
                    i.Id,
                    i.Name,
                    i.Description,
                    i.BasePrice,
                    i.ImageUrl,
                    i.IsVegetarian,
                    i.PreparationTimeMinutes,
                    i.Tags,
                    restaurant.Id,
                    restaurant.Name,
                    restaurant.IsAcceptingOrders);
            })
            .ToList();

        return response;
    }
}