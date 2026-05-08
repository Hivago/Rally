# Rally Development Guide

This is the day-to-day guide for changing the backend without getting lost.

## Prerequisites

- .NET 8 SDK
- Docker Desktop
- PowerShell

Optional but useful:

- PostgreSQL client
- Redis CLI
- Visual Studio or Rider

## Local Services

Start Postgres and Redis:

```powershell
docker compose up -d postgres redis
```

Run the API:

```powershell
dotnet run --project src/RallyAPI.Host/RallyAPI.Host.csproj
```

Run the whole stack through Docker:

```powershell
docker compose up -d
```

Stop services:

```powershell
docker compose down
```

Delete local DB and Redis data:

```powershell
docker compose down -v
```

## Configuration

Default local configuration is in:

```text
src/RallyAPI.Host/appsettings.json
```

Important settings:

| Area | Keys |
|---|---|
| Postgres | `ConnectionStrings__Database` |
| Redis | `ConnectionStrings__Redis` |
| JWT | `JwtSettings__Issuer`, `JwtSettings__Audience`, RSA key paths or PEM env vars |
| Google Maps | `GoogleMaps__ApiKey` |
| ProRouting | `ProRouting__ApiKey` |
| Msg91 WhatsApp | `Msg91WhatsApp__AuthKey` |
| Cloudflare R2 | `R2__AccountId`, `R2__AccessKeyId`, `R2__SecretAccessKey`, `R2__BucketName` |
| PayU | `PayU__MerchantKey`, `PayU__MerchantSalt`, `PayU__BaseUrl` |

Do not commit real secrets. Use environment variables or local user secrets.

## Build and Test

Run this after backend changes:

```powershell
dotnet build
dotnet test
```

Focused test examples:

```powershell
dotnet test tests/Modules/Orders/RallyAPI.Orders.Domain.Tests
dotnet test tests/Modules/Orders/RallyAPI.Orders.Application.Tests
dotnet test tests/Modules/Delivery/RallyAPI.Delivery.Application.Tests
dotnet test tests/Modules/Pricing/RallyAPI.Pricing.Application.Tests
dotnet test tests/Modules/Users/RallyAPI.Users.Infrastructure.Tests
dotnet test tests/RallyAPI.Integration.Tests
```

CI runs unit tests as required and integration tests as non-blocking.

## Adding a Backend Feature

Use this order:

1. Spec: check or create a file in `specs/`.
2. Domain: entities, value objects, domain errors/events.
3. Application: command/query record, handler, validator, DTOs.
4. Infrastructure: EF config, migration, repository/provider implementation.
5. Endpoints: minimal API route that calls MediatR.
6. Tests: handler/unit tests first; endpoint integration tests for HTTP behavior.

New command shape:

```text
src/Modules/{Module}/RallyAPI.{Module}.Application/{Area}/Commands/{Action}/
  {Action}Command.cs
  {Action}CommandHandler.cs
  {Action}CommandValidator.cs
```

Endpoint shape:

```text
app.MapPost("/api/...", HandleAsync)
    .WithTags("...")
    .RequireAuthorization("...");
```

## EF Core Migrations

Create migrations in the module that owns the DbContext.

Examples:

```powershell
dotnet ef migrations add AddThing `
  --project src/Modules/Orders/RallyAPI.Orders.Infrastructure `
  --startup-project src/RallyAPI.Host `
  --context OrdersDbContext
```

```powershell
dotnet ef migrations add AddRestaurantColumn `
  --project src/Modules/Users/RallyAPI.Users.Infrastructure `
  --startup-project src/RallyAPI.Host `
  --context UsersDbContext `
  --output-dir Persistence/Migrations
```

The app currently applies module migrations at startup in development/runtime startup.

## Authentication Policies

Defined in `src/RallyAPI.Host/Program.cs`:

- `Customer`
- `Rider`
- `Restaurant`
- `Owner`
- `Admin`
- `AdminOrRestaurant`
- `AdminOrRider`
- `RestaurantOrAdmin`
- `RiderOrAdmin`

Use the narrowest policy that fits the endpoint.

## Real-Time Notifications

SignalR hub:

```text
/hubs/notifications
```

JWT is passed via `access_token` query string for hub connections. Notification handlers live in `src/RallyAPI.Host/Notifications` so the modules do not depend on SignalR.

## Development Hygiene

- Keep generated files out of commits unless they are migrations.
- Do not commit `.vs/`, `bin/`, `obj/`, large logs, or runtime scratch files.
- Prefer small specs and focused tests over huge undocumented changes.
- If you touch a module boundary, update `docs/PROJECT-MAP.md` or `CLAUDE.md`.
- If the current state changes, update `HANDOFF.md`.
