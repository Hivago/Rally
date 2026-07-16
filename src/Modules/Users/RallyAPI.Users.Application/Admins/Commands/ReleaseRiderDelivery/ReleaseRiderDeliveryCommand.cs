using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.ReleaseRiderDelivery;

/// <summary>
/// Force-clears a rider's current delivery assignment. Used by ops when a rider is
/// stuck pinned to a delivery that ended abnormally (e.g. a dispatch race left the
/// delivery Failed without releasing the rider), which otherwise blocks the rider from
/// receiving any new offers.
/// </summary>
public sealed record ReleaseRiderDeliveryCommand(Guid RiderId)
    : IRequest<Result<ReleaseRiderDeliveryResult>>;

public sealed record ReleaseRiderDeliveryResult(
    Guid RiderId,
    Guid? ClearedDeliveryId,
    bool WasOnDelivery);
