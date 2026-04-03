using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.ProcessPayout;

public sealed record ProcessPayoutCommand : IRequest<Result>
{
    public Guid PayoutId { get; init; }
    public string TransactionReference { get; init; } = null!;
    public string? Notes { get; init; }
}
