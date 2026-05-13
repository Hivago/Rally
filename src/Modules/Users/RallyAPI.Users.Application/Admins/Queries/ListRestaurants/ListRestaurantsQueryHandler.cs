using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Admins.Queries.ListRestaurants;

internal sealed class ListRestaurantsQueryHandler
    : IRequestHandler<ListRestaurantsQuery, Result<ListRestaurantsResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IOrderStatsService _orderStats;

    public ListRestaurantsQueryHandler(
        IAdminRepository adminRepository,
        IRestaurantRepository restaurantRepository,
        IOrderStatsService orderStats)
    {
        _adminRepository = adminRepository;
        _restaurantRepository = restaurantRepository;
        _orderStats = orderStats;
    }

    public async Task<Result<ListRestaurantsResponse>> Handle(
        ListRestaurantsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<ListRestaurantsResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, total) = await _restaurantRepository.GetPagedAsync(
            request.IsActive,
            request.Search,
            page,
            pageSize,
            cancellationToken);

        var restaurantIds = items.Select(r => r.Id).ToList();
        var orderCounts = await _orderStats.GetOrderCountsByRestaurantAsync(restaurantIds, cancellationToken);

        var mapped = items
            .Select(r => new RestaurantListItem(
                r.Id,
                r.RstCode,
                r.Name,
                r.Phone.Value,
                r.Email.Value,
                r.IsActive,
                r.IsAcceptingOrders,
                r.AcceptsPickup,
                // r.CommissionPercentage,   // Deprecated: percentage commission no longer surfaced. Only flat fee is used.
                r.CommissionFlatFee,
                r.OwnerId,
                orderCounts.TryGetValue(r.Id, out var count) ? count : 0,
                FormatOperatingHours(r),
                r.CreatedAt))
            .ToList();

        return Result.Success(new ListRestaurantsResponse(mapped, total, page, pageSize));
    }

    private static string FormatOperatingHours(Restaurant r) =>
        r.UseCustomSchedule
            ? "Custom weekly schedule"
            : $"{r.OpeningTime:HH\\:mm} – {r.ClosingTime:HH\\:mm}";
}
