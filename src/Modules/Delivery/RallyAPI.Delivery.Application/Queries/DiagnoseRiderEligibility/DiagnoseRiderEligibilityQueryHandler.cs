using MediatR;
using Microsoft.Extensions.Options;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Application.Services;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Queries.DiagnoseRiderEligibility;

public sealed class DiagnoseRiderEligibilityQueryHandler
    : IRequestHandler<DiagnoseRiderEligibilityQuery, Result<RiderEligibilityDiagnosticsDto>>
{
    private readonly IRiderQueryService _riderQueryService;
    private readonly DispatchOptions _dispatchOptions;

    public DiagnoseRiderEligibilityQueryHandler(
        IRiderQueryService riderQueryService,
        IOptions<DispatchOptions> dispatchOptions)
    {
        _riderQueryService = riderQueryService;
        _dispatchOptions = dispatchOptions.Value;
    }

    public async Task<Result<RiderEligibilityDiagnosticsDto>> Handle(
        DiagnoseRiderEligibilityQuery request,
        CancellationToken cancellationToken)
    {
        // Default to the same radius real dispatch uses, so the diagnostic matches
        // production behavior unless the caller deliberately widens it.
        var radiusKm = request.RadiusKm ?? _dispatchOptions.SearchRadiusKm;

        IReadOnlyList<RiderEligibilityReport> reports = request.RiderId.HasValue
            ? new[]
            {
                await _riderQueryService.DiagnoseEligibilityAsync(
                    request.RiderId.Value,
                    request.PickupLatitude,
                    request.PickupLongitude,
                    radiusKm,
                    cancellationToken)
            }
            : await _riderQueryService.DiagnoseAllRidersAsync(
                request.PickupLatitude,
                request.PickupLongitude,
                radiusKm,
                ct: cancellationToken);

        return Result.Success(new RiderEligibilityDiagnosticsDto
        {
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            RadiusKm = radiusKm,
            TotalEvaluated = reports.Count,
            EligibleCount = reports.Count(r => r.Eligible),
            Riders = reports
        });
    }
}
