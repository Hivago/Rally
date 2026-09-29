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

        if (!IsAuthorized(order, query.CallerId, query.CallerRole, query.CallerRestaurantIds))
        {
            // Return NotFound — do not reveal that the order exists to unauthorized callers
            return Result.Failure<OrderDto>(OrderErrors.NotFound(query.OrderId));
        }

        return Result.Success(order.ToDto());
    }

    private static bool IsAuthorized(
        Domain.Entities.Order order, Guid callerId, string callerRole, IReadOnlyList<Guid>? callerRestaurantIds) =>
        callerRole switch
        {
            "Admin"      => true,
            "Customer"   => order.CustomerId == callerId,
            "Restaurant" => (callerRestaurantIds ?? new[] { callerId }).Contains(order.RestaurantId),
            "Rider"      => order.DeliveryInfo.RiderId == callerId,
            _            => false
        };
}