using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetRestaurantEarnings;

/// <summary>
/// Gets an earnings summary for a restaurant owner over an arbitrary date range
/// (defaults to the current week when the caller omits the range).
/// </summary>
public sealed record GetRestaurantEarningsQuery : IRequest<Result<EarningsSummaryDto>>
{
    public Guid OwnerId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
}
