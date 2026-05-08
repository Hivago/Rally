# Feature Spec: Payouts Phase 2 — Commission flat fee, rider payouts, server quote, PayU outbound

> **Status**: Ready for Review
> **Priority**: P0 (Critical — blocks first real payout cycle)
> **Estimated Effort**: ~8–10 working days, sliced into 9 PRs
> **Module(s)**: Orders, Users, Pricing
> **Owner**: Yash
> **Date**: 2026-04-24

---

## 1. Problem Statement

Payouts phase 1 landed the plumbing — `PayoutLedger`, `Payout`, `WeeklyPayoutBatchService`, admin review endpoints. Before Hivago can run a real payout cycle, four gaps need to close:

1. Commission model is percentage-based. Finance needs a flat fee per order.
2. Riders currently get nothing — no earnings config, no ledger, no batch.
3. Restaurants and riders have no bank details, so admin cannot actually disburse.
4. PayU integration is inbound-only (customer checkout). Outbound disbursement does not exist.

Phase 2 also introduces a server-side quote endpoint so the customer bill is not trusted to frontend math, and makes the platform fee configurable.

## 2. User Stories

- As a **customer**, I want to see the final bill (subtotal + GST + delivery + platform fee) before I pay, computed server-side, so the amount I am charged matches what I saw.
- As a **restaurant owner**, I want my bank details stored once and verified by admin so every Monday's payout lands in the right account without re-asking.
- As a **rider**, I want my earnings per delivery calculated transparently (base + per-km + weather/festival bonuses) and paid out weekly to my bank/UPI.
- As an **admin**, I want to tune rider earnings config, platform fee, and per-restaurant commission flat fee without a deploy, and one-click approve weekly payout batches.

## 3. Acceptance Criteria (overall)

- [ ] `Restaurant.CommissionPercentage` replaced with `Restaurant.CommissionFlatFee`. Existing orders still compute correct historical payouts (snapshot already stored on `PayoutLedger`, no backfill needed).
- [ ] New `PlatformFeeConfig` table, admin CRUD, feeds `OrderPricing.ServiceFee`.
- [ ] New `RiderEarningsConfig` table, admin CRUD, drives `RiderEarningsCalculator`.
- [ ] Every delivered Hivago+Delivery order produces exactly one `RiderPayoutLedger` row.
- [ ] Weekly batch produces `Payout` (restaurant) and `RiderPayout` (rider) rows for the prior week, at Sunday 23:30 IST.
- [ ] Admin cannot process a payout whose payee has `IsBankVerified = false`.
- [ ] `POST /api/orders/quote` returns the exact total a customer will be charged. `PlaceOrder` validates that `quote_id` matches and has not expired.
- [ ] `IPayUPayoutClient.DisburseAsync` is called during `ProcessPayout`; failure surfaces as `PayoutStatus.Failed` with the error message stored in `notes`.

## 4. Money Flow (source of truth)

```
Customer pays (quote total):
  subtotal
  + gst_on_food         = subtotal × 5%
  + delivery_fee        = PricingEngine (all 7 rules)
  + gst_on_delivery     = delivery_fee × 18%
  + platform_fee        = PlatformFeeConfig.Amount
  + gst_on_platform     = platform_fee × 18%

Restaurant receives (weekly):
  subtotal
  - commission_flat_fee
  - gst_on_commission   = commission_flat_fee × 18%
  - tds                 = subtotal × 1%      (Section 194-O, on gross)

Rider receives, per Hivago+Delivery order (weekly):
  base_fee              = RiderEarningsConfig.BaseFee (flat up to BaseDistanceKm)
  + per_km_charge       = max(0, distance_km - BaseDistanceKm) × PerKmAboveBase
  + weather_bonus       = RiderEarningsConfig.WeatherBonus if rainy
  + special_day_bonus   = RiderEarningsConfig.SpecialDayBonus if festival/special day

Hivago net revenue per order:
  commission_flat_fee
  + (delivery_fee - rider_earnings)
  + platform_fee
  (GST and TDS are pass-through to govt, not revenue)
```

All rupee amounts rounded with `Math.Round(x, 2)` to match existing `PayoutLedger.Create` precision.

## 5. Quote endpoint (Item 6 reference)

