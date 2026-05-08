# Rally API

Rally is a .NET 8 backend for a food delivery platform in India. It supports customers, restaurant partners, delivery riders, restaurant owners, and admins.

The codebase is a modular monolith: one deployable API, split into business modules with Clean Architecture boundaries.

## Start Here

Read these in order:

1. `CLAUDE.md` - full project context and agent rules.
2. `docs/PROJECT-MAP.md` - short human map of the modules, folders, and flows.
3. `docs/DEVELOPMENT.md` - how to run, test, and add features safely.
4. `HANDOFF.md` - current project state and known cleanup items.

## What This Backend Does

- Customer OTP login, profile, addresses, geocoding, and place lookup.
- Restaurant and owner login, restaurant profile/settings, outlet management, menu management, and item images.
- Cart and order lifecycle.
- PayU hosted checkout, webhook processing, verification, refunds, and restaurant payouts.
- Delivery quotes, rider dispatch, rider delivery workflow, public tracking, and ProRouting webhook intake.
- SignalR notifications for web-first real-time updates.
- Health checks, Serilog logging, rate limiting, Docker Compose, and GitHub Actions CI.

## Repo Shape

```text
src/
  RallyAPI.Host/                  API entry point, middleware, auth, SignalR, endpoint mapping
  RallyAPI.SharedKernel/          Result pattern, base domain types, shared contracts, integration events
  RallyAPI.Infrastructure/        Shared infrastructure such as Google Maps and Cloudflare R2
  Modules/
    Users/                        Auth, customers, riders, restaurants, owners, admins
    Catalog/                      Menus, menu items, option groups, customer catalog browsing
    Orders/                       Cart, order lifecycle, payments, payouts
    Delivery/                     Delivery requests, dispatch, tracking, rider workflow
    Pricing/                      Delivery fee calculation
    Integrations/ProRouting/      Third-party delivery provider client

tests/
  Modules/                        Unit tests by module
  RallyAPI.Integration.Tests/     Cross-module API flow tests

docs/                             Human documentation
specs/                            Feature specs before implementation
reviews/                          Daily review notes and cleanup observations
```

## Run Locally

Prerequisites:

- .NET 8 SDK
- Docker Desktop, for Postgres and Redis

Start dependencies:

```powershell
docker compose up -d postgres redis
```

Run the API:

```powershell
dotnet run --project src/RallyAPI.Host/RallyAPI.Host.csproj
```

Useful URLs:

- API root: `http://localhost:5000`
- Swagger in development: `http://localhost:5000/swagger`
- Health: `http://localhost:5000/health`
- SignalR hub: `/hubs/notifications`

You can also run the full compose stack:

```powershell
docker compose up -d
```

## Verify Changes

Backend rule of thumb:

```powershell
dotnet build
dotnet test
```

CI currently builds the solution, runs module unit tests, and runs integration tests as a non-blocking job.

## Feature Workflow

For anything larger than a small fix:

1. Check `specs/` first.
2. Add or update a spec if the behavior is not already documented.
3. Implement in this order: Domain, Application, Infrastructure, Endpoints, Tests.
4. Keep modules isolated. Cross-module behavior goes through shared contracts or integration events.
5. Run `dotnet build` and `dotnet test`.

## Important Rules

- Commands change state; queries read state.
- Business logic belongs in domain entities or MediatR handlers, not endpoints.
- Commands need FluentValidation validators.
- Domain projects stay pure: no EF Core, ASP.NET, Redis, HTTP clients, or external service SDKs.
- Do not directly import another module's internal domain/application types.
- Use `DateTimeOffset`, not `DateTime`.
- Use the Result pattern for business failures.

## Current Cleanliness Notes

The project has real structure, but the repo root has accumulated noise:

- Large old log files live at the root.
- Some Visual Studio and `obj/` artifacts appear in `git status`.
- Several generated or debugging files make the first impression harder than it needs to be.
- `HANDOFF.md` had drifted from the code and has now been rewritten as a current snapshot.

See `HANDOFF.md` for the sharper cleanup list.
