# Hivago × ProRouting — Phase 1 Implementation

**Date:** 2026-05-05
**Branch:** `feat/pickup-orders`
**Status:** ✅ Complete — build green, 86 unit tests + 25 new Phase 1 tests passing, migration applied, smoke test passed
**Owner:** Backend

---

## Why this exists

Cross-referenced the **ProRouting 3rd-Party Support SOP** against the Hivago backend and identified what was missing for the platform to honor the SOP end to end:

- 8-stage delivery lifecycle including **RTO** (Return To Origin) sub-states
- **Pickup + Delivery OTPs** stored on the delivery and passed to the LSP
- **IGM** (Issue / Grievance Management) ticket entity with refund-policy hooks
- Inbound **Status Callback + Track Callback** webhooks from ProRouting (push, not poll)
- **OrderCategory** field (F&B / Grocery / Pharma) so the LSP can branch RTO disposal rules

---

## What ALREADY existed (didn't rebuild)

| Capability | File |
|---|---|
| `IThirdPartyDeliveryProvider` for ProRouting | `ProRoutingTaskService.cs` |
| `partner/quotes` (multi-LSP quotes) | `ProRoutingTaskService.GetQuotesAsync` |
| `partner/order/createasync` | `ProRoutingTaskService.CreateTaskAsync` |
| `partner/order/cancel` | `ProRoutingTaskService.CancelTaskAsync` |
| `partner/order/status` (poll) | `ProRoutingTaskService.GetTaskStatusAsync` |
| Inbound status webhook (HMAC + idempotency + audit log) | `WebhookEndpoints.cs` |
| `OrderReadyForPickup` event handler that triggers dispatch | `OrderReadyForPickupIntegrationEventHandler.cs` |
| OTP fields on the `ProRoutingCreateRequest` DTO | `Models/ProRoutingCreateRequest.cs` |

---

## What's NEW in Phase 1

### Domain (zero EF / framework deps)

- `OrderCategory` enum — `FoodAndBeverage` (default) / `Grocery` / `Pharma`
- `DeliveryRequestStatus` — added `RtoInitiated` (75), `RtoDelivered` (76), `RtoDisposed` (77)
- `DeliveryRequest` aggregate gained:
  - `OrderCategory`, `PickupCode` (6 digits), `DropCode` (4 digits) — crypto-random via `RandomNumberGenerator`
  - `LastRiderLatitude`, `LastRiderLongitude`, `LastLocationUpdatedAt` for live tracking
  - `RtoInitiatedAt`, `RtoDeliveredAt`, `RtoDisposedAt` timestamps
  - Methods: `InitiateRto()`, `MarkRtoDelivered()`, `MarkRtoDisposed()` (the last enforces FoodAndBeverage-only), `UpdateLiveLocation()` (rejects stale GPS pings)
- `IgmTicket` aggregate root with full lifecycle: `Create` → `MarkPushed` → `MarkResolved` → `Close`
- `IgmIssueType` enum mirroring SOP issue types — DelayInDelivery / FakePickup / FoodSpillage / RudeAgent / RiderRunaway / DeliveredButNotMarked
- `IgmTicketState` enum — Open / Processing / Resolved / Closed
- `IgmResolutionAction` enum — Refund / NoAction
- `IIgmTicketRepository` interface

### Integration — ProRouting

- `ProRoutingTaskService` now also implements `IIgmProvider`. Added:
  - `UpdateOrderAsync` → `POST /partner/order/update` (push OTPs + customer promised time)
  - `RaiseIssueAsync` → `POST /partner/order/issue`
  - `GetIssueStatusAsync` → `POST /partner/order/issue_status`
  - `CloseIssueAsync` → `POST /partner/order/issue_close`
- New request/response models: `ProRoutingUpdateRequest`, `ProRoutingIssueRequests`, `ProRoutingCallbacks`
- DI: `IIgmProvider` resolution forwards to the same `ProRoutingTaskService` instance — one HttpClient, one auth
- 🛠 **Pre-existing bug fixed:** the `IThirdPartyDeliveryProvider` HttpClient registration was using `api-key` header instead of `x-pro-api-key`. Now corrected.

### Inbound webhooks — `/api/webhooks/prorouting/*`

- **Auth (Option B): both modes accepted**
  - HMAC-SHA256 if `X-ProRouting-Signature` + `X-ProRouting-Timestamp` headers are present
  - `x-pro-api-key` header otherwise (matches actual ProRouting contract)
- **Status callback** at `/api/webhooks/prorouting` (alias `/api/webhooks/prorouting/status`)
  - Full state mapping including new RTO sub-states + optional `at-pickup` / `at-delivery`
  - 3PL pre-pickup failure → automatic fallback to own-fleet via `TriggerDispatchCommand`
  - Maintains existing Redis idempotency + `WebhookAuditLog`
