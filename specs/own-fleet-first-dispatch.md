# Feature Spec: Own-Fleet-First Broadcast Dispatch

> **Status**: Draft
> **Priority**: P1 (High)
> **Estimated Effort**: 1.5–2 days
> **Module(s)**: Delivery, Users
> **Owner**: Yash
> **Date**: 2026-07-08
> **Base branch**: `staging` (already contains the xmin concurrency token + accept enablers — see §0)

---

## 0. Base Branch & Prerequisites (READ FIRST)

Broadcast dispatch offers one order to **multiple own riders at once**; the first to accept wins. This is only correct if concurrent accepts are race-safe. The enabling infra already exists on **`staging`** but **NOT** on `master` or `feature/payment-fail-and-session-hardening`:

| Prerequisite | Commit | On `staging`? | On `master`/current? |
|---|---|---|---|
| `xmin` optimistic-concurrency token on `DeliveryRequest` (`UseXminAsConcurrencyToken`) | `b78e764` | ✅ | ❌ |
| `IDeliveryRequestRepository.TryUpdateAsync` (catches `DbUpdateConcurrencyException`) | `b78e764` | ✅ | ❌ |
| `IDeliveryRequestRepository.GetCurrentStatusAsync` (AsNoTracking scalar) + `MarkFailed` `Status >= RiderAssigned` guard | `894df2f` | ✅ | ❌ |

**Decision: build this feature on a branch cut from `staging`** (e.g. `feature/own-fleet-broadcast-dispatch`). `staging` also already contains this branch's payment/session work (`HEAD` is an ancestor of `staging`), so nothing is lost.

⚠️ The xmin migration (`20260627181442_AddDeliveryRequestXminConcurrencyToken`) has **empty Up/Down** on purpose — `xmin` is a Postgres system column. Do not regenerate or "fix" it.

**Still-open hardening (deferred per `project_concurrent_dispatch_deferred`, required now):** even on `staging`, `AcceptDeliveryOfferCommandHandler` still uses plain `UpdateAsync` and has no "already assigned" guard. Broadcast makes concurrent accepts the common case, so this hardening (§4.3) is **in scope** for this feature.

## 1. Problem Statement

Dispatch currently offers every order to the **3PL provider (ProRouting) first**, falling back to our own fleet only on 3PL timeout — and the own-fleet fallback is **sequential** (offer rider 1, wait full timeout, offer rider 2, …), so time-to-assign scales `N × timeout`. We want to (a) prefer our own riders, and (b) offer to all nearby own riders **simultaneously** so the fastest to accept wins, then fall back to 3PL only if none accept. This maximizes in-house fulfilment (better margin) without the latency of one-at-a-time offers.

## 2. User Stories

- As the **business**, I want orders broadcast to our own riders first so we save the 3PL fee whenever an own rider can take it.
- As a **customer**, I want the fastest available rider assigned, and a quick fall-through to 3PL if none accept, so my food isn't delayed.
- As an **own-fleet rider**, I want to see nearby orders the moment they're ready and grab them first-come-first-served.
- As an **admin**, I want to see whether each order went own-fleet vs 3PL and why, so I can monitor fleet utilisation.

## 3. Acceptance Criteria

- [ ] A new delivery request begins dispatch in `SearchingOwnFleet`, not `Searching3PL`.
- [ ] **Broadcast:** an offer is created for **every** eligible own rider within `SearchRadiusKm`, capped at `MaxRidersToTry` (10), and all notifications are sent concurrently (`Task.WhenAll`).
- [ ] **Single wait window:** the orchestrator waits up to `AcceptanceTimeoutSeconds`, polling fresh (AsNoTracking) status for an early accept, instead of one timeout per rider.
- [ ] **First-accept-wins:** exactly one rider is assigned; all other pending offers for that request are expired/cancelled.
- [ ] Two riders accepting near-simultaneously → exactly one succeeds; the loser gets a clean `Result.Failure("offer already taken")` (HTTP 409/validation), **not** a 500, and the loser rider's `CurrentDeliveryId` is **not** left set.
- [ ] If **no** eligible own riders exist → fall through to 3PL immediately (no artificial delay).
- [ ] If own riders are offered but none accept within the window → expire all offers, fall back to 3PL.
- [ ] If 3PL then fails/times out → request marked `Failed` with an accurate combined-exhaustion reason.
- [ ] 3PL path unchanged once reached (task create → OTP push → wait for webhook → `Assigned3PL` or cancel-and-fail).
- [ ] Behaviour guarded by `OwnFleetFirst` config kill-switch (default `true`) to restore 3PL-first without a redeploy.

