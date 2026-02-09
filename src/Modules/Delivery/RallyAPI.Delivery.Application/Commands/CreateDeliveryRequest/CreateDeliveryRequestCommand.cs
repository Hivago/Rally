using MediatR;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.CreateDeliveryRequest;

public sealed record CreateDeliveryRequestCommand : IRequest<Result<DeliveryRequestDto>>
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required Guid QuoteId { get; init; }

    // Pickup
    public required double PickupLatitude { get; init; }
    public required double PickupLongitude { get; init; }
    public required string PickupPincode { get; init; }
    public required string PickupAddress { get; init; }
    public required string PickupContactName { get; init; }
    public required string PickupContactPhone { get; init; }

    // Drop
    public required double DropLatitude { get; init; }
    public required double DropLongitude { get; init; }
    public required string DropPincode { get; init; }
    public required string DropAddress { get; init; }
    public required string DropContactName { get; init; }
    public required string DropContactPhone { get; init; }

    // Prep Time
    public int ItemCount { get; init; } = 1;
}