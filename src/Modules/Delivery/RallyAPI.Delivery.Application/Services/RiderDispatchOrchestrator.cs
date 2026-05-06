using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.Abstractions.Riders;

namespace RallyAPI.Delivery.Application.Services;

public sealed class RiderDispatchOrchestrator
{
    private readonly IRiderQueryService _riderQueryService;
    private readonly IRiderNotificationService _notificationService;
    private readonly IThirdPartyDeliveryProvider _thirdPartyProvider;
    private readonly IDeliveryRequestRepository _requestRepository;
    private readonly DispatchOptions _options;
    private readonly ILogger<RiderDispatchOrchestrator> _logger;

    public RiderDispatchOrchestrator(
        IRiderQueryService riderQueryService,
        IRiderNotificationService notificationService,
        IThirdPartyDeliveryProvider thirdPartyProvider,
        IDeliveryRequestRepository requestRepository,
        IOptions<DispatchOptions> options,
        ILogger<RiderDispatchOrchestrator> logger)
    {
        _riderQueryService = riderQueryService;
        _notificationService = notificationService;
        _thirdPartyProvider = thirdPartyProvider;
        _requestRepository = requestRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(
        DeliveryRequest deliveryRequest,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting dispatch for delivery {DeliveryId}",
            deliveryRequest.Id);

        // If explicitly falling back to Own Fleet, bypass 3PL entirely
        if (deliveryRequest.Status == DeliveryRequestStatus.SearchingOwnFleet)
        {
             _logger.LogInformation("Skipping 3PL and assigning via Own Fleet directly for delivery {DeliveryId}.", deliveryRequest.Id);
             return await AssignViaOwnFleetAsync(deliveryRequest, ct);
        }

        // Start searching 3PL first priority
        if (deliveryRequest.Status == DeliveryRequestStatus.Created || deliveryRequest.Status == DeliveryRequestStatus.PendingDispatch)
        {
            deliveryRequest.StartSearching3PL();
            await _requestRepository.UpdateAsync(deliveryRequest, ct);
        }

        var dispatchResult = await AssignVia3PLAsync(deliveryRequest, ct);

        if (!dispatchResult.IsSuccess)
        {
            _logger.LogInformation("3PL assignment failed or timed out. Falling back to Own Fleet for delivery {DeliveryId}", deliveryRequest.Id);
            return await AssignViaOwnFleetAsync(deliveryRequest, ct);
        }

        return dispatchResult;
    }

