using MediatR;
using RallyAPI.Catalog.Application.MenuItems.Queries.GetMenuItemsByMenu;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Queries.GetOptionGroups;

public sealed record GetOptionGroupsQuery(Guid MenuItemId)
    : IRequest<Result<List<OptionGroupResponse>>>;
