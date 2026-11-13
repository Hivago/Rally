# 02 — Environments, Config, Deploy

## Environments

| Env | Backend URL | Host | Notes |
|-----|-------------|------|-------|
| Production | `https://api.hivago.in` (Railway-internal alias `rally-production-2004.up.railway.app`) | Railway project `perceptive-essence`, env `production` | Single instance. Postgres + Redis on Railway |
| Staging | `https://rally-staging-9ae8.up.railway.app` | Railway, env `staging`, service `Rally` | `ProRouting__Enabled=false`. Not a safe place to test 3PL (R6) |
| Local | `http://localhost:5023` | Docker Postgres + Redis | See below |

Frontends: `hivago.in` (customer), `admin.hivago.in`, `restaurant.hivago.in` — static React/Vite apps on
**Hostinger**, deployed by GitHub Actions over FTPS. See `docs/frontend-cicd/`.

## Branch / deploy rules (non-negotiable — each came from an incident)

1. Work on `feature/*` or `fix/*`, push, open a PR **into `staging`**.
2. Verify on staging. Promote with a **`staging → master` PR**. Never merge a feature branch straight to master, never push to master.
3. Railway deploys from the branch on push/merge. **Railway auto-migrates on boot.** If a migration is broken the
   new container fails its health check and the *old* container keeps serving with a stale schema, so
   you can find out hours later. Always run the migration checklist below locally first.
4. Verify columns actually exist after a migration, not only that it is in `__EFMigrationsHistory` (a partial failure can record it without the DDL running).
5. New env vars / external services must exist on Railway **before** merging the code that needs them.

### Migration checklist (from CLAUDE.md, condensed)
- Generate with `dotnet ef migrations add` — don't hand-write DDL EF can model. Raw `migrationBuilder.Sql` only for sequences/data/triggers, always **schema-qualified** and with exact table/column names (e.g. `orders.delivery_info.order_id`).
- **`xmin` concurrency tokens:** `xmin` is a Postgres system column. EF generates `AddColumn xmin xid` — **delete it** so Up/Down are empty or boot crashes ("column name xmin conflicts with a system column name"). See `AddDeliveryRequestXminConcurrencyToken` as the template.
- `dotnet ef database update` locally, `dotnet build` clean, boot the Host and watch the `Migrating X database...` lines.
- EF CLI needs the full app DI; if Redis isn't up use the design-time factory pattern (`UsersDbContextFactory`) and set `ConnectionStrings__Database`.

```powershell
dotnet ef migrations add Name `
  --context OrdersDbContext `
  --project src/Modules/Orders/RallyAPI.Orders.Infrastructure `
  --startup-project src/RallyAPI.Host
