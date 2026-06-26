namespace RallyAPI.SharedKernel.Abstractions.Riders;

/// <summary>
/// Diagnostic report explaining whether a rider is eligible to receive a delivery
/// offer for a given pickup location, and exactly which gate(s) failed if not.
/// Mirrors the filters applied by <see cref="IRiderQueryService.GetAvailableRidersAsync"/>.
/// </summary>
public sealed record RiderEligibilityReport
{
    public required Guid RiderId { get; init; }
    public string? RiderName { get; init; }

    /// <summary>False when no rider exists with this id.</summary>
    public bool Found { get; init; }

    /// <summary>True only when every check passed — i.e. the rider would receive an offer.</summary>
    public bool Eligible { get; init; }

    /// <summary>Straight-line distance from the rider's last known location to the pickup, in km.</summary>
    public double? DistanceToPickupKm { get; init; }

    /// <summary>Every gate evaluated, in dispatch order.</summary>
    public IReadOnlyList<RiderEligibilityCheck> Checks { get; init; } = [];

    /// <summary>Names of the gates that failed (empty when eligible).</summary>
    public IReadOnlyList<string> FailedChecks { get; init; } = [];
}

/// <summary>
/// A single eligibility gate result.
/// </summary>
public sealed record RiderEligibilityCheck
{
    public required string Name { get; init; }
    public required bool Passed { get; init; }

    /// <summary>The rider's actual value for this gate (human-readable).</summary>
    public string? Actual { get; init; }

    /// <summary>What the gate requires to pass (human-readable).</summary>
    public string? Requirement { get; init; }
}
