using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderByNumber;

/// <summary>
/// Query to get order by order number.
/// </summary>
public sealed record GetOrderByNumberQuery : IRequest<Result<OrderDto>>
{
    public string OrderNumber { get; init; } = string.Empty;
    public Guid? RequestingUserId { get; init; }
    public string? RequestingUserType { get; init; }
    public bool IsAdmin { get; init; }
}
