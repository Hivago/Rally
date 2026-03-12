using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderById;

/// <summary>
/// Query to get order by ID.
/// </summary>
public sealed record GetOrderByIdQuery : IRequest<Result<OrderDto>>
{
    public Guid OrderId { get; init; }
    public Guid? RequestingUserId { get; init; }
    public string? RequestingUserType { get; init; }
    public bool IsAdmin { get; init; }
}
