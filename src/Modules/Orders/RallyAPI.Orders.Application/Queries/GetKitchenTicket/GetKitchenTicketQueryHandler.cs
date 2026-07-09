using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Mappings;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetKitchenTicket;

public sealed class GetKitchenTicketQueryHandler
    : IRequestHandler<GetKitchenTicketQuery, Result<KitchenTicketDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetKitchenTicketQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<KitchenTicketDto>> Handle(
        GetKitchenTicketQuery query, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(query.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<KitchenTicketDto>(OrderErrors.NotFound(query.OrderId));
        }

        if (!IsAuthorized(order, query.CallerId, query.CallerRole))
        {
            // Return NotFound — do not reveal that the order exists to unauthorized callers
            return Result.Failure<KitchenTicketDto>(OrderErrors.NotFound(query.OrderId));
        }

        return Result.Success(order.ToKitchenTicket());
    }

    // A KOT is only ever printed by the kitchen (the owning restaurant) or an Admin.
    private static bool IsAuthorized(Domain.Entities.Order order, Guid callerId, string callerRole) =>
        callerRole switch
        {
            "Admin"      => true,
            "Restaurant" => order.RestaurantId == callerId,
            _            => false
        };
}
