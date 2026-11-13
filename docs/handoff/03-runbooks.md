# 03 — Runbooks (incidents we've already had)

Format: **Symptom → Cause → Fix/Check**. Dates are when diagnosed.

## R1 — Customer paid, restaurant sees "payment pending" (2026-07-16, twice)
- **Cause:** PayU S2S webhook was never configured in the PayU dashboard (`audit.webhook_audit_logs` had only `prorouting` rows), and `/return/success` discarded PayU's signed POST. Order stayed `Pending` until auto-cancel → manual refund.
- **Fix in code:** return endpoints now run `ProcessPayuWebhookCommand` before redirecting; `PendingPaymentReconciliationService` (60 s) verifies with PayU; `OrderAutoCancelService` verifies before cancelling; Redis idempotency lock is released on transient failure.
- **Still needs a human:** (a) PayU dashboard webhooks (success+failure) → `https://api.hivago.in/api/payments/webhook`; (b) `PayU__FrontendSuccessUrl/FailureUrl` set on Railway. Verify both exist.
- **Check:** `select * from audit.webhook_audit_logs where provider ilike '%payu%' order by created_at desc limit 20;` — empty ⇒ webhook not configured.
- Reference: `specs/payu-return-redirect-and-same-tab.md`. Refund via admin endpoint / `RefundPayment`.

## R2 — UPI not shown on PayU checkout (2026-07-13)
Check in order: (1) posted `phone` must be a bare 10-digit Indian mobile (fixed by `NormalizePhone`, commit `6275f99`); (2) `PayU__BaseUrl=https://secure.payu.in` **and** live key/salt (all three must match); (3) UPI enabled on the PayU MID (escalate to PayU naming the live merchant key);
(4) phone test: on mobile PayU shows UPI as *intent* and hides it if no UPI app is installed — not a bug.
Diagnostic: call `POST /api/payments/initiate` and read `payUBaseUrl`, `key`, `phone`, `surl`.

## R3 — Rider never gets the offer
Work down this list (each was a real cause):
1. **Auth** (R5): rider 401 on SignalR hub / `pending-offer` poll.
2. **Eligibility:** `GET /api/deliveries/diagnostics/rider-eligibility?...` (Admin) shows `failedChecks`. Gates: active, online, KYC Verified, `CurrentDeliveryId` null, has location, location fresh, within 5 km of the *restaurant*.
3. **Rider pinned to a dead delivery:** `POST /api/admin/riders/{riderId}/release-delivery` (Admin) or
   `UPDATE users.riders SET current_delivery_id = NULL, current_delivery_assigned_at = NULL, updated_at = now() WHERE id = '<id>';`
4. **Dispatch wedged:** `DeliveryDispatchRecoveryService` re-triggers requests stuck >2 min (but never older than 3 h — R4).
- Note `SendDeliveryOfferAsync` returns success even if the rider socket is offline; riders can still get it by polling.

## R4 — Mass re-dispatch of old orders / unexpected 3PL bookings (2026-07-09)
- **What happened:** on deploy the recovery service re-dispatched ~68 April/May orders in 48 s; each booked a *real* ProRouting rider. Fixed by `StuckMaxAge = 3h` age floor (commit `403ce00`).
- **Diagnose bookings:** use `delivery.delivery_requests.third_party_dispatched_at` (survives cancel). `external_task_id` is cleared on cancel/timeout — counting it undercounts. Cost columns (`provider_total_with_tax`) only fill on the agent-assignment webhook; reconcile charges against the ProRouting invoice.
- **Danger:** the "never-fail dispatch" change (commit `1aa1869`, `feature/early-predictive-dispatch`) removes all `MarkFailed(NoRidersAvailable)`. Against a stale backlog it loops forever booking 3PL. Verify the backlog is clean before deploying it.

