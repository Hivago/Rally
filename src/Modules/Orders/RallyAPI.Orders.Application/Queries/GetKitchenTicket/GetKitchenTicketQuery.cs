using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetKitchenTicket;

/// <summary>
/// Query to get the Kitchen Order Ticket (KOT) for an order.
/// Restaurant-facing: only the owning restaurant (or an Admin) may print it.
/// </summary>
public sealed record GetKitchenTicketQuery(Guid OrderId, Guid CallerId, string CallerRole)
    : IRequest<Result<KitchenTicketDto>>;
