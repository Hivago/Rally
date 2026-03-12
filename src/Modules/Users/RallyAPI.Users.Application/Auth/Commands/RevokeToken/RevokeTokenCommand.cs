using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Auth.Commands.RevokeToken;

public sealed record RevokeTokenCommand(string RefreshToken, Guid RequestingUserId)
    : IRequest<Result>;
