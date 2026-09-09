using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.ApproveRestaurantLocationCorrection;

public sealed record ApproveRestaurantLocationCorrectionCommand(
    Guid QueueEntryId,
    Guid AdminId) : IRequest<Result>;
