using MediatR;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayoutDetail;

public sealed record GetRestaurantPayoutDetailQuery(
    Guid RequestedByAdminId,
    Guid PayoutId) : IRequest<Result<AdminPayoutDetail>>;
