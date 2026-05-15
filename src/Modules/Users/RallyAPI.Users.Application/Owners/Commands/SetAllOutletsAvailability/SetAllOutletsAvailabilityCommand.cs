using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.SetAllOutletsAvailability;

public sealed record SetAllOutletsAvailabilityCommand(
    Guid OwnerId,
    bool IsAcceptingOrders) : IRequest<Result<SetAllOutletsAvailabilityResponse>>;

public sealed record SetAllOutletsAvailabilityResponse(
    int UpdatedCount,
    int SkippedCount);
