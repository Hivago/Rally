# ProRouting Integration Guide

> Last updated: 2026-05-05
> Owner: Backend
> Contact (ProRouting): mahesh@prorouting.in

## What ProRouting Is

ProRouting is a **logistics gateway** that fronts multiple LSPs (Logistics Service Providers) on the ONDC network — LoadShare, Shadowfax, OLA, Adloggs, Pidge, Zypp, Porter, SUPR (Swiggy Genie), Qwqer, Yulu, Telyport, Magicfleet. Hivago calls ProRouting; ProRouting picks the best LSP, books the rider, and pushes lifecycle updates to our webhook.

Hivago uses the **`fastest_agent`** mode (createasync). LSP fallback within the ProRouting layer is automatic — we don't implement 3PL retry logic. If ProRouting can't find any LSP, our `RiderDispatchOrchestrator` falls back to own-fleet riders.

## Environments

| Environment | Base URL |
|---|---|
| Preprod (staging) | `https://preprod.logistics-buyer.prorouting.in` |
| Production | `https://client-api.prorouting.in` |

Configured via `ProRouting:BaseUrl` in `appsettings.json`.

## Auth

Single static API key in the `x-pro-api-key` header for **outbound** calls. Configured via `ProRouting:ApiKey`.

For **inbound** webhooks we accept either:
- **HMAC-SHA256** if `X-ProRouting-Signature` + `X-ProRouting-Timestamp` headers are present (used internally for tests / future-proofing)
- **`x-pro-api-key`** if HMAC headers are absent (matches actual ProRouting contract)

The inbound key is read from `PROROUTING_INBOUND_API_KEY` (preferred) or falls back to `ProRouting:ApiKey`.

## Endpoints We Call

| Endpoint | Wrapper | Purpose |
|---|---|---|
| `POST /partner/quotes` | `ProRoutingTaskService.GetQuotesAsync` | Pricing across all LSPs |
| `POST /partner/estimate` | `ProRoutingClient.GetQuoteAsync` | Locked single price |
| `POST /partner/order/createasync` | `ProRoutingTaskService.CreateTaskAsync` | Create + immediately mark ready (Hivago compresses two ProRouting calls into one) |
| `POST /partner/order/update` | `ProRoutingTaskService.UpdateOrderAsync` | Push OTPs + customer promised time **after** order placement (admin override / OTP regen) |
| `POST /partner/order/cancel` | `ProRoutingTaskService.CancelTaskAsync` | Cancel pre-pickup only — post-pickup → RTO is the only path |
| `POST /partner/order/status` | `ProRoutingTaskService.GetTaskStatusAsync` | Fallback poll if webhook missed |
| `POST /partner/order/issue` | `ProRoutingTaskService.RaiseIssueAsync` | Raise IGM ticket |
| `POST /partner/order/issue_status` | `ProRoutingTaskService.GetIssueStatusAsync` | Poll resolution |
| `POST /partner/order/issue_close` | `ProRoutingTaskService.CloseIssueAsync` | Close ticket with rating + refund decision |

## Endpoints They Call (Inbound Webhooks)

We expose two callbacks. **Both URLs must be registered with ProRouting (mahesh@prorouting.in) before preprod testing.**

