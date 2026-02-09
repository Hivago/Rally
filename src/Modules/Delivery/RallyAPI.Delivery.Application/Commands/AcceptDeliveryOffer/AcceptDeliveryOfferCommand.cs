using MediatR;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.AcceptDeliveryOffer;

public sealed record AcceptDeliveryOfferCommand : IRequest<Result<DeliveryRequestDto>>
{
    public required Guid OfferId { get; init; }
    public required Guid RiderId { get; init; }
}