using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.MarkPickedUp;

public sealed record MarkPickedUpCommand : IRequest<Result>
{
    public required Guid DeliveryRequestId { get; init; }
    public required Guid RiderId { get; init; }

    /// <summary>
    /// 4-digit code the restaurant reads out to the rider at pickup.
    /// Verified against <c>DeliveryRequest.PickupCode</c> before the
    /// status transitions to PickedUp.
    /// </summary>
    public required string PickupCode { get; init; }
}