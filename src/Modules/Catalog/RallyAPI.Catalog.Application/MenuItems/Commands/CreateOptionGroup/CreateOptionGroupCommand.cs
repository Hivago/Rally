using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.CreateOptionGroup;

public sealed record CreateOptionGroupCommand(
    Guid RestaurantId,
    Guid MenuItemId,
    string GroupName,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    List<OptionItemDto>? Options) : IRequest<Result<CreateOptionGroupResponse>>;

public sealed record OptionItemDto(
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault);

public sealed record CreateOptionGroupResponse(Guid OptionGroupId);
