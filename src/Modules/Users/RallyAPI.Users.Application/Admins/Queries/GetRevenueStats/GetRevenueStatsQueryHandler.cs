using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRevenueStats;

internal sealed class GetRevenueStatsQueryHandler
    : IRequestHandler<GetRevenueStatsQuery, Result<RevenueStatsResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IOrderStatsService _orderStats;

    public GetRevenueStatsQueryHandler(
        IAdminRepository adminRepository,
        IOrderStatsService orderStats)
    {
        _adminRepository = adminRepository;
        _orderStats = orderStats;
    }

    public async Task<Result<RevenueStatsResponse>> Handle(
        GetRevenueStatsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<RevenueStatsResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var now = DateTime.UtcNow;
        var startOfDay = now.Date;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        // Sequential to avoid concurrent operations on the same OrdersDbContext.
        var todayRevenue = await _orderStats.GetRevenueSinceAsync(startOfDay, cancellationToken);
        var week = await _orderStats.GetRevenueSinceAsync(sevenDaysAgo, cancellationToken);
        var month = await _orderStats.GetRevenueSinceAsync(thirtyDaysAgo, cancellationToken);
        var lifetimeRevenue = await _orderStats.GetLifetimeRevenueAsync(cancellationToken);

        var todayCommission = await _orderStats.GetCommissionSinceAsync(startOfDay, cancellationToken);
        var weekCommission = await _orderStats.GetCommissionSinceAsync(sevenDaysAgo, cancellationToken);
        var monthCommission = await _orderStats.GetCommissionSinceAsync(thirtyDaysAgo, cancellationToken);
        var lifetimeCommission = await _orderStats.GetLifetimeCommissionAsync(cancellationToken);

        var pendingLedger = await _orderStats.GetPendingLedgerEntriesCountAsync(cancellationToken);
        var unpaidPayout = await _orderStats.GetUnpaidPayoutAmountAsync(cancellationToken);

        return Result.Success(new RevenueStatsResponse(
            "INR",
            todayRevenue,
            week,
            month,
            lifetimeRevenue,
            todayCommission,
            weekCommission,
            monthCommission,
            lifetimeCommission,
            pendingLedger,
            unpaidPayout));
    }
}
