# Rally API - Handoff

Last updated: 2026-04-27

## What This Is

Rally is a .NET 8 modular monolith backend for a food delivery platform. It is one deployable ASP.NET Core API with modules for users, catalog, orders, delivery, pricing, and integrations.

Think "Swiggy/Zomato backend", but web-first for launch.

## Current Architecture

```text
RallyAPI.Host
  - Program.cs, auth, CORS, health checks, Swagger, SignalR, endpoint registration

src/Modules
  Users
    Auth, customers, riders, restaurants, owners, admins, profiles, addresses, KYC
  Catalog
    Restaurants, menus, menu items, option groups, search/browse APIs
  Orders
    Cart, order lifecycle, PayU payments, refunds, restaurant payouts, order notes
  Delivery
    Delivery quotes, delivery requests, rider offers, tracking, ProRouting webhook handling
  Pricing
    Delivery fee calculation
  Integrations/ProRouting
    Third-party delivery API client and models

src/RallyAPI.SharedKernel
  Result pattern, base domain types, shared abstractions, integration events

src/RallyAPI.Infrastructure
  Shared provider infrastructure such as Google Maps and Cloudflare R2 storage
```

Each business module is intended to follow:

```text
Domain -> Application -> Infrastructure -> Endpoints
```

## What Works Now

- OTP login for customers and riders.
- Restaurant, owner, and admin login/management flows.
- JWT auth with RSA keys and role/user-type policies.
- Customer addresses, Google Places autocomplete, place details, reverse geocoding.
- Restaurant profile/settings, hours, operations, delivery, dietary, logo upload flow.
- Catalog browsing, restaurant menus, menu items, option groups, item image upload flow.
- Cart APIs.
- Order placement and order lifecycle transitions.
- PayU payment initiation, webhook, verify, refund, success/failure return endpoints.
- Restaurant payout ledger, earnings, payout history, GST/TDS summaries, admin payout processing.
- Delivery quote, delivery requests, rider offers, rider delivery status flow, tracking endpoint.
- ProRouting integration and webhook endpoint.
- SignalR notification hub and host-level notification handlers.
- Serilog logging, health checks, rate limiting, Docker Compose, GitHub Actions CI.
- Unit tests for key Orders, Delivery, Pricing, Users, and ProRouting behavior.
- Integration test project for cross-module flows.

## Known Cleanup Items

These are about understandability, not necessarily broken behavior.

1. Root directory is noisy.
   - Large old logs: `build-new.log`, `review-build.log`, `review-test.log`, crash/OTP logs.
   - Stray root ProRouting model files duplicate the proper files under `src/Modules/Integrations/ProRouting/Models`.
   - Visual Studio files under `.vs/` and generated `obj/` files show up in `git status`.

2. Documentation had drifted.
   - This handoff previously described old missing features that now exist.
   - `SETUP-GUIDE.md` is mostly an AI-workflow setup note, not a developer onboarding guide.

3. Endpoint style is mixed.
   - Users and Catalog mostly use one endpoint class per route/use case.
   - Orders and Delivery use larger endpoint classes.
   - This is manageable, but a newcomer has to learn two styles.

4. Module boundaries need continued care.
   - Cross-module behavior should stay on SharedKernel abstractions or integration events.
   - Avoid referencing another module's Domain or Application types directly.

5. `Program.cs` is doing too much.
   - It currently owns service registration, auth, policies, CORS, Swagger, middleware, endpoint mapping, health checks, and startup migrations.
   - Splitting this later into extension methods would make startup easier to scan.

6. Dev-only behavior must stay gated.
   - Dev seed and purge endpoints are currently environment-gated.
   - Keep future debug endpoints behind `app.Environment.IsDevelopment()`.

## First Files To Read

| Need | File |
|---|---|
| Human overview | `README.md` |
| Project map | `docs/PROJECT-MAP.md` |
| How to run/change/test | `docs/DEVELOPMENT.md` |
| Full AI/project context | `CLAUDE.md` |
| Agent rules | `AGENTS.md` |
| Feature specs | `specs/` |
| API startup | `src/RallyAPI.Host/Program.cs` |

## Current API Surface At A Glance

| Area | Example routes |
|---|---|
| Customers | `/api/customers/otp/send`, `/api/customers/profile`, `/api/customers/addresses` |
| Riders | `/api/riders/otp/send`, `/api/riders/profile`, `/api/riders/location` |
| Restaurants | `/api/restaurants/login`, `/api/restaurants/profile`, `/api/restaurants/me/*` |
| Owners | `/api/owners/login`, `/api/owners/me`, `/api/owners/me/outlets` |
| Admins | `/api/admins/login`, `/api/admins/restaurants`, `/api/admins/riders`, `/api/admins/stats` |
| Catalog | `/api/catalog/restaurants`, `/api/catalog/search`, `/api/items/{itemId}` |
| Restaurant menus | `/api/restaurant/menus`, `/api/restaurant/items`, `/api/restaurant/options/*` |
| Cart | `/api/cart`, `/api/cart/items`, `/api/cart/sync` |
| Orders | `/api/orders`, `/api/orders/{orderId}/confirm`, `/api/orders/{orderId}/ready` |
| Payments | `/api/payments/initiate`, `/api/payments/webhook`, `/api/payments/verify` |
| Payouts | `/api/restaurants/payouts/*`, `/api/admin/payouts/*` |
| Delivery | `/api/delivery/quote`, `/api/rider/delivery/*`, `/api/track/{orderNumber}` |
| Realtime | `/hubs/notifications` |
| Health | `/health` |

## Recommended Next Cleanup Pass

1. Remove or archive root log files and duplicate root ProRouting DTO files after confirming they are not referenced.
2. Stop tracking `.vs/`, `bin/`, and `obj/` artifacts if they are already in Git history.
3. Split `Program.cs` into clear extension methods:
   - `AddRallyAuthentication`
   - `AddRallyAuthorization`
   - `AddRallyCors`
   - `AddRallySwagger`
   - `MapRallyEndpoints`
   - `MigrateRallyDatabases`
4. Pick one endpoint organization style per module and document exceptions.
5. Add a lightweight endpoint catalog generated from route mappings, or keep `HANDOFF.md` updated when endpoint groups change.
