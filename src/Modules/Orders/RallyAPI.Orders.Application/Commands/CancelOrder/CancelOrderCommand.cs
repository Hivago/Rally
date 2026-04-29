using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.CancelOrder;

/// <summary>
/// Command to cancel an order.
/// </summary>
public sealed record CancelOrderCommand : IRequest<Result<OrderDto>>
{
    public Guid OrderId { get; init; }
    public Guid CancelledBy { get; init; }
    public string CallerRole { get; init; } = string.Empty;
    public CancellationReason Reason { get; init; }
    public string? Notes { get; init; }

    /// <summary>
    /// Admin-only flag to bypass the normal status guard
    /// (allows cancelling from Preparing, ReadyForPickup, PickedUp).
    /// Ignored for non-admin callers.
    /// </summary>
    public bool ForceCancel { get; init; }
}