// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/SearchMenuItems/SearchMenuItemsQuery.cs

using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.SearchMenuItems;

public sealed record SearchMenuItemsQuery(
    string Query,
    int MaxResults = 20
) : IRequest<Result<List<SearchMenuItemResponse>>>;

public sealed record SearchMenuItemResponse(
    Guid ItemId,
    string ItemName,
    string? Description,
    decimal BasePrice,
    string? ImageUrl,
    bool IsVegetarian,
    int PreparationTimeMinutes,
    List<string> Tags,
    Guid RestaurantId,
    string RestaurantName,
    bool IsAcceptingOrders);