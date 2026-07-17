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
/// agent accepts. If a Searching3PL delivery's provider search runs past
/// <see cref="DispatchOptions.ThirdPartySearchTimeoutMinutes"/> with no agent, this service
/// cancels the stale task and RE-BOOKS a fresh 3PL task (never gives up / never fails for lack
/// of a rider — 3PL is the guaranteed backstop, it just takes time).
///
/// Re-dispatch is safe: TriggerDispatch is idempotent per state, and the xmin concurrency token
/// rejects a losing write (skipped, retried next tick).
/// </summary>
public sealed class DeliveryDispatchRecoveryService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(2);

    // Hard age floor for stuck-recovery: never re-dispatch an order created longer ago than this.
    // A genuinely recoverable stuck order (interrupted dispatch) is minutes old; anything older is
    // dead. Without this, a deploy/restart re-dispatches the entire historical backlog and books
    // real 3PL riders for months-old orders (incident 2026-07-09: 68 April/May orders re-booked).
    private static readonly TimeSpan StuckMaxAge = TimeSpan.FromHours(3);

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

        var now = DateTime.UtcNow;
        var stuckBefore = now - StuckThreshold;
        var createdAfter = now - StuckMaxAge;
        var stuck = await repository.GetStuckForRedispatchAsync(stuckBefore, createdAfter, ct);
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
            "3PL search timeout: {Total} delivery(ies) past {Minutes}min with no agent, re-booking a fresh 3PL task for {Batch}",
            timedOut.Count, _dispatchOptions.ThirdPartySearchTimeoutMinutes, batch.Count);

        foreach (var request in batch)
        {
            try
            {
                // Reload fresh and act only if still genuinely waiting on 3PL — a webhook
                // may have assigned an agent between the query and now.
                var fresh = await repository.GetByIdFreshAsync(request.Id, ct);
                if (fresh is null || fresh.Status != DeliveryRequestStatus.Searching3PL)
                    continue;

                var taskId = fresh.ExternalTaskId;

                // Our status saying Searching3PL only means no webhook reached us — it is NOT
                // evidence that the provider found nobody. Ask the provider before touching a
                // live booking: cancelling here would strand a real rider mid-delivery and book
                // a second one we also pay for. Anything short of "the provider confirms it is
                // still searching" means hands off.
                if (!string.IsNullOrEmpty(taskId))
                {
                    var progress = await ResolveProviderProgressAsync(provider, repository, fresh, taskId, ct);

                    // Assigned → adopted above, nothing to re-book. Unknown → we don't know, so we
                    // must not touch it. Only a confirmed "still searching" or a dead task earns a
                    // cancel + re-book below.
                    if (progress is ThirdPartyTaskProgress.AssignedOrBeyond or ThirdPartyTaskProgress.Unknown)
                        continue;
                }

                // Reset the 3PL search FIRST (xmin-guarded), clearing the stale task so the next
                // dispatch books a FRESH one. If a concurrent webhook assigned an agent, this loses
                // the race — skip, don't cancel the live task. We never give up on 3PL for lack of
                // a rider; it always assigns eventually, it just takes time.
                fresh.ResetForThirdPartyRetry();
                if (!await repository.TryUpdateAsync(fresh, ct))
                {
                    _logger.LogInformation(
                        "3PL timeout re-book of delivery {DeliveryId} lost the race to a webhook assignment; skipping.",
                        request.Id);
                    continue;
                }

                _logger.LogWarning(
                    "3PL search timed out for delivery {DeliveryId} (Order {OrderId}, task {TaskId}); cancelling stale task and re-booking 3PL.",
                    fresh.Id, fresh.OrderId, taskId);

                if (!string.IsNullOrEmpty(taskId))
                {
                    var cancel = await provider.CancelTaskAsync(taskId, "3PL search timeout — re-booking a fresh task", ct);
                    if (!cancel.IsSuccess)
                        _logger.LogWarning(
                            "Failed to cancel timed-out 3PL task {TaskId} for delivery {DeliveryId}: {Error}",
                            taskId, fresh.Id, cancel.ErrorMessage);
                }

                // Re-dispatch: the delivery is Searching3PL with no live task, so dispatch books a
                // fresh 3PL task and keeps searching. The order is never failed for lack of a rider.
                var result = await sender.Send(new TriggerDispatchCommand { DeliveryRequestId = fresh.Id }, ct);
                if (result.IsFailure)
                    _logger.LogWarning(
                        "3PL re-book after timeout for delivery {DeliveryId} returned failure: {Error}",
                        fresh.Id, result.Error);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "3PL timeout handling for delivery {DeliveryId} threw", request.Id);
            }
        }
    }

    /// <summary>
    /// Asks the provider what actually happened to <paramref name="taskId"/> before we consider
    /// cancelling it, and adopts an assignment we never got a webhook for.
    /// </summary>
    /// <returns>
    /// <see cref="ThirdPartyTaskProgress.Searching"/> ONLY when the provider positively confirms it
    /// is still hunting for an agent — the one case where cancelling and re-booking is correct.
    /// Every other outcome (assigned, dead, unrecognised, or the status call failing) returns
    /// something else, which the caller must treat as "leave this booking alone".
    /// </returns>
    private async Task<ThirdPartyTaskProgress> ResolveProviderProgressAsync(
        IThirdPartyDeliveryProvider provider,
        IDeliveryRequestRepository repository,
        DeliveryRequest fresh,
        string taskId,
        CancellationToken ct)
    {
        var status = await provider.GetTaskStatusAsync(taskId, ct);

        if (!status.IsSuccess)
        {
            // We could not find out. Silence is not evidence of "no rider" — the last time we
            // assumed it was, we cancelled a live rider and double-booked. Leave the task alone
            // and re-check on the next sweep.
            _logger.LogError(
                "3PL timeout: could not read provider status for task {TaskId} (delivery {DeliveryId}): {Error}. " +
                "Leaving the booking untouched rather than risk cancelling a live rider.",
                taskId, fresh.Id, status.ErrorMessage);
            return ThirdPartyTaskProgress.Unknown;
        }

        var progress = ThirdPartyTaskStateClassifier.Classify(status.State);

        switch (progress)
        {
            case ThirdPartyTaskProgress.AssignedOrBeyond:
                // The provider DID assign an agent — we simply never received the webhook.
                // Adopt it instead of cancelling. This is the missed-webhook self-heal.
                _logger.LogWarning(
                    "3PL timeout: provider reports task {TaskId} is at '{State}' (rider '{RiderName}') for delivery " +
                    "{DeliveryId} — the assignment webhook never arrived. Adopting it instead of re-booking.",
                    taskId, status.State, status.RiderName, fresh.Id);

                fresh.Assign3PLRider(
                    taskId,
                    fresh.ExternalLspName ?? "ProRouting",
                    status.RiderName,
                    status.RiderPhone,
                    status.TrackingUrl ?? fresh.ExternalTrackingUrl,
                    fresh.QuotedPrice);

                if (!await repository.TryUpdateAsync(fresh, ct))
                    _logger.LogInformation(
                        "3PL timeout: adopting assignment for delivery {DeliveryId} lost a concurrency race; " +
                        "the winning write already moved it on.",
                        fresh.Id);
                break;

            case ThirdPartyTaskProgress.CancelledOrFailed:
                _logger.LogWarning(
                    "3PL timeout: provider reports task {TaskId} is dead ('{State}') for delivery {DeliveryId}; " +
                    "re-booking without cancelling.",
                    taskId, status.State, fresh.Id);
                break;

            case ThirdPartyTaskProgress.Unknown:
                _logger.LogError(
                    "3PL timeout: unrecognised provider state '{State}' for task {TaskId} (delivery {DeliveryId}). " +
                    "Leaving the booking untouched — treating an unknown state as 'no rider' risks cancelling a live one.",
                    status.State, taskId, fresh.Id);
                break;

            case ThirdPartyTaskProgress.Searching:
                _logger.LogWarning(
                    "3PL timeout: provider confirms task {TaskId} is still searching ('{State}') for delivery " +
                    "{DeliveryId}; cancelling and re-booking a fresh task.",
                    taskId, status.State, fresh.Id);
                break;
        }

        return progress;
    }
}
