using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetAlerts;

public sealed record GetAdminAlertsQuery(Guid RequestedByAdminId, int Limit = 50)
    : IRequest<Result<AdminAlertsResponse>>;

public sealed record AdminAlertsResponse(
    int Count,
    IReadOnlyList<AdminAlert> Alerts);