### Request

```http
POST /api/orders/quote
Authorization: Customer
Content-Type: application/json

{
  "restaurantId": "…",
  "fulfillmentType": "Delivery",        // or "Pickup"
  "items": [ { "menuItemId": "…", "quantity": 2 } ],
  "deliveryLatitude": 12.9352,
  "deliveryLongitude": 77.6245,
  "deliveryPincode": "560034",
  "promoCode": null
}
```

Pickup orders skip `deliveryLatitude/Longitude/Pincode` and the response zeroes out delivery lines.

### Response (200)

```json
{
  "subtotal": 500.00,
  "gst_on_food": 25.00,
  "delivery_fee": 65.00,
  "gst_on_delivery": 11.70,
  "platform_fee": 10.00,
  "gst_on_platform": 1.80,
  "total": 613.50,
  "quote_id": "quote_7f3c1a9e2b8d4f5a6c7d",
  "quote_expires_at": "2026-04-24T11:30:00Z",
  "delivery_fee_breakdown": {
    "base_fee": 30.00,
    "distance_charge": 20.00,
    "weather_surcharge": 10.00,
    "special_day_surcharge": 0.00,
    "demand_surge": 5.00,
    "time_surge": 0.00,
    "third_party_delivery": 0.00
  },
  "currency": "INR"
}
```

Rider earnings are **not** in this response — internal only.

### Quote expiry / replay

- `quote_id` is a ULID-like string. Back it with a short-lived store — Redis key `quote:{id}` TTL 10 min — holding the computed total and all inputs.
- `PlaceOrderCommand` must receive `quoteId`. Handler looks it up, rejects if missing/expired, and writes the stored total into `OrderPricing` rather than re-computing. This is what stops frontend math drift.
- Clock-skew tolerance: accept a quote up to 10 s past `expires_at`.

---

## 6. Build list — one PR per item

Ordered so each PR compiles and deploys independently. Where two items are truly parallelisable, a "parallel with" note is added.

### PR 1 — Commission flat fee migration

**Goal:** swap `CommissionPercentage` (decimal %) → `CommissionFlatFee` (decimal ₹) and update ledger math. No behaviour change for the customer; only change for Hivago finance.

**Files**
- `src/Modules/Users/RallyAPI.Users.Domain/Entities/Restaurant.cs` — rename property, update constructor default (propose `CommissionFlatFee = 30m`), rename `SetCommissionPercentage` → `SetCommissionFlatFee`.
- `src/Modules/Users/RallyAPI.Users.Infrastructure/Persistence/Configurations/RestaurantConfiguration.cs` — map new column `commission_flat_fee numeric(10,2)`.
- `src/Modules/Users/RallyAPI.Users.Application/Admins/Commands/EditRestaurant/EditRestaurantCommand*.cs` — replace `CommissionPercentage` field.
- `src/Modules/Users/RallyAPI.Users.Infrastructure/Services/RestaurantQueryService.cs` + `src/RallyAPI.SharedKernel/Abstractions/Restaurants/RestaurantDetails.cs` — propagate rename.
- `src/Modules/Orders/RallyAPI.Orders.Domain/Entities/PayoutLedger.cs` — `Create(ownerId, outletId, orderId, orderAmount, commissionFlatFee)`. New formula:
  ```csharp
  var gstOnFood         = Math.Round(orderAmount * 0.05m, 2);
  var commissionAmount  = commissionFlatFee;
  var commissionGst     = Math.Round(commissionAmount * 0.18m, 2);
  var tdsAmount         = Math.Round(orderAmount * 0.01m, 2);   // was (orderAmount - commission) × 1% — that was wrong
  var netAmount         = orderAmount - commissionAmount - commissionGst - tdsAmount;
  ```
  Column `commission_percentage` on `payout_ledger` becomes `commission_flat_fee` — rename the column and the property. Existing rows carry the legacy snapshot under the old name; rename is safe because migration can `RenameColumn`.
