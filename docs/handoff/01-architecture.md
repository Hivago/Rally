# 01 — Architecture

## Shape

.NET 8 / C# 12, Minimal APIs, **modular monolith**. Each module = 4 projects:
`Domain → Application → Infrastructure → Endpoints`. One Postgres database, **one DbContext and
schema per module** (users, catalog, orders, delivery, pricing, marketing, audit). Migrations
auto-run on startup (`Program.cs`, `Database.Migrate()` per context). MediatR for CQRS,
FluentValidation per command, `Result<T>` instead of exceptions for business errors.
Entry point: `src/RallyAPI.Host/Program.cs`.

| Module | Owns |
|--------|------|
| Users | Customers, Restaurants, Riders, Admins; JWT auth, OTP, addresses, KYC, restaurant location review |
| Catalog | Restaurant browse, menus, items/options, search, Google reviews |
| Orders | Cart, order lifecycle, PayU payments, payouts (ICICI manual export), outbox |
| Delivery | DeliveryRequest/offers, dispatch orchestrator, tracking, pin-drift detection |
| Pricing | Delivery fee, platform fee + GST, surge/rules engine (`PricingEngine`) |
| Marketing | Promotions (has its own tests) |
| Integrations/ProRouting | 3PL client + webhook endpoints |
| SharedKernel | Base types, `Result<T>`, abstractions per module, integration events, idempotency filter |
| RallyAPI.Infrastructure | Google Maps (distance, geocoding, places) |

## Module boundaries (convention only!)

Rule: modules never reference each other's internals; they talk through
`SharedKernel/Abstractions/<Module>`, `SharedKernel/Domain/IntegrationEvents/<Module>`, or MediatR notifications.
**Not enforced by tests.** Known violations (verified 2026-10-06): `Users.Endpoints` references
`Orders.Application`/`Orders.Domain` (14 admin endpoints: cancel/refund/assign-rider/escalate/payout export+reconcile);
`Marketing.Endpoints` references `Users.Application`/`Users.Domain.Enums`. Fix idea: move those admin
endpoints into Orders (or an Admin module) and add a NetArchTest rule.

## Core flow 1 — Order → payment → confirmation

1. Customer gets a delivery quote (`POST /api/delivery/quote`) → `deliveryQuoteId`.
2. `POST /api/orders` (requires `Idempotency-Key` header, `IdempotencyEndpointFilter`) → `PlaceOrderCommand`.
   Creates a **Pending** order. Pickup orders (`fulfillmentType: Pickup`) have no `DeliveryInfo` and no rider.
3. `POST /api/payments/initiate` → PayU hash + params; frontend POSTs the form to PayU (same tab, no `target="_blank"`).
4. PayU confirms via **three independent paths** (any one is enough):
   - S2S webhook `POST /api/payments/webhook` (must be configured in PayU dashboard — see R1)
   - Browser POST to surl/furl = backend `/api/payments/return/success|failure`, which runs
     `ProcessPayuWebhookCommand` (hash-verified, idempotent) *then* 302s to the SPA
   - `PendingPaymentReconciliationService` (60 s sweep) calls PayU `verify_payment` for Pending orders >90 s old
   - plus frontend `POST /api/payments/verify`
5. `OrderAutoCancelService` cancels unpaid orders after `PaymentTimeoutMinutes` (10) **but first verifies with PayU** so a paid order is never cancelled.
6. Status: `Delivery: Paid → Confirmed → Preparing → ReadyForPickup → PickedUp → Delivered`;
   `Pickup: … ReadyForPickup → Delivered` (`PUT /api/orders/{id}/customer-pickup`, Admin/Restaurant).
   Transitions are guarded inside the `Order` aggregate (throws `DomainException`).

## Core flow 2 — Dispatch (rider assignment)

1. Restaurant marks **Ready** → `OrderReadyForPickupEventHandler` writes to the **outbox**
   (`OutboxWriter`) → `OutboxProcessor` → `TriggerDispatchCommand` → `RiderDispatchOrchestrator.DispatchAsync`.
   (`OrderConfirmedIntegrationEvent` also goes through the outbox and creates the `DeliveryRequest`; skipped for pickup orders.)
2. Orchestrator, with `Delivery:Dispatch:OwnFleetFirst=true`: **broadcast** offers to all eligible own riders
   (max `MaxRidersToTry`=10, radius 5 km, one `AcceptanceTimeoutSeconds`=30 window, first accept wins),
   then fall back to ProRouting 3PL; only `MarkFailed` if both fail. Spec: `specs/own-fleet-first-dispatch.md`.
3. Rider gates (`RiderQueryService.GetAvailableRidersAsync`): IsActive, IsOnline, KYC Verified,
   `CurrentDeliveryId == null`, has lat/lng, location ping within `LocationMaxAgeMinutes`, within radius **of the restaurant**.
   Diagnose one rider: `GET /api/deliveries/diagnostics/rider-eligibility?pickupLat=&pickupLng=&riderId=` (Admin).
