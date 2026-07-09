using Microsoft.Extensions.Options;
using MediatR;
using RallyAPI.Delivery.Application.Commands.TriggerDispatch;
using RallyAPI.Delivery.Application.Services;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Delivery;

namespace RallyAPI.Host.BackgroundServices;

/// <summary>
/// Safety net + timeout enforcer for rider dispatch.
///
/// (0) Early/predictive dispatch (when DispatchOptions.EarlyDispatchEnabled): fires the rider
/// search for PendingDispatch requests whose scheduled DispatchAt has arrived, so the search
/// starts during prep rather than at food-ready. See <see cref="SweepDueDispatchesAsync"/>.
///
/// (1) Re-dispatch stuck requests: the normal trigger (OrderReadyForPickup -> TriggerDispatch)
/// runs dispatch on the outbox thread. If that work is interrupted, a DeliveryRequest can wedge
/// in a pre-assignment state (Created / PendingDispatch / SearchingOwnFleet) and never retry.
/// This service re-triggers dispatch for any such request idle longer than <see cref="StuckThreshold"/>.
///
/// (2) 3PL search timeout: after own fleet finds no rider we hand off to the 3PL provider
/// NON-BLOCKING (status Searching3PL). The provider's webhook flips us to Assigned3PL when an
/// agent accepts. This service enforces the ceiling: a Searching3PL delivery whose provider
/// search has run past <see cref="DispatchOptions.ThirdPartySearchTimeoutMinutes"/> gets its
/// provider task cancelled and own fleet retried once (which then fails if still no rider —
/// ThirdPartyDispatchedAt marks that 3PL was already attempted).
///
/// Re-dispatch is safe: TriggerDispatch is idempotent per state, and the xmin concurrency token
/// rejects a losing write (skipped, retried next tick).
/// </summary>
public sealed class DeliveryDispatchRecoveryService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(2);
    private const int BatchSize = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DispatchOptions _dispatchOptions;
    private readonly ILogger<DeliveryDispatchRecoveryService> _logger;

    public DeliveryDispatchRecoveryService(
        IServiceScopeFactory scopeFactory,
        IOptions<DispatchOptions> dispatchOptions,
        ILogger<DeliveryDispatchRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _dispatchOptions = dispatchOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DeliveryDispatchRecoveryService started (poll every {Seconds}s, re-dispatch idle >{Minutes}min, 3PL search timeout {ThirdPartyMinutes}min)",
            PollInterval.TotalSeconds, StuckThreshold.TotalMinutes, _dispatchOptions.ThirdPartySearchTimeoutMinutes);

        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (_dispatchOptions.EarlyDispatchEnabled)
                    await SweepDueDispatchesAsync(stoppingToken);

                await RecoverStuckAsync(stoppingToken);
                await SweepThirdPartyTimeoutsAsync(stoppingToken);
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

    /// <summary>
    /// Early/predictive dispatch: fires the rider search for any PendingDispatch request whose
    /// scheduled <see cref="DeliveryRequest.DispatchAt"/> has arrived. This starts the search DURING
    /// prep so a rider reaches the restaurant around food-ready. Gated on
    /// <see cref="DispatchOptions.EarlyDispatchEnabled"/>. Idempotent: TriggerDispatch no-ops if the
    /// request already moved past PendingDispatch (e.g. the food-ready event fired first), and a
    /// request that stays PendingDispatch (interrupted dispatch) is simply retried next tick.
    /// </summary>
    private async Task SweepDueDispatchesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeliveryRequestRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var due = await repository.GetPendingDispatchAsync(DateTime.UtcNow, ct);
        if (due.Count == 0)
            return;

        var batch = due.Take(BatchSize).ToList();
        _logger.LogInformation(
            "Early dispatch: {Total} delivery request(s) due, dispatching {Batch}",
            due.Count, batch.Count);

        foreach (var request in batch)
        {
            try
            {
                _logger.LogInformation(
                    "Early-dispatching delivery {DeliveryId} (Order {OrderId}, DispatchAt {DispatchAt:o})",
                    request.Id, request.OrderId, request.DispatchAt);

                var result = await sender.Send(
                    new TriggerDispatchCommand { DeliveryRequestId = request.Id }, ct);

                if (result.IsFailure)
                    _logger.LogWarning(
                        "Early dispatch of delivery {DeliveryId} returned failure: {Error}",
                        request.Id, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Early dispatch of delivery {DeliveryId} threw", request.Id);
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

    private async Task SweepThirdPartyTimeoutsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeliveryRequestRepository>();
        var provider = scope.ServiceProvider.GetRequiredService<IThirdPartyDeliveryProvider>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var dispatchedBefore = DateTime.UtcNow - TimeSpan.FromMinutes(_dispatchOptions.ThirdPartySearchTimeoutMinutes);
        var timedOut = await repository.GetThirdPartySearchTimedOutAsync(dispatchedBefore, ct);
        if (timedOut.Count == 0)
            return;

        var batch = timedOut.Take(BatchSize).ToList();
        _logger.LogWarning(
            "3PL search timeout: {Total} delivery(ies) past {Minutes}min with no agent, retrying own fleet for {Batch}",
            timedOut.Count, _dispatchOptions.ThirdPartySearchTimeoutMinutes, batch.Count);

        foreach (var request in batch)
        {
            try
            {
                // Reload fresh and take over only if still genuinely waiting on 3PL — a webhook
                // may have assigned an agent between the query and now.
                var fresh = await repository.GetByIdFreshAsync(request.Id, ct);
                if (fresh is null || fresh.Status != DeliveryRequestStatus.Searching3PL)
                    continue;

                var taskId = fresh.ExternalTaskId;

                // Flip to own-fleet search FIRST (xmin-guarded). If a concurrent webhook assigned
                // an agent, this loses the race — skip, don't cancel the live task.
                fresh.TransitionToOwnFleetSearch();
                if (!await repository.TryUpdateAsync(fresh, ct))
                {
                    _logger.LogInformation(
                        "3PL timeout takeover of delivery {DeliveryId} lost the race to a webhook assignment; skipping.",
                        request.Id);
                    continue;
                }

                _logger.LogWarning(
                    "3PL search timed out for delivery {DeliveryId} (Order {OrderId}, task {TaskId}); cancelling and retrying own fleet.",
                    fresh.Id, fresh.OrderId, taskId);

                if (!string.IsNullOrEmpty(taskId))
                {
                    var cancel = await provider.CancelTaskAsync(taskId, "3PL search timeout — retrying own fleet", ct);
                    if (!cancel.IsSuccess)
                        _logger.LogWarning(
                            "Failed to cancel timed-out 3PL task {TaskId} for delivery {DeliveryId}: {Error}",
                            taskId, fresh.Id, cancel.ErrorMessage);
                }

                // Retry own fleet once. The fallback in the orchestrator sees ThirdPartyDispatchedAt
                // is set and will mark the delivery Failed rather than looping back to 3PL.
                var result = await sender.Send(new TriggerDispatchCommand { DeliveryRequestId = fresh.Id }, ct);
                if (result.IsFailure)
                    _logger.LogWarning(
                        "Own-fleet retry after 3PL timeout for delivery {DeliveryId} returned failure: {Error}",
                        fresh.Id, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "3PL timeout handling for delivery {DeliveryId} threw", request.Id);
            }
        }
    }
}
