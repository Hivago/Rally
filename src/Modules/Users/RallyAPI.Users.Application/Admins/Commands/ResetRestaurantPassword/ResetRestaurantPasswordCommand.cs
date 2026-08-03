using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.ResetRestaurantPassword;

/// <summary>
/// Admin-only forced password reset for a restaurant that lost access. Generates a new
/// temporary password server-side rather than accepting one, so it returns the plaintext
/// exactly once — the caller relays it to the restaurant out of band, who should change it
/// on next login via PATCH /api/restaurants/me/password.
/// </summary>
public sealed record ResetRestaurantPasswordCommand(
    Guid RequestedByAdminId,
    Guid RestaurantId) : IRequest<Result<ResetRestaurantPasswordResponse>>;

public sealed record ResetRestaurantPasswordResponse(Guid RestaurantId, string TemporaryPassword);
