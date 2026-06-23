using MediatR;
using RallyAPI.Delivery.Application.Commands.TriggerDispatch;
using RallyAPI.Delivery.Domain.Abstractions;

namespace RallyAPI.Host.BackgroundServices;

/// <summary>
/// Safety net for rider dispatch.
///
/// The normal trigger (OrderReadyForPickup -> TriggerDispatch) runs the long, blocking
/// dispatch (3PL wait + per-rider acceptance wait) inline on the request/outbox thread.
/// If that work is interrupted — request thread torn down after the HTTP response returns,
/// an exception swallowed by the domain-event interceptor, a ProRouting hang — a
/// DeliveryRequest can wedge in a pre-assignment state (Created / PendingDispatch /
/// SearchingOwnFleet / Searching3PL) and never retry, so the rider is never offered the
/// delivery.
///
/// This service periodically re-triggers dispatch for any such request that has been idle
/// longer than <see cref="StuckThreshold"/> — comfortably past a single dispatch attempt's
/// worst case, so it does not race an in-flight attempt. Re-dispatch is safe: TriggerDispatch
/// is idempotent per state, and DeliveryRequest's optimistic-concurrency Version rejects a
/// losing concurrent write (logged and retried next tick).
/// </summary>
public sealed class DeliveryDispatchRecoveryService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(2);
    private const int BatchSize = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryDispatchRecoveryService> _logger;

    public DeliveryDispatchRecoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeliveryDispatchRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DeliveryDispatchRecoveryService started (poll every {Seconds}s, re-dispatch requests idle >{Minutes}min)",
            PollInterval.TotalSeconds, StuckThreshold.TotalMinutes);

        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RecoverStuckAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delivery dispatch recovery tick failed");
            }
        }
    }

    private async Task RecoverStuckAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeliveryRequestRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var stuckBefore = DateTime.UtcNow - StuckThreshold;
        var stuck = await repository.GetStuckForRedispatchAsync(stuckBefore, ct);
        if (stuck.Count == 0)
            return;

        var batch = stuck.Take(BatchSize).ToList();
        _logger.LogWarning(
            "Dispatch recovery: {Total} stuck delivery request(s) found, re-dispatching {Batch}",
            stuck.Count, batch.Count);

        foreach (var request in batch)
        {
            try
            {
                _logger.LogWarning(
                    "Re-dispatching stuck delivery {DeliveryId} (Order {OrderId}, status {Status}, idle since {UpdatedAt:o})",
                    request.Id, request.OrderId, request.Status, request.UpdatedAt);

                var result = await sender.Send(
                    new TriggerDispatchCommand { DeliveryRequestId = request.Id }, ct);

                if (result.IsFailure)
                {
                    _logger.LogWarning(
                        "Re-dispatch of delivery {DeliveryId} returned failure: {Error}",
                        request.Id, result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Re-dispatch of delivery {DeliveryId} threw", request.Id);
            }
        }
    }
}
