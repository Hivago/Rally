using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.AddOptionToGroup;

public sealed record AddOptionToGroupCommand(
    Guid RestaurantId,
    Guid OptionGroupId,
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault) : IRequest<Result<AddOptionToGroupResponse>>, IMenuCacheInvalidatingCommand;

public sealed record AddOptionToGroupResponse(Guid OptionId);