dotnet ef database update --context OrdersDbContext --startup-project src/RallyAPI.Host   # no --no-build!
```
(`--no-build` uses the stale Host DLL and reports "up to date".)

## Local development

```powershell
docker compose up -d postgres redis        # start Docker Desktop first if the daemon is down
# Postgres: Host=localhost;Port=5432;Database=rallydb;Username=rally;Password=rally123 ; Redis: localhost:6379
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=rallydb;Username=rally;Password=rally123"
$env:ConnectionStrings__Redis = "localhost:6379"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/RallyAPI.Host      # port 5023 is forced by launchSettings.json (ASPNETCORE_URLS is ignored)
```
Health: `GET /health`. All module DbContexts migrate on boot.

Local gotchas:
- Phantom `Sentry.Serilog` FileNotFound at `builder.Build()` ⇒ stale bin/obj: delete `src/RallyAPI.Host/bin` and `obj`.
- Random `CS0246` (StackExchange.Redis / FluentValidation) ⇒ `dotnet restore RallyAPI.sln --force`.
- Auth for manual testing: OTPs are hashed so you can't read them. Mint an RS256 JWT with `Keys/private.pem` with claims
  `{sub, user_type, iss:"RallyAPI", aud:"RallyApp", iat, nbf, exp, role}`. Queries that look up the caller (e.g. rider overview) need a seeded admin row.
- Windows test project paths are **4 levels** deep: `../../../../src/...`.

Tests: `dotnet test` (unit) — CI runs the module test projects listed in `.github/workflows/ci.yml`.
Integration tests (`tests/RallyAPI.Integration.Tests`) need Docker (TestContainers Postgres + Redis, Respawn).

## Configuration inventory

Config is .NET configuration: `appsettings.json` defaults, overridden by Railway env vars using `__` for nesting
(`PayU__MerchantKey`). `appsettings.json` holds placeholders (`<set-via-env>`) — **real values live only in Railway.**
Confirm every row below against Railway **Variables** on both environments; I could not read Railway from here.

| Variable | Purpose / gotcha |
|----------|------------------|
| `ConnectionStrings__Database`, `ConnectionStrings__Redis` | Railway-provided Postgres/Redis |
| `JwtSettings__PrivateKeyPem`, `JwtSettings__PublicKeyPem` | **Must be ONE matching RSA pair, identical on every replica, never regenerated.** Mismatch ⇒ intermittent 401s + riders get no offers (R5). Falls back to `Keys/*.pem` if unset |
| `JwtSettings__Issuer` / `Audience` | `RallyAPI` / `RallyApp` (appsettings) |
| `PayU__MerchantKey`, `PayU__MerchantSalt` | Prod must be the **live** pair (key `3P3tsj`); test key was `7CKBWL` |
| `PayU__BaseUrl` | Prod `https://secure.payu.in`. appsettings default is `https://test.payu.in` — a missing var fails *toward test* (no real UPI) |
| `PayU__SuccessUrl` / `FailureUrl` | surl/furl → the **backend** `/api/payments/return/success|failure` of *that environment's own domain* |
| `PayU__FrontendSuccessUrl` / `FrontendFailureUrl` | SPA pages the backend 302s to (backend appends `?orderId=`) |
| `ProRouting__ApiKey`, `ProRouting__BaseUrl`, `ProRouting__Enabled` | Prod live `client-api.prorouting.in`; staging has Enabled=false |
| `PROROUTING_INBOUND_API_KEY` | Inbound webhook auth (`x-pro-api-key`); not yet enforced by ProRouting on preprod (R7) |
| `Delivery__Dispatch__WebhookUrl`, `Delivery__Dispatch__SendOrderLevelCallbackUrl` | Callback target for ProRouting. **Staging currently points at prod (R6)** |
| `Delivery__Dispatch__OwnFleetFirst` | Kill-switch for own-fleet-first dispatch (default true) |
| `GoogleMaps__ApiKey` | Server key: Geocoding + Places + Routes. Plan: split from the browser key (see 04) |
| `GoogleVision__CredentialsJson` | KYC document OCR |
| `AuthKey__AuthKey` (+ `OtpProvider=AuthKey`) | Live SMS OTP provider. `Msg91WhatsApp__AuthKey` is built but blocked |
| `R2__AccountId`, `R2__AccessKeyId`, `R2__SecretAccessKey` | Cloudflare R2 (bucket `rally-media`) — images/KYC |
| `Discord__OnboardingWebhookUrl`, `Discord__OpsAlertsWebhookUrl` | Ops/onboarding alerts |
| `Sentry__Dsn` / `SENTRY_DSN` | Error tracking (check it is actually wired; local runs showed a phantom Sentry reference) |
| `Cors__AllowedOrigins` | Frontend origins |
| `AutoCancel__*`, `OrderPlacement__MinimumOrderValue`, `PaymentReconciliation__*` | Tunables, defaults in appsettings |
| GitHub Actions secret `DISCORD_CI_WEBHOOK_URL` | CI failure notification |

The `Delivery__Dispatch__*` tunables (radius, timeouts, `RiderEarningsPercentage`) and Pricing defaults are in `appsettings.json`.

## Observability

Serilog (console) → Railway logs (`railway logs`). Sentry for errors. `audit.webhook_audit_logs` records inbound
PayU/ProRouting webhooks — first place to look in a payment/dispatch dispute.
Prod DB: `railway link --project perceptive-essence --environment production`, read `DATABASE_PUBLIC_URL` from the
Postgres service variables, query with `docker run --rm postgres:16-alpine psql "$URL"`. **Read-only unless you mean it.**

## Not yet true (don't assume)

- Multiple replicas are **not safe** (04, item 3). Prod and staging are single-instance.
- Railway Postgres backup/restore status was never verified — check it in week one.
- k6 load-test results (plateau ~520 req/s, bottleneck was the Npgsql pool; catalog caching since added) are not documented in the repo; scripts are in `scripts/`.
