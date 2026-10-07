using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderById;

/// <summary>
/// Query to get order by ID.
/// </summary>
/// <param name="CallerRestaurantIds">For Restaurant callers: all outlets this login is authorized to act on (includes CallerId).</param>
public sealed record GetOrderByIdQuery(
    Guid OrderId,
    Guid CallerId,
    string CallerRole,
    IReadOnlyList<Guid>? CallerRestaurantIds = null) : IRequest<Result<OrderDto>>;