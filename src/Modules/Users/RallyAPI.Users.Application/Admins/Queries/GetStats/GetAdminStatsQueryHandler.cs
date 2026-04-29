using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetStats;

internal sealed class GetAdminStatsQueryHandler
    : IRequestHandler<GetAdminStatsQuery, Result<AdminStatsResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRiderRepository _riderRepository;
    private readonly IOrderStatsService _orderStats;

    public GetAdminStatsQueryHandler(
        IAdminRepository adminRepository,
        ICustomerRepository customerRepository,
        IRestaurantRepository restaurantRepository,
        IRiderRepository riderRepository,
        IOrderStatsService orderStats)
    {
        _adminRepository = adminRepository;
        _customerRepository = customerRepository;
        _restaurantRepository = restaurantRepository;
        _riderRepository = riderRepository;
        _orderStats = orderStats;
    }

    public async Task<Result<AdminStatsResponse>> Handle(
        GetAdminStatsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<AdminStatsResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var startOfDay = DateTime.UtcNow.Date;

        // Sequential. Repos share UsersDbContext and OrderStatsService uses OrdersDbContext;
        // EF Core does not support concurrent operations on the same DbContext, so we run
        // these one at a time. The total wall time is still well under 100ms in practice.
        var totalCustomers = await _customerRepository.CountAsync(cancellationToken);
        var totalRestaurants = await _restaurantRepository.CountAsync(cancellationToken);
        var totalRiders = await _riderRepository.CountAsync(cancellationToken: cancellationToken);
        var onlineRiders = await _riderRepository.CountAsync(isOnline: true, cancellationToken: cancellationToken);
        var totalOrders = await _orderStats.GetTotalCountAsync(cancellationToken);
        var activeOrders = await _orderStats.GetActiveCountAsync(cancellationToken);
        var todayOrders = await _orderStats.GetTodayCountAsync(cancellationToken);
        var todayRevenue = await _orderStats.GetRevenueSinceAsync(startOfDay, cancellationToken);
        var todayCommission = await _orderStats.GetCommissionSinceAsync(startOfDay, cancellationToken);
        var lifetimeRevenue = await _orderStats.GetLifetimeRevenueAsync(cancellationToken);
        var lifetimeCommission = await _orderStats.GetLifetimeCommissionAsync(cancellationToken);
        var pendingLedger = await _orderStats.GetPendingLedgerEntriesCountAsync(cancellationToken);
        var unpaidPayout = await _orderStats.GetUnpaidPayoutAmountAsync(cancellationToken);

        return Result.Success(new AdminStatsResponse(
            totalCustomers,
            totalRestaurants,
            totalRiders,
            onlineRiders,
            totalOrders,
            activeOrders,
            todayOrders,
            todayRevenue,
            todayCommission,
            lifetimeRevenue,
            lifetimeCommission,
            pendingLedger,
            unpaidPayout));
    }
}
