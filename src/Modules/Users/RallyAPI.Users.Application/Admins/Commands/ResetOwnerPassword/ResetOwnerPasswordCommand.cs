using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.ResetOwnerPassword;

/// <summary>
/// Admin-only forced password reset for a restaurant owner that lost access. Generates a new
/// temporary password server-side rather than accepting one, so it returns the plaintext
/// exactly once — the caller relays it to the owner out of band, who should change it
/// on next login via PATCH /api/owners/me/password.
/// </summary>
public sealed record ResetOwnerPasswordCommand(
    Guid RequestedByAdminId,
    Guid OwnerId) : IRequest<Result<ResetOwnerPasswordResponse>>;

public sealed record ResetOwnerPasswordResponse(Guid OwnerId, string TemporaryPassword);
