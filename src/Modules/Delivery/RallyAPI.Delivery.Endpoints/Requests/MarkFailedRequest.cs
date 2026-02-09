using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Endpoints.Requests;

public sealed record MarkFailedRequest
{
    public required DeliveryFailureReason Reason { get; init; }
    public string? Notes { get; init; }
    public string? PhotoUrl { get; init; }
}
