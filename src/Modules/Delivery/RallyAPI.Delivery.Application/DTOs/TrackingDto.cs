namespace RallyAPI.Delivery.Application.DTOs;

public sealed record TrackingDto
{
    public string OrderNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;

    // Rider (if assigned)
    public TrackingRiderDto? Rider { get; init; }

    // ETA
    public string? Eta { get; init; }
    public int? EtaMinutes { get; init; }

    // Timeline
    public IReadOnlyList<TrackingTimelineItem> Timeline { get; init; } = [];
}

public sealed record TrackingRiderDto
{
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public bool IsOwnFleet { get; init; }
}

public sealed record TrackingTimelineItem
{
    public string Status { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public DateTime? At { get; init; }
    public bool IsDone { get; init; }
    public bool IsCurrent { get; init; }
}