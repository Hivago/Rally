using MediatR;
using RallyAPI.Catalog.Application.MenuItems.Commands.CreateMenuItem;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.UpdateMenuItem;

public sealed record UpdateMenuItemCommand(
    Guid MenuItemId,
    Guid RestaurantId,
    string Name,
    string? Description,
    decimal BasePrice,
    string? ImageUrl,
    int DisplayOrder,
    bool IsVegetarian,
    int PreparationTimeMinutes,
    List<MenuItemOptionDto>? Options,
    List<OptionGroupDto>? OptionGroups,
    List<string>? Tags) : IRequest<Result>;