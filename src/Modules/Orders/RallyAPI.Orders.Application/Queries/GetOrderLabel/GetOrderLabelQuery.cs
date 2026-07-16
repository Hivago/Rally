using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderLabel;

/// <summary>
/// Query to get the customer bill / order label for an order (the copy that goes on the bag).
/// Restaurant-facing: only the owning restaurant (or an Admin) may print it.
/// </summary>
public sealed record GetOrderLabelQuery(Guid OrderId, Guid CallerId, string CallerRole)
    : IRequest<Result<OrderLabelDto>>;