## R5 — Intermittent 401s / "signature key was not found" (2026-06-24)
- **Cause:** replicas (or a redeploy transition) signing with different RSA keys. API validates exactly one public key.
- **Fix:** set `JwtSettings__PrivateKeyPem` + `JwtSettings__PublicKeyPem` as one matching pair in shared Railway env; redeploy; keep it stable. Rotating it logs every user out — do it deliberately and announce it.

## R6 — Orders stuck at ReadyForPickup / "assigning rider" on staging; 3PL tasks cancelled & re-booked (2026-07-17)
- **Cause:** staging's `Delivery__Dispatch__WebhookUrl` points at **prod**, and `SendOrderLevelCallbackUrl` defaults false (commit `834e940`), so ProRouting calls prod, staging never hears of assignments; the orchestrator then cancels the live booking after 30 s and re-books, looping. Real money on the live account.
- **Rule:** do not test 3PL on staging. Fix = give staging its own ProRouting account/callback, or `SendOrderLevelCallbackUrl=true` + staging `WebhookUrl` (order-level callbacks arrive **without** `x-pro-api-key`, so inbound auth must allow that).
- **Design flaw still open:** the orchestrator cancels the 3PL task after one `AcceptanceTimeoutSeconds`(30 s) wait when no webhook has arrived. 30 s is too tight even with healthy callbacks.

## R7 — ProRouting integration notes
- Fixed 2026-05-15: phone normalisation (last 10 digits); status endpoint is `POST partner/order/status {order_id}`.
- **Open:** prod webhook auth. Code ready (`WebhookEndpoints.cs`): HMAC if headers present, else `x-pro-api-key` vs `PROROUTING_INBOUND_API_KEY`, else Development-only bypass. Set the env var and ask ProRouting to send the header.
- Docs: `docs/prorouting-integration.md`, `docs/hivago-prorouting-phase1-notion.md`, `RallyAPI.Integrations.ProRouting.Tests/TROUBLESHOOTING.md`. Simulate a happy path: `scripts/simulate-prorouting-happy-path.ps1`.

## R8 — Menu item update 500: `DbUpdateConcurrencyException` "expected 1 row, affected 0" (2026-07-10)
Cause: new child entities with client-set Guid keys discovered via a tracked aggregate are marked Modified. Fix: explicitly `context.Add(child)` (`IMenuItemRepository.AddOption/AddOptionGroup`). Watch for this in any handler that adds children to a loaded aggregate. Repro technique: throwaway console against docker Postgres with `.LogTo(Console.WriteLine)`.

## R9 — Assigned orders flipping to Failed / startup crash on xmin migration (2026-06-26)
- **Cause:** lost-update race in `RiderDispatchOrchestrator` (stale tracked copy blind-overwrote an accepted delivery). Fixed with `GetCurrentStatusAsync` (AsNoTracking), `MarkFailed` guard, and `xmin` concurrency token + `TryUpdateAsync` (commits `894df2f`, `b78e764`).
- **Migration trap:** EF's generated `AddColumn xmin` must be removed (system column). Same applies to the pending Order/Payment xmin migration.
- Other `DeliveryRequest` writers (MarkPickedUp/MarkDelivered/…) can now surface `DbUpdateConcurrencyException` as 500 under real concurrency — not handled yet.

## R10 — 503 for all users after a deploy (2026-03-26)
A migration (`AddPlaceIdToCustomerAddresses`) wasn't applied → EF model expected a missing column. Apply/verify migrations (02) before or immediately on merge; check columns exist.

## R11 — 429s
Fixed in PR #180 (per-user buckets for order/payment). OTP/login remain per-IP by design. Local repro: force strict rate limits (see `Program.cs` ~270–412).

## R12 — UI-dev facing
- Google Reviews showing nothing: check PR #197 merged to staging, Place ID linked, Places API enabled, and whether an **Empty result was cached for 24 h** after a Google failure (`CatalogCacheKeys.GoogleReviews`) — most likely culprit; flush that key.
- Pin drift: admin queue at `GET /api/admins/restaurant-location-reviews?status=Pending`, approve/reject sub-routes.
