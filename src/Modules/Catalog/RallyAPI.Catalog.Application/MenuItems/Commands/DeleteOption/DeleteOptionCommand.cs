using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.DeleteOption;

public sealed record DeleteOptionCommand(
    Guid RestaurantId,
    Guid OptionId) : IRequest<Result>;
