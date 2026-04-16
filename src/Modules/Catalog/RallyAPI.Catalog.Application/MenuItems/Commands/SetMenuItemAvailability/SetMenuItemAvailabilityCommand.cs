using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.SetMenuItemAvailability;

public sealed record SetMenuItemAvailabilityCommand(
    Guid MenuItemId,
    Guid RestaurantId,
    bool IsAvailable) : IRequest<Result<SetMenuItemAvailabilityResponse>>;

public sealed record SetMenuItemAvailabilityResponse(bool IsAvailable);
