using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Mappings;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(query.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(OrderErrors.NotFound(query.OrderId));
        }

        if (!CanAccessOrder(order, query))
        {
            return Result.Failure<OrderDto>(OrderErrors.NotFound(query.OrderId));
        }

        return Result.Success(order.ToDto());
    }

    private static bool CanAccessOrder(Domain.Entities.Order order, GetOrderByIdQuery query)
    {
        if (query.IsAdmin)
        {
            return true;
        }

        if (!query.RequestingUserId.HasValue || string.IsNullOrWhiteSpace(query.RequestingUserType))
        {
            return false;
        }

        return query.RequestingUserType.ToLowerInvariant() switch
        {
            "customer" => order.CustomerId == query.RequestingUserId.Value,
            "restaurant" => order.RestaurantId == query.RequestingUserId.Value,
            "rider" => order.DeliveryInfo.RiderId == query.RequestingUserId.Value,
            _ => false
        };
    }
}
