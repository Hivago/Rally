using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.EventHandlers;

/// <summary>
/// Consumes the Delivery module's pin-drift finding: flags the restaurant and
/// enqueues an admin_review_queue entry. Delivery cannot write to Restaurant/Users
/// tables directly, so this cross-module write happens here instead.
/// </summary>
public sealed class RestaurantLocationDriftDetectedIntegrationEventHandler
    : INotificationHandler<RestaurantLocationDriftDetectedIntegrationEvent>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantLocationReviewQueueRepository _reviewQueueRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RestaurantLocationDriftDetectedIntegrationEventHandler> _logger;

    public RestaurantLocationDriftDetectedIntegrationEventHandler(
        IRestaurantRepository restaurantRepository,
        IRestaurantLocationReviewQueueRepository reviewQueueRepository,
        IUnitOfWork unitOfWork,
        ILogger<RestaurantLocationDriftDetectedIntegrationEventHandler> logger)
    {
        _restaurantRepository = restaurantRepository;
        _reviewQueueRepository = reviewQueueRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(RestaurantLocationDriftDetectedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        // The sweep re-checks every tick until someone reviews the finding — don't
        // pile up duplicate Pending rows for the same restaurant.
        if (await _reviewQueueRepository.HasPendingAsync(notification.RestaurantId, cancellationToken))
            return;

        var restaurant = await _restaurantRepository.GetByIdAsync(notification.RestaurantId, cancellationToken);
        if (restaurant is null)
        {
            _logger.LogWarning(
                "Pin drift reported for unknown restaurant {RestaurantId} — ignoring.",
                notification.RestaurantId);
            return;
        }

        var queueEntryResult = RestaurantLocationReviewQueue.Create(
            notification.RestaurantId,
            (decimal)notification.CurrentLatitude,
            (decimal)notification.CurrentLongitude,
            (decimal)notification.SuggestedLatitude,
            (decimal)notification.SuggestedLongitude,
            notification.AverageDriftMeters,
            notification.SampleSize,
            notification.DetectedAt);

        if (queueEntryResult.IsFailure)
        {
            _logger.LogError(
                "Failed to create review-queue entry for restaurant {RestaurantId}: {Error}",
                notification.RestaurantId, queueEntryResult.Error.Message);
            return;
        }

        restaurant.FlagLocationDrift(notification.DetectedAt);
        await _reviewQueueRepository.AddAsync(queueEntryResult.Value, cancellationToken);
        _restaurantRepository.Update(restaurant, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Restaurant {RestaurantId} flagged for location drift: {AvgDrift:F1}m avg over {SampleSize} pickups. Queued for admin review.",
            notification.RestaurantId, notification.AverageDriftMeters, notification.SampleSize);
    }
}
