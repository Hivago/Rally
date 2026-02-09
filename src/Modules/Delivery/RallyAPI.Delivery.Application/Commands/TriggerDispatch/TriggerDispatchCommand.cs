using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.TriggerDispatch;

public record TriggerDispatchCommand : IRequest<Result>
{
    public Guid DeliveryRequestId { get; init; }
}