## 4. Technical Design

### Overview: the flip + broadcast

Today (`RiderDispatchOrchestrator.DispatchAsync`):

```
StartSearching() → Searching3PL
  → AssignVia3PLAsync (wait 30s)
      fail → AssignViaOwnFleetAsync  ← SEQUENTIAL: rider 1 (30s) → rider 2 (30s) → …
```

Target:

```
StartSearchingOwnFleet() → SearchingOwnFleet
  → BroadcastToOwnFleetAsync            ← ALL eligible riders at once, ONE wait window
      no eligible riders → instant fallthrough
      window elapses, none accepted → expire all → StartSearching3PL()
          → AssignVia3PLAsync (unchanged)
              fail → MarkFailed(NoRidersAvailable, "Own fleet + 3PL exhausted")
```

The existing state machine already supports both directions — **no new statuses**:
- `StartSearchingOwnFleet()` legalizes `Created`/`PendingDispatch`/`Searching3PL` → `SearchingOwnFleet`
- `StartSearching3PL()` legalizes `Created`/`PendingDispatch`/`SearchingOwnFleet` → `Searching3PL`

### 4.1 Domain Changes (Delivery.Domain)

- `DeliveryRequest.StartSearching()` — currently hardcodes `Searching3PL`; repoint to open in `SearchingOwnFleet` (delegate to `StartSearchingOwnFleet()`) so `TriggerDispatchCommandHandler` needs no change.
- `MarkFailed` — keep/confirm the `if (Status >= RiderAssigned) return;` guard (from `894df2f`, present on `staging`). No new schema.
- No change to `RiderOffer` (already supports `Accept`/`Expire`/`Cancel`, `IsPending`).

### 4.2 Orchestrator: `BroadcastToOwnFleetAsync` (Delivery.Application)

Replace the sequential `AssignViaOwnFleetAsync` loop with a broadcast:

