# Rally / Hivago Backend — Handoff

> Last updated: 2026-11-13. Replaces the Feb 2026 version (it was badly out of date).
> Author: backend developer leaving the company. Written to be read cold.

Rally (brand: **Hivago**) is a food-delivery backend for India: customers order, restaurants
accept, riders (own fleet first, ProRouting 3PL as fallback) deliver. One .NET 8 modular
monolith, one PostgreSQL database, Redis, SignalR.

## Read in this order

| # | Doc | Read it for |
|---|-----|-------------|
| 1 | This file | Orientation, status, first-week plan |
| 2 | [docs/handoff/01-architecture.md](docs/handoff/01-architecture.md) | Modules, the 3 core flows, background services |
| 3 | [docs/handoff/02-environments-and-deploy.md](docs/handoff/02-environments-and-deploy.md) | Environments, config/env vars, deploy + migration rules |
| 4 | [docs/handoff/03-runbooks.md](docs/handoff/03-runbooks.md) | Past incidents: symptoms, root cause, fix, how to diagnose |
| 5 | [docs/handoff/04-open-items.md](docs/handoff/04-open-items.md) | Everything unfinished, ranked |
| 6 | [docs/handoff/05-access-and-offboarding.md](docs/handoff/05-access-and-offboarding.md) | Accounts to transfer, secrets to rotate, checklist |
| — | [CLAUDE.md](CLAUDE.md) + `.claude/rules/` | Coding conventions (also read by AI tools) |
| — | `specs/`, `docs/` | Per-feature specs and payout/ProRouting/frontend-CI docs |

## Current status (snapshot)

- **Production:** `api.hivago.in` (Railway, project `perceptive-essence`, env `production`). Healthy as of the last check.
  ProRouting 3PL bookings work on prod.
- **Staging:** `rally-staging-9ae8.up.railway.app`. Do NOT test 3PL there (see runbook R6).
- **Branch model:** feature branch → `staging` → (promotion PR) → `master`. Never merge to `master` directly.
- **Frontends:** 3 React repos (customer, admin, restaurant) under the personal GitHub account
  `yashdvishwakarma`, deployed to Hostinger by GitHub Actions FTPS. See `docs/frontend-cicd/`.
- **Tests:** unit tests run in CI; integration tests (`tests/RallyAPI.Integration.Tests`) are
  `continue-on-error` in CI and 13 of 41 still fail (stale test assumptions, not prod bugs — see open items).

## Top risks if nobody acts (details in 04 and 05)

1. **Secrets and accounts tied to one person** (personal GitHub, Railway, PayU, Hostinger, Google, etc.). Transfer before the last day.
2. **Exotel API key/token are committed in `appsettings.json`** (commented out, in git since `3eb9e16`). Rotate and purge.
3. **Money-path concurrency:** PayU webhook lock fix is on an unmerged branch; Order/Payment `xmin` migration is uncommitted in the working tree.
4. **Background services run inside every API instance** — do not scale to 2+ replicas until fixed (04, item 3).
5. **Prod JWT keys:** keep the RSA keypair stable in Railway env or every deploy logs everyone out.

## Work in flight (all committed and pushed, NOT merged, no PRs opened)

| Branch | Commits | Contents |
|--------|---------|----------|
| `docs/handoff` | `06b121d` + follow-up | These handoff docs |
| `fix/stale-integration-tests` | `00cb6b6` | xmin concurrency token on Order + Payment. Migration `AddPaymentOrderXminConcurrencyToken` has **empty Up/Down** (xmin is a system column; verified, build green). Webhook handler does not yet catch `DbUpdateConcurrencyException` — see 04, item 1 |
| | `02da89c` | Integration test fixes (idempotency key, delivery quote, role claim). 13/41 still fail (04, item 16) |
| | `1cf4c03` | `specs/own-fleet-first-dispatch.md` update; new `specs/admin-google-place-linking.md` for the UI dev |
| `fix/payu-webhook-lock-release` | `bcbbff5` | Webhook lock release fix (pushed earlier, no PR) |

Merge path when ready: PR each branch into `staging`, verify, then a `staging → master` promotion PR.

## First-week plan for the new owner

1. Get repo + Railway + GitHub org admin access (05).
2. Run it locally (02, "Local development"), hit `/health`, run unit tests.
3. Read 01, then trace one order end to end in code: `PlaceOrderCommandHandler` → PayU webhook → `OrderConfirmedIntegrationEvent` → `RiderDispatchOrchestrator`.
4. Skim runbooks 03 so you recognise the failure modes when they page you.
5. Pick up open items 1–3 in 04.
