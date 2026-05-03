using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Queries.RiderOverview;

public class RiderOverviewQueryHandler
    : IRequestHandler<RiderOverviewQuery, Result<RiderOverviewResponseDTO>>
{
    private readonly IRiderRepository _riders;
    private readonly IRiderOrderStatsService _orderStats;
    private readonly IRiderPayoutLedgerRepository _payouts;

    public RiderOverviewQueryHandler(
        IRiderRepository riders,
        IRiderOrderStatsService orderStats,
        IRiderPayoutLedgerRepository payouts)
    {
        _riders = riders;
        _orderStats = orderStats;
        _payouts = payouts;
    }

    public async Task<Result<RiderOverviewResponseDTO>> Handle(
        RiderOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var rider = await _riders.GetByIdAsync(request.RiderId, cancellationToken);

        if (rider is null)
            return Result.Failure<RiderOverviewResponseDTO>(Error.NotFound("Rider", request.RiderId));

        var deliveries = await _orderStats.GetDeliveryStatsAsync(rider.Id, cancellationToken);
        var earnings = await _payouts.GetEarningsBreakdownAsync(rider.Id, DateTime.UtcNow, cancellationToken);

        var status = !rider.IsActive ? "Inactive" : (rider.IsOnline ? "Online" : "Offline");

        // Ratings: no Ratings module exists yet. Returning zeros until rider rating
        // collection ships (tracked separately).
        var response = new RiderOverviewResponseDTO(
            RiderId: rider.Id,
            FullName: rider.Name,
            Email: rider.Email?.Value ?? string.Empty,
            PhoneNumber: rider.Phone.Value,
            Status: status,
            IsVerified: rider.KycStatus == KycStatus.Verified,
            IsActive: rider.IsActive,
            JoinedAt: rider.CreatedAt,
            VehicleType: rider.VehicleType.ToString(),
            VehiclePlateNumber: rider.VehicleNumber,

            TotalDeliveries: deliveries.Total,
            CompletedDeliveries: deliveries.Completed,
            CancelledDeliveries: deliveries.Cancelled,
            OngoingDeliveries: deliveries.Ongoing,
            AverageRating: 0m,
            TotalRatings: 0,

            TotalEarnings: earnings.Total,
            PendingEarnings: earnings.Pending,
            EarningsThisWeek: earnings.ThisWeek,
            EarningsThisMonth: earnings.ThisMonth,

            LastKnownLatitude: rider.CurrentLatitude.HasValue ? (double?)rider.CurrentLatitude.Value : null,
            LastKnownLongitude: rider.CurrentLongitude.HasValue ? (double?)rider.CurrentLongitude.Value : null,
            LastActiveAt: rider.LastLocationUpdate
        );

        return Result.Success(response);
    }
}
