using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.SetOwnerCrossOutletAccept;

/// <summary>
/// Admin-only toggle for multi-outlet cross-accept. Off by default for every owner — an
/// admin must explicitly enable it per owner (e.g. after verifying the request), and can
/// disable it again if the owner no longer wants shared access across their outlets.
/// </summary>
public sealed record SetOwnerCrossOutletAcceptCommand(
    Guid RequestedByAdminId,
    Guid OwnerId,
    bool Enabled) : IRequest<Result<SetOwnerCrossOutletAcceptResponse>>;

public sealed record SetOwnerCrossOutletAcceptResponse(Guid OwnerId, bool CrossOutletAcceptEnabled);
