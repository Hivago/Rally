# Feature Spec: Early / Predictive Dispatch

> **Status**: Draft
> **Priority**: P1 (High)
> **Estimated Effort**: 1–1.5 days
> **Module(s)**: Delivery (+ small Orders event change if fee not on event)
> **Owner**: Yash
> **Date**: 2026-07-08
> **Branch**: `feature/early-predictive-dispatch` (off `feature/own-fleet-broadcast-dispatch`)

---

## 1. Problem Statement

Rider/3PL search only starts when the food is **ready** (`OrderReadyForPickup`). That adds the rider's travel-to-restaurant time on top of prep time, slowing every delivery. We want to start the search **during prep** (around restaurant accept) so a rider arrives at the restaurant right about when the food is ready.

## 2. Current State (verified 2026-07-08)

- **Restaurant accepts** → `OrderConfirmedIntegrationEventHandler` creates the `DeliveryRequest` (status `PendingDispatch`, `DispatchAt = ConfirmedAt`) but **does NOT trigger dispatch** ("NO AUTO-DISPATCH — admin handles" comment). It also sets **`quotedPrice: 0m`**.
- **Food ready** → `OrderReadyForPickupIntegrationEventHandler` sends `TriggerDispatchCommand` → own-fleet broadcast → 3PL.
- Dead scaffolding that was built for exactly this feature but never wired: `DispatchAt`, `PendingDispatch` status, `PrepTimeCalculator` (computes `DispatchAfterMinutes = prep − buffer`), `GetPendingDispatchAsync`, and the whole `CreateDeliveryRequestCommand(Handler)`.

## 3. Blockers / Prerequisites

1. **⚠️ QuotedPrice = 0 (hard blocker).** The orchestrator computes rider earnings from `QuotedPrice`. The accept-path request has `quotedPrice: 0m` → riders would be offered **₹0** and never accept. Must resolve the real delivery fee at accept time. `OrderConfirmedIntegrationEvent` carries **`QuoteId`**, and `IDeliveryQuoteRepository.GetByIdAsync` → `quote.FinalFee` gives the fee. Fix = load the quote in the accept handler and use `FinalFee` (mark it used, mirroring the dead `CreateDeliveryRequestCommandHandler`). Fallback when `QuoteId` is null: skip early dispatch, keep today's ready-time trigger.

## 4. Technical Design

### 4.1 Set a predictive DispatchAt at accept (Delivery.Application)
`OrderConfirmedIntegrationEventHandler`:
- If `QuoteId` present → load quote, `quotedPrice = quote.FinalFee`, mark quote used.
- `prep = PrepTimeCalculator.Calculate(itemCount)`; `dispatchAt = ConfirmedAt + prep.DispatchAfterMinutes` (dispatch ~5 min before ready by default).
- Create the `DeliveryRequest` with that `dispatchAt` (status `PendingDispatch`). No inline dispatch here.
- If no quote → create as today (no early dispatch); ready-event still drives it.

### 4.2 Scheduler that fires due dispatches (Host background service)
Extend `DeliveryDispatchRecoveryService` (already polls every 15s) with a sweep:
- `due = GetPendingDispatchAsync(DateTime.UtcNow)` → requests with `DispatchAt <= now` still in `PendingDispatch`.
- For each, send `TriggerDispatchCommand` → starts own-fleet broadcast (→ 3PL per §4.4).
- Idempotent: `TriggerDispatch` no-ops if already searching/assigned; xmin guards concurrent fire vs. the ready-event.

### 4.3 Food-ready stays as the floor
`OrderReadyForPickupIntegrationEventHandler` is unchanged. If the kitchen is faster than predicted (food ready before `DispatchAt`), it triggers dispatch immediately (`ShouldTriggerImmediateDispatch` is true for `PendingDispatch`). Net effect: **dispatch fires at whichever comes first** — the scheduled prep-offset time OR food-ready.

### 4.4 3PL during prep — DECISION NEEDED (see §6)
Two options for what the early dispatch does when own fleet doesn't accept:
- **(a) Own-fleet-only during prep; 3PL only at/after food-ready.** Early window offers own riders (free to expire). If none accept, it waits — 3PL is not booked until the food-ready event. Avoids 3PL cancellation fees for mid-prep cancellations and agents arriving before food. Requires the orchestrator to know "food not ready yet → don't go to 3PL yet."
- **(b) Full flow early (own → 3PL) as today.** Simpler (reuses current orchestrator). But books 3PL during prep → agent may arrive early and wait, and a mid-prep cancel may incur a 3PL cancellation fee.

