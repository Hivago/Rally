using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Application.MenuItems.Queries.GetMenuItemsByMenu;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Queries.GetOptionGroups;

internal sealed class GetOptionGroupsQueryHandler
    : IRequestHandler<GetOptionGroupsQuery, Result<List<OptionGroupResponse>>>
{
    private readonly IMenuItemRepository _menuItemRepository;

    public GetOptionGroupsQueryHandler(IMenuItemRepository menuItemRepository)
    {
        _menuItemRepository = menuItemRepository;
    }

    public async Task<Result<List<OptionGroupResponse>>> Handle(
        GetOptionGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _menuItemRepository.GetByIdWithOptionsAsync(request.MenuItemId, cancellationToken);

        if (menuItem is null)
            return Result.Failure<List<OptionGroupResponse>>(MenuItemErrors.NotFound);

        var groups = menuItem.OptionGroups.Select(g => new OptionGroupResponse(
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
                o.IsDefault)).ToList()))
            .OrderBy(g => g.DisplayOrder)
            .ToList();

        return groups;
    }
}
