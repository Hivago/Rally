using MediatR;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Queries.DiagnoseRiderEligibility;

/// <summary>
/// Diagnoses why riders would or would not receive a delivery offer for a pickup
/// location. When <see cref="RiderId"/> is set, reports just that rider; otherwise
/// sweeps all riders. <see cref="RadiusKm"/> defaults to the dispatch search radius.
/// </summary>
public sealed record DiagnoseRiderEligibilityQuery(
    double PickupLatitude,
    double PickupLongitude,
    double? RadiusKm,
    Guid? RiderId) : IRequest<Result<RiderEligibilityDiagnosticsDto>>;
