using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Infrastructure.Services;

public class StubRiderNotificationService : IRiderNotificationService
{
    private readonly ILogger<StubRiderNotificationService> _logger;

    public StubRiderNotificationService(ILogger<StubRiderNotificationService> logger)
    {
        _logger = logger;
    }

    public Task<Result> SendDeliveryOfferAsync(Guid riderId, DeliveryOfferNotification offer, CancellationToken ct = default)
    {
        _logger.LogInformation("STUB: Sending delivery offer to rider {RiderId}", riderId);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendOfferCancelledAsync(Guid riderId, Guid offerId, string reason, CancellationToken ct = default)
    {
        _logger.LogInformation("STUB: Sending offer cancelled to rider {RiderId} for offer {OfferId}. Reason: {Reason}", riderId, offerId, reason);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendNotificationAsync(Guid riderId, string title, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("STUB: Sending notification to rider {RiderId}. Title: {Title}, Message: {Message}", riderId, title, message);
        return Task.FromResult(Result.Success());
    }

    public Task<bool> IsRiderConnectedAsync(Guid riderId, CancellationToken ct = default)
    {
        _logger.LogInformation("STUB: Checking if rider {RiderId} is connected. Returning true.", riderId);
        return Task.FromResult(true);
    }
}
