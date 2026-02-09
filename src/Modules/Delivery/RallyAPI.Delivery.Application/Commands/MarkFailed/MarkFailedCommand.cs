using MediatR;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.MarkFailed;

public sealed record MarkFailedCommand : IRequest<Result>
{
    public required Guid DeliveryRequestId { get; init; }
    public required Guid RiderId { get; init; }
    public required DeliveryFailureReason Reason { get; init; }
    public string? Notes { get; init; }
    public string? PhotoUrl { get; init; }
}