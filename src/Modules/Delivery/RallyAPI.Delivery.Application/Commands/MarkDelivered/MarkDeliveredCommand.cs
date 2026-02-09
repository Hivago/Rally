using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.MarkDelivered;

public sealed record MarkDeliveredCommand : IRequest<Result>
{
    public required Guid DeliveryRequestId { get; init; }
    public required Guid RiderId { get; init; }
}