| Path | Purpose | Frequency |
|---|---|---|
| `POST /api/webhooks/prorouting` (alias: `/api/webhooks/prorouting/status`) | State transitions: `Agent-assigned`, `At-pickup`, `Order-picked-up`, `At-delivery`, `Order-delivered`, `RTO-Initiated`, `RTO-Delivered`, `RTO-Disposed`, `Cancelled` | One per state change |
| `POST /api/webhooks/prorouting/track` | Bulk live-GPS payload for every active order | Continuous (per ProRouting's schedule) |

### Callback URLs to Send to Mahesh

**Preprod:**
```
Status: https://<your-preprod-host>/api/webhooks/prorouting/status
Track:  https://<your-preprod-host>/api/webhooks/prorouting/track
```

**Production:**
```
Status: https://<your-prod-host>/api/webhooks/prorouting/status
Track:  https://<your-prod-host>/api/webhooks/prorouting/track
```

Replace `<your-preprod-host>` / `<your-prod-host>` with the Railway-deployed hostnames.

### Local Testing (ngrok)

ProRouting cannot reach `localhost`. Use ngrok to expose your dev machine:

```powershell
# 1. Install ngrok (one-time)
choco install ngrok            # or download from https://ngrok.com/download

# 2. Start the API locally
dotnet run --project src/RallyAPI.Host

# 3. In a second terminal, expose port 5000 (or whatever Host listens on)
ngrok http 5000

# 4. Send the ngrok URL to mahesh@prorouting.in:
#    Status: https://<random>.ngrok-free.app/api/webhooks/prorouting/status
#    Track:  https://<random>.ngrok-free.app/api/webhooks/prorouting/track
```

ngrok URLs change on every restart — re-send to ProRouting whenever you restart the tunnel.

## Status State Machine — ProRouting → Hivago Mapping

| ProRouting | Hivago `DeliveryRequestStatus` | Method called on `DeliveryRequest` |
|---|---|---|
| `UnFulfilled` / `Pending` | `PendingDispatch` | (no-op — already in this state) |
| `Searching-for-Agent` | `Searching3PL` | (set when CreateTaskAsync returns) |
| `Agent-assigned` | `Assigned3PL` | `Assign3PLRider()` / `Update3PLRiderInfo()` |
| `At-pickup` | `RiderArrivedPickup` | `MarkRiderEnRoutePickup()` + `MarkRiderArrivedPickup()` |
| `Order-picked-up` | `PickedUp` | `MarkPickedUp()` |
| `At-delivery` | `RiderArrivedDrop` | `MarkRiderEnRouteDrop()` + `MarkRiderArrivedDrop()` |
| `Order-delivered` | `Delivered` | `MarkDelivered()` |
| `RTO-Initiated` | `RtoInitiated` | `InitiateRto()` (or fallback to own-fleet if pre-pickup) |
| `RTO-Delivered` | `RtoDelivered` | `MarkRtoDelivered()` |
| `RTO-Disposed` | `RtoDisposed` | `MarkRtoDisposed()` (FoodAndBeverage only) |
| `Cancelled` / `failed` | `Failed` (or fallback) | `MarkFailed()` |

`At-pickup` and `At-delivery` are documented as optional by ProRouting — some LSPs don't emit them. The state machine accepts both flows (with or without them).

## OTPs

Hivago generates OTPs locally at `DeliveryRequest.Create()`:
- `PickupCode` — 6 digits, shown to rider at restaurant (`pickup_code` in ProRouting)
- `DropCode` — 4 digits, shared with customer (`drop_code` in ProRouting)

Both are crypto-random (`RandomNumberGenerator.GetInt32`) and stored cleartext (LSP needs them in cleartext). They are passed to ProRouting via `createasync`, in the existing `Pickup.Otp` / `Drop.Otp` fields. No separate `/update` call needed in the standard flow — that endpoint is reserved for admin OTP regen / time-promise updates.

## RTO Rules

RTO is **system-initiated by the LSP**. Hivago has no API to *trigger* RTO; we only react to the inbound webhook. Admin overrides flag the ticket internally for follow-up; they do not call any ProRouting endpoint.

`RTO-Disposed` is only valid for `OrderCategory.FoodAndBeverage`. Grocery/Pharma must always be returned to the store (`RTO-Delivered`). The domain method `MarkRtoDisposed()` enforces this with an `InvalidOperationException`.

## Cancel Rules

`/partner/order/cancel` works **only before pickup**. After pickup the only path is RTO. The current `RiderDispatchOrchestrator` already cancels the 3PL task on its own timeout before falling back to own fleet.

## IGM (Issue / Grievance Management)

Phase 1 ships the `IgmTicket` aggregate + repository + ProRouting client wrappers. Auto-trigger rules (e.g. delay watcher → IGM, fake-pickup detection → IGM) are deferred to Phase 2.

ProRouting issue sub-categories used:
- `FLM02`
- `FLM03`
- `FLM08`

Resolution `action_triggered` values: `REFUND` (with `refund_amount`) or `NO-ACTION`.

## Environment Variables

```
ProRouting__BaseUrl=<preprod or prod URL>
ProRouting__ApiKey=<from mahesh@prorouting.in>
ProRouting__Enabled=true

# Inbound webhook auth (Option B: both modes accepted)
PROROUTING_INBOUND_API_KEY=<same key as ApiKey, or different per env>

# Optional HMAC backup (only if we ever sign callbacks ourselves in tests)
WEBHOOK_PROROUTING_SECRET_CURRENT=<hex secret>
WEBHOOK_PROROUTING_SECRET_PREVIOUS=<previous secret during rotation>
WEBHOOK_PROROUTING_TIMESTAMP_TOLERANCE_SECONDS=300
```

## Pre-Launch Checklist

- [ ] Send preprod callback URLs to mahesh@prorouting.in
- [ ] Confirm ProRouting acks the registration (test fire from their end)
- [ ] Set `ProRouting:ApiKey` and `PROROUTING_INBOUND_API_KEY` in Railway preprod env
- [ ] Run a manual end-to-end: place order → mark ready → confirm rider assigned via webhook → confirm Track Callback updates `last_rider_*` columns
- [ ] Switch to prod URLs + send prod callback URLs to mahesh
