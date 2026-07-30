using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.UpdateOptionGroup;

public sealed record UpdateOptionGroupCommand(
    Guid RestaurantId,
    Guid MenuItemId,
    Guid OptionGroupId,
    string GroupName,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder) : IRequest<Result>, IMenuCacheInvalidatingCommand;
