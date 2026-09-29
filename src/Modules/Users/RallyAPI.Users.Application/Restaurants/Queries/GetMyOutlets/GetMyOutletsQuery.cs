using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Owners.Queries.GetOutlets;

namespace RallyAPI.Users.Application.Restaurants.Queries.GetMyOutlets;

/// <summary>
/// Resolves the outlet ids from a Restaurant JWT's restaurant_ids claim into
/// displayable outlet summaries (name, etc.) — for the multi-outlet switcher UI.
/// </summary>
public sealed record GetMyOutletsQuery(IReadOnlyList<Guid> RestaurantIds)
    : IRequest<Result<IReadOnlyList<OutletSummaryResponse>>>;
