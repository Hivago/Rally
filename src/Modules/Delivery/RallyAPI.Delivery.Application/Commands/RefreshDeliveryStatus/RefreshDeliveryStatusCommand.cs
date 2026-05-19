using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.RefreshDeliveryStatus;

/// <summary>
/// Manually pulls current state from the 3PL provider (ProRouting) and
/// reconciles both the DeliveryRequest and the Order. Used to recover orders
/// where a webhook was lost or where bridge handlers were missing when
/// transitions occurred.
/// </summary>
public sealed record RefreshDeliveryStatusCommand(
    Guid OrderId,
    Guid CallerId,
    bool IsAdmin) : IRequest<Result<RefreshDeliveryStatusResult>>;

public sealed record RefreshDeliveryStatusResult(
    Guid DeliveryRequestId,
    string DeliveryStatusBefore,
    string DeliveryStatusAfter,
    string? ProRoutingState,
    string? ProRoutingError,
    string? ExternalTaskId,
    string? RiderName,
    string? RiderPhone,
    string? TrackingUrl,
    int IntegrationEventsRepublished);
