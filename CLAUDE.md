# RallyAPI — Project Context

> This file is read by Claude Code, Codex, Cursor, and Antigravity.
> It describes the project so AI generates code that fits THIS architecture exactly.
> Last updated: 2026-03-24

## Project Overview

**Rally** is a food delivery platform for the Indian market. Three user roles:
- **Customers** — Browse restaurants, order food, track delivery in real-time
- **Restaurant Partners** — Receive orders, manage menus, update availability
- **Delivery Riders** — Accept delivery offers, navigate to pickup/drop, update status

### Launch Strategy (Updated March 14, 2026)

**Web-first launch.** All user-facing apps are React web apps, NOT Flutter.
- Flutter mobile apps come LATER (post-launch)
- FCM (push notifications) is DEFERRED — not needed for web
- **SignalR** replaces FCM for real-time web notifications
- PayU browser redirect works the same without WebView

### Who Builds What

| App | Tech | Developer | Status |
|-----|------|-----------|--------|
| Backend (RallyAPI) | .NET 8, PostgreSQL | You | Core working |
| Customer Web App | React + TS + Tailwind + GSAP | Other developer | In progress |
| Rider Web App | React + TS + Tailwind + GSAP | Other developer | In progress |
| Restaurant Dashboard | React + TS + Tailwind | You | Not started |
| Admin Panel | React + TS + Tailwind | You | Not started |
| Flutter Apps (later) | Flutter | Other developer | Deferred |

---

## Tech Stack — Backend

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8 / ASP.NET Core |
| Language | C# 12 |
| API Style | Minimal APIs |
| Architecture | Modular Monolith (Clean Architecture per module) |
| Database | PostgreSQL (EF Core + Npgsql) |
| Cache | Redis (OTP storage, rate limiting) |
| Auth | RS256 JWT (RSA-2048), BCrypt passwords, SHA256 OTP, refresh token rotation with theft detection |
| Patterns | CQRS via MediatR, DDD (aggregates, value objects, domain events), FluentValidation, Repository + Unit of Work |
| Payments | PayU India (Hosted Checkout + S2S webhook) |
| Maps/Routing | Google Maps API (Routes, Geocoding, Places Autocomplete) + ProRouting integration |
| Real-time | SignalR (for web-first launch, replacing FCM) |
| Hosting | Railway |
| Database Hosting | Railway Postgres + Redis |
| Image Storage | Cloudflare R2 (pending) |
| Local Dev | Docker |

### Planned / In Progress

- Firebase Cloud Messaging — DEFERRED until Flutter apps ship
- MSG91 WhatsApp OTP — code complete, blocked on Meta Business Verification
- Petpooja POS integration
- Sentry (error tracking) — NOT YET ADDED
- Serilog (structured logging) — ADDED (feat/logging branch merged)
- GitHub Actions (CI/CD) — ADDED (build + unit tests + integration tests)
- Google Maps Geocoding + Places Autocomplete — ADDED (feat/maps-address branch)
- Customer Pickup Orders — ADDED (feat/pickup-orders branch, PR pending)

## Tech Stack — Frontend (React Apps)

| Layer | Technology |
|-------|-----------|
| Framework | React 18+ with TypeScript |
| Styling | Tailwind CSS |
| Animation | GSAP |
| State Management | TBD (React Query recommended for server state) |
| Real-time | SignalR client (@microsoft/signalr) |
| Build | Vite |

---

## Project Structure (ACTUAL)

