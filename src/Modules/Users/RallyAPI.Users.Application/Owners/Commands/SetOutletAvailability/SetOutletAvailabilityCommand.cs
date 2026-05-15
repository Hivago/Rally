using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.SetOutletAvailability;

public sealed record SetOutletAvailabilityCommand(
    Guid OwnerId,
    Guid RestaurantId,
    bool IsAcceptingOrders) : IRequest<Result>;
