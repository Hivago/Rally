using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetRestaurantDetail;

public sealed record GetRestaurantDetailQuery(
    Guid RequestedByAdminId,
    Guid RestaurantId) : IRequest<Result<RestaurantDetailResponse>>;

public sealed record RestaurantDetailResponse(
    Guid Id,
    string Name,
    string Phone,
    string Email,
    string AddressLine,
    decimal Latitude,
    decimal Longitude,
    bool IsActive,
    bool IsAcceptingOrders,
    bool AutoAcceptOrders,
    int AvgPrepTimeMins,
    TimeOnly OpeningTime,
    TimeOnly ClosingTime,
    bool UseCustomSchedule,
    // decimal CommissionPercentage,   // Deprecated: percentage commission no longer surfaced. Only flat fee is used.
    decimal CommissionFlatFee,
    decimal MinOrderAmount,
    string? FssaiNumber,
    string? Description,
    string? LogoUrl,
    string DietaryType,
    string DeliveryMode,
    bool IsPureVeg,
    bool IsVeganFriendly,
    bool HasJainOptions,
    List<string> CuisineTypes,
    Guid? OwnerId,
    RestaurantOwnerSummary? Owner,
    NotificationPreferencesSummary Notifications,
    List<ScheduleSlotSummary> Schedule,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record RestaurantOwnerSummary(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string? PanNumber,
    string? GstNumber,
    string? BankAccountNumber,
    string? BankIfscCode,
    string? BankAccountName,
    bool IsActive);

public sealed record NotificationPreferencesSummary(
    bool EmailAlerts,
    bool BrowserNotifications,
    bool OrderSound);

public sealed record ScheduleSlotSummary(
    DayOfWeek DayOfWeek,
    TimeOnly OpensAt,
    TimeOnly ClosesAt);
