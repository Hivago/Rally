using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetOrderStats;

internal sealed class GetOrderStatsQueryHandler
    : IRequestHandler<GetOrderStatsQuery, Result<OrderStatsResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IOrderStatsService _orderStats;

    public GetOrderStatsQueryHandler(
        IAdminRepository adminRepository,
        IOrderStatsService orderStats)
    {
        _adminRepository = adminRepository;
        _orderStats = orderStats;
    }

    public async Task<Result<OrderStatsResponse>> Handle(
        GetOrderStatsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<OrderStatsResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var now = DateTime.UtcNow;
        var startOfDay = now.Date;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        // Sequential to avoid concurrent operations on the same OrdersDbContext.
        var total = await _orderStats.GetTotalCountAsync(cancellationToken);
        var active = await _orderStats.GetActiveCountAsync(cancellationToken);
        var today = await _orderStats.GetCountSinceAsync(startOfDay, cancellationToken);
        var last7 = await _orderStats.GetCountSinceAsync(sevenDaysAgo, cancellationToken);
        var last30 = await _orderStats.GetCountSinceAsync(thirtyDaysAgo, cancellationToken);
        var pendingLedger = await _orderStats.GetPendingLedgerEntriesCountAsync(cancellationToken);
        var byStatus = await _orderStats.GetOrdersByStatusAsync(cancellationToken);

        return Result.Success(new OrderStatsResponse(
            total,
            active,
            today,
            last7,
            last30,
            pendingLedger,
            new Dictionary<string, int>(byStatus)));
    }
}
