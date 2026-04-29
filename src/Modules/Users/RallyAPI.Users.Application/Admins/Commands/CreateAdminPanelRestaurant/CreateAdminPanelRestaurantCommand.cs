using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.CreateAdminPanelRestaurant;

/// <summary>
/// Admin-panel restaurant creation. Differs from the legacy CreateRestaurantCommand
/// in two ways:
///   1. OwnerId is required (every outlet must belong to an active owner).
///   2. RstCode is auto-generated server-side via a Postgres sequence.
/// Old POST /api/admins/restaurants stays as-is for backward compat.
/// </summary>
public sealed record CreateAdminPanelRestaurantCommand(
    Guid RequestedByAdminId,
    Guid OwnerId,
    string Name,
    string Phone,
    string Email,
    string Password,
    string AddressLine,
    decimal Latitude,
    decimal Longitude) : IRequest<Result<CreateAdminPanelRestaurantResponse>>;

public sealed record CreateAdminPanelRestaurantResponse(
    Guid RestaurantId,
    string RstCode);
