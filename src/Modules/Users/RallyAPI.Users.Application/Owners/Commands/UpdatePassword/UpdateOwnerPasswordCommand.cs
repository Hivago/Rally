using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.UpdatePassword;

public sealed record UpdateOwnerPasswordCommand(
    Guid OwnerId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;