```
RallyAPI/
|
|-- src/
|   |-- RallyAPI.Host/                          # Entry point: Program.cs, middleware, DI config
|   |   |-- Keys/                               # RSA keys for JWT signing
|   |   \-- Properties/                         # launchSettings.json
|   |
|   |-- Modules/
|   |   |-- Catalog/                            # Restaurant & menu management
|   |   |   |-- RallyAPI.Catalog.Domain/
|   |   |   |   |-- Enums/
|   |   |   |   |-- MenuItems/                  # MenuItem entity/aggregate
|   |   |   |   \-- Menus/                      # Menu entity/aggregate
|   |   |   |-- RallyAPI.Catalog.Application/
|   |   |   |   |-- Abstractions/
|   |   |   |   |-- MenuItems/
|   |   |   |   |   |-- Commands/               # CreateMenuItem, ToggleMenuItemAvailability, UpdateMenuItem
|   |   |   |   |   \-- Queries/                # GetMenuItemById, GetMenuItemsByMenu
|   |   |   |   |-- Menus/
|   |   |   |   |   |-- Commands/               # CreateMenu, DeleteMenu, UpdateMenu
|   |   |   |   |   \-- Queries/                # GetMenusByRestaurant
|   |   |   |   \-- Restaurants/
|   |   |   |       \-- Queries/                # GetRestaurantMenu, GetRestaurants, SearchMenuItems
|   |   |   |-- RallyAPI.Catalog.Endpoints/
|   |   |   |   |-- MenuItems/
|   |   |   |   |-- Menus/
|   |   |   |   \-- Restaurants/
|   |   |   \-- RallyAPI.Catalog.Infrastructure/
|   |   |       \-- Persistence/
|   |   |           |-- Configurations/
|   |   |           |-- Migrations/
|   |   |           \-- Repositories/
|   |   |
|   |   |-- Orders/                             # Order lifecycle + payments
|   |   |   |-- RallyAPI.Orders.Domain/
|   |   |   |   |-- Abstractions/
|   |   |   |   |-- Entities/
|   |   |   |   |-- Enums/
|   |   |   |   |-- Errors/
|   |   |   |   |-- Events/
|   |   |   |   |-- Repositories/               # Repository interfaces
|   |   |   |   \-- ValueObjects/
|   |   |   |-- RallyAPI.Orders.Application/
|   |   |   |   |-- Abstractions/
|   |   |   |   |-- Behaviors/                  # MediatR pipeline behaviors
|   |   |   |   |-- Commands/                   # PlaceOrder, ConfirmOrder, CancelOrder, RejectOrder,
|   |   |   |   |                               # AssignRider, UpdateOrderStatus, InitiatePayment,
|   |   |   |   |                               # ProcessPayuWebhook, VerifyPayment, RefundPayment
|   |   |   |   |-- DTOs/
|   |   |   |   |   \-- Requests/
|   |   |   |   |-- EventHandlers/
|   |   |   |   |-- Mappings/
|   |   |   |   \-- Queries/                    # GetOrderById, GetOrderByNumber, GetOrdersByCustomer,
|   |   |   |                                   # GetOrdersByRestaurant, GetActiveOrders
|   |   |   |-- RallyAPI.Orders.Endpoints/
|   |   |   \-- RallyAPI.Orders.Infrastructure/
|   |   |       |-- BackgroundServices/
|   |   |       |-- Configurations/
|   |   |       |-- Migrations/
|   |   |       |-- Repositories/
|   |   |       \-- Services/
|   |   |           \-- PayU/                   # PayU payment gateway integration
|   |   |
|   |   |-- Delivery/                           # Rider dispatch, tracking, delivery lifecycle
|   |   |   |-- RallyAPI.Delivery.Domain/
|   |   |   |   |-- Abstractions/
|   |   |   |   |-- Entities/
|   |   |   |   |-- Enums/
|   |   |   |   |-- Errors/
|   |   |   |   \-- Events/
|   |   |   |-- RallyAPI.Delivery.Application/
|   |   |   |   |-- Commands/                   # AcceptDeliveryOffer, CreateDeliveryRequest,
|   |   |   |   |                               # GetQuote, MarkDelivered, MarkFailed,
|   |   |   |   |                               # MarkPickedUp, TriggerDispatch
|   |   |   |   |-- DTOs/
|   |   |   |   |-- EventHandlers/
|   |   |   |   |-- Queries/                    # GetTrackingInfo
|   |   |   |   \-- Services/
|   |   |   |-- RallyAPI.Delivery.Endpoints/
|   |   |   |   \-- Requests/
|   |   |   \-- RallyAPI.Delivery.Infrastructure/
|   |   |       |-- Migrations/
|   |   |       |-- Persistence/
|   |   |       |   \-- Configurations/
|   |   |       |-- Repositories/
|   |   |       \-- Services/
|   |   |
|   |   |-- Pricing/                            # Delivery fee calculation, surge pricing
|   |   |   |-- RallyAPI.Pricing.Domain/
|   |   |   |   |-- Abstractions/
|   |   |   |   |-- Entities/
|   |   |   |   |-- Enums/
|   |   |   |   |-- Errors/
|   |   |   |   |-- Repositories/
|   |   |   |   \-- ValueObjects/
|   |   |   |-- RallyAPI.Pricing.Application/
|   |   |   |   |-- Abstractions/
|   |   |   |   |-- DTOs/
|   |   |   |   |-- Queries/                    # CalculateDeliveryFee
|   |   |   |   |-- Rules/                      # Pricing rules engine
|   |   |   |   \-- Services/
|   |   |   |-- RallyAPI.Pricing.Endpoints/
|   |   |   |   \-- Endpoints/
|   |   |   \-- RallyAPI.Pricing.Infrastructure/
|   |   |       |-- Persistence/
|   |   |       |   \-- Configurations/
|   |   |       |-- Providers/
|   |   |       |-- Repositories/
|   |   |       \-- Services/
|   |   |
|   |   |-- Users/                              # All user roles: auth, profiles, addresses, KYC
|   |   |   |-- RallyAPI.Users.Domain/
|   |   |   |   |-- Entities/
|   |   |   |   |-- Enums/
|   |   |   |   |-- Events/
|   |   |   |   \-- ValueObjects/
|   |   |   |-- RallyAPI.Users.Application/
|   |   |   |   |-- Abstractions/
|   |   |   |   |-- Admins/
|   |   |   |   |   |-- Commands/               # CreateAdmin, CreateRestaurant, CreateRider, Login, UpdateRiderKyc
|   |   |   |   |   \-- Queries/                # GetProfile, ListRestaurants, ListRiders
|   |   |   |   |-- Auth/
|   |   |   |   |   \-- Commands/               # RefreshToken, RevokeToken
|   |   |   |   |-- Customers/
|   |   |   |   |   |-- Commands/               # SendOtp, VerifyOtp, AddAddress, DeleteAddress,
|   |   |   |   |   |                           # SetDefaultAddress, UpdateAddress, UpdateProfile
|   |   |   |   |   \-- Queries/                # GetProfile, GetAddresses
|   |   |   |   |-- Restaurants/
|   |   |   |   |   |-- Commands/               # Login, SetAvailability, SetBusinessHours,
|   |   |   |   |   |                           # UpdateProfile, UploadLogo
|   |   |   |   |   \-- Queries/                # GetProfile
|   |   |   |   \-- Riders/
|   |   |   |       |-- Commands/               # SendOtp, VerifyOtp, GoOnline, GoOffline,
|   |   |   |       |                           # UpdateLocation, UpdateProfile, UploadKyc
|   |   |   |       \-- Queries/                # GetProfile
|   |   |   |-- RallyAPI.Users.Endpoints/
|   |   |   |   |-- Admins/
|   |   |   |   |-- Auth/
|   |   |   |   |-- Customers/
|   |   |   |   |-- Restaurants/
|   |   |   |   \-- Riders/
|   |   |   \-- RallyAPI.Users.Infrastructure/
|   |   |       |-- Persistence/
|   |   |       |   |-- Configurations/
|   |   |       |   |-- Migrations/
|   |   |       |   \-- Repositories/
|   |   |       \-- Services/
|   |   |
|   |   \-- Integrations/
|   |       \-- ProRouting/                     # External route optimization
|   |           \-- Models/
|   |
|   |-- RallyAPI.SharedKernel/                  # Cross-cutting concerns (shared by ALL modules)
|   |   |-- Abstractions/
|   |   |   |-- Delivery/
|   |   |   |-- Distance/
|   |   |   |-- Geocoding/                      # IGeocodingService, ReverseGeocodeResult, PlaceSuggestion, PlaceDetail
|   |   |   |-- Notifications/
|   |   |   |-- Orders/
|   |   |   |-- Pricing/
|   |   |   |-- Restaurants/
|   |   |   \-- Riders/
|   |   |-- Domain/
|   |   |   \-- IntegrationEvents/
|   |   |       |-- Delivery/
|   |   |       \-- Orders/
|   |   |-- Infrastructure/
|   |   |-- Middleware/
|   |   |-- Results/                            # Result<T> pattern
|   |   |-- Storage/
|   |   \-- Utilities/
|   |
|   \-- RallyAPI.Infrastructure/                # Shared infrastructure (Google Maps, Storage)
|       |-- GoogleMaps/
|       |   |-- Models/
|       |   |-- GoogleMapsDistanceCalculator.cs  # Routes API (distance/duration)
|       |   |-- GoogleGeocodingService.cs        # Geocoding + Places Autocomplete + Place Details
|       |   \-- GoogleMapsOptions.cs
|       \-- Storage/
|
|-- RallyAPI.Integrations.ProRouting.Tests/     # Integration tests
|-- specs/                                       # Feature specifications
\-- reviews/                                     # Daily AI workflow reviews
```

