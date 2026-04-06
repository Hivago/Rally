using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Queries.GetOrdersByCustomer;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetFilteredOrders;

public sealed record GetFilteredOrdersQuery : IRequest<Result<PagedResult<OrderSummaryDto>>>
{
    public OrderStatus? Status { get; init; }
    public Guid? RestaurantId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
