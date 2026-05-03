using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRiderOverview;

internal sealed class GetRiderOverviewQueryHandler
    : IRequestHandler<GetRiderOverviewQuery, Result<RiderOverviewResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRiderRepository _riderRepository;
    private readonly IRiderOrderStatsService _riderOrderStats;
    private readonly IRiderPayoutLedgerRepository _riderPayouts;

    public GetRiderOverviewQueryHandler(
        IAdminRepository adminRepository,
        IRiderRepository riderRepository,
        IRiderOrderStatsService riderOrderStats,
        IRiderPayoutLedgerRepository riderPayouts)
    {
        _adminRepository = adminRepository;
        _riderRepository = riderRepository;
        _riderOrderStats = riderOrderStats;
        _riderPayouts = riderPayouts;
    }

    public async Task<Result<RiderOverviewResponse>> Handle(
        GetRiderOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<RiderOverviewResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var rider = await _riderRepository.GetByIdAsync(request.RiderId, cancellationToken);
        if (rider is null)
            return Result.Failure<RiderOverviewResponse>(Error.NotFound("Rider", request.RiderId));

        var deliveryStats = await _riderOrderStats.GetDeliveryStatsAsync(rider.Id, cancellationToken);
        var earnings = await _riderPayouts.GetEarningsBreakdownAsync(rider.Id, DateTime.UtcNow, cancellationToken);

        return new RiderOverviewResponse(
            RiderId: rider.Id,
            Name: rider.Name,
            Phone: rider.Phone.GetFormatted(),
            Email: rider.Email?.Value,
            VehicleType: rider.VehicleType.ToString(),
            VehicleNumber: rider.VehicleNumber,
            KycStatus: rider.KycStatus.ToString(),
            IsActive: rider.IsActive,
            IsOnline: rider.IsOnline,
            IsAvailableForDelivery: rider.IsAvailableForDelivery(),
            CurrentDeliveryId: rider.CurrentDeliveryId,
            CurrentDeliveryAssignedAt: rider.CurrentDeliveryAssignedAt,
            CurrentLatitude: rider.CurrentLatitude,
            CurrentLongitude: rider.CurrentLongitude,
            LastLocationUpdate: rider.LastLocationUpdate,
            JoinedAt: rider.CreatedAt,

            TotalDeliveries: deliveryStats.Total,
            CompletedDeliveries: deliveryStats.Completed,
            CancelledDeliveries: deliveryStats.Cancelled,
            OngoingDeliveries: deliveryStats.Ongoing,

            TotalEarnings: earnings.Total,
            PendingEarnings: earnings.Pending,
            EarningsThisWeek: earnings.ThisWeek,
            EarningsThisMonth: earnings.ThisMonth,

            // No rider ratings module yet — returning zeros is honest.
            // Will populate when the ratings aggregate ships.
            AverageRating: 0m,
            TotalRatings: 0);
    }
}
