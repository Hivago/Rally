using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetOrderStats;

public sealed record GetOrderStatsQuery(Guid RequestedByAdminId)
    : IRequest<Result<OrderStatsResponse>>;

public sealed record OrderStatsResponse(
    int Total,
    int Active,
    int Today,
    int Last7Days,
    int Last30Days,
    int PendingPayoutLedgerEntries,
    Dictionary<string, int> ByStatus);