### 4.5 Offer `IsFoodReady` flag
When dispatching early, the offer notification's `IsFoodReady` stays `false` (already the case). Optional later: include a food-ready ETA so riders pace themselves. Out of scope for v1.

## 5. Edge Cases

| Scenario | Behavior |
|----------|----------|
| No `QuoteId` on the order | No early dispatch; ready-event drives it (today's behavior). |
| Kitchen faster than predicted | Food-ready event fires dispatch before `DispatchAt`; scheduler then no-ops. |
| Order cancelled during prep | Own-fleet offers just expire (free). 3PL exposure depends on §4.4 choice. |
| Scheduler fires same instant as ready-event | Both send `TriggerDispatch`; idempotent + xmin — one wins, the other no-ops. |
| Very short prep (`DispatchAfter = 0`) | `dispatchAt ≈ ConfirmedAt` → dispatches ~immediately (fine). |

## 6. Decisions (resolved 2026-07-09)

- **D1 — 3PL during prep**: **(b) Full own→3PL early.** Reuse the orchestrator as-is; the early dispatch runs the full own→3PL flow. No orchestrator change. Accepts the mid-prep 3PL exposure for simplicity.
- **D2 — timing**: **prep-based offset** (`prep − DispatchBufferMinutes`, ~5 min before predicted ready) via the existing `PrepTimeCalculator`.

### ⚠️ Correction to §2 (found during build)
The recovery service's `GetStuckForRedispatchAsync` **already sweeps `PendingDispatch`** and fires `TriggerDispatch` after 2 min idle — **ignoring `DispatchAt`**. So accept-path requests already auto-dispatch ~2 min after accept today (and, with `quotedPrice: 0`, would offer riders ₹0 — the blocker is live, not hypothetical). To keep the prep timer from being front-run, `GetStuckForRedispatchAsync` now only treats a `PendingDispatch` request as stuck once its `DispatchAt` has passed. With early dispatch OFF, `DispatchAt == ConfirmedAt` (already past), so flag-off behavior is unchanged.

## 7. Testing Plan

- Accept handler: quote loaded → `quotedPrice = FinalFee`; `DispatchAt = ConfirmedAt + DispatchAfter`; status `PendingDispatch`; no dispatch fired.
- Scheduler: due request → `TriggerDispatch` sent; not-yet-due → skipped; already-searching → no duplicate.
- Integration: confirm → (wait) → own-fleet offer goes out before food-ready; fast-kitchen path still works.

## 8. Rollout

- Feature flag `Delivery:Dispatch:EarlyDispatchEnabled` (default off until validated) so we can ship dark and flip on.
- Metric: time-to-assign relative to food-ready; % orders with a rider already assigned when food becomes ready; mid-prep cancel count with rider/3PL committed.
- Rollback: flip the flag off → falls back to ready-time dispatch.

---

## Implementation Notes (built 2026-07-09)

### Files changed
- `RiderDispatchOrchestrator.cs` (`DispatchOptions`) — added `EarlyDispatchEnabled` flag (default `false`).
- `OrderConfirmedIntegrationEventHandler.cs` — flag-gated: loads the quote → `quotedPrice = FinalFee`, `DispatchAt = ConfirmedAt + (prep − buffer)`, passes distance/ETA, marks the quote used (after the request is persisted). Falls back to today's behavior (fee 0, `DispatchAt = ConfirmedAt`) when the flag is off, no `QuoteId`, or the quote is missing.
- `DeliveryRequestRepository.GetStuckForRedispatchAsync` — a `PendingDispatch` request is only "stuck" once its `DispatchAt` has passed (stops the 2-min net front-running the prep timer; no-op when flag off).
- `DeliveryDispatchRecoveryService.cs` — new flag-gated `SweepDueDispatchesAsync` fires `TriggerDispatch` for due `PendingDispatch` requests via `GetPendingDispatchAsync(now)`.
- `appsettings.json` — `Delivery:Dispatch:EarlyDispatchEnabled: false`.
- Tests: `OrderConfirmedEarlyDispatchTests.cs` (6 cases). Full Delivery suite green (67).

### Not needed (D1 = full early flow)
- No `RiderDispatchOrchestrator` 3PL-gating change.
- No DB migration (reused existing `DispatchAt` column, `GetPendingDispatchAsync`, `PrepTimeCalculator`).