4. Offer delivery to rider: SignalR `ReceiveDeliveryOffer` to group `rider_{riderId}` (hub needs JWT with
   `user_type=rider`); poll fallback `GET /api/v1/riders/delivery/pending-offer`.
5. Accept → `AcceptDeliveryOfferCommandHandler` → `RiderAssigned` → integration events update the Order
   (rider info, PickedUp, Delivered, Failed).
6. 3PL: ProRouting quotes → create task → webhooks (`/api/webhooks/prorouting`) for assignment/status/OTP.
   Webhook is the **only** signal an agent was assigned. See `docs/prorouting-integration.md`.
7. Safety nets: `DeliveryDispatchRecoveryService` (15 s) re-triggers requests wedged >2 min, with a hard
   **3 h age floor** (R4). `DeliveryRequest` has an `xmin` concurrency token; terminal-fail writes use `TryUpdateAsync`.

## Core flow 3 — Auth

- **RS256 JWT**, RSA-2048. Keys from env `JwtSettings__PrivateKeyPem`/`PublicKeyPem` or files `Keys/private.pem`/`public.pem`.
  Access token 15 min, refresh 60 days (rotated; reuse ⇒ whole family revoked).
- Customers + riders: OTP (SHA256-hashed in Redis; provider `OtpProvider=AuthKey`, MSG91 WhatsApp is built but blocked on Meta verification).
  Restaurants + admins: email/password (BCrypt).
- Policies check the `user_type` claim (`customer|rider|restaurant|owner|admin`); `Orders` also uses the `role` claim via `IsInRole`.
- Rate limiting (`Program.cs` ~270–412): order/payment per-user (JWT `sub`); OTP/login/refresh per-IP (widened for carrier CGNAT — accepted tradeoff). Real anti-abuse for OTP is the per-phone guard in `OtpService`.

## Real-time

SignalR `NotificationHub` with a Redis backplane. Replaced FCM for the web launch. Frontends invalidate
React Query on events (`NewOrderReceived`, `OrderStatusChanged`, `ReceiveDeliveryOffer`).

## Background services (all run in EVERY API instance, no leader election)

| Service | Purpose |
|---------|---------|
| `OutboxProcessor` | Drains outbox → Delivery handlers. **No row locking** |
| `DeliveryDispatchRecoveryService` | Re-dispatches wedged delivery requests (15 s poll, 3 h age floor) |
| `OrderAutoCancelService` | Unaccepted-order escalation/cancel; payment timeout (verifies with PayU first) |
| `PendingPaymentReconciliationService` | PayU `verify_payment` backstop (60 s) |
| `RiderPresenceSweepService` | Marks stale riders offline |
| `WeeklyPayoutBatchService`, `RiderPayoutAggregationJob` | Restaurant/rider payout batches (check-then-act races if scaled) |
| `CartCleanupService` | Expires old carts |
| `RestaurantPinDriftDetectionService` | Every 30 min; flags restaurants whose last 10 rider arrivals all drift >75 m → admin review queue |

## Data conventions (gotchas that bit us)

- `DateTimeOffset` everywhere; soft delete via `IsDeleted` + global filter; money `HasPrecision(18,2)`.
- Guid PKs are client-generated. **New child entities of a tracked aggregate must be `context.Add(...)`-ed explicitly** or EF marks them Modified → 0-row UPDATE → `DbUpdateConcurrencyException` (R8).
- `List<string>` → `jsonb` needs an explicit `HasConversion` + `ValueComparer` on Npgsql 8.x.
- `JsonStringEnumConverter` must be registered in **both** JSON option chains in `Program.cs`.
- Redis cache is fail-open (`RedisCacheService`): catalog browse 30 s, menu 60 s, Google reviews 24 h by Place ID. Menu-changing commands implement `IMenuCacheInvalidatingCommand`.
- `Order.DeliveryInfo` is nullable (pickup orders) — always null-check.

## Where things live

| Thing | Path |
|-------|------|
| Entry/DI/auth/rate limits | `src/RallyAPI.Host/Program.cs` |
| Order aggregate | `src/Modules/Orders/RallyAPI.Orders.Domain/Entities/Order.cs` |
| PlaceOrder | `src/Modules/Orders/RallyAPI.Orders.Application/Commands/PlaceOrder/` |
| PayU | `src/Modules/Orders/RallyAPI.Orders.Infrastructure/Services/PayU/`, endpoints in `PaymentEndpoints.cs` |
| Dispatch | `src/Modules/Delivery/RallyAPI.Delivery.Application/Services/RiderDispatchOrchestrator.cs` |
| ProRouting | `src/Modules/Integrations/ProRouting/`, webhooks in `WebhookEndpoints.cs` |
| Domain event dispatch | `src/RallyAPI.SharedKernel/Infrastructure/DomainEventInterceptor.cs` (swallows handler exceptions!) |
| Pricing engine | `src/Modules/Pricing/RallyAPI.Pricing.Application/Services/PricingEngine.cs` |
