using MediatR;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.GetQuote;

public sealed record GetQuoteCommand : IRequest<Result<DeliveryQuoteDto>>
{
    public required Guid RestaurantId { get; init; }
    public required double PickupLatitude { get; init; }
    public required double PickupLongitude { get; init; }
    public string? PickupPincode { get; init; }
    public required double DropLatitude { get; init; }
    public required double DropLongitude { get; init; }
    public string? DropPincode { get; init; }
    public string? City { get; init; }
    public required decimal OrderAmount { get; init; }
}
