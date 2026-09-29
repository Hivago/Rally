using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.AssignRider;

/// <summary>
/// Command to assign a rider to an order.
/// </summary>
public sealed record AssignRiderCommand : IRequest<Result<OrderDto>>
{
    public Guid OrderId { get; init; }
    public Guid RiderId { get; init; }
    public string? RiderName { get; init; }
    public string? RiderPhone { get; init; }
    public Guid? AssignedById { get; init; }
    public string? AssignedByRole { get; init; }

    /// <summary>For Restaurant callers: all outlets this login is authorized to act on (includes AssignedById).</summary>
    public IReadOnlyList<Guid> AssignedByRestaurantIds { get; init; } = Array.Empty<Guid>();
}