using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.RejectRestaurantLocationCorrection;

public sealed record RejectRestaurantLocationCorrectionCommand(
    Guid QueueEntryId,
    Guid AdminId) : IRequest<Result>;
