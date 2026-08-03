using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Menus.Commands.CreateMenu;

public sealed record CreateMenuCommand(
    Guid RestaurantId,
    string Name,
    string? Description,
    int DisplayOrder) : IRequest<Result<CreateMenuResponse>>, IMenuCacheInvalidatingCommand;

public sealed record CreateMenuResponse(Guid MenuId);