- **Track callback** at `/api/webhooks/prorouting/track` — bulk live-GPS for every active order
  - Updates `LastRiderLatitude / Longitude / UpdatedAt` on each delivery
  - Stale pings (older than recorded last update) are silently ignored
  - Opportunistically refreshes rider name/phone if the LSP reassigned

### Workflow wiring

- `RiderDispatchOrchestrator.AssignVia3PLAsync` now passes `PickupCode`, `DropCode`, and `OrderCategory` (mapped to ProRouting's "F&B" / "Grocery" / "Pharma") to `CreateTaskRequest`
- `MapToCreateRequest` in `ProRoutingTaskService` writes them into `Pickup.Otp` / `Drop.Otp` / `OrderCategory`
- Hivago compresses ProRouting's two-call pattern (`createasync` then `update`) into a single `createasync` because dispatch is delayed until `OrderReadyForPickup` fires anyway. The standalone `/update` wrapper remains useful for admin OTP regen / customer-promised-time updates.

### Schema

EF migration `20260504221736_AddRtoStatesAndIgmTickets`:
- Adds 9 columns to `delivery.delivery_requests`: `order_category`, `pickup_code`, `drop_code`, `last_rider_latitude`, `last_rider_longitude`, `last_location_updated_at`, `rto_initiated_at`, `rto_delivered_at`, `rto_disposed_at`
- Creates table `delivery.igm_tickets` (24 columns + 4 indexes)
- Auto-generated, schema-qualified, applied successfully to local Postgres ✅

---

## ProRouting status mapping (canonical)

| ProRouting state | Hivago `DeliveryRequestStatus` | Method on `DeliveryRequest` |
|---|---|---|
| `UnFulfilled` / `Pending` | `PendingDispatch` | (no-op) |
| `Searching-for-Agent` | `Searching3PL` | (set during dispatch) |
| `Agent-assigned` | `Assigned3PL` | `Assign3PLRider()` |
| `At-pickup` (optional) | `RiderArrivedPickup` | `MarkRiderEnRoutePickup()` + `MarkRiderArrivedPickup()` |
| `Order-picked-up` | `PickedUp` | `MarkPickedUp()` |
| `At-delivery` (optional) | `RiderArrivedDrop` | `MarkRiderEnRouteDrop()` + `MarkRiderArrivedDrop()` |
| `Order-delivered` | `Delivered` | `MarkDelivered()` |
| `RTO-Initiated` | `RtoInitiated` | `InitiateRto()` (or fallback to own-fleet if pre-pickup) |
| `RTO-Delivered` | `RtoDelivered` | `MarkRtoDelivered()` |
| `RTO-Disposed` | `RtoDisposed` | `MarkRtoDisposed()` (FoodAndBeverage only) |
| `Cancelled` / `failed` | `Failed` (or fallback) | `MarkFailed()` |

`At-pickup` and `At-delivery` are **optional** per the ProRouting docs — some LSPs don't emit them. The state machine accepts both flows.

---

## Refund eligibility (per SOP — Phase 2 wiring)

| Issue type | Refund? |
|---|---|
| Delay in delivery | ✅ |
| Fake pickup | ✅ |
| Food spillage | ✅ |
| Rude agent | ✅ |
| Rider runaway | ✅ |
| Delivered but not marked | ❌ |

Phase 1 ships the data model only (`IgmTicket.IssueType`). The refund-trigger automation lives in Phase 2.

---

## RTO disposal rules

- `RTO-Disposed` is **only valid for `OrderCategory.FoodAndBeverage`**
- `Grocery` and `Pharma` must always be returned to the store (`RTO-Delivered`)
- Enforced in `MarkRtoDisposed()` with an `InvalidOperationException`

---

## Auth

| Direction | Method | Header | Source of value |
|---|---|---|---|
| Outbound (Hivago → ProRouting) | API key | `x-pro-api-key` | `ProRouting:ApiKey` config |
| Inbound (ProRouting → Hivago) | API key (default) | `x-pro-api-key` | `PROROUTING_INBOUND_API_KEY` config |
| Inbound (optional, future-proofing) | HMAC-SHA256 | `X-ProRouting-Signature` + `X-ProRouting-Timestamp` | `WEBHOOK_PROROUTING_SECRET_CURRENT` (with rotation slot) |

---

## Verification done

| Check | Result |
|---|---|
| Full solution build | ✅ 0 errors, 0 warnings |
| `Delivery.Application.Tests` | ✅ 36/36 passing (11 pre-existing + 25 new for Phase 1) |
| `Orders.Application.Tests` | ✅ 22/22 |
| `Orders.Domain.Tests` | ✅ 34/34 |
| `Pricing.Application.Tests` | ✅ 9/9 |
| `Users.Infrastructure.Tests` | ✅ 10/10 |
| EF migration generated + applied to local Postgres | ✅ schema verified via psql |
| Host smoke test (`dotnet run`) | ✅ all 6 module DBs migrated, listening on `:5023`, no startup errors |
| Pre-existing flow regression | ✅ none — confirmed by running `Orders` + `Delivery` tests against the changed code |

**Pre-existing integration test failures** (e.g. `DeliveryFlowTests` calling non-existent `/api/delivery/request`, `OrderFlowTests` 400s on Google Maps geocoding) are unrelated to Phase 1 — proven by stashing the changes and observing the baseline doesn't even build.

---

## Files changed

### New files

```
src/Modules/Delivery/RallyAPI.Delivery.Domain/Enums/OrderCategory.cs
src/Modules/Delivery/RallyAPI.Delivery.Domain/Enums/IgmIssueType.cs
src/Modules/Delivery/RallyAPI.Delivery.Domain/Enums/IgmTicketState.cs
src/Modules/Delivery/RallyAPI.Delivery.Domain/Enums/IgmResolutionAction.cs
src/Modules/Delivery/RallyAPI.Delivery.Domain/Entities/IgmTicket.cs
src/Modules/Delivery/RallyAPI.Delivery.Domain/Abstractions/IIgmTicketRepository.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/DeliveryDbContextFactory.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/Repositories/IgmTicketRepository.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/Persistence/Configurations/IgmTicketConfiguration.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/Migrations/20260504221736_AddRtoStatesAndIgmTickets.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/Migrations/20260504221736_AddRtoStatesAndIgmTickets.Designer.cs
src/Modules/Integrations/ProRouting/Models/ProRoutingUpdateRequest.cs
src/Modules/Integrations/ProRouting/Models/ProRoutingIssueRequests.cs
src/Modules/Integrations/ProRouting/Models/ProRoutingCallbacks.cs
src/RallyAPI.SharedKernel/Abstractions/Delivery/IIgmProvider.cs
tests/Modules/Delivery/RallyAPI.Delivery.Application.Tests/DeliveryRequestPhase1Tests.cs
tests/Modules/Delivery/RallyAPI.Delivery.Application.Tests/IgmTicketTests.cs
docs/prorouting-integration.md
docs/hivago-prorouting-phase1-notion.md
```

### Modified files

```
src/Modules/Delivery/RallyAPI.Delivery.Domain/Entities/DeliveryRequest.cs
src/Modules/Delivery/RallyAPI.Delivery.Domain/Enums/DeliveryRequestStatus.cs
src/Modules/Delivery/RallyAPI.Delivery.Application/Services/RiderDispatchOrchestrator.cs
src/Modules/Delivery/RallyAPI.Delivery.Endpoints/WebhookEndpoints.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/DependencyInjection.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/Persistence/DeliveryDbContext.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/Persistence/Configurations/DeliveryRequestConfiguration.cs
src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/Migrations/DeliveryDbContextModelSnapshot.cs
src/Modules/Integrations/ProRouting/ProRoutingTaskService.cs
src/Modules/Integrations/ProRouting/DependencyInjection.cs
src/RallyAPI.SharedKernel/Abstractions/Delivery/IThirdPartyDeliveryProvider.cs
src/RallyAPI.SharedKernel/Abstractions/Delivery/CreateTaskRequest.cs
```

---

## Pre-launch checklist (operational, before going live)

- [ ] Send the two preprod callback URLs to **mahesh@prorouting.in**:
  - `https://<preprod-host>/api/webhooks/prorouting/status`
  - `https://<preprod-host>/api/webhooks/prorouting/track`
- [ ] Confirm with Mahesh they ack registration and fire a test event
- [ ] Set `ProRouting__ApiKey` and `PROROUTING_INBOUND_API_KEY` in Railway preprod env vars
- [ ] Run a manual end-to-end: place order → restaurant marks ready → confirm 3PL assigned via webhook → confirm Track Callback updates `last_rider_*` columns
- [ ] Confirm with Mahesh whether they HMAC-sign callbacks in production. If yes, set `WEBHOOK_PROROUTING_SECRET_CURRENT`. If no, leave the api-key path active.
- [ ] Re-run all of the above for production URLs

---

## Phase 2 (intentionally out of scope)

- Auto-trigger rules for IGM (delay watcher → IGM, fake-pickup detection → IGM)
- 45–60 min in-transit delivery delay watcher (background hosted service)
- Refund-policy mapping by `IgmIssueType` → automatic refund via PayU
- WhatsApp L2 escalation wiring (`Msg91WhatsAppService` exists but isn't connected)
- Admin action audit log (who did what when)
- Cleanup of orphan duplicate `Rally/ProRoutingWebhookPayload.cs` at the repo root
- Standalone admin `UpdateProRoutingOrderCommand` for OTP regen / time-promise updates

---

## ⚠️ Pre-commit reminder for migrations

This branch touches EF entities and adds a migration. Before `git commit`:

- `dotnet build` is green ✅
- Migration applied locally ✅ (verified via `docker exec rally-postgres psql ...`)
- Smoke test: host boots without throwing ✅
- Confirm preprod env vars set in Railway before deploying
- Railway auto-migrates on boot — if the migration is broken, the deploy silently stays on the old container