    private async Task<DispatchResult> AssignViaOwnFleetAsync(
        DeliveryRequest deliveryRequest,
        CancellationToken ct)
    {
        if (deliveryRequest.Status != DeliveryRequestStatus.SearchingOwnFleet)
        {
            deliveryRequest.TransitionToOwnFleetSearch();
            await _requestRepository.UpdateAsync(deliveryRequest, ct);
        }

        // Get available riders
        var riders = await _riderQueryService.GetAvailableRidersAsync(
            deliveryRequest.PickupLatitude,
            deliveryRequest.PickupLongitude,
            _options.SearchRadiusKm,
            _options.MaxRidersToTry,
            ct);

        _logger.LogDebug("Found {Count} available riders", riders.Count);

        if (!riders.Any())
        {
            _logger.LogInformation("No riders available in Own Fleet");
            deliveryRequest.MarkFailed(DeliveryFailureReason.NoRidersAvailable, "All dispatch options exhausted (ProRouting failed/timed out and Own Fleet unavailable)");
            await _requestRepository.UpdateAsync(deliveryRequest, ct);
            return DispatchResult.Failed("No riders available after exhausting all options.");
        }

        // Sequential notification
        foreach (var rider in riders)
        {
            var offer = deliveryRequest.CreateOffer(
                rider.RiderId,
                CalculateEarnings(deliveryRequest.QuotedPrice),
                _options.AcceptanceTimeoutSeconds,
                rider.Latitude,
                rider.Longitude,
                (decimal)rider.DistanceToPickupKm);

            await _requestRepository.UpdateAsync(deliveryRequest, ct);

            // Send notification
            var notification = new DeliveryOfferNotification
            {
                OfferId = offer.Id,
                DeliveryRequestId = deliveryRequest.Id,
                OrderNumber = deliveryRequest.OrderNumber,
                RestaurantName = deliveryRequest.PickupContactName,
                PickupAddress = deliveryRequest.PickupAddress,
                PickupLatitude = deliveryRequest.PickupLatitude,
                PickupLongitude = deliveryRequest.PickupLongitude,
                DropAddress = deliveryRequest.DropAddress,
                DropLatitude = deliveryRequest.DropLatitude,
                DropLongitude = deliveryRequest.DropLongitude,
                DistanceToPickupKm = (decimal)rider.DistanceToPickupKm,
                DistanceToDropKm = deliveryRequest.DistanceKm ?? 0,
                Earnings = offer.Earnings,
                ExpiresInSeconds = _options.AcceptanceTimeoutSeconds,
                CreatedAt = offer.OfferedAt,
                ExpiresAt = offer.ExpiresAt,
                IsFoodReady = false
            };

            var notifyResult = await _notificationService.SendDeliveryOfferAsync(
                rider.RiderId, notification, ct);

            if (notifyResult.IsSuccess)
            {
                offer.MarkNotificationSent();
            }

            _logger.LogDebug(
                "Sent offer to rider {RiderId}, waiting {Timeout}s",
                rider.RiderId, _options.AcceptanceTimeoutSeconds);

            // Wait for response
            await Task.Delay(TimeSpan.FromSeconds(_options.AcceptanceTimeoutSeconds), ct);

            // Reload to check if accepted
            deliveryRequest = (await _requestRepository.GetByIdWithOffersAsync(deliveryRequest.Id, ct))!;

            if (deliveryRequest.Status == DeliveryRequestStatus.RiderAssigned)
            {
                _logger.LogInformation(
                    "Rider {RiderId} accepted delivery {DeliveryId}",
                    rider.RiderId, deliveryRequest.Id);

                return DispatchResult.Success(FleetType.OwnFleet, rider.RiderId);
            }

            // Mark offer as expired if still pending
            var currentOffer = deliveryRequest.RiderOffers.First(o => o.Id == offer.Id);
            if (currentOffer.Status == RiderOfferStatus.Pending)
            {
                currentOffer.Expire();
                await _requestRepository.UpdateAsync(deliveryRequest, ct);
            }
        }

        // All riders exhausted
        _logger.LogInformation(
            "All {Count} Own Fleet riders exhausted",
            riders.Count);

        deliveryRequest.MarkFailed(DeliveryFailureReason.NoRidersAvailable, "All dispatch options exhausted (ProRouting failed/timed out and Own Fleet declined)");
        await _requestRepository.UpdateAsync(deliveryRequest, ct);

        return DispatchResult.Failed("All riders exhausted");
    }