- `src/Modules/Orders/RallyAPI.Orders.Application/EventHandlers/OrderDeliveredPayoutLedgerHandler.cs` — pass `restaurant.CommissionFlatFee`.
- Migrations (two — one per module):
  ```powershell
  dotnet ef migrations add ChangeCommissionToFlatFee `
    --context UsersDbContext `
    --project src/Modules/Users/RallyAPI.Users.Infrastructure `
    --startup-project src/RallyAPI.Host

  dotnet ef migrations add RenameLedgerCommissionColumn `
    --context OrdersDbContext `
    --project src/Modules/Orders/RallyAPI.Orders.Infrastructure `
    --startup-project src/RallyAPI.Host
  ```
  The Users migration should `RenameColumn("commission_percentage" → "commission_flat_fee")` and seed existing rows with `30.00` (one-time UPDATE — acceptable because no real payout run has happened yet).
- Seed: update `src/Modules/Users/RallyAPI.Users.Endpoints/Dev/SeedUsers.cs` if it sets a commission.

**SharedKernel abstraction update**
- `IRestaurantQueryService.RestaurantDetails` — rename `CommissionPercentage` → `CommissionFlatFee`. Any caller outside Users must also update.

**Acceptance**
- [ ] `dotnet build` green with zero warnings.
- [ ] Unit test: `PayoutLedger.Create` with `orderAmount=500, commissionFlatFee=30` produces `netAmount = 500 - 30 - 5.40 - 5.00 = 459.60`.
- [ ] Startup against local Postgres migrates without error.
- [ ] Existing `GET /api/restaurants/payouts/earnings` still returns for a seeded restaurant.

---

### PR 2 — Platform fee config + wire into pricing

**Goal:** configurable platform fee, applied as `OrderPricing.ServiceFee` (field already exists, never populated).

**Files**
- New entity `src/Modules/Orders/RallyAPI.Orders.Domain/Entities/PlatformFeeConfig.cs`:
  ```csharp
  public sealed class PlatformFeeConfig : AggregateRoot
  {
      public decimal Amount { get; private set; }       // flat ₹
      public bool IsActive { get; private set; }
      public string? Notes { get; private set; }
      // history preserved by never deleting — always mark previous row IsActive=false before activating a new one.
  }
  ```
