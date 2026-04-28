using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetLiveOrders;

public sealed record GetLiveOrdersQuery(Guid RequestedByAdminId, int Limit = 200)
    : IRequest<Result<LiveOrdersResponse>>;

public sealed record LiveOrdersResponse(
    int Count,
    IReadOnlyList<LiveOrderRow> Orders);
