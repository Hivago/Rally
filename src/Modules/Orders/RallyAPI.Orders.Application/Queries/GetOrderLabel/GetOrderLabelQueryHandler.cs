using MediatR;
using Microsoft.Extensions.Options;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Mappings;
using RallyAPI.Orders.Application.Options;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderLabel;

public sealed class GetOrderLabelQueryHandler
    : IRequestHandler<GetOrderLabelQuery, Result<OrderLabelDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IRestaurantQueryService _restaurantQueryService;
    private readonly IOrderDeliveryCodeService _deliveryCodeService;
    private readonly PlatformOptions _platformOptions;

    public GetOrderLabelQueryHandler(
        IOrderRepository orderRepository,
        IRestaurantQueryService restaurantQueryService,
        IOrderDeliveryCodeService deliveryCodeService,
        IOptions<PlatformOptions> platformOptions)
    {
        _orderRepository = orderRepository;
        _restaurantQueryService = restaurantQueryService;
        _deliveryCodeService = deliveryCodeService;
        _platformOptions = platformOptions.Value;
    }

    public async Task<Result<OrderLabelDto>> Handle(
        GetOrderLabelQuery query, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(query.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderLabelDto>(OrderErrors.NotFound(query.OrderId));
        }

        if (!IsAuthorized(order, query.CallerId, query.CallerRole))
        {
            // Return NotFound — do not reveal that the order exists to unauthorized callers
            return Result.Failure<OrderLabelDto>(OrderErrors.NotFound(query.OrderId));
        }

        // Restaurant address + FSSAI (from Users module via shared abstraction)
        var restaurant = await _restaurantQueryService.GetByIdAsync(order.RestaurantId, cancellationToken);

        // Delivery OTP (from Delivery module) — pickup code shown to the rider. Null for
        // pickup orders or before a delivery request exists.
        string? deliveryOtp = null;
        if (order.FulfillmentType != Domain.Enums.FulfillmentType.Pickup)
        {
            var codes = await _deliveryCodeService.GetByOrderIdAsync(order.Id, cancellationToken);
            deliveryOtp = codes?.PickupCode;
        }

        var label = order.ToOrderLabel(
            restaurantAddress: restaurant?.AddressLine,
            restaurantFssai: restaurant?.FssaiNumber,
            platformFssai: _platformOptions.FssaiNumber,
            deliveryOtp: deliveryOtp);

        return Result.Success(label);
    }

    // A label is only ever printed by the owning restaurant or an Admin (same rule as the KOT).
    private static bool IsAuthorized(Domain.Entities.Order order, Guid callerId, string callerRole) =>
        callerRole switch
        {
            "Admin"      => true,
            "Restaurant" => order.RestaurantId == callerId,
            _            => false
        };
}
