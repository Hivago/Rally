using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Queries.GetTimeOffs;

public sealed record GetTimeOffsQuery(
    Guid OwnerId,
    Guid RestaurantId,
    bool IncludeCancelled,
    bool IncludePast) : IRequest<Result<IReadOnlyList<TimeOffResponse>>>;

public sealed record TimeOffResponse(
    Guid Id,
    Guid RestaurantId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Reason,
    DateTime? CancelledAtUtc,
    DateTime CreatedAtUtc,
    bool IsActive);
