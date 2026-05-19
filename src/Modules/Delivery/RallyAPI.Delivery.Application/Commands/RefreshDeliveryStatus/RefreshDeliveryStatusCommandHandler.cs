using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.RefreshDeliveryStatus;

internal sealed class RefreshDeliveryStatusCommandHandler
    : IRequestHandler<RefreshDeliveryStatusCommand, Result<RefreshDeliveryStatusResult>>
{
    private readonly IDeliveryRequestRepository _repository;
    private readonly IThirdPartyDeliveryProvider _provider;
    private readonly IPublisher _publisher;
    private readonly ILogger<RefreshDeliveryStatusCommandHandler> _logger;

    public RefreshDeliveryStatusCommandHandler(
        IDeliveryRequestRepository repository,
        IThirdPartyDeliveryProvider provider,
        IPublisher publisher,
        ILogger<RefreshDeliveryStatusCommandHandler> logger)
    {
        _repository = repository;
        _provider = provider;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<Result<RefreshDeliveryStatusResult>> Handle(
        RefreshDeliveryStatusCommand request,
        CancellationToken cancellationToken)
    {
        var delivery = await _repository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (delivery is null)
            return Result.Failure<RefreshDeliveryStatusResult>(
                Error.NotFound("DeliveryRequest", request.OrderId));

        if (!request.IsAdmin)
        {
            if (delivery.RestaurantId != request.CallerId)
            {
                _logger.LogWarning(
                    "Restaurant {CallerId} attempted to refresh delivery for order {OrderId} owned by {OwnerId}",
                    request.CallerId, request.OrderId, delivery.RestaurantId);
                return Result.Failure<RefreshDeliveryStatusResult>(
                    Error.Validation("You are not authorized to refresh this order."));
            }
        }

        var statusBefore = delivery.Status;

        string? proroutingState = null;
        string? proroutingError = null;

        if (!string.IsNullOrEmpty(delivery.ExternalTaskId))
        {
            var statusResult = await _provider.GetTaskStatusAsync(
                delivery.ExternalTaskId,
                cancellationToken);

            if (statusResult.IsSuccess)
            {
                proroutingState = statusResult.State;
                ApplyStateTransition(delivery, statusResult, _logger);

                if (delivery.Status != statusBefore)
                {
                    await _repository.UpdateAsync(delivery, cancellationToken);
                }
            }
            else
            {
                proroutingError = statusResult.ErrorMessage;
                _logger.LogWarning(
                    "ProRouting status fetch failed for task {TaskId}: {Error}",
                    delivery.ExternalTaskId, statusResult.ErrorMessage);
            }
        }

        var republished = await ReconcileOrderFromDeliveryAsync(delivery, cancellationToken);

        _logger.LogInformation(
            "Refreshed delivery {DeliveryId} for order {OrderId}: {Before} -> {After} (republished {N} events)",
            delivery.Id, request.OrderId, statusBefore, delivery.Status, republished);

        return Result.Success(new RefreshDeliveryStatusResult(
            delivery.Id,
            statusBefore.ToString(),
            delivery.Status.ToString(),
            proroutingState,
            proroutingError,
            delivery.ExternalTaskId,
            delivery.RiderName ?? delivery.ExternalRiderName,
            delivery.RiderPhone ?? delivery.ExternalRiderPhone,
            delivery.ExternalTrackingUrl,
            republished));
    }

    private static void ApplyStateTransition(
        DeliveryRequest delivery,
        TaskStatusResult status,
        ILogger logger)
    {
        var stateNormalized = status.State?.Trim().ToLowerInvariant() ?? string.Empty;

        try
        {
            switch (stateNormalized)
            {
                case "agent-assigned":
                case "agent_assigned":
                    if (delivery.Status == DeliveryRequestStatus.Searching3PL
                        && !string.IsNullOrEmpty(delivery.ExternalTaskId))
                    {
                        delivery.Assign3PLRider(
                            delivery.ExternalTaskId,
                            delivery.ExternalLspName ?? "ProRouting",
                            status.RiderName,
                            status.RiderPhone,
                            status.TrackingUrl,
                            delivery.QuotedPrice);
                    }
                    else
                    {
                        delivery.Update3PLRiderInfo(status.RiderName, status.RiderPhone, status.TrackingUrl);
                    }
                    break;

                case "at-pickup":
                case "at_pickup":
                    if (delivery.Status is DeliveryRequestStatus.RiderAssigned
                        or DeliveryRequestStatus.Assigned3PL)
                    {
                        delivery.MarkRiderEnRoutePickup();
                    }
                    if (delivery.Status == DeliveryRequestStatus.RiderEnRoutePickup)
                    {
                        delivery.MarkRiderArrivedPickup();
                    }
                    break;

                case "picked-up":
                case "picked_up":
                case "order-picked-up":
                    delivery.MarkPickedUp();
                    break;

                case "at-delivery":
                case "at_delivery":
                    if (delivery.Status == DeliveryRequestStatus.PickedUp)
                        delivery.MarkRiderEnRouteDrop();
                    if (delivery.Status == DeliveryRequestStatus.RiderEnRouteDrop)
                        delivery.MarkRiderArrivedDrop();
                    break;

                case "delivered":
                case "order-delivered":
                    delivery.MarkDelivered();
                    break;

                default:
                    logger.LogDebug(
                        "Refresh: unhandled or non-actionable ProRouting state {State} for delivery {DeliveryId}",
                        status.State, delivery.Id);
                    break;
            }
        }
        catch (InvalidOperationException ex)
        {
            logger.LogInformation(
                "Refresh: ProRouting state {State} not applicable to delivery {DeliveryId} in status {Status}: {Reason}",
                status.State, delivery.Id, delivery.Status, ex.Message);
        }
    }

    /// <summary>
    /// Republishes the integration events implied by the current DeliveryRequest state.
    /// This catches Orders that fell behind because bridge handlers were missing when
    /// the original domain events fired. Orders-side handlers are idempotent (they
    /// catch InvalidOperationException on no-op transitions).
    /// </summary>
    private async Task<int> ReconcileOrderFromDeliveryAsync(
        DeliveryRequest delivery,
        CancellationToken cancellationToken)
    {
        var republished = 0;

        var hasRider = delivery.RiderId.HasValue
            || !string.IsNullOrEmpty(delivery.RiderName)
            || !string.IsNullOrEmpty(delivery.ExternalRiderName);

        if (hasRider)
        {
            var fleetType = delivery.FleetType ?? FleetType.ThirdParty;
            await _publisher.Publish(new DeliveryRiderAssignedIntegrationEvent(
                delivery.Id,
                delivery.OrderId,
                delivery.OrderNumber,
                fleetType == FleetType.OwnFleet,
                delivery.RiderId,
                delivery.RiderName ?? delivery.ExternalRiderName,
                delivery.RiderPhone ?? delivery.ExternalRiderPhone,
                delivery.ExternalTrackingUrl), cancellationToken);
            republished++;
        }

        if (delivery.Status >= DeliveryRequestStatus.PickedUp
            && delivery.Status != DeliveryRequestStatus.Cancelled
            && delivery.Status != DeliveryRequestStatus.Failed)
        {
            await _publisher.Publish(new DeliveryPickedUpIntegrationEvent(
                delivery.Id,
                delivery.OrderId,
                delivery.PickedUpAt ?? DateTime.UtcNow), cancellationToken);
            republished++;
        }

        if (delivery.Status == DeliveryRequestStatus.Delivered)
        {
            await _publisher.Publish(new DeliveryCompletedIntegrationEvent(
                delivery.Id,
                delivery.OrderId,
                delivery.DeliveredAt ?? DateTime.UtcNow), cancellationToken);
            republished++;
        }

        if (delivery.Status == DeliveryRequestStatus.Failed)
        {
            await _publisher.Publish(new DeliveryFailedIntegrationEvent(
                delivery.Id,
                delivery.OrderId,
                delivery.FailureReason?.ToString() ?? "Unknown",
                delivery.FailureNotes,
                delivery.FailedAt ?? DateTime.UtcNow), cancellationToken);
            republished++;
        }

        return republished;
    }
}
