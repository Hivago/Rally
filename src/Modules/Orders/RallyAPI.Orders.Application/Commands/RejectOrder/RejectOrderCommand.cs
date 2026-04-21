using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.RejectOrder;

/// <summary>
/// Command to reject an order (restaurant rejects).
/// </summary>
public sealed record RejectOrderCommand : IRequest<Result<OrderDto>>
{
    public Guid OrderId { get; init; }
    public Guid RestaurantId { get; init; }
    public string Reason { get; init; } = string.Empty;
}