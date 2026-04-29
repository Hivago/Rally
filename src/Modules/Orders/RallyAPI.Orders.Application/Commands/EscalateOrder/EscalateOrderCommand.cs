using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.EscalateOrder;

/// <summary>
/// Manually escalate an order to admin. Used when an admin wants to flag an order
/// for follow-up before the auto-escalator kicks in. Idempotent — escalating an
/// already-escalated order is a no-op.
/// </summary>
public sealed record EscalateOrderCommand(
    Guid OrderId,
    string Reason) : IRequest<Result>;
