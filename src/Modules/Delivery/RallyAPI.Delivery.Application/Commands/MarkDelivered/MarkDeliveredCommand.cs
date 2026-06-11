using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.MarkDelivered;

public sealed record MarkDeliveredCommand : IRequest<Result>
{
    public required Guid DeliveryRequestId { get; init; }
    public required Guid RiderId { get; init; }

    /// <summary>
    /// 4-digit code the customer reads out to the rider at the door.
    /// Verified against <c>DeliveryRequest.DropCode</c> before the
    /// status transitions to Delivered.
    /// </summary>
    public required string DropCode { get; init; }
}
