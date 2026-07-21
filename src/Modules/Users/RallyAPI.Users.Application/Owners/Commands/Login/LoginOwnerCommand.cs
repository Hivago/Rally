using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.Login;

public sealed record LoginOwnerCommand(
    string Email,
    string Password) : IRequest<Result<LoginOwnerResponse>>;

public sealed record LoginOwnerResponse(
    Guid OwnerId,
    string Name,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    bool MustChangePassword);
