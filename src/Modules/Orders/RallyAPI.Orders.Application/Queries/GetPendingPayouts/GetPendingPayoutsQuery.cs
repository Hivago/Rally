using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetPendingPayouts;

/// <summary>
/// Admin query: get all pending payouts waiting to be processed.
/// </summary>
public sealed record GetPendingPayoutsQuery : IRequest<Result<IReadOnlyList<PayoutDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
