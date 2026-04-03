using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetPayoutsByOwner;

public sealed record GetPayoutsByOwnerQuery : IRequest<Result<IReadOnlyList<PayoutDto>>>
{
    public Guid OwnerId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
