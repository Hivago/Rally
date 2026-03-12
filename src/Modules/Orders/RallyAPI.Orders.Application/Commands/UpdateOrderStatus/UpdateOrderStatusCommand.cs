using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.UpdateOrderStatus;

/// <summary>
/// Generic command to update order status.
/// Flexible for multiple status transitions.
/// </summary>
public sealed record UpdateOrderStatusCommand : IRequest<Result<OrderDto>>
{
    public Guid OrderId { get; init; }
    public OrderStatus TargetStatus { get; init; }
    public Guid? ActorId { get; init; } // Restaurant, Rider, or Admin ID
    public string? ActorRole { get; init; }
    public string? ActorUserType { get; init; }
}
