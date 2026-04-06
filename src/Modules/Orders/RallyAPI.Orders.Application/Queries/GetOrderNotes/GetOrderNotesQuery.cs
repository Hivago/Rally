using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetOrderNotes;

public sealed record GetOrderNotesQuery(Guid OrderId) : IRequest<Result<OrderNotesResponse>>;

public sealed record OrderNotesResponse(
    Guid OrderId,
    string? Notes);