    private async Task<DispatchResult> AssignVia3PLAsync(
        DeliveryRequest deliveryRequest,
        CancellationToken ct)
    {
        var createResult = await _thirdPartyProvider.CreateTaskAsync(
            new CreateTaskRequest
            {
                OrderId = deliveryRequest.OrderId,
                OrderNumber = deliveryRequest.OrderNumber,
                DeliveryRequestId = deliveryRequest.Id,
                PickupLatitude = deliveryRequest.PickupLatitude,
                PickupLongitude = deliveryRequest.PickupLongitude,
                PickupPincode = deliveryRequest.PickupPincode,
                PickupAddressLine1 = deliveryRequest.PickupAddress,
                PickupCity = "City", // TODO: Get from order
                PickupState = "State",
                PickupContactName = deliveryRequest.PickupContactName,
                PickupContactPhone = deliveryRequest.PickupContactPhone,
                DropLatitude = deliveryRequest.DropLatitude,
                DropLongitude = deliveryRequest.DropLongitude,
                DropPincode = deliveryRequest.DropPincode,
                DropAddressLine1 = deliveryRequest.DropAddress,
                DropCity = "City",
                DropState = "State",
                DropContactName = deliveryRequest.DropContactName,
                DropContactPhone = deliveryRequest.DropContactPhone,
                OrderAmount = deliveryRequest.QuotedPrice,
                IsOrderReady = true,
                PickupCode = deliveryRequest.PickupCode,
                DropCode = deliveryRequest.DropCode,
                OrderCategory = MapOrderCategory(deliveryRequest.OrderCategory),
                CallbackUrl = _options.WebhookUrl,
                SelectionMode = "fastest_agent"
            }, ct);

        if (!createResult.IsSuccess)
        {
            _logger.LogError(
                "3PL booking failed for delivery {DeliveryId}: {Error}",
                deliveryRequest.Id, createResult.ErrorMessage);

            return DispatchResult.Failed(createResult.ErrorMessage ?? "3PL booking failed");
        }

        _logger.LogInformation(
            "3PL task created for delivery {DeliveryId}: {TaskId}. Waiting {Timeout}s for assignment.",
            deliveryRequest.Id, createResult.TaskId, _options.AcceptanceTimeoutSeconds);

        // Wait for webhook assignment
        await Task.Delay(TimeSpan.FromSeconds(_options.AcceptanceTimeoutSeconds), ct);

        // Reload to get the freshest state (avoid race condition with late webhook)
        deliveryRequest = (await _requestRepository.GetByIdWithOffersAsync(deliveryRequest.Id, ct))!;

        if (deliveryRequest.Status == DeliveryRequestStatus.Searching3PL)
        {
            _logger.LogWarning("ProRouting timeout reached, cancelling 3PL and falling back...");
            await _thirdPartyProvider.CancelTaskAsync(createResult.TaskId!, "Rider assignment timeout from Orchestrator", ct);
            return DispatchResult.Failed("3PL assignment timed out");
        }

        // It either got Assigned3PL or some other status (e.g. Cancelled/Failed via webhook)
        if (deliveryRequest.Status == DeliveryRequestStatus.Assigned3PL)
        {
             return DispatchResult.Success(FleetType.ThirdParty, null, createResult.TaskId);
        }
        else 
        {
             // If the status is not Assigned3PL and not Searching3PL, it means the webhook explicitly failed/cancelled it already. 
             // We return a failure here so we fallback to Own Fleet. 
            return DispatchResult.Failed($"3PL webhook returned alternative status: {deliveryRequest.Status}");
        }
    }

    private decimal CalculateEarnings(decimal deliveryFee)
    {
        // Rider gets X% of delivery fee
        return Math.Round(deliveryFee * _options.RiderEarningsPercentage / 100, 2);
    }

    private static string MapOrderCategory(OrderCategory category) => category switch
    {
        OrderCategory.FoodAndBeverage => "F&B",
        OrderCategory.Grocery => "Grocery",
        OrderCategory.Pharma => "Pharma",
        _ => "F&B"
    };
}

public sealed record DispatchResult
{
    public bool IsSuccess { get; init; }
    public FleetType? FleetType { get; init; }
    public Guid? RiderId { get; init; }
    public string? ExternalTaskId { get; init; }
    public string? ErrorMessage { get; init; }

    public static DispatchResult Success(FleetType fleetType, Guid? riderId, string? taskId = null) =>
        new() { IsSuccess = true, FleetType = fleetType, RiderId = riderId, ExternalTaskId = taskId };

    public static DispatchResult Failed(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };
}

public sealed class DispatchOptions
{
    public const string SectionName = "Delivery:Dispatch";

    public double SearchRadiusKm { get; set; } = 5.0;
    public int MaxRidersToTry { get; set; } = 10;
    public int AcceptanceTimeoutSeconds { get; set; } = 30;
    public decimal RiderEarningsPercentage { get; set; } = 80;
    public string WebhookUrl { get; set; } = "https://your-domain.com/api/webhooks/prorouting";
}