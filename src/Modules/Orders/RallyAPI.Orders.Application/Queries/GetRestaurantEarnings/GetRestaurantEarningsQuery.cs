using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetRestaurantEarnings;

/// <summary>
/// Gets current week's earnings summary for a restaurant owner.
/// </summary>
public sealed record GetRestaurantEarningsQuery : IRequest<Result<EarningsSummaryDto>>
{
    public Guid OwnerId { get; init; }
}
