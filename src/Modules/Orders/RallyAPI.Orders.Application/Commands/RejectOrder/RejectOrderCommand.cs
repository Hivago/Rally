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

    /// <summary>The acting outlet (the login used) — kept for audit/logging.</summary>
    public Guid RestaurantId { get; init; }

    /// <summary>All outlets this login is authorized to act on (includes RestaurantId).</summary>
    public IReadOnlyList<Guid> RestaurantIds { get; init; } = Array.Empty<Guid>();
    public string Reason { get; init; } = string.Empty;
}