# Rally Project Map

This is the short human map. Use it when the repo feels too big.

## Mental Model

Rally is one deployable ASP.NET Core app with multiple business modules inside it.

```text
HTTP / SignalR
    |
RallyAPI.Host
    |
module endpoints
    |
MediatR commands and queries
    |
domain entities, repositories, external services
    |
Postgres, Redis, PayU, Google Maps, ProRouting, Cloudflare R2
```

The important idea: modules should be understandable on their own. Shared things live in `RallyAPI.SharedKernel`; provider clients and cross-cutting infrastructure live outside the modules.

## Modules

| Module | Owns | Look here first |
|---|---|---|
| Users | Customers, riders, restaurants, owners, admins, auth, profiles, addresses, KYC | `src/Modules/Users` |
| Catalog | Menus, menu items, option groups, customer browsing/search | `src/Modules/Catalog` |
| Orders | Cart, order lifecycle, payment, refunds, payouts, notes | `src/Modules/Orders` |
| Delivery | Quotes, delivery request lifecycle, rider offers, tracking | `src/Modules/Delivery` |
| Pricing | Delivery fee rules and calculations | `src/Modules/Pricing` |
| Integrations/ProRouting | Third-party delivery provider API models and client | `src/Modules/Integrations/ProRouting` |

## Layer Pattern

Most modules follow this structure:

```text
RallyAPI.{Module}.Domain
  Entities, value objects, enums, domain events, domain errors.

RallyAPI.{Module}.Application
  Commands, queries, handlers, validators, DTOs, repository interfaces.

RallyAPI.{Module}.Infrastructure
  EF DbContext, migrations, repository implementations, provider services.

RallyAPI.{Module}.Endpoints
  Minimal API route mapping. Translate HTTP to commands/queries only.
```

When adding behavior, move top to bottom:

1. Domain: what state and business rule changes?
2. Application: what command/query exposes that behavior?
3. Infrastructure: what persistence or provider implementation is needed?
4. Endpoints: what route calls the command/query?
5. Tests: what proves the behavior?

## Entry Points

| Purpose | File |
|---|---|
| App startup, DI, auth, CORS, health checks, endpoint mapping | `src/RallyAPI.Host/Program.cs` |
| SignalR hub | `src/RallyAPI.Host/Hubs/NotificationHub.cs` |
| Users endpoint registration | `src/Modules/Users/RallyAPI.Users.Endpoints/DependencyInjection.cs` |
| Catalog endpoint registration | `src/Modules/Catalog/RallyAPI.Catalog.Endpoints/DependencyInjection.cs` |
| Orders endpoint registration | `src/Modules/Orders/RallyAPI.Orders.Endpoints/OrderEndpoints.cs` |
| Cart endpoint registration | `src/Modules/Orders/RallyAPI.Orders.Endpoints/CartEndpoints.cs` |
| Payment endpoint registration | `src/Modules/Orders/RallyAPI.Orders.Endpoints/PaymentEndpoints.cs` |
| Payout endpoint registration | `src/Modules/Orders/RallyAPI.Orders.Endpoints/PayoutEndpoints.cs` |
| Delivery endpoint registration | `src/Modules/Delivery/RallyAPI.Delivery.Endpoints/DependencyInjection.cs` |

## Main Flows

### Customer Auth

```text
POST /api/customers/otp/send
POST /api/customers/otp/verify
    -> Users.Application Customers commands
    -> Users.Infrastructure OtpService, CustomerRepository, JwtProvider
    -> JWT access and refresh tokens
```

### Cart to Order

```text
/api/cart/*
    -> Orders.Application Cart commands/queries
    -> Redis-backed cart cache / order data

POST /api/orders
    -> PlaceOrderCommand
    -> Order aggregate
    -> OrdersDbContext
```

### Payment

```text
POST /api/payments/initiate
    -> InitiatePaymentCommand
    -> PayU hosted checkout payload

POST /api/payments/webhook
    -> ProcessPayuWebhookCommand
    -> verifies PayU response
    -> updates order/payment state
```

### Restaurant Accepts Order

```text
PUT /api/orders/{orderId}/confirm
    -> ConfirmOrderCommand
    -> OrderConfirmed domain event
    -> OrderConfirmedIntegrationEvent
    -> Delivery module creates dispatch request
    -> SignalR notification handlers notify connected clients
```

### Rider Delivery

```text
Delivery dispatch creates rider offer
    -> rider receives SignalR notification
    -> /api/rider/delivery/offer/{offerId}/accept
    -> rider marks pickup/drop/delivered through /api/rider/delivery/*
    -> Delivery integration events update Orders
```

### Restaurant Payout

```text
Order delivered
    -> payout ledger entry
    -> restaurant payout APIs read earnings/history
    -> admin payout APIs process pending payouts
```

## Cross-Module Rules

Direct module-to-module imports are the fastest way to make the code impossible to reason about. Use these routes instead:

| Need | Use |
|---|---|
| Shared primitive contracts | `RallyAPI.SharedKernel/Abstractions` |
| Cross-module lifecycle changes | Integration events in `RallyAPI.SharedKernel/Domain/IntegrationEvents` |
| External provider behavior | An abstraction in SharedKernel or Application, implementation in Infrastructure |

## Current Rough Edges

- Endpoint style is inconsistent: Users and Catalog mostly use per-file endpoint classes; Orders and Delivery use larger endpoint classes.
- Some repository interfaces live in Application; Delivery still has some Domain-facing abstractions.
- The root has old logs and generated files that obscure the actual project.
- `src/RallyAPI.Host/Program.cs` does a lot and could eventually be split into smaller registration methods.
- Some dev/test routes exist beside production routes. Keep them environment-gated.