---

## Module Responsibilities

| Module | What It Owns | Key Entities |
|--------|-------------|--------------|
| **Catalog** | Restaurant listings, menus, menu items, search | Menu, MenuItem |
| **Orders** | Order lifecycle, PayU payments, order queries | Order, OrderItem, Payment |
| **Delivery** | Rider dispatch, delivery offers, tracking, status updates | DeliveryRequest, DeliveryOffer |
| **Pricing** | Delivery fee calculation, surge pricing rules | PricingRule, DeliveryFee |
| **Users** | All user types (Customer, Restaurant, Rider, Admin), auth, OTP, addresses, KYC | Customer, Restaurant, Rider, Admin, Address |
| **Integrations/ProRouting** | External route optimization | ProRouting API models |

## Cross-Module Communication

Modules NEVER import each other's internal types. They communicate via:

1. **SharedKernel.Abstractions/{ModuleName}/** — Shared interfaces per domain area
2. **SharedKernel.Domain.IntegrationEvents/{ModuleName}/** — Cross-module events
3. **MediatR Notifications** — Domain events published in one module, handled in another

| From | To | Via | Example |
|------|----|-----|---------|
| Orders | Delivery | IntegrationEvent (via SharedKernel) | Order confirmed -> Create delivery request (skipped for pickup orders) |
| Orders | Pricing | SharedKernel.Abstractions.Pricing | Get delivery fee during order placement |
| Delivery | Orders | IntegrationEvent (via SharedKernel) | Rider picked up -> Update order status |
| Users | (all) | SharedKernel.Abstractions | Auth/profile lookups via shared interfaces |

---

## Key Architecture Rules

1. **Module boundaries are STRICT.** Never reference `RallyAPI.Orders.Domain` from `RallyAPI.Delivery.Application`. Use SharedKernel abstractions or integration events.
2. **CQRS via MediatR.** Every write = Command + Handler. Every read = Query + Handler. Handlers in `Application/`.
3. **Domain layer has ZERO dependencies.** No EF Core, no MediatR, no external packages in any `.Domain` project.
4. **FluentValidation for all commands.** Validators live next to their command in `Application/Commands/{CommandName}/`.
5. **Repository pattern.** Interfaces in `Domain/Repositories/` (Orders) or `Application/Abstractions/` (other modules). Implementations in `Infrastructure/`.
6. **Endpoints are THIN.** Deserialize -> `mediator.Send(command)` -> return response. Zero business logic.
7. **Result<T> pattern.** Use `SharedKernel.Results` for handler return types. No exceptions for business errors.
8. **PayU webhook is the source of truth.** Never trust frontend payment redirect alone. Verify via S2S webhook in `Orders.Infrastructure/Services/PayU/`.
9. **Users module uses sub-grouping.** Commands/Queries are organized by role: `Admins/`, `Customers/`, `Restaurants/`, `Riders/`, plus shared `Auth/`.

## EF Core Conventions

- DbContext per module (e.g., `CatalogDbContext` in `Catalog.Infrastructure/Persistence/`)
- Entity configs via `IEntityTypeConfiguration<T>` in `Persistence/Configurations/` or `Configurations/`
- Migrations per module in `Infrastructure/Migrations/`
- `DateTimeOffset` for ALL timestamps, never `DateTime`
- Soft delete via `IsDeleted` + global query filter
- Audit fields: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`
- Decimal precision: `.HasPrecision(18, 2)` for money

## Auth Pattern

- RSA keys stored in `RallyAPI.Host/Keys/`
- RS256 JWT with RSA-2048 asymmetric signing
- Access token (short-lived) + Refresh token (long-lived, rotated)
- Theft detection: reused refresh token -> revoke entire token family
- BCrypt for passwords (restaurant/admin login via `Restaurants/Commands/Login`, `Admins/Commands/Login`)
- SHA256 for OTP hashing (customer/rider via `Customers/Commands/SendOtp+VerifyOtp`, `Riders/Commands/SendOtp+VerifyOtp`)
- Auth endpoints in `Users.Endpoints/Auth/` (RefreshToken, RevokeToken)

## Payment Flow (PayU India)

1. `InitiatePayment` command -> creates PayU hash -> returns redirect URL
2. Frontend redirects to PayU Hosted Checkout
3. User pays -> PayU redirects back to frontend
4. PayU sends S2S webhook -> `ProcessPayuWebhook` command
5. `VerifyPayment` validates hash -> updates order + payment status
6. `RefundPayment` for cancellations
7. All PayU code in: `Orders.Infrastructure/Services/PayU/`

## Order Fulfillment (Delivery vs Pickup)

Orders have a `FulfillmentType` enum: `Delivery` (default) or `Pickup`.

### Status Flow by Fulfillment Type
```
Delivery: Paid → Confirmed → Preparing → ReadyForPickup → PickedUp → Delivered
Pickup:   Paid → Confirmed → Preparing → ReadyForPickup → Delivered (skip rider)
```

### Key Rules
- **Pickup orders have NO DeliveryInfo** — `Order.DeliveryInfo` is nullable. Always null-check before accessing.
- **No DeliveryRequest created** for pickup orders — `OrderConfirmedIntegrationEventHandler` checks `IsPickupOrder` and returns early.
- **No rider assignment** for pickup orders — `AssignRider()`, `MarkPickedUp()`, `UpdateRiderInfo()` throw if `FulfillmentType == Pickup`.
- **DeliveryFee should be 0** for pickup orders — frontend must send `deliveryFee: 0` in pricing.
- **Customer pickup endpoint**: `PUT /api/orders/{id}/customer-pickup` (authorized: Admin or Restaurant) — transitions `ReadyForPickup → Delivered`.
- **Backward compatible**: `FulfillmentType` defaults to `Delivery` if omitted in request. Existing orders default to `Delivery` in the database.

### Pickup Order Request (Frontend)
```json
POST /api/orders
{
  "fulfillmentType": "Pickup",
  "restaurantId": "...",
  "pickupLatitude": 28.63,
  "pickupLongitude": 77.21,
  "pickupPincode": "110001",
  "items": [...],
  "pricing": { "subTotal": 500, "deliveryFee": 0, ... },
  "paymentId": "..."
}
```
Note: `deliveryAddress` is NOT required for pickup orders.

---

## Google Maps Address Integration

Server-side proxy for Google Maps APIs. The Google API key stays on the backend — frontend never calls Google directly.

### Geocoding Endpoints

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `GET /api/places/autocomplete?input=&lat=&lng=` | Customer | Type-ahead address search. Returns up to 5 suggestions with `placeId`, `description`, `mainText`, `secondaryText`. Biased to India. |
| `GET /api/places/{placeId}` | Customer | Resolve a Place ID to full details: `formattedAddress`, `latitude`, `longitude`, `locality`, `pincode`. |
| `GET /api/geocode/reverse?lat=&lng=` | Customer | Pin drop → formatted address. Returns `formattedAddress`, `placeId`, `locality`, `pincode`. India bounds validated. |

### Address Entry Flow (Frontend)

Two address entry methods (both supported):

**Method A — Autocomplete (user types):**
1. User types → `GET /api/places/autocomplete?input=Koramangala`
2. Show suggestions dropdown
3. User selects → `GET /api/places/{placeId}` to get lat/lng
4. Pre-fill address form with `formattedAddress`, `latitude`, `longitude`, `placeId`
5. User adds label (Home/Work/Other) and optional landmark
6. `POST /api/customers/addresses` with all fields including `placeId`

**Method B — Pin drop (user taps map):**
1. User drops pin on map → capture lat/lng
2. `GET /api/geocode/reverse?lat=12.93&lng=77.63`
3. Pre-fill `formattedAddress` and `placeId`
4. User confirms/edits, adds label and landmark
5. `POST /api/customers/addresses`

### Address Value Object

```json
{
  "addressLine": "123 MG Road, Koramangala",
  "landmark": "Near Forum Mall",
  "latitude": 12.9352,
  "longitude": 77.6245,
  "label": "Home",
  "placeId": "ChIJLfyY2E4UrjsRVq4AjI7zgRY"
}
```

`PlaceId` is optional but recommended — allows re-fetching details and avoids duplicate geocoding.

---

## Common Commands (Windows PowerShell)

```powershell
# Backend
dotnet run --project src/RallyAPI.Host                              # Start API
dotnet build                                                         # Build all
dotnet test                                                          # Run all tests

# EF Core Migrations (per module)
dotnet ef migrations add MigrationName `
  --context CatalogDbContext `
  --project src/Modules/Catalog/RallyAPI.Catalog.Infrastructure `
  --startup-project src/RallyAPI.Host

dotnet ef database update `
  --context CatalogDbContext `
  --startup-project src/RallyAPI.Host

# Docker (local dev)
docker compose up -d                         # Start Postgres + Redis
docker compose down

# Railway
railway up                                   # Deploy
railway logs                                 # View logs
```

## Environment Variables

```
DATABASE_URL=postgresql://...
REDIS_URL=redis://...
JWT_ISSUER=rally-api
JWT_AUDIENCE=rally-app
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=30
PAYU_MERCHANT_KEY=
PAYU_MERCHANT_SALT=
PAYU_BASE_URL=
GOOGLE_MAPS_API_KEY=
R2_ACCOUNT_ID=
R2_ACCESS_KEY=
R2_SECRET_KEY=
R2_BUCKET_NAME=
MSG91_AUTH_KEY=
MSG91_TEMPLATE_ID=
```

---

## AI Agent Instructions

### Before Writing Any Code

1. **Read this entire file** — understand the modular monolith structure.
2. **Search existing code** — Grep/Glob for related handlers, entities, endpoints. Don't create duplicates.
3. **Identify the correct module** — Catalog? Orders? Delivery? Users? Pricing?
4. **Follow the layer order** — Domain -> Application -> Infrastructure -> Endpoints.
5. **Check existing patterns** — Look at how similar features are implemented in the same module.

### When Adding a Feature to an Existing Module

1. Check `specs/` for a spec
2. Look at existing commands/queries in that module for the pattern to follow
3. Create Domain changes first (if any), then Application (Command + Handler + Validator), then Infrastructure, then Endpoint
4. FluentValidation validator for EVERY new command
5. `dotnet build` then `dotnet test`

### When Creating a New Module (e.g., Notifications for SignalR)

1. Create four projects:
   - `src/Modules/{Name}/RallyAPI.{Name}.Domain`
   - `src/Modules/{Name}/RallyAPI.{Name}.Application`
   - `src/Modules/{Name}/RallyAPI.{Name}.Infrastructure`
   - `src/Modules/{Name}/RallyAPI.{Name}.Endpoints`
2. Add shared abstractions to `SharedKernel/Abstractions/{Name}/` if needed
3. Add integration events to `SharedKernel/Domain/IntegrationEvents/{Name}/` if needed
4. Register services in `RallyAPI.Host/Program.cs`
5. Add to solution file

## Common Mistakes

Mistakes observed in practice. Check these before writing test or module code.

### 1. Test Project `ProjectReference` depth is 4 levels, not 3

Test projects live at `tests/Modules/{Module}/RallyAPI.{Module}.Tests/`.
That is **4 directories deep** from the repo root, so the path to `src/` is `../../../../src/`.

```xml
<!-- WRONG — only 3 levels up -->
<ProjectReference Include="../../../src/Modules/Orders/RallyAPI.Orders.Domain/..." />

<!-- CORRECT -->
<ProjectReference Include="../../../../src/Modules/Orders/RallyAPI.Orders.Domain/..." />
```

### 2. Always read the enum file before using enum values

Enum member names are not guessable. Wrong names compile only if you misread the type.
Always `grep` or open the enum file first.

```csharp
// WRONG — "CustomerRequest" does not exist
order.Cancel(CancellationReason.CustomerRequest, ...);

// CORRECT — the actual member name
order.Cancel(CancellationReason.CustomerRequested, ...);
```

Same rule applies to `OrderStatus`, `PaymentStatus`, `DeliveryStatus`, `FleetType`, etc.
When writing tests that send enum values as JSON strings, the string must exactly match the C# member name (e.g. `"CustomerRequested"`, not `"Changed my mind"`).

### 3. xUnit needs an explicit `using Xunit;`

xUnit is **not** included in `ImplicitUsings`. Every test file must have it explicitly.

```csharp
using Xunit; // required — not pulled in by ImplicitUsings
```

FluentAssertions and NSubstitute also need explicit usings:

```csharp
using FluentAssertions;
using NSubstitute;
```

### 4. Microsoft.Extensions packages require version 8.0.2 in test projects

Do **not** pin `Microsoft.Extensions.*` packages to `8.0.0` in test project `.csproj` files.
The transitive dependency chain (e.g. `SharedKernel → Extensions.Logging 8.0.1 → Abstractions >= 8.0.2`) requires at least `8.0.2`, causing a restore failure if you pin lower.

```xml
<!-- WRONG — causes NuGet restore failure -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />

<!-- CORRECT — omit the reference and let NuGet resolve transitively -->
<!-- Or pin to 8.0.2 if an explicit reference is truly needed -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
```

### 5. Never call SaveChangesAsync after ExecuteDeleteAsync

`ExecuteDeleteAsync` (EF Core 7+) executes a `DELETE` directly against the database — it does not go through the change tracker. Calling `SaveChangesAsync` afterwards is a no-op at best and misleading at worst.

```csharp
// WRONG
await _context.CartItems.Where(x => x.CartId == cartId).ExecuteDeleteAsync(ct);
await _context.SaveChangesAsync(ct); // ← unnecessary, remove it

// CORRECT
await _context.CartItems.Where(x => x.CartId == cartId).ExecuteDeleteAsync(ct);
```

Same rule applies to `ExecuteUpdateAsync`.

### 6. Always specify schema in manual SQL migrations

When writing a raw SQL migration (e.g. `migrationBuilder.Sql(...)`), always qualify table names with their schema. Without the schema, Postgres defaults to `public` and the statement silently targets the wrong table or throws.

```csharp
// WRONG — targets public."Carts" if schema is "orders"
migrationBuilder.Sql("CREATE INDEX ...");

// CORRECT — always qualify
migrationBuilder.Sql(@"CREATE INDEX ... ON orders.""Carts"" ...");
// For the users module:
migrationBuilder.Sql(@"ALTER TABLE users.""Riders"" ...");
```

**Even better — don't hand-write DDL that EF can model.** Use `migrationBuilder.AlterColumn(...)`, `AddColumn(...)`, `DropColumn(...)`, `CreateTable(...)` etc. These read the real table/column names from your `IEntityTypeConfiguration<T>` mappings, so typos become impossible. Reserve `migrationBuilder.Sql(...)` for data migrations, sequences, or raw DDL that EF genuinely can't express (triggers, extensions, complex constraints).

Real incident (2026-04-21): a hand-written `ALTER TABLE orders."DeliveryInfos" ALTER COLUMN "OrderId"` migration crashed every Railway startup because the actual table is `orders.delivery_info` with column `order_id`. Auto-generated `AlterColumn(...)` would have used the right names.

### 7. Add JsonStringEnumConverter to BOTH JSON option chains in Program.cs

`Program.cs` configures JSON serialization in two places. Missing either one causes enum values to serialize as integers in some responses.

```csharp
// Both chains must include the converter:
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()); // ← required here
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); // ← AND here
});
```

### Security Checklist (ALWAYS)

- [ ] No secrets in code — use environment variables or `Host/Keys/`
- [ ] All command inputs validated with FluentValidation
- [ ] Auth required on endpoint (`.RequireAuthorization("RoleName")`)
- [ ] PayU webhook hash verified in `ProcessPayuWebhook`
- [ ] Rate limiting on OTP endpoints (SendOtp)
- [ ] CORS configured for allowed origins only

### Pre-Commit Checklist — Migrations (MANDATORY)

If the commit touches any file under `**/Migrations/**` OR changes any EF entity/configuration, run through this before `git commit`:

- [ ] **Migration is auto-generated, not hand-written** — ran `dotnet ef migrations add X` rather than pasting raw SQL. Raw `migrationBuilder.Sql(...)` is allowed only for sequences, data migrations, or DDL EF can't model.
- [ ] **Table and column names verified** — if you DID write raw SQL, grep the initial migration or the `IEntityTypeConfiguration<T>` to confirm the exact casing (`delivery_info` not `"DeliveryInfos"`, `order_id` not `"OrderId"`).
- [ ] **Schema qualified** — every `migrationBuilder.Sql(...)` includes the schema (`orders.`, `users.`, etc.).
- [ ] **Applied locally** — ran `dotnet ef database update --context <Name>DbContext --project src/Modules/<Module>/.../Infrastructure --startup-project src/RallyAPI.Host` against local Postgres and it succeeded.
- [ ] **`dotnet build` is green** with zero errors.
- [ ] **Startup smoke test** — `dotnet run --project src/RallyAPI.Host` boots without throwing in the migration block (`Migrating X database...` lines all succeed).

Why this checklist exists: a bad migration crashes Railway startup silently — the old container keeps serving traffic with stale schema, and you only find out hours later when a query hits a missing column. Catch it locally.

### Pre-Commit Reminder for AI Agents

Before proposing `git commit`, always prompt the user with:
1. The pre-commit migration checklist above if any migration/entity file changed.
2. A confirmation that `dotnet build` succeeded.
3. A reminder that Railway auto-migrates on boot — if the migration is broken, the deploy silently stays on the old container.

Never run `git commit` without explicit user approval when migration files are in the diff.