- Repository `IPlatformFeeConfigRepository` with `GetActiveAsync()`, `AddAsync()`, `DeactivateCurrentAsync()`.
- Configuration: `src/Modules/Orders/RallyAPI.Orders.Infrastructure/Configurations/PlatformFeeConfigConfiguration.cs`.
- Migration:
  ```powershell
  dotnet ef migrations add AddPlatformFeeConfig `
    --context OrdersDbContext `
    --project src/Modules/Orders/RallyAPI.Orders.Infrastructure `
    --startup-project src/RallyAPI.Host
  ```
  Seed a default row `Amount=10, IsActive=true` via migration SQL or startup seeder.
- Admin endpoints (new file `src/Modules/Orders/RallyAPI.Orders.Endpoints/PlatformFeeEndpoints.cs`):
  | Method | Path | Auth | Command |
  |---|---|---|---|
  | GET | `/api/admin/platform-fee` | Admin | `GetActivePlatformFeeQuery` |
  | POST | `/api/admin/platform-fee` | Admin | `UpdatePlatformFeeCommand` (deactivates current + activates new) |
  | GET | `/api/admin/platform-fee/history` | Admin | `GetPlatformFeeHistoryQuery` |
- Extend `OrderPricingService.CalculatePricingAsync` to accept or resolve the active platform fee and pass it through as `serviceFee` in `OrderPricing.Create(...)`.

**Acceptance**
- [ ] Creating a new config row deactivates the previous one (single active invariant enforced in `UpdatePlatformFeeCommandHandler`).
- [ ] A placed order has non-zero `service_fee` in DB.
- [ ] Unit test: handler rejects negative `Amount`.

---

### PR 3 — Rider earnings config + calculator service

Parallel with PR 2. Pure config + pure service — no entities beyond the config table.

**Files**
- New entity `src/Modules/Orders/RallyAPI.Orders.Domain/Entities/RiderEarningsConfig.cs` (placed in Orders module because it is consumed by `OrderDelivered…Handler`; keep away from Users module to avoid cross-module coupling):
  ```csharp
  public sealed class RiderEarningsConfig : AggregateRoot
  {
      public decimal BaseFee { get; private set; }             // ₹30
      public decimal BaseDistanceKm { get; private set; }      // 3
      public decimal PerKmAboveBase { get; private set; }      // ₹10
      public decimal WeatherBonus { get; private set; }        // ₹10
      public decimal SpecialDayBonus { get; private set; }     // ₹10
      public bool IsActive { get; private set; }
  }
  ```
  Same single-active-row rule as `PlatformFeeConfig`.
- `IRiderEarningsConfigRepository` + EF config + migration `AddRiderEarningsConfig` (Orders context). Seed initial row.
- New service `src/Modules/Orders/RallyAPI.Orders.Application/Services/RiderEarningsCalculator.cs`:
  ```csharp
  public sealed record RiderEarningsBreakdown(
      decimal BaseFee,
      decimal PerKmCharge,
      decimal WeatherBonus,
      decimal SpecialDayBonus,
      decimal Total,
      decimal DistanceKm);

  public interface IRiderEarningsCalculator
  {
      Task<RiderEarningsBreakdown> CalculateAsync(
          decimal distanceKm,
          DateTime orderDeliveredAtUtc,
          decimal pickupLat, decimal pickupLng,        // for weather lookup
          CancellationToken ct);
  }
  ```
  Implementation reuses **weather/special-day detection only** from `IPricingConfigRepository.GetWeatherSurgeAsync` and `GetSpecialDaySurgeAsync` — returning `true/false` on whether a surge row exists. The bonus **amounts** come from `RiderEarningsConfig`, never from Pricing rows. This is the "separate config, shared detection" line we agreed on (Decision 1).
- Admin endpoints:
  | Method | Path | Auth |
  |---|---|---|
  | GET | `/api/admin/rider-earnings-config` | Admin |
  | POST | `/api/admin/rider-earnings-config` | Admin |
  | GET | `/api/admin/rider-earnings-config/history` | Admin |

**Acceptance**
- [ ] Unit tests:
  - 2 km delivery, clear weather, non-festival → `BaseFee` only (₹30).
  - 5 km delivery → `BaseFee + (5-3) × PerKmAboveBase` = ₹50.
  - 2 km on Republic Day + rain → `BaseFee + WeatherBonus + SpecialDayBonus` = ₹50.
- [ ] Calculator does not call PricingEngine — only reads detection rows from `IPricingConfigRepository`.

---

### PR 4 — Rider payout ledger + handler + batch extension

**Files**
- New entity `src/Modules/Orders/RallyAPI.Orders.Domain/Entities/RiderPayoutLedger.cs` — mirror `PayoutLedger` but for riders:
  ```csharp
  public sealed class RiderPayoutLedger : BaseEntity
  {
      public Guid RiderId { get; private set; }
      public Guid OrderId { get; private set; }
      public decimal DistanceKm { get; private set; }
      public decimal BaseFee { get; private set; }
      public decimal PerKmCharge { get; private set; }
      public decimal WeatherBonus { get; private set; }
      public decimal SpecialDayBonus { get; private set; }
      public decimal TotalEarnings { get; private set; }
      public RiderPayoutLedgerStatus Status { get; private set; } // Pending | Batched | PaidOut
      public Guid? RiderPayoutId { get; private set; }
      public static RiderPayoutLedger Create(Guid riderId, Guid orderId, RiderEarningsBreakdown b) { … }
      public void AssignToPayout(Guid payoutId) { … }
      public void MarkAsPaidOut() { … }
  }
  ```
- New aggregate `src/Modules/Orders/RallyAPI.Orders.Domain/Entities/RiderPayout.cs` — mirrors `Payout`:
  ```csharp
  public sealed class RiderPayout : AggregateRoot
  {
      public Guid RiderId { get; private set; }
      public DateOnly PeriodStart { get; private set; }
      public DateOnly PeriodEnd { get; private set; }
      public int OrderCount { get; private set; }
      public decimal TotalEarnings { get; private set; }
      public PayoutStatus Status { get; private set; }
      public string? BankAccountNumber { get; private set; }
      public string? BankIfscCode { get; private set; }
      public string? UpiId { get; private set; }              // riders may have UPI instead of bank
      public string? TransactionReference { get; private set; }
      public DateTime? PaidAt { get; private set; }
      public string? Notes { get; private set; }
      // same state methods as Payout
  }
  ```
- Repositories + EF configs + migration `AddRiderPayoutTables` (Orders context). Indexes: `rider_payout_ledger (rider_id, status)`, `rider_payouts (rider_id, period_start, period_end)`, `rider_payouts (status)`.
- New event handler `src/Modules/Orders/RallyAPI.Orders.Application/EventHandlers/OrderDeliveredRiderPayoutHandler.cs`:
  - Subscribes to `OrderDeliveredEvent`.
  - **Gate:** skip if `order.FulfillmentType != Delivery` **or** `restaurant.DeliveryMode != Hivago` **or** `order.RiderId is null`.
  - Reads actual pickup→drop distance from `DeliveryInfo` if present; otherwise calls `IDistanceCalculator` (Google Maps Routes) — per Decision 5, no haversine fallback.
  - Calls `IRiderEarningsCalculator.CalculateAsync(...)`.
  - Creates `RiderPayoutLedger` row, idempotency check by `OrderId`.
- Extend `WeeklyPayoutBatchService` **or** add parallel `WeeklyRiderPayoutBatchService` (recommend parallel — smaller blast radius, same scheduling logic, different repositories). The parallel service runs the same `thisMondayIst.Date` / `lastMondayIst` window and creates `RiderPayout` rows grouped by `RiderId`.
- Admin endpoints (new file `RiderPayoutEndpoints.cs`):
  | Method | Path | Auth |
  |---|---|---|
  | GET | `/api/admin/rider-payouts/pending` | Admin |
  | PUT | `/api/admin/rider-payouts/{id}/process` | Admin |
  | GET | `/api/admin/rider-payouts/{id}` | Admin |
  | GET | `/api/riders/payouts` | Rider (uses `sub` claim) |
  | GET | `/api/riders/payouts/{id}` | Rider |

**Acceptance**
- [ ] Pickup order → no `rider_payout_ledger` row.
- [ ] 3PL (DeliveryMode != Hivago) order → no row.
- [ ] Hivago delivery order → exactly one row with correct breakdown.
- [ ] Re-firing `OrderDeliveredEvent` for the same order does not create a duplicate.

---

### PR 5 — Bank details + verify flag

**Goal:** collect payment destination from payees, admin verifies before first disbursement.

**Files — Restaurant Owner (primary payee for restaurant payouts)**
- `src/Modules/Users/RallyAPI.Users.Domain/Entities/RestaurantOwner.cs` (from yesterday's commit) — add value object `BankDetails`:
  ```csharp
  public sealed record BankDetails(
      string AccountHolderName,
      string AccountNumber,     // masked in logs
      string IfscCode,
      string? BankName,
      string? UpiId);           // optional — for fallback
  ```
  Plus:
  ```csharp
  public BankDetails? BankDetails { get; private set; }
  public bool IsBankVerified { get; private set; }
  public DateTime? BankVerifiedAt { get; private set; }
  public Guid? BankVerifiedByAdminId { get; private set; }
  ```
- Commands in `Users.Application/Owners/Commands/UpdateBankDetails/` (owner updates their own).
- Admin commands in `Users.Application/Admins/Commands/VerifyOwnerBank/`.

**Files — Rider**
- Same pattern on `Rider` entity. UPI-only riders: `AccountNumber` and `IfscCode` nullable if `UpiId` present — validator enforces "either bank or UPI must be provided".
- Owner commands in `Users.Application/Riders/Commands/UpdateBankDetails/` (rider updates via OTP-authenticated endpoint).
- Admin commands in `Users.Application/Admins/Commands/VerifyRiderBank/`.

**Endpoints**
| Method | Path | Auth |
|---|---|---|
| PUT | `/api/owners/bank-details` | Owner |
| GET | `/api/owners/bank-details` | Owner |
| PUT | `/api/riders/bank-details` | Rider |
| GET | `/api/riders/bank-details` | Rider |
| PUT | `/api/admin/owners/{id}/verify-bank` | Admin |
| PUT | `/api/admin/riders/{id}/verify-bank` | Admin |

**Migration** (Users context): `AddBankDetailsToOwnerAndRider`.

**Gate in `ProcessPayoutCommandHandler`**
- Load owner → if `!owner.IsBankVerified` return `Result.Failure("BankNotVerified")`.
- Same for rider payouts in `ProcessRiderPayoutCommandHandler`.

**Security**
- Log account numbers with last-4 only (`****1234`). Add a formatter in `SharedKernel.Utilities`.
- Never log the full IFSC alongside full account number.
- Encrypt at rest? — current DB is not encrypted column-level. **Open question** — note in rollout.

**Acceptance**
- [ ] Updating bank details resets `IsBankVerified = false` (change forces re-verify).
- [ ] `ProcessPayout` against unverified owner returns 400 with reason.
- [ ] Logs show only masked account number.

---

### PR 6 — `POST /api/orders/quote` endpoint

**Goal:** one source of truth for the bill. Frontend stops doing math.

**Files**
- New command `src/Modules/Orders/RallyAPI.Orders.Application/Queries/GetOrderQuote/GetOrderQuoteQuery.cs` (it's a query because it has no side effects beyond caching):
  - Input matches the request JSON in §5.
  - Handler:
    1. Resolve restaurant via `IRestaurantQueryService`. Reject if not accepting orders.
    2. Load menu items, compute `subtotal`.
    3. Compute `gst_on_food` = subtotal × 5%.
    4. If Delivery: call `IPricingEngine.CalculateDeliveryFeeAsync` (existing). Use returned breakdown verbatim. If Pickup: all delivery lines zero.
    5. Compute `gst_on_delivery` = delivery_fee × 18%.
    6. Load active `PlatformFeeConfig`, compute `gst_on_platform`.
    7. Compute `total`. Generate `quote_id = "quote_" + ulid`. Store in Redis `quote:{id}` with TTL 10 min, value = serialized computed quote.
    8. Return the §5 response.
- New endpoint file `src/Modules/Orders/RallyAPI.Orders.Endpoints/QuoteEndpoints.cs`:
  ```csharp
  app.MapPost("/api/orders/quote", HandleAsync)
     .RequireAuthorization("Customer");
  ```
- `PlaceOrderCommand` — add required `string QuoteId`. `PlaceOrderCommandHandler`:
  - Fetch quote from Redis. If missing or older than `expires_at + 10s`, reject with `Result.Failure("QuoteExpired")`.
  - Use the stored `OrderPricing` snapshot instead of re-computing. Delete the Redis key after use (single-use).
- `PlaceOrderCommandValidator` — `QuoteId` required, matches regex `^quote_[a-z0-9]+$`.

**Frontend contract update**
- Document that `POST /api/orders` now requires `quoteId` obtained from `POST /api/orders/quote` within the last 10 minutes.

**Acceptance**
- [ ] Quote request with valid cart + address returns a total that matches `subtotal + gst_on_food + delivery_fee + gst_on_delivery + platform_fee + gst_on_platform` exactly.
- [ ] Placing an order with a stale `quoteId` returns 400.
- [ ] Placing an order with a matched `quoteId` stores exactly the quoted `Total` on the order.
- [ ] Pickup quote zeroes `delivery_fee`, `gst_on_delivery`, and all breakdown lines.

---

### PR 7 — PayU outbound payout client

**Goal:** actually move money when admin clicks process.

**Files**
- New interface `src/Modules/Orders/RallyAPI.Orders.Application/Abstractions/IPayUPayoutClient.cs`:
  ```csharp
  public record PayoutDisbursementRequest(
      Guid PayoutId,             // used as merchantRefId for idempotency
      decimal Amount,
      string Currency,
      string BeneficiaryName,
      string? AccountNumber,
      string? IfscCode,
      string? UpiId,
      string Purpose);

  public record PayoutDisbursementResult(
      bool Success,
      string? TransactionReference,
      string? ProviderStatus,
      string? ErrorCode,
      string? ErrorMessage);

  public interface IPayUPayoutClient
  {
      Task<PayoutDisbursementResult> DisburseAsync(
          PayoutDisbursementRequest request,
          CancellationToken ct);
  }
  ```
- Implementation `src/Modules/Orders/RallyAPI.Orders.Infrastructure/Services/PayU/PayUPayoutClient.cs` — HTTP client against PayU Payouts API. Env vars:
  - `PAYU_PAYOUTS_CLIENT_ID`
  - `PAYU_PAYOUTS_CLIENT_SECRET`
  - `PAYU_PAYOUTS_BASE_URL`
  - `PAYU_PAYOUTS_ACCOUNT_ID`
  Wire OAuth token cache (in-memory) with 55-minute refresh.
- `ProcessPayoutCommandHandler` — after admin approval:
  1. Load payout + owner bank details.
  2. Call `DisburseAsync`.
  3. On success: `payout.MarkProcessing()` → `payout.MarkPaid(result.TransactionReference)`.
  4. On failure: `payout.MarkProcessing()` → `payout.MarkFailed(result.ErrorMessage)`. Leave ledger entries on this payout so admin can retry (add `RetryPayoutCommand`).
- Mirror handler for `ProcessRiderPayoutCommand`.
- Telemetry: log with `correlationId = payoutId` and mask amounts aren't needed but mask account numbers.

**Idempotency**
- Use `PayoutId` as the merchant reference sent to PayU. PayU rejects duplicate refs — turns retries into safe operations.

**Acceptance**
- [ ] Integration test with stubbed `HttpMessageHandler` — verifies auth header, request body fields, idempotency ref.
- [ ] Failure path: disbursement returns `Success=false` → payout row ends in `Failed` with error in `notes`, ledger stays batched (not paid out).
- [ ] Retry command re-uses the same `PayoutId` reference.

---

### PR 8 — Batch schedule tweak

**File**
- `src/Modules/Orders/RallyAPI.Orders.Infrastructure/BackgroundServices/WeeklyPayoutBatchService.cs` — change the trigger:
  ```csharp
  // was: istNow.DayOfWeek == DayOfWeek.Monday && istNow.Hour == 6
  if (istNow.DayOfWeek == DayOfWeek.Sunday
      && istNow.Hour == 23
      && istNow.Minute >= 30)
  {
      await CreateWeeklyPayoutBatchesAsync(stoppingToken);
  }
  ```
  Hour-granular check was fine for a Monday 6 AM window; for 23:30 we need a minute check. To avoid missed runs when the hourly timer skips past 23:30, tighten `CheckInterval` to `TimeSpan.FromMinutes(15)`.
- Update period math: previous week window becomes Sunday → Saturday IST, closed on the Sunday batch run.
- Same change to `WeeklyRiderPayoutBatchService` (added in PR 4).

**Acceptance**
- [ ] Unit test a time-travel wrapper that verifies batch runs at 23:35 Sunday, not at 22:00 Sunday or 00:15 Monday.
- [ ] A single run does not double-create payouts if the service restarts at 23:40 (idempotency already handled via `GetCurrentPeriodPayoutAsync` check).

---

### PR 9 — Admin Payouts UI (Page 8)

Frontend-only PR in the admin panel repo. Backend endpoints from PR 1–8 are all it needs.

**Components**
- **Pending payouts list** — two tabs: Restaurants / Riders. Calls `GET /api/admin/payouts/pending` and `GET /api/admin/rider-payouts/pending`.
- **Payout detail drawer** — order-level breakdown (`GET /api/admin/payouts/{id}`).
- **Process button** — calls `PUT /api/admin/payouts/{id}/process`. Disabled if payee unverified (from 4th-column flag in list response — add `IsBankVerified` to DTO in PR 5).
- **Bank verify inbox** — list owners/riders awaiting bank verification (new query + endpoint noted; can slip to a later PR if scope-tight).
- **Config editors** — simple forms for `PlatformFeeConfig` and `RiderEarningsConfig` with history view.

**Acceptance**
- [ ] Admin can filter pending by period / by owner / by status.
- [ ] One-click approve triggers real PayU call in staging (with sandbox credentials).

---

## 7. Dependency graph

```
PR1 ───┬─> PR2  (both touch OrderPricing, PR1 first to de-risk migration)
       ├─> PR3  (independent config)
       ├─> PR4  (needs PR3's calculator)
       ├─> PR5  (independent)
       ├─> PR6  (needs PR2's platform fee; can read PR1 flat fee but doesn't strictly need it)
       └─> PR7  (needs PR5's bank details to have anything to send)
PR8 — independent, can ship any time after phase 1
PR9 — after PR1-7; PR8 just needs schedule note
```

Parallelisable slices:
- PR3 // PR5 // PR8
- PR4 after PR3
- PR6 after PR2
- PR7 after PR5

## 8. Edge cases

| Scenario | Expected behavior |
|---|---|
| Order delivered before `RiderEarningsConfig` seeded | Handler logs error, skips ledger row. Surfaced via admin "orders missing rider payout" query (add later). |
| Restaurant commission flat fee changed mid-week | Already-delivered orders are unaffected — `PayoutLedger.CommissionFlatFee` is a snapshot. Future orders use new value. Same as phase-1 behaviour with percentage. |
| Rider delivers an order, then is deleted | `RiderPayoutLedger.RiderId` is a Guid — deletion doesn't cascade. Batch still aggregates. Disbursement fails if bank details are gone; mark `Failed` with notes. |
| Quote used for an order where menu price changed after quote generation | Quote stored the full `OrderPricing`. Order uses stored snapshot, not current menu price. This is intentional — a price hike mid-quote would be a bad UX. |
| PayU Payouts API down | `DisburseAsync` returns `Success=false` with `ErrorCode="PROVIDER_UNAVAILABLE"`. Payout → `Failed`. Admin retries later; idempotency via `PayoutId` means a duplicate is impossible. |
| Two payouts for same owner in same period | Blocked by existing `GetCurrentPeriodPayoutAsync` idempotency check in batch service. |
| Admin processes same payout twice | `Payout.MarkProcessing()` guards against non-Pending states, so second click 409s. |

## 9. Testing plan

- **Domain unit tests**: `PayoutLedger.Create` with new formula; `RiderPayoutLedger.Create`; `RiderEarningsCalculator` permutations; config single-active invariant.
- **Handler unit tests**: `OrderDeliveredRiderPayoutHandler` gating (pickup skip, 3PL skip, Hivago happy path); `ProcessPayoutCommandHandler` unverified-bank block; `GetOrderQuoteQueryHandler` pickup vs delivery shape.
- **Integration tests**:
  - `POST /api/orders/quote` → `POST /api/orders` roundtrip; stale quote rejection.
  - Weekly batch against an in-memory clock yields correct restaurant + rider payouts.
  - PayU client with mocked HTTP handler.
- **Manual staging**: one end-to-end weekly cycle with sandbox PayU, a seeded verified owner, a seeded verified rider.

## 10. Rollout

- No feature flag needed — new endpoints are additive; the commission rename is a hard swap.
- **Pre-deploy**: take a DB snapshot. Migration is reversible but verify on a staging copy first.
- **Post-deploy checks**:
  - Hit `GET /api/admin/platform-fee` returns seeded default.
  - Hit `GET /api/admin/rider-earnings-config` returns seeded default.
  - Create a test order end-to-end, confirm `service_fee` populated on `orders` row.
  - On next Sunday 23:30 IST, verify `payouts` and `rider_payouts` rows appear.
- **Metrics to track**:
  - `payouts.batch.duration` (histogram).
  - `payouts.disbursement.success_count` / `failure_count`.
  - Count of orders with `rider_payout_ledger.status = Pending` older than 24h (alert threshold).
- **Rollback**:
  - PR1 migration is reversible. Reverting mid-cycle is awkward if a payout has already snapshotted the flat fee — keep `CommissionPercentage` column NULL-able for one migration cycle as a safety net. (Actually, cleaner: don't drop the old column in PR1. Rename and keep both for one release. Drop in a follow-up migration.)
  - PRs 2–7 are additive; rollback = revert endpoints.

---

## Open questions

1. **Commission flat-fee rollback safety** — keep the `commission_percentage` column for one release before dropping? Recommend yes.
2. **Encryption at rest for bank details** — current DB has no column-level encryption. Decide before PR5 ships to production. Short-term: restrict DB access + audit log reads.
3. **Rider payout to UPI vs bank** — PayU Payouts supports both but routing logic (prefer UPI if present?) needs a call.
4. **GST invoicing** — this spec captures GST collection/remittance amounts but does not cover customer-facing GST invoices or supplier invoices from Hivago to restaurants. Separate track.

## Implementation notes (filled during build)

### Files Created/Modified
- _to fill_

### Decisions made during build
- _to fill_

### Deferred
- _to fill_
