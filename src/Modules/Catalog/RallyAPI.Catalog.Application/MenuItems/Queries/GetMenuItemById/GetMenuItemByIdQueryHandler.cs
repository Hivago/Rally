using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Application.MenuItems.Queries.GetMenuItemsByMenu;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Queries.GetMenuItemById;

internal sealed class GetMenuItemByIdQueryHandler
    : IRequestHandler<GetMenuItemByIdQuery, Result<MenuItemResponse>>
{
    private readonly IMenuItemRepository _menuItemRepository;

    public GetMenuItemByIdQueryHandler(IMenuItemRepository menuItemRepository)
    {
        _menuItemRepository = menuItemRepository;
    }

    public async Task<Result<MenuItemResponse>> Handle(
        GetMenuItemByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await _menuItemRepository.GetByIdWithOptionsAsync(
            request.MenuItemId,
            cancellationToken);

        if (item is null)
            return Result.Failure<MenuItemResponse>(MenuItemErrors.NotFound);

        var response = new MenuItemResponse(
            item.Id,
            item.Name,
            item.Description,
            item.BasePrice,
            item.ImageUrl,
            item.IsAvailable,
            item.IsVegetarian,
            item.PreparationTimeMinutes,
            item.Options.Where(o => o.OptionGroupId == null).Select(o => new MenuItemOptionResponse(
                o.Id,
                o.Name,
                o.Type.ToString(),
                o.AdditionalPrice,
                o.IsDefault)).ToList(),
            item.OptionGroups.OrderBy(g => g.DisplayOrder).Select(g => new OptionGroupResponse(
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
                    o.IsDefault)).ToList())).ToList(),
            item.Tags);

        return response;
    }
}