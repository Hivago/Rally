using MediatR;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderNotes;

internal sealed class GetOrderNotesQueryHandler
    : IRequestHandler<GetOrderNotesQuery, Result<OrderNotesResponse>>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderNotesQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<OrderNotesResponse>> Handle(
        GetOrderNotesQuery request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<OrderNotesResponse>(OrderErrors.NotFound(request.OrderId));

        return new OrderNotesResponse(order.Id, order.InternalNotes);
    }
}
