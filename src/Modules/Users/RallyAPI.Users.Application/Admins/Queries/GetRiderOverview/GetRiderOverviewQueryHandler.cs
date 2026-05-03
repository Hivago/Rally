using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRiderOverview;

internal sealed class GetRiderOverviewQueryHandler
    : IRequestHandler<GetRiderOverviewQuery, Result<RiderOverviewResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRiderRepository _riderRepository;

    public GetRiderOverviewQueryHandler(
        IAdminRepository adminRepository,
        IRiderRepository riderRepository)
    {
        _adminRepository = adminRepository;
        _riderRepository = riderRepository;
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
            JoinedAt: rider.CreatedAt);
    }
}
