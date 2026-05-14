using MediatR;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Queries.GetDeliveryCodes;

public sealed class GetDeliveryCodesQueryHandler
    : IRequestHandler<GetDeliveryCodesQuery, Result<DeliveryCodesDto>>
{
    private readonly IDeliveryRequestRepository _repository;

    public GetDeliveryCodesQueryHandler(IDeliveryRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<DeliveryCodesDto>> Handle(
        GetDeliveryCodesQuery query,
        CancellationToken cancellationToken)
    {
        var delivery = await _repository.GetByOrderIdAsync(query.OrderId, cancellationToken);

        if (delivery is null)
        {
            return Result.Failure<DeliveryCodesDto>(
                Error.NotFound($"No delivery request found for order {query.OrderId}"));
        }

        var dto = Authorize(delivery, query.CallerId, query.CallerRole);
        if (dto is null)
        {
            // Treat unauthorized as NotFound so existence isn't leaked.
            return Result.Failure<DeliveryCodesDto>(
                Error.NotFound($"No delivery request found for order {query.OrderId}"));
        }

        return Result.Success(dto);
    }

    private static DeliveryCodesDto? Authorize(DeliveryRequest delivery, Guid callerId, string callerRole) =>
        callerRole switch
        {
            "Admin" => new DeliveryCodesDto
            {
                PickupCode = delivery.PickupCode,
                DropCode = delivery.DropCode
            },
            "Restaurant" when delivery.RestaurantId == callerId => new DeliveryCodesDto
            {
                PickupCode = delivery.PickupCode
            },
            "Customer" when delivery.CustomerId == callerId => new DeliveryCodesDto
            {
                DropCode = delivery.DropCode
            },
            _ => null
        };
}
