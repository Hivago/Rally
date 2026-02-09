using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.MarkPickedUp;

public sealed record MarkPickedUpCommand : IRequest<Result>
{
    public required Guid DeliveryRequestId { get; init; }
    public required Guid RiderId { get; init; }
}