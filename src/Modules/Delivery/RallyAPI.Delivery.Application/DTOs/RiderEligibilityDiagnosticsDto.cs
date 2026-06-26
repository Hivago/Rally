using RallyAPI.SharedKernel.Abstractions.Riders;

namespace RallyAPI.Delivery.Application.DTOs;

/// <summary>
/// Result of the rider-eligibility diagnostic: every evaluated rider plus a summary,
/// so ops can see who would receive an offer for a pickup and why the rest are excluded.
/// </summary>
public sealed record RiderEligibilityDiagnosticsDto
{
    public required double PickupLatitude { get; init; }
    public required double PickupLongitude { get; init; }
    public required double RadiusKm { get; init; }
    public required int TotalEvaluated { get; init; }
    public required int EligibleCount { get; init; }
    public required IReadOnlyList<RiderEligibilityReport> Riders { get; init; }
}
