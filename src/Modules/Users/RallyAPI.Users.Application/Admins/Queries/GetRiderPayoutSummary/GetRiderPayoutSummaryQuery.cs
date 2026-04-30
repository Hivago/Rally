using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRiderPayoutSummary;

public sealed record GetRiderPayoutSummaryQuery(Guid RequestedByAdminId)
    : IRequest<Result<RiderPayoutSummary>>;
