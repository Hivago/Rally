using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderByNumber;

/// <summary>
/// Query to get order by order number.
/// </summary>
public sealed record GetOrderByNumberQuery(
    string OrderNumber,
    Guid CallerId,
    string CallerRole,
    IReadOnlyList<Guid>? CallerRestaurantIds = null) : IRequest<Result<OrderDto>>;