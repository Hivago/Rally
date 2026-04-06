using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.AddOrderNote;

public sealed record AddOrderNoteCommand(
    Guid OrderId,
    Guid AdminId,
    string Note) : IRequest<Result>;
