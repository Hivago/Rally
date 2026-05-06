using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.CreateOwner;

/// <summary>
/// Admin-only command to onboard a new RestaurantOwner. The returned OwnerId
/// is then used as the FK when creating outlets via CreateAdminPanelRestaurantCommand.
/// </summary>
public sealed record CreateOwnerCommand(
    Guid RequestedByAdminId,
    string Name,
    string Email,
    string Phone,
    string Password,
    string? PanNumber,
    string? GstNumber,
    string? BankAccountNumber,
    string? BankIfscCode,
    string? BankAccountName) : IRequest<Result<CreateOwnerResponse>>;

public sealed record CreateOwnerResponse(Guid OwnerId);
