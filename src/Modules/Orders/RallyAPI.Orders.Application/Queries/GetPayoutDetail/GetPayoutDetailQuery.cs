using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetPayoutDetail;

public sealed record GetPayoutDetailQuery : IRequest<Result<PayoutDetailDto>>
{
    public Guid PayoutId { get; init; }
    public Guid OwnerId { get; init; }
}
