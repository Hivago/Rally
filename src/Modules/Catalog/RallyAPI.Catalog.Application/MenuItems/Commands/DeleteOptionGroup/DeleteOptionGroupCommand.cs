using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.DeleteOptionGroup;

public sealed record DeleteOptionGroupCommand(
    Guid RestaurantId,
    Guid MenuItemId,
    Guid OptionGroupId) : IRequest<Result>;
