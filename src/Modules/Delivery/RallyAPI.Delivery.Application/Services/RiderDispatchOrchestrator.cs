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
            "Starting dispatch for delivery {DeliveryId} (OwnFleetFirst={OwnFleetFirst})",
            deliveryRequest.Id, _options.OwnFleetFirst);

        return _options.OwnFleetFirst
            ? await DispatchOwnFleetFirstAsync(deliveryRequest, ct)
            : await DispatchThirdPartyFirstAsync(deliveryRequest, ct);
    }

    /// <summary>
    /// Own-fleet-first: broadcast the order to all eligible own riders at once and
    /// let the fastest to accept win. Falls back to 3PL only if none accept.
    /// </summary>
    private async Task<DispatchResult> DispatchOwnFleetFirstAsync(
        DeliveryRequest deliveryRequest,
        CancellationToken ct)
    {
        // If a prior/recovery run already handed this to 3PL, don't restart own-fleet.
        if (deliveryRequest.Status is DeliveryRequestStatus.Searching3PL or DeliveryRequestStatus.Assigned3PL)
        {
            return await AssignVia3PLAsync(deliveryRequest, ct);
        }

        var ownResult = await BroadcastToOwnFleetAsync(deliveryRequest, ct);
        if (ownResult.IsSuccess)
        {
            return ownResult;
        }

        _logger.LogInformation(
            "No own-fleet rider took delivery {DeliveryId}. Falling back to 3PL.",
            deliveryRequest.Id);

        // Reload fresh: the own-fleet phase probed status via GetCurrentStatusAsync and
        // may have detached the tracked entity on a concurrency-guarded write.
        var fresh = await _requestRepository.GetByIdAsync(deliveryRequest.Id, ct);
        if (fresh is null)
        {
            return DispatchResult.Failed("Delivery request no longer exists");
        }

        // A rider may have accepted right at the window boundary — never override it.
        if (fresh.Status >= DeliveryRequestStatus.RiderAssigned)
        {
            return DispatchResult.Success(fresh.FleetType ?? FleetType.OwnFleet, fresh.RiderId);
        }

        fresh.StartSearching3PL();
        await _requestRepository.UpdateAsync(fresh, ct);

        var thirdPartyResult = await AssignVia3PLAsync(fresh, ct);
        if (thirdPartyResult.IsSuccess)
        {
            return thirdPartyResult;
        }

        // Both own fleet and 3PL exhausted → terminal failure (concurrency-guarded).
        fresh = await _requestRepository.GetByIdAsync(fresh.Id, ct);
        if (fresh is null)
        {
            return DispatchResult.Failed("Delivery request no longer exists");
        }
        if (fresh.Status >= DeliveryRequestStatus.RiderAssigned)
        {
            return DispatchResult.Success(fresh.FleetType ?? FleetType.OwnFleet, fresh.RiderId);
        }

        fresh.MarkFailed(
            DeliveryFailureReason.NoRidersAvailable,
            "All dispatch options exhausted (Own Fleet declined/unavailable and 3PL failed/timed out)");

        if (!await _requestRepository.TryUpdateAsync(fresh, ct))
        {
            _logger.LogInformation(
                "Delivery {DeliveryId} failure write rejected by concurrency token — a rider accepted concurrently. Honoring the assignment.",
                fresh.Id);
            return DispatchResult.Success(FleetType.OwnFleet, null);
        }

        return DispatchResult.Failed("All dispatch options exhausted");
    }

    /// <summary>
    /// Broadcasts the delivery to every eligible own-fleet rider in range at once,
    /// then waits a single window for the first acceptance. Returns success if a rider
    /// accepted; failure (WITHOUT marking the request Failed) if none did, so the caller
    /// can fall back to 3PL.
    /// </summary>
    private async Task<DispatchResult> BroadcastToOwnFleetAsync(
        DeliveryRequest deliveryRequest,
        CancellationToken ct)
    {
        // Enter the own-fleet search state.
        if (deliveryRequest.Status != DeliveryRequestStatus.SearchingOwnFleet)
        {
            if (deliveryRequest.Status is DeliveryRequestStatus.Searching3PL or DeliveryRequestStatus.Assigned3PL)
                deliveryRequest.TransitionToOwnFleetSearch();
            else
                deliveryRequest.StartSearchingOwnFleet();
            await _requestRepository.UpdateAsync(deliveryRequest, ct);
        }

        var riders = await _riderQueryService.GetAvailableRidersAsync(
            deliveryRequest.PickupLatitude,
            deliveryRequest.PickupLongitude,
            _options.SearchRadiusKm,
            _options.MaxRidersToTry,
            ct);

        _logger.LogInformation(
            "Broadcasting delivery {DeliveryId} to {Count} available own-fleet rider(s)",
            deliveryRequest.Id, riders.Count);

        if (riders.Count == 0)
        {
            return DispatchResult.Failed("No own-fleet riders available");
        }

        // Create one offer per rider up front, then persist once.
        var offers = new List<(RiderOffer Offer, AvailableRider Rider)>(riders.Count);
        foreach (var rider in riders)
        {
            var offer = deliveryRequest.CreateOffer(
                rider.RiderId,
                CalculateEarnings(deliveryRequest.QuotedPrice),
                _options.AcceptanceTimeoutSeconds,
                rider.Latitude,
                rider.Longitude,
                (decimal)rider.DistanceToPickupKm);
            offers.Add((offer, rider));
        }
        await _requestRepository.UpdateAsync(deliveryRequest, ct);

        // Fan out all notifications concurrently — the whole point of broadcast.
        await Task.WhenAll(offers.Select(async pair =>
        {
            var (offer, rider) = pair;
            var notifyResult = await _notificationService.SendDeliveryOfferAsync(
                rider.RiderId, BuildOfferNotification(deliveryRequest, offer, rider), ct);

            if (notifyResult.IsSuccess)
                offer.MarkNotificationSent();
            else
                _logger.LogWarning(
                    "Failed to notify rider {RiderId} of offer {OfferId} for delivery {DeliveryId}",
                    rider.RiderId, offer.Id, deliveryRequest.Id);
        }));

        // Best-effort persist of the NotificationSent flags. A rider could already have
        // accepted (bumping xmin); that's fine — the accept handler owns offer cleanup.
        await _requestRepository.TryUpdateAsync(deliveryRequest, ct);

        // Single wait window: poll fresh DB status for an early acceptance. Uses the
        // AsNoTracking scalar read so an accept committed on another connection is visible
        // even though this dispatch runs on one long-lived DbContext.
        var deadline = DateTime.UtcNow.AddSeconds(_options.AcceptanceTimeoutSeconds);
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.OfferPollIntervalSeconds));

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollInterval, ct);

            var status = await _requestRepository.GetCurrentStatusAsync(deliveryRequest.Id, ct);
            if (status is null)
            {
                return DispatchResult.Failed("Delivery request no longer exists");
            }
            if (status >= DeliveryRequestStatus.RiderAssigned)
            {
                var assigned = await _requestRepository.GetByIdAsync(deliveryRequest.Id, ct);
                _logger.LogInformation(
                    "Own-fleet rider {RiderId} accepted delivery {DeliveryId}.",
                    assigned?.RiderId, deliveryRequest.Id);
                return DispatchResult.Success(FleetType.OwnFleet, assigned?.RiderId);
            }
        }

        // Window elapsed with no acceptance → expire pending offers and fall back to 3PL.
        var latest = await _requestRepository.GetByIdWithOffersAsync(deliveryRequest.Id, ct);
        if (latest is null)
        {
            return DispatchResult.Failed("Delivery request no longer exists");
        }
        if (latest.Status >= DeliveryRequestStatus.RiderAssigned)
        {
            return DispatchResult.Success(FleetType.OwnFleet, latest.RiderId);
        }

        latest.ExpireAllPendingOffers();
        if (!await _requestRepository.TryUpdateAsync(latest, ct))
        {
            // A rider accepted between our last poll and this write — honor it.
            var assigned = await _requestRepository.GetByIdAsync(deliveryRequest.Id, ct);
            return DispatchResult.Success(FleetType.OwnFleet, assigned?.RiderId);
        }

        _logger.LogInformation(
            "No own-fleet rider accepted delivery {DeliveryId} within {Timeout}s.",
            deliveryRequest.Id, _options.AcceptanceTimeoutSeconds);
        return DispatchResult.Failed("No own-fleet rider accepted");
    }

    private DeliveryOfferNotification BuildOfferNotification(
        DeliveryRequest dr, RiderOffer offer, AvailableRider rider) => new()
    {
        OfferId = offer.Id,
        DeliveryRequestId = dr.Id,
        OrderNumber = dr.OrderNumber,
        RestaurantName = dr.PickupContactName,
        PickupAddress = dr.PickupAddress,
        PickupLatitude = dr.PickupLatitude,
        PickupLongitude = dr.PickupLongitude,
        DropAddress = dr.DropAddress,
        DropLatitude = dr.DropLatitude,
        DropLongitude = dr.DropLongitude,
        DistanceToPickupKm = (decimal)rider.DistanceToPickupKm,
        DistanceToDropKm = dr.DistanceKm ?? 0,
        Earnings = offer.Earnings,
        ExpiresInSeconds = _options.AcceptanceTimeoutSeconds,
        CreatedAt = offer.OfferedAt,
        ExpiresAt = offer.ExpiresAt,
        IsFoodReady = false
    };

    /// <summary>
    /// Legacy 3PL-first ordering, preserved behind the <c>OwnFleetFirst=false</c>
    /// kill-switch: try 3PL, fall back to sequential own-fleet offers.
    /// </summary>
    private async Task<DispatchResult> DispatchThirdPartyFirstAsync(
        DeliveryRequest deliveryRequest,
        CancellationToken ct)
    {
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
            if (!await _requestRepository.TryUpdateAsync(deliveryRequest, ct))
            {
                _logger.LogInformation(
                    "Delivery {DeliveryId} failure write rejected by concurrency token — a rider accepted concurrently. Honoring the assignment.",
                    deliveryRequest.Id);
                return DispatchResult.Success(FleetType.OwnFleet, null);
            }
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

            // Read the TRUE status from the DB. This whole dispatch runs on one long-lived
            // DbContext, so a tracking reload returns our own stale copy and MISSES the
            // rider's acceptance (committed on another connection) — which is exactly how an
            // assigned delivery used to get clobbered back to Failed below.
            var statusAfterWait = await _requestRepository.GetCurrentStatusAsync(deliveryRequest.Id, ct);

            if (statusAfterWait >= DeliveryRequestStatus.RiderAssigned)
            {
                _logger.LogInformation(
                    "Delivery {DeliveryId} is now {Status} (rider {RiderId} accepted); ending own-fleet dispatch.",
                    deliveryRequest.Id, statusAfterWait, rider.RiderId);

                return DispatchResult.Success(FleetType.OwnFleet, rider.RiderId);
            }

            // Still searching → expire this offer and move to the next rider.
            deliveryRequest = (await _requestRepository.GetByIdWithOffersAsync(deliveryRequest.Id, ct))!;
            var currentOffer = deliveryRequest.RiderOffers.First(o => o.Id == offer.Id);
            if (currentOffer.Status == RiderOfferStatus.Pending)
            {
                currentOffer.Expire();
                await _requestRepository.UpdateAsync(deliveryRequest, ct);
            }
        }

        // All riders exhausted. Final fresh check: a rider may have accepted in the gap
        // between the last poll and now. Never fail over an assignment.
        var finalStatus = await _requestRepository.GetCurrentStatusAsync(deliveryRequest.Id, ct);
        if (finalStatus >= DeliveryRequestStatus.RiderAssigned)
        {
            _logger.LogInformation(
                "Delivery {DeliveryId} became {Status} before fail — not failing.",
                deliveryRequest.Id, finalStatus);
            return DispatchResult.Success(FleetType.OwnFleet, null);
        }

        _logger.LogInformation(
            "All {Count} Own Fleet riders exhausted",
            riders.Count);

        deliveryRequest.MarkFailed(DeliveryFailureReason.NoRidersAvailable, "All dispatch options exhausted (ProRouting failed/timed out and Own Fleet declined)");
        if (!await _requestRepository.TryUpdateAsync(deliveryRequest, ct))
        {
            _logger.LogInformation(
                "Delivery {DeliveryId} failure write rejected by concurrency token — a rider accepted concurrently. Honoring the assignment.",
                deliveryRequest.Id);
            return DispatchResult.Success(FleetType.OwnFleet, null);
        }

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
            "3PL task created for delivery {DeliveryId}: {TaskId}. Pushing OTPs via update...",
            deliveryRequest.Id, createResult.TaskId);

        // ProRouting requires partner/order/update to push the OTPs AND transition the
        // task from UnFulfilled -> Searching-for-Agent. Without this call the task stalls
        // at UnFulfilled and the rider gets ProRouting's auto-generated OTPs instead of
        // ours, causing a pickup-code mismatch at the restaurant.
        var updateResult = await _thirdPartyProvider.UpdateOrderAsync(
            new UpdateOrderRequest
            {
                ExternalTaskId = createResult.TaskId!,
                PickupCode = deliveryRequest.PickupCode ?? string.Empty,
                DropCode = deliveryRequest.DropCode,
                OrderReady = true
            }, ct);

        if (!updateResult.IsSuccess)
        {
            _logger.LogError(
                "ProRouting update (OTP push) failed for delivery {DeliveryId}, task {TaskId}: {Error}. Cancelling task.",
                deliveryRequest.Id, createResult.TaskId, updateResult.ErrorMessage);

            await _thirdPartyProvider.CancelTaskAsync(
                createResult.TaskId!,
                "Update/OTP push failed: " + updateResult.ErrorMessage,
                ct);

            return DispatchResult.Failed(updateResult.ErrorMessage ?? "3PL OTP update failed");
        }

        _logger.LogInformation(
            "ProRouting task {TaskId} updated. Waiting {Timeout}s for agent assignment.",
            createResult.TaskId, _options.AcceptanceTimeoutSeconds);

        // Wait for webhook assignment
        await Task.Delay(TimeSpan.FromSeconds(_options.AcceptanceTimeoutSeconds), ct);

        // Read the true status from the DB. The 3PL assignment arrives via webhook on a
        // separate connection, which this long-lived dispatch context's tracking reload
        // would miss (stale identity-map copy) — so probe fresh instead.
        var status3pl = await _requestRepository.GetCurrentStatusAsync(deliveryRequest.Id, ct);

        if (status3pl == DeliveryRequestStatus.Searching3PL)
        {
            _logger.LogWarning("ProRouting timeout reached, cancelling 3PL and falling back...");
            await _thirdPartyProvider.CancelTaskAsync(createResult.TaskId!, "Rider assignment timeout from Orchestrator", ct);
            return DispatchResult.Failed("3PL assignment timed out");
        }

        // It either got Assigned3PL or some other status (e.g. Cancelled/Failed via webhook)
        if (status3pl == DeliveryRequestStatus.Assigned3PL)
        {
             return DispatchResult.Success(FleetType.ThirdParty, null, createResult.TaskId);
        }
        else
        {
             // If the status is not Assigned3PL and not Searching3PL, it means the webhook explicitly failed/cancelled it already.
             // We return a failure here so we fallback to Own Fleet.
            return DispatchResult.Failed($"3PL webhook returned alternative status: {status3pl}");
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

    /// <summary>
    /// When true (default), dispatch broadcasts to our own fleet first and only
    /// falls back to 3PL if no own rider accepts. Set false to restore the legacy
    /// 3PL-first ordering without a redeploy (kill-switch).
    /// </summary>
    public bool OwnFleetFirst { get; set; } = true;

    public double SearchRadiusKm { get; set; } = 5.0;
    public int MaxRidersToTry { get; set; } = 10;
    public int AcceptanceTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// How often the own-fleet broadcast window re-checks the DB for an early
    /// acceptance, in seconds. Smaller = snappier assignment, more queries.
    /// </summary>
    public int OfferPollIntervalSeconds { get; set; } = 2;

    public decimal RiderEarningsPercentage { get; set; } = 80;
    public string WebhookUrl { get; set; } = "https://your-domain.com/api/webhooks/prorouting";
}