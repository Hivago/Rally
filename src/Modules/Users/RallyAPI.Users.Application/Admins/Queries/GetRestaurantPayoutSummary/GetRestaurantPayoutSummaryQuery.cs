using MediatR;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayoutSummary;

public sealed record GetRestaurantPayoutSummaryQuery(Guid RequestedByAdminId)
    : IRequest<Result<RestaurantPayoutSummary>>;
