# 04 — Open items (ranked)

Effort is a rough guess for someone who knows the codebase. P0 = do first.

## P0 — money / correctness

1. **PayU webhook lock fix → PR into staging.** Branch `fix/payu-webhook-lock-release` (commit `bcbbff5`, pushed, cut from staging, **no PR yet**): 2-min in-flight TTL, release key on failure + failed audit row, `RedisIdempotencyService.ExtendAsync`, 24 h on success/bad signature. ~1 h to PR + staging verify.
   Remaining on top of it (~1–2 days):
   - Npgsql `EnableRetryOnFailure` for deadlock/serialization (40P01/40001) — verify `DomainEventInterceptor` cannot double-publish on a retried save
   - `xmin` token on **Order + Payment** — migration exists **uncommitted** in the working tree on `fix/stale-integration-tests` (`20261008125337_AddPaymentOrderXminConcurrencyToken`); review it (Up/Down must be empty, R9), then apply per the migration checklist
   - decide whether handled failures (`PersistFailed`) return 5xx so PayU retries instead of 200
   - tests: injected deadlock, killed request, webhook-vs-sweep concurrency, exhausted retry
2. **PayU dashboard + env verification** (R1): webhook URL configured? `PayU__Frontend*Url` set? Prod on `secure.payu.in` with live creds?
3. **Do not scale beyond 1 replica until:** Redis leader lock (or a separate worker process / durable job queue) for all hosted services; `FOR UPDATE SKIP LOCKED` in `OutboxProcessor` (otherwise **double 3PL bookings**); Postgres advisory lock around startup `Migrate()` ×7 contexts; identical `JwtSettings__*PemKey` env on all replicas; cap Npgsql pool so replicas × pool < Postgres `max_connections`; check `ConnectionTracker` (in-memory singleton) isn't used for cross-replica "is rider online". ~3–5 days.
4. **ProRouting inbound webhook auth on prod** (R7) — env var + ask the vendor. ~30 min of work, then vendor lead time.

## P1 — dispatch

5. **Own-fleet broadcast dispatch**: implemented on `feature/own-fleet-broadcast-dispatch` (2026-07-08). Confirm what is merged to staging/prod vs. only on the feature branch (prod runs the feature-branch orchestrator per the 2026-07-09 incident). Spec: `specs/own-fleet-first-dispatch.md` (has local uncommitted edits). **Accept-handler hardening still required** (`AcceptDeliveryOfferCommandHandler`): early "already assigned" guard; use `TryUpdateAsync` → clean 409 not 500; reorder so the DeliveryRequest write wins *before* setting rider `CurrentDeliveryId` (otherwise a losing rider is stuck, R3).
6. **Orchestrator 30 s 3PL wait cancels live bookings** (R6). Decide a longer window or event-driven wait.
7. **Dispatch blocks inline on the outbox thread** for ~30 s; accept detection is 2 s polling. Move off the outbox thread.
8. **Never-fail dispatch** (`1aa1869`) — only ship after confirming the backlog is clean (R4).
9. Non-dispatch `DeliveryRequest` writers can 500 on `DbUpdateConcurrencyException`.
10. Only the Orders→Delivery hop is durable (outbox). Other cross-module MediatR notifications are in-process and lost on a crash.
11. Early/predictive dispatch: spec `specs/early-predictive-dispatch.md` (`Delivery:Dispatch:EarlyDispatchEnabled=false`).

## P1 — security / hygiene (see 05)

12. Rotate + purge the Exotel keys committed in `appsettings.json`.
13. Split the Google Maps keys (browser referrer-locked vs server) and delete the old shared key.
14. Delete stray files at the repo root: `private.pem`/`public.pem` (confirmed untracked, but they sit in the tree), `*.log`, `ProRouting*.cs` loose files, `Host/error.log`, `startup_log.txt`. `appsettings.json` also carries a dummy `JwtSettings:SecretKey` that is unused with RS256 — remove it.
15. CI hardening: integration tests are `continue-on-error`; no migration check; no secret scan.

## P2 — tests

16. **Integration tests: 13 of 41 failing**, all one root cause: tests assume Paid-immediately but `PlaceOrder` creates **Pending** orders (payment must be completed first). Already fixed (not prod bugs): missing `Idempotency-Key`, missing `deliveryQuoteId`, test JWT missing `role` claim. Fix = have the test base simulate the PayU webhook/confirm, or call the payment-confirm path.
17. Add NetArchTest rules for module boundaries (violations listed in 01).
18. Tests for webhook concurrency (item 1).

## P2 — features / product

19. **Admin UI for Google Place linking** — only raw API exists (`GET /api/admins/places/search`, `PUT /api/admins/restaurants/{id}` with `googlePlaceId`). Spec: `specs/admin-google-place-linking.md` (uncommitted — commit it for the UI dev). PR #197 (restaurant-list rating commit `b2f23cf`) may never have merged to staging — check.
20. **Veg / non-veg:** no enforcement rule exists anywhere. A sketch is in `reviews`/memory: customer preference + snapshot flag at cart/order item + revalidate at PlaceOrder.
21. **Pricing:** `PricingEngine.CalculateFinal` matches `RuleName == "BaseFee"` by magic string (add `ModificationType.Base`); engine swallows rule exceptions ⇒ silently cheaper fee (decide: fail quote or alert).
22. **Multi-outlet cross-accept** (PRs #200 etc.): shipped to master; spec `specs/multi-outlet-cross-accept.md`.
23. MSG91 WhatsApp OTP: code complete, blocked on Meta Business Verification. SMS provider history: Exotel → MSG91 → AuthKey (current).
24. Petpooja POS integration — planned, not started. Sentry — confirm wired. FCM — deferred until Flutter apps exist.
25. Image storage on Cloudflare R2 — confirm working in prod.
26. Payouts: ICICI manual export + reconciliation (`docs/restaurant-payout-system.md`, `docs/icici-payout-*.md`, `specs/icici-manual-payout-export.md`) — operator-run process; confirm who operates it.

## Frontend (separate repos, personal GitHub account)

27. Move the 3 repos into the `Hivago` GitHub org; branch protection + CODEOWNERS + gated `deploy.yml` exist already. Pipeline armed but idle; `master` is stale on 2 repos (nothing deployed by the pipeline yet). Admin `dev → master` PR for the `.htaccess` no-store cache fix. Disconnect the orphan Vercel integration. Clean stray uploads on Hostinger. Details: `docs/frontend-cicd/`.

## Unresolved user reports

- `admin.hivago.in` "gateway timeout" — returned 200 in a probe; API-side cause not found. Check what the admin panel calls on `api.hivago.in` and Railway logs.
- Google reviews not visible — see R12.
