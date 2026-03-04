# Rally API — Handoff Document
> Last updated: 17 Feb 2026

## What Is This
A **modular monolith** (.NET 8, Minimal APIs, MediatR, EF Core, PostgreSQL, Redis) for a food delivery platform. Think Swiggy/Zomato backend.

## Architecture
```
RallyAPI.Host (entry point)
├── Modules/
│   ├── Users        — Auth (OTP-based JWT), Customer/Rider/Restaurant/Admin entities
│   ├── Catalog      — Restaurants, Menus, MenuItems
│   ├── Orders       — Order lifecycle, state machine (Paid→Confirmed→Preparing→Ready→PickedUp→Delivered)
│   ├── Delivery     — DeliveryQuote, DeliveryRequest, rider dispatch
│   └── Pricing      — Delivery fee calculation
├── Integrations/
│   └── ProRouting   — 3PL delivery provider integration
├── RallyAPI.SharedKernel   — Base entities, Result pattern, integration events
└── RallyAPI.Infrastructure — Google Maps distance calculator
```

Each module follows: `Domain → Application → Infrastructure → Endpoints`

## Cross-Module Communication
Modules talk via **integration events** through MediatR (in-process, same DB transaction):

```
Orders module                          Delivery module
─────────────                          ───────────────
Order.Confirm()
  → OrderConfirmedEvent (domain)
    → OrderConfirmedEventHandler
      enriches: addresses, contacts, pricing
      → publishes OrderConfirmedIntegrationEvent (SharedKernel)
                                         → OrderConfirmedIntegrationEventHandler
                                           creates DeliveryRequest (PendingDispatch)
```

Reverse flow (Delivery → Orders) has 4 handlers:
- `DeliveryRiderAssignedEventHandler` → updates order with rider info
- `DeliveryPickedUpEventHandler` → marks order PickedUp
- `DeliveryCompletedEventHandler` → marks order Delivered
- `DeliveryFailedEventHandler` → marks order Failed

## Key Patterns
- **CQRS**: Commands/Queries via MediatR `IRequest<Result<T>>`
- **Result pattern**: `Result<T>` / `Result` with `Error` — no exceptions for business logic
- **Domain events**: Raised in aggregate roots via `AddDomainEvent()`, dispatched by `DomainEventInterceptor` on `SaveChanges`
- **Aggregate roots**: `Order`, `DeliveryRequest` — all mutations through methods, not setters
- **Value objects**: `OrderNumber`, `Money`, `GeoCoordinate`, `Address`, `OrderPricing`

## Auth
- OTP-based login (Redis-backed, SHA256-hashed OTPs)
- JWT with RSA256 (asymmetric keys)  
- Policies: `Customer`, `Rider`, `Restaurant`, `Admin`
- OTP currently logs to console (no SMS provider)

## What Works
- ✅ Full order lifecycle (place → confirm → prepare → ready → pickup → deliver)
- ✅ Event bridge between Orders ↔ Delivery
- ✅ Delivery quoting (Google Maps + ProRouting)
- ✅ OTP auth with Redis (customers, riders)
- ✅ Catalog CRUD
- ✅ Rate limiting on auth endpoints

## What's Missing (Validated Gaps)
| Category | Gaps |
|---|---|
| **Launch blockers** | No payment integration, no SMS provider, no push notifications (stub only), no HTTPS, no CORS, no health checks |
| **Day-1 features** | No cart, no search, no order tracking (no SignalR/WebSocket), no admin login endpoint |
| **Infrastructure** | No frontend, no Dockerfile, no CI/CD, no monitoring, effectively zero tests |
| **Rider dispatch** | Manual only — admin calls rider and updates status; `RiderDispatchOrchestrator` exists but not auto-triggered |

## Known Inconsistencies
- Endpoint style: Users uses per-file `IEndpoint` pattern; Orders/Delivery use large static classes
- Repository interfaces: `Application/Abstractions/` (most) vs `Domain/Abstractions/` (Delivery)
- `UnitOfWork`: Different folders, access modifiers, and async patterns across modules
- Admin endpoints (`Login`, `CreateRestaurant`, `CreateRider`): empty class stubs

## Key Files
| Purpose | Path |
|---|---|
| Entry point | `src/RallyAPI.Host/Program.cs` |
| Order aggregate | `src/Modules/Orders/RallyAPI.Orders.Domain/Entities/Order.cs` |
| Event bridge handler | `src/Modules/Orders/RallyAPI.Orders.Application/EventHandlers/OrderConfirmedEventHandler.cs` |
| Integration event | `src/RallyAPI.SharedKernel/Domain/IntegrationEvents/Orders/OrderConfirmedIntegrationEvent.cs` |
| Delivery consumer | `src/Modules/Delivery/RallyAPI.Delivery.Application/EventHandlers/OrderConfirmedIntegrationEventHandler.cs` |
| OTP service | `src/Modules/Users/RallyAPI.Users.Infrastructure/Services/OtpService.cs` |
| Domain event dispatcher | `src/RallyAPI.SharedKernel/Infrastructure/DomainEventInterceptor.cs` |
