using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.DeleteMenuItem;

public sealed record DeleteMenuItemCommand(
    Guid MenuItemId,
    Guid RestaurantId) : IRequest<Result>;