1. Enter own-fleet state: from `Created`/`PendingDispatch` use `StartSearchingOwnFleet()`; from `Searching3PL`/`Assigned3PL` use `TransitionToOwnFleetSearch()` (clears stale 3PL fields). Persist.
2. `riders = GetAvailableRidersAsync(pickupLat, pickupLng, SearchRadiusKm, MaxRidersToTry)` — already ordered by distance ascending.
3. If empty → **return `Failed`** (do NOT `MarkFailed`; let `DispatchAsync` proceed to 3PL).
4. Create one `RiderOffer` per rider up front (`CreateOffer`), single `UpdateAsync`/save.
5. Send all notifications concurrently: `await Task.WhenAll(riders.Select(r => _notificationService.SendDeliveryOfferAsync(...)))`. Mark `NotificationSent` on success. (Broadcast still works even if a notification fails — that rider just won't see it.)
6. **Single wait window with polling:** loop until `AcceptanceTimeoutSeconds` elapsed, sleeping a short poll interval (`OfferPollIntervalSeconds`, e.g. 2s); each tick call `GetCurrentStatusAsync(id)` (AsNoTracking scalar — avoids the stale-tracking clobber from `project_dispatch_race_clobber`). If status `>= RiderAssigned` (i.e. an accept committed) → reload, return `Success(OwnFleet, riderId)`.
7. On timeout → `ExpireAllPendingOffers()`, `TryUpdateAsync`. Return `Failed` (fall to 3PL). Terminal `MarkFailed` only happens after 3PL also fails, in `DispatchAsync`.

Notes:
- Keep `GetCurrentStatusAsync` for the poll (cheap, no tracking-map staleness). Reload the full aggregate only once, when an accept is detected.
- Fix the misleading failure strings that mention "ProRouting failed/timed out" for the own-fleet-empty case.

### 4.3 Accept-handler hardening — REQUIRED (Delivery.Application + Users)

`AcceptDeliveryOfferCommandHandler` today: loads ALL `SearchingOwnFleet` requests, finds the offer, checks `offer.Status == Pending`, `AssignOwnFleetRider`, then `AssignDeliveryToRiderAsync` (Users, separate DbContext/txn), then `UpdateAsync`. Two problems under broadcast:

**(a) Lost update on the DeliveryRequest.** Two riders with two different pending offers both pass the per-offer guard and both write. Fix:
- Add an early guard: after loading, `if (deliveryRequest.Status >= RiderAssigned) return Result.Failure(Error.Validation("This delivery has already been assigned."))`.
- Switch the assignment write to `TryUpdateAsync`; on concurrency loss return the same clean `"offer already taken"` failure (409-style), **not** a 500.

**(b) Loser rider left stuck.** Currently the Users-side `AssignDeliveryToRiderAsync` (sets `CurrentDeliveryId`, commits) runs **before** the DeliveryRequest write is confirmed. If this rider loses the race, their `CurrentDeliveryId` is already committed → stuck rider (`project_stuck_rider_release`). Fix by reordering:
1. Assign on the aggregate (`AssignOwnFleetRider`), `TryUpdateAsync` **first** — this is the race-deciding write (xmin).
2. Only if that write **wins**, call `AssignDeliveryToRiderAsync` to set the rider's `CurrentDeliveryId`.
3. If the write loses, do **not** touch the rider; return the clean failure.

`Rider.AssignDelivery(...)` already returns a `Result` and rejects if the rider already holds a delivery — keep that as a second-layer guard, but ordering (1→2) is what prevents the stuck-loser.

(Optional efficiency, not required: the "load ALL SearchingOwnFleet requests then loop" lookup is O(n) per accept. Consider `GetByOfferIdAsync`. Out of scope unless trivial.)

### 4.4 Config (`DispatchOptions` + appsettings `Delivery:Dispatch`)

```jsonc
"Dispatch": {
  "OwnFleetFirst": true,            // NEW — kill-switch; false = legacy 3PL-first
  "SearchRadiusKm": 5.0,
  "MaxRidersToTry": 10,             // broadcast fan-out cap (reused)
  "OfferPollIntervalSeconds": 2,    // NEW — poll cadence within the wait window
  "AcceptanceTimeoutSeconds": 30,   // single own-fleet broadcast window
  "RiderEarningsPercentage": 80,
  "WebhookUrl": "https://your-domain.com/api/webhooks/prorouting"
}
```

### Commands & Queries (MediatR)

| Type | Name | Change |
|------|------|--------|
| Command | `TriggerDispatchCommand` | No signature change; `StartSearching()` now opens `SearchingOwnFleet`. |
| Command | `AcceptDeliveryOfferCommand` | Hardened (§4.3): status guard + `TryUpdateAsync` + reorder rider write. |
| Repo | `GetCurrentStatusAsync`, `TryUpdateAsync` | Already on `staging`; used by orchestrator + accept handler. |

### API Endpoints / SignalR

No new endpoints. Broadcast reuses `IRiderNotificationService.SendDeliveryOfferAsync` (SignalR/stub) — now fanned out via `Task.WhenAll` instead of one-at-a-time.

### Cross-Module Communication

| From | To | Via | Note |
|------|----|-----|------|
| Delivery | Users | `IRiderQueryService.GetAvailableRidersAsync` | Called first; returns all eligible in radius (cap 10). |
| Delivery | Users | `IRiderCommandService.AssignDeliveryToRiderAsync` | Called **only after** the DeliveryRequest assignment write wins (§4.3b). |
| Delivery | ProRouting | `IThirdPartyDeliveryProvider` | Reached only on own-fleet miss; unchanged. |

### Database Migrations

None new. Relies on the existing empty-body xmin migration already on `staging`.

## 5. Edge Cases & Error Handling

| Scenario | Expected Behavior |
|----------|-------------------|
| Zero eligible own riders | `GetAvailableRidersAsync` empty → return `Failed` from own phase → book 3PL immediately. No delay, no `MarkFailed`. |
| Riders offered, none accept | Window elapses → `ExpireAllPendingOffers` → fall to 3PL. |
| Two riders accept ~same instant | xmin makes one write win; loser gets `Result.Failure("already assigned")` (409), loser `CurrentDeliveryId` untouched. |
| Rider accepts as window is closing | Poll or final reload detects `RiderAssigned` → `Success(OwnFleet)`; 3PL never booked. |
| Notification send fails for some riders | Broadcast proceeds; those riders simply don't get the offer. Log per-rider failures. |
| 3PL `CreateTask` fails after own miss | `MarkFailed(NoRidersAvailable, "Own fleet + 3PL exhausted")`, no dangling task. |
| `OwnFleetFirst=false` | Legacy 3PL-first ordering restored exactly. |
| Genuine concurrent non-dispatch edit (MarkPickedUp etc.) | Pre-existing gap: those writers still surface `DbUpdateConcurrencyException` as 500. Out of scope; note for follow-up. |

## 6. Testing Plan

- **Domain unit tests** (`DeliveryRequest`): `StartSearching()` → `SearchingOwnFleet`; `SearchingOwnFleet → StartSearching3PL()` legal; `MarkFailed` no-ops when `Status >= RiderAssigned`.
- **Orchestrator unit tests** (`RiderDispatchOrchestratorTests`):
  - No own riders → 3PL booked once, no delay, no offers created.
  - N riders → **N offers created and N notifications sent** (assert broadcast, not sequential), single wait window.
  - Accept mid-window (simulate status flip via mocked `GetCurrentStatusAsync`) → `Success(OwnFleet)`, 3PL never called.
  - No accept → all offers expired, 3PL booked → `Success(ThirdParty)`.
  - Own miss + 3PL fail → `MarkFailed(NoRidersAvailable)` combined note.
  - `OwnFleetFirst=false` → legacy order of calls.
- **Accept-handler unit tests** (new/expanded): concurrent accept where DeliveryRequest is already `RiderAssigned` → clean `Result.Failure`, no rider write; concurrency-loss (`TryUpdateAsync` returns false) → clean failure, loser `CurrentDeliveryId` not set.
- **Integration**: two parallel `AcceptDeliveryOffer` calls against one broadcast request → exactly one 200, one 409, one rider assigned, one free.

## 7. Rollout

- [ ] Build on branch off `staging`.
- [ ] Config kill-switch `Delivery:Dispatch:OwnFleetFirst` (default `true`; `false` reverts instantly).
- [ ] Metrics: % own-fleet vs 3PL; time-to-assign p50/p95; broadcast fan-out size; concurrent-accept 409 count; failed dispatches.
- [ ] Rollback: flip `OwnFleetFirst=false` (no redeploy).
- [ ] Deploy note: merge order must keep the xmin migration ahead of any deploy of this feature (already on `staging`).

---

## Implementation Notes (updated during build)

### Files to Modify
- `src/Modules/Delivery/RallyAPI.Delivery.Application/Services/RiderDispatchOrchestrator.cs` — flip order; replace sequential loop with broadcast + single polling window; options.
- `src/Modules/Delivery/RallyAPI.Delivery.Domain/Entities/DeliveryRequest.cs` — repoint `StartSearching()` to `SearchingOwnFleet`.
- `src/Modules/Delivery/RallyAPI.Delivery.Application/Commands/AcceptDeliveryOffer/AcceptDeliveryOfferCommandHandler.cs` — status guard, `TryUpdateAsync`, reorder rider write (§4.3).
- `src/RallyAPI.Host/appsettings.json` — `OwnFleetFirst`, `OfferPollIntervalSeconds`.
- `tests/Modules/Delivery/RallyAPI.Delivery.Application.Tests/RiderDispatchOrchestratorTests.cs` (+ accept-handler tests) — update/extend.

### Decisions Made
- **Broadcast (parallel), not sequential** — fire offers to ALL eligible riders in radius, cap `MaxRidersToTry` (10), first-accept-wins. (Supersedes the earlier "cap to nearest 1–2" idea.)
- **Own-fleet first, 3PL fallback** — via config kill-switch `OwnFleetFirst`.
- **Base = `staging`** — already has the xmin token + `TryUpdateAsync`/`GetCurrentStatusAsync` prerequisites (`b78e764`, `894df2f`) that `master`/current branch lack.
- Accept-handler hardening promoted from "deferred" to in-scope (broadcast makes concurrent accepts normal).

### Known Related Issues (not addressed here)
- Dispatch still runs **inline/blocking** inside the integration-event handler. Broadcast shortens the own-fleet phase to one window (better than sequential), but the 3PL 30s wait still blocks. Durable/background runner is a separate follow-up.
- Non-dispatch `DeliveryRequest` writers (MarkPickedUp/MarkDelivered/…) can still surface `DbUpdateConcurrencyException` as 500 under genuine concurrent edits — pre-existing, out of scope.

### Open Questions
- Poll interval (2s) vs. push-based early-accept signal — polling chosen for simplicity; revisit if 2s latency matters.
- Should the broadcast radius differ from the 3PL/quote radius? Reusing existing `SearchRadiusKm` (5 km) for now.
