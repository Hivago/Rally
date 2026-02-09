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

        // Start searching own fleet
        deliveryRequest.StartSearchingOwnFleet();
        await _requestRepository.UpdateAsync(deliveryRequest, ct);

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
            _logger.LogInformation("No riders available, falling back to 3PL");
            return await FallbackTo3PLAsync(deliveryRequest, ct);
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
            "All {Count} riders exhausted, falling back to 3PL",
            riders.Count);

        return await FallbackTo3PLAsync(deliveryRequest, ct);
    }

    private async Task<DispatchResult> FallbackTo3PLAsync(
        DeliveryRequest deliveryRequest,
        CancellationToken ct)
    {
        deliveryRequest.StartSearching3PL();
        await _requestRepository.UpdateAsync(deliveryRequest, ct);

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
                CallbackUrl = _options.WebhookUrl,
                SelectionMode = "fastest_agent"
            }, ct);

        if (!createResult.IsSuccess)
        {
            _logger.LogError(
                "3PL booking failed for delivery {DeliveryId}: {Error}",
                deliveryRequest.Id, createResult.ErrorMessage);

            deliveryRequest.MarkFailed(
                DeliveryFailureReason.ThirdPartyFailed,
                createResult.ErrorMessage);

            await _requestRepository.UpdateAsync(deliveryRequest, ct);

            return DispatchResult.Failed(createResult.ErrorMessage ?? "3PL booking failed");
        }

        // 3PL will send rider info via webhook
        deliveryRequest.Assign3PLRider(
            createResult.TaskId!,
            createResult.ProviderName ?? "ProRouting",
            null, // Rider info comes via webhook
            null,
            createResult.TrackingUrl,
            deliveryRequest.QuotedPrice); // Actual price TBD

        await _requestRepository.UpdateAsync(deliveryRequest, ct);

        _logger.LogInformation(
            "3PL task created for delivery {DeliveryId}: {TaskId}",
            deliveryRequest.Id, createResult.TaskId);

        return DispatchResult.Success(FleetType.ThirdParty, null, createResult.TaskId);
    }

    private decimal CalculateEarnings(decimal deliveryFee)
    {
        // Rider gets X% of delivery fee
        return Math.Round(deliveryFee * _options.RiderEarningsPercentage / 100, 2);
    }
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