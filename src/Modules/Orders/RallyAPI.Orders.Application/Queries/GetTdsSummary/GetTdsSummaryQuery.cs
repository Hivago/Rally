using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetTdsSummary;

public sealed record GetTdsSummaryQuery : IRequest<Result<TdsSummaryDto>>
{
    public Guid OwnerId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
}
