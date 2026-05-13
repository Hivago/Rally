// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/GetRestaurantMenu/GetRestaurantMenuQuery.cs

using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurantMenu;

public sealed record GetRestaurantMenuQuery(Guid RestaurantId)
    : IRequest<Result<RestaurantMenuResponse>>;

public sealed record RestaurantMenuResponse(
    Guid RestaurantId,
    string RestaurantName,
    bool IsAcceptingOrders,
    bool AcceptsPickup,
    int AvgPrepTimeMins,
    List<MenuWithItemsResponse> Menus);

public sealed record MenuWithItemsResponse(
    Guid MenuId,
    string Name,
    string? Description,
    int DisplayOrder,
    List<MenuItemResponse> Items);

public sealed record MenuItemResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal BasePrice,
    string? ImageUrl,
    bool IsAvailable,
    bool IsVegetarian,
    int PreparationTimeMinutes,
    List<MenuItemOptionResponse> Options,
    List<OptionGroupResponse> OptionGroups,
    List<string> Tags);

public sealed record MenuItemOptionResponse(
    Guid Id,
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault);

public sealed record OptionGroupResponse(
    Guid Id,
    string GroupName,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    List<MenuItemOptionResponse> Options);