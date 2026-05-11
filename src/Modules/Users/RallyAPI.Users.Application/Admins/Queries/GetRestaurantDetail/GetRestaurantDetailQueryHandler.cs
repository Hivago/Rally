using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Admins.Queries.GetRestaurantDetail;

internal sealed class GetRestaurantDetailQueryHandler
    : IRequestHandler<GetRestaurantDetailQuery, Result<RestaurantDetailResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantOwnerRepository _ownerRepository;

    public GetRestaurantDetailQueryHandler(
        IAdminRepository adminRepository,
        IRestaurantRepository restaurantRepository,
        IRestaurantOwnerRepository ownerRepository)
    {
        _adminRepository = adminRepository;
        _restaurantRepository = restaurantRepository;
        _ownerRepository = ownerRepository;
    }

    public async Task<Result<RestaurantDetailResponse>> Handle(
        GetRestaurantDetailQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<RestaurantDetailResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var restaurant = await _restaurantRepository.GetByIdWithScheduleAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure<RestaurantDetailResponse>(Error.NotFound("Restaurant", request.RestaurantId));

        RestaurantOwner? owner = null;
        if (restaurant.OwnerId.HasValue)
            owner = await _ownerRepository.GetByIdAsync(restaurant.OwnerId.Value, cancellationToken);

        return Result.Success(Map(restaurant, owner));
    }

    private static RestaurantDetailResponse Map(Restaurant r, RestaurantOwner? owner) =>
        new(
            r.Id,
            r.Name,
            r.Phone.Value,
            r.Email.Value,
            r.AddressLine,
            r.Latitude,
            r.Longitude,
            r.IsActive,
            r.IsAcceptingOrders,
            r.AutoAcceptOrders,
            r.AvgPrepTimeMins,
            r.OpeningTime,
            r.ClosingTime,
            r.UseCustomSchedule,
            // r.CommissionPercentage,   // Deprecated: percentage commission no longer surfaced. Only flat fee is used.
            r.CommissionFlatFee,
            r.MinOrderAmount,
            r.FssaiNumber,
            r.Description,
            r.LogoUrl,
            r.DietaryType.ToString(),
            r.DeliveryMode.ToString(),
            r.IsPureVeg,
            r.IsVeganFriendly,
            r.HasJainOptions,
            r.CuisineTypes?.ToList() ?? new(),
            r.OwnerId,
            owner is null ? null : new RestaurantOwnerSummary(
                owner.Id,
                owner.Name,
                owner.Email.Value,
                owner.Phone.Value,
                owner.PanNumber,
                owner.GstNumber,
                owner.BankAccountNumber,
                owner.BankIfscCode,
                owner.BankAccountName,
                owner.IsActive),
            new NotificationPreferencesSummary(
                r.Notifications.EmailAlerts,
                r.Notifications.BrowserNotifications,
                r.Notifications.OrderSound),
            r.ScheduleSlots
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.OpensAt)
                .Select(s => new ScheduleSlotSummary(s.DayOfWeek, s.OpensAt, s.ClosesAt))
                .ToList(),
            r.CreatedAt,
            r.UpdatedAt);
}
