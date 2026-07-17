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
/// (2) 3PL reconciliation: after own fleet finds no rider we hand off to the 3PL provider
/// NON-BLOCKING (status Searching3PL). The provider's webhook flips us to Assigned3PL when an
/// agent accepts. Once a task is booked the provider OWNS the delivery — they see it through and
/// an agent always turns up eventually, it just takes time. So a long search is not a failure and
/// this service never cancels one. Past <see cref="DispatchOptions.ThirdPartySearchTimeoutMinutes"/>
/// it simply ASKS the provider what happened and reconciles:
///   - agent assigned (our webhook was lost) -> adopt the assignment
///   - still searching / unknown / no answer -> leave the booking with them, re-check next tick
///   - dead at the provider (cancelled/failed) -> the only case that re-books a fresh task
///
/// It must never cancel a live task on our own status alone: Searching3PL only means no webhook
/// reached us, never that the provider found nobody. Doing so cancels a rider mid-delivery and
/// pays for a duplicate booking (incident 2026-07-17, staging ORD-20260717-00279 / task
/// mfnb_fx6fsryz — cancelled and re-booked while it was already "Order-delivered").
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
            "DeliveryDispatchRecoveryService started (poll every {Seconds}s, re-dispatch idle >{Minutes}min, 3PL reconcile after {ThirdPartyMinutes}min)",
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
            "3PL reconcile: {Total} delivery(ies) still searching past {Minutes}min; asking the provider what happened to {Batch}",
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

                // A booked ProRouting task is a commitment: once they accept the order they see it
                // through, and an agent always turns up eventually — it can just take a while. So a
                // long search is NOT a failure and must never be "rescued" by cancelling. Our status
                // saying Searching3PL only means no webhook reached us; it is not evidence about the
                // provider at all. Ask them, then act:
                //
                //   assigned/beyond  -> adopt the assignment (webhook was lost), nothing to re-book
                //   still searching  -> LEAVE IT ALONE, they are still working the order
                //   unknown / no answer -> leave it alone; silence is not evidence
                //   dead at provider -> the only case that earns a fresh booking, handled below
                if (!string.IsNullOrEmpty(taskId))
                {
                    var progress = await ResolveProviderProgressAsync(provider, repository, fresh, taskId, ct);
                    if (progress != ThirdPartyTaskProgress.CancelledOrFailed)
                        continue;
                }

                // Only reached when the provider itself says the task is dead (cancelled/failed), so
                // nobody is coming and re-booking is the correct repair. Reset the 3PL search
                // (xmin-guarded) to clear the dead task so dispatch books a FRESH one. If a
                // concurrent webhook assigned an agent, this loses the race — skip.
                fresh.ResetForThirdPartyRetry();
                if (!await repository.TryUpdateAsync(fresh, ct))
                {
                    _logger.LogInformation(
                        "3PL re-book of delivery {DeliveryId} lost the race to a webhook assignment; skipping.",
                        request.Id);
                    continue;
                }

                _logger.LogWarning(
                    "3PL task {TaskId} is dead at the provider for delivery {DeliveryId} (Order {OrderId}); re-booking a fresh task.",
                    taskId, fresh.Id, fresh.OrderId);

                // Re-dispatch: the delivery is Searching3PL with no live task, so dispatch books a
                // fresh 3PL task and keeps searching. The order is never failed for lack of a rider.
                var result = await sender.Send(new TriggerDispatchCommand { DeliveryRequestId = fresh.Id }, ct);
                if (result.IsFailure)
                    _logger.LogWarning(
                        "3PL re-book of dead task for delivery {DeliveryId} returned failure: {Error}",
                        fresh.Id, result.Error);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "3PL reconcile for delivery {DeliveryId} threw", request.Id);
            }
        }
    }

    /// <summary>
    /// Asks the provider what actually happened to <paramref name="taskId"/>, and adopts an
    /// assignment we never got a webhook for.
    /// </summary>
    /// <returns>
    /// <see cref="ThirdPartyTaskProgress.CancelledOrFailed"/> ONLY when the provider itself says the
    /// task is dead — the one case where re-booking is correct, because nobody is coming on it.
    /// Every other outcome (assigned, still searching, unrecognised, or the status call failing)
    /// means the provider still owns the delivery and the caller must leave the booking alone.
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
                "3PL reconcile: could not read provider status for task {TaskId} (delivery {DeliveryId}): {Error}. " +
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
                    "3PL reconcile: provider reports task {TaskId} is at '{State}' (rider '{RiderName}') for delivery " +
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
                        "3PL reconcile: adopting assignment for delivery {DeliveryId} lost a concurrency race; " +
                        "the winning write already moved it on.",
                        fresh.Id);
                break;

            case ThirdPartyTaskProgress.CancelledOrFailed:
                _logger.LogWarning(
                    "3PL: provider reports task {TaskId} is dead ('{State}') for delivery {DeliveryId}; " +
                    "nobody is coming on this task, so a fresh booking is needed.",
                    taskId, status.State, fresh.Id);
                break;

            case ThirdPartyTaskProgress.Unknown:
                _logger.LogError(
                    "3PL reconcile: unrecognised provider state '{State}' for task {TaskId} (delivery {DeliveryId}). " +
                    "Leaving the booking untouched — treating an unknown state as 'no rider' risks cancelling a live one.",
                    status.State, taskId, fresh.Id);
                break;

            case ThirdPartyTaskProgress.Searching:
                // Not a problem to fix — the provider is still working the order and will deliver it.
                // Re-booking here would throw away a live commitment and pay for a second one.
                _logger.LogInformation(
                    "3PL: provider is still searching for an agent on task {TaskId} ('{State}') for delivery " +
                    "{DeliveryId} after {Minutes}min. Leaving it with them.",
                    taskId, status.State, fresh.Id, _dispatchOptions.ThirdPartySearchTimeoutMinutes);
                break;
        }

        return progress;
    }
}
