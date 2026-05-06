using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetRiderOverview;

public sealed record GetRiderOverviewQuery(
    Guid RequestedByAdminId,
    Guid RiderId) : IRequest<Result<RiderOverviewResponse>>;

public sealed record RiderOverviewResponse(
    Guid RiderId,
    string Name,
    string Phone,
    string? Email,
    string VehicleType,
    string? VehicleNumber,
    string KycStatus,
    bool IsActive,
    bool IsOnline,
    bool IsAvailableForDelivery,
    Guid? CurrentDeliveryId,
    DateTime? CurrentDeliveryAssignedAt,
    decimal? CurrentLatitude,
    decimal? CurrentLongitude,
    DateTime? LastLocationUpdate,
    DateTime JoinedAt,

    // Delivery stats — sourced from Orders module via IRiderOrderStatsService
    int TotalDeliveries,
    int CompletedDeliveries,
    int CancelledDeliveries,
    int OngoingDeliveries,

    // Earnings — sourced from RiderPayoutLedger
    decimal TotalEarnings,
    decimal PendingEarnings,
    decimal EarningsThisWeek,
    decimal EarningsThisMonth,

    // Ratings — placeholder until rider ratings module ships
    decimal AverageRating,
    int TotalRatings);
