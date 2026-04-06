using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.AddOptionToGroup;

public sealed record AddOptionToGroupCommand(
    Guid RestaurantId,
    Guid OptionGroupId,
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault) : IRequest<Result<AddOptionToGroupResponse>>;

public sealed record AddOptionToGroupResponse(Guid OptionId);
