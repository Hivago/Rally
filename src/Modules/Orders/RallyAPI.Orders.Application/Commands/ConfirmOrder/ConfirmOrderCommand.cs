using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.ConfirmOrder;

/// <summary>
/// Command to confirm an order (restaurant accepts).
/// </summary>
/// <param name="RestaurantId">The acting outlet (the login used) — kept for audit/logging.</param>
/// <param name="RestaurantIds">All outlets this login is authorized to act on (includes RestaurantId).</param>
public sealed record ConfirmOrderCommand(
    Guid OrderId,
    Guid RestaurantId,
    IReadOnlyList<Guid> RestaurantIds) : IRequest<Result<OrderDto>>;