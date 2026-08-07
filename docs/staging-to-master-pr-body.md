## Summary

Promotes 21 commits from `staging` to `master` — a clean fast-forward, no conflicts (`master` has nothing `staging` lacks). Covers four workstreams that have been running on staging since ~Jul 22:

### 💰 Payments — ICICI bulk payout export
Replaces the old "type in a UTR and mark paid" flow (no statement match, no amount verification) with a proper export + bank-reconciliation pipeline:
- `POST /api/admin/payouts/restaurant/export` and `.../rider/export` — pull only `Pending` payouts for an exact period, join live bank details (never a stale snapshot), exclude anyone with missing bank details into a separate list, and atomically flip included payouts to `Processing` so nothing can land in two files.
- `RestaurantPayoutExportBatch` / `RiderPayoutExportBatch` record who generated a file, its control-sum, and a SHA-256 hash for audit trail. Unique index on `Payout(OwnerId, PeriodStart, PeriodEnd)` + xmin concurrency stops two admins double-exporting the same week.
- Hardened against bad data: payouts with `NetAmount <= 0` are excluded instead of crashing the export; UTC `DateTimeKind` pinned explicitly on query params.
- `OrderNumber` threaded onto `PayoutLedger` (was GUID-only) so admins can match a ledger line back to an order without a separate lookup — includes a backfill migration and a regression test for the one handler (`GetRestaurantEarnings`) that was missed on the first pass and shipped blank order numbers to staging.
- `GetRestaurantEarnings` now takes optional `from`/`to` so past weeks are browsable, not just the current in-progress one. New `GET /api/admin/payouts/restaurant/{payoutId}` gives admins the same order-level breakdown restaurant owners already had, for any owner.

### ⚡ Reliability
- **Redis read-through cache** for restaurant browse (30s TTL) and full menu (60s TTL) — these were the two hottest anonymous endpoints, hitting Postgres on every request. Fail-open: a cache outage degrades to DB reads, never fails the request. Any Catalog write command evicts its cache key immediately so dashboard edits and open/close toggles show up instantly.
- **Per-user rate limits for order/payment**, split off the shared login policy (60/min per IP), which was collapsing real customers behind Indian CGNAT into 429s at checkout during dinner peak. New: 15/min per user on order placement, 30/min per user on payment initiate/verify (verify previously had *no* limiter at all), OTP verify split from the send bucket so a mistyped code can't burn your quota for a fresh one.

### 🔐 Auth
- Admins can force-reset an owner/restaurant password to a generated temporary one; owners can self-service change given their current password. `MustChangePassword` flag set on force-reset, cleared on self-change, surfaced in the login response so the frontend can redirect to a forced-change screen.

### 🍔 Order experience
- Customer SignalR feed was silently skipping `Preparing` and `ReadyForPickup` — customers sat on a stale "Confirmed" screen while food was ready and waiting on a rider. Both now push, with a distinct message for self-pickup orders.

### 🐛 Fix
- Menu item update returning 500 (`DbUpdateConcurrencyException`, "expected 1 row, affected 0") when adding new option groups — EF was treating client-generated Guid keys on new children as `Modified` instead of `Added`. Fixed by explicitly `Add`-ing new options/groups through the context.

## Migrations included (8)
Auto-applied on Railway boot via `Program.cs`'s startup `Database.Migrate()` block — no manual step needed, just confirm `"All migrations completed successfully."` in the deploy log after merge:
- `AddPayoutExportTracking`, `AddRestaurantPayoutExportBatch`, `AddPayoutConcurrencyAndUniquePeriod`, `AddOrderNumberToPayoutLedger` (Orders)
- `AddRiderPayoutExportTracking`, `AddRiderPayoutExportBatch`, `AddRiderPayoutConcurrency`, `AddMustChangePasswordFlag` (Users)

## Why now
Two frontend PRs are blocked waiting on this: `hivago_admin` #9 (ICICI export UI) and `hivago_restaurant` #14 (earnings date-range filter) both call endpoints/params that only exist here.

## Test plan
- [ ] `dotnet build` — 0 errors
- [ ] `dotnet test` — all pass
- [ ] Confirm Railway deploy log shows `"All migrations completed successfully."`
- [ ] Smoke test: run an ICICI restaurant export and rider export against prod data, verify control-sum matches the ICICI portal prompt
- [ ] Spot-check restaurant browse/menu still returns correct data after cache warms (toggle a restaurant's availability, confirm it reflects within a couple seconds)
- [ ] Confirm order placement / payment endpoints no longer 429 under normal use
