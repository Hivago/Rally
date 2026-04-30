namespace RallyAPI.Users.Application.Queries.RiderOverview;

public record RiderOverviewResponseDTO(
    Guid RiderId,
    string FullName,
    string Email,
    string PhoneNumber,
    string Status,             // e.g. Online, Offline, Busy
    bool IsVerified,
    bool IsActive,
    DateTime JoinedAt,
    string? VehicleType,
    string? VehiclePlateNumber,

    // Performance / stats
    int TotalDeliveries,
    int CompletedDeliveries,
    int CancelledDeliveries,
    int OngoingDeliveries,
    decimal AverageRating,
    int TotalRatings,

    // Earnings
    decimal TotalEarnings,
    decimal PendingEarnings,
    decimal EarningsThisWeek,
    decimal EarningsThisMonth,

    // Location (last known)
    double? LastKnownLatitude,
    double? LastKnownLongitude,
    DateTime? LastActiveAt
);