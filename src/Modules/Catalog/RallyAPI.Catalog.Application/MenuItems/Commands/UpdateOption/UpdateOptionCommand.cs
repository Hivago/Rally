using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.UpdateOption;

public sealed record UpdateOptionCommand(
    Guid RestaurantId,
    Guid OptionId,
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault) : IRequest<Result>;
