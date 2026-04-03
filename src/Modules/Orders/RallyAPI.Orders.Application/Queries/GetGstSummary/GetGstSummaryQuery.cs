using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetGstSummary;

public sealed record GetGstSummaryQuery : IRequest<Result<GstSummaryDto>>
{
    public Guid OwnerId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
}
