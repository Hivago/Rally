using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetDashboardStats;

internal sealed class GetDashboardStatsQueryHandler
    : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRiderRepository _riderRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IOrderStatsService _orderStats;

    public GetDashboardStatsQueryHandler(
        IAdminRepository adminRepository,
        IRiderRepository riderRepository,
        IRestaurantRepository restaurantRepository,
        IOrderStatsService orderStats)
    {
        _adminRepository = adminRepository;
        _riderRepository = riderRepository;
        _restaurantRepository = restaurantRepository;
        _orderStats = orderStats;
    }

    public async Task<Result<DashboardStatsResponse>> Handle(
        GetDashboardStatsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<DashboardStatsResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        // Sequential — repos share UsersDbContext; OrderStats uses OrdersDbContext.
        // EF Core does not support concurrent ops on the same context.
        var activeOrders = await _orderStats.GetActiveCountAsync(cancellationToken);
        var todayOrders = await _orderStats.GetTodayCountAsync(cancellationToken);
        var onlineRiders = await _riderRepository.CountAsync(isOnline: true, cancellationToken: cancellationToken);
        var pendingKyc = await _riderRepository.CountPendingKycAsync(cancellationToken);
        var activeRestaurants = await _restaurantRepository.CountActiveAsync(cancellationToken);

        return Result.Success(new DashboardStatsResponse(
            activeOrders,
            todayOrders,
            onlineRiders,
            pendingKyc,
            activeRestaurants));
    }
}
