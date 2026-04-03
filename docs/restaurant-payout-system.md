# Restaurant Owner & Payout System — Technical Documentation

> Last updated: 2026-03-31
> Branch: `feat/auto-accept-orders`
> Commits: `11546b0` (Phase 1), `3977468` (Phase 2)

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Domain Entities](#domain-entities)
4. [Financial Calculations](#financial-calculations)
5. [API Endpoints](#api-endpoints)
6. [Data Flow](#data-flow)
7. [Background Services](#background-services)
8. [Database Schema](#database-schema)
9. [Cross-Module Communication](#cross-module-communication)

---

## Overview

The payout system handles all financial settlements between Rally and restaurant partners. When a customer places an order and it gets delivered, the system automatically records a ledger entry with the full tax breakdown (GST, TDS, commission). At the end of each week, a background service batches these entries into a payout that an admin can then process (transfer to the restaurant owner's bank account).

### Key Concepts

- **RestaurantOwner** — A person/entity that owns one or more restaurant outlets. Holds PAN, GST, and bank details.
- **Restaurant (Outlet)** — A single restaurant location. Linked to an owner via `OwnerId`. Each outlet can receive orders independently.
- **PayoutLedger** — One row per delivered order. The atomic unit of financial tracking.
- **Payout** — A weekly batch that aggregates multiple ledger entries into a single bank transfer.

---

## Architecture

The system spans two modules, following Rally's modular monolith pattern:

```
Users Module (owns RestaurantOwner + Restaurant entities)
  |
  |-- Domain: RestaurantOwner entity, Restaurant.OwnerId link
  |-- Application: AddOutlet command, IRestaurantOwnerRepository
  |-- Infrastructure: EF configs, migrations, RestaurantOwnerRepository
  |-- Endpoints: POST /api/restaurants/outlets
  
Orders Module (owns Payout + PayoutLedger entities)
  |
  |-- Domain: Payout, PayoutLedger entities, enums
  |-- Application: Queries (earnings, payouts, GST/TDS summaries, detail), ProcessPayout command
  |-- Infrastructure: EF configs, migrations, repositories, WeeklyPayoutBatchService
  |-- Endpoints: All /api/restaurants/payouts/* and /api/admin/payouts/* routes
```

Cross-module communication uses `SharedKernel.Abstractions.Restaurants.IRestaurantQueryService` — the Orders endpoints resolve the restaurant's `OwnerId` through this interface without importing Users module types.

---

## Domain Entities

### RestaurantOwner (`Users.Domain`)

```
RestaurantOwner : AggregateRoot
├── Name, Email, Phone, PasswordHash
├── PanNumber (10 chars, for TDS/IT filings)
├── GstNumber (15 chars, GSTIN)
├── BankAccountNumber, BankIfscCode, BankAccountName
├── IsActive
└── Methods: Create(), UpdateProfile(), SetPanNumber(), SetGstNumber(), UpdateBankDetails()
```

An owner can have multiple outlets. The `Restaurant` entity has a nullable `OwnerId` FK that links it back.

### Restaurant — Owner Link (`Users.Domain`)

The existing `Restaurant` entity gained:
- `Guid? OwnerId` — nullable to maintain backward compatibility with restaurants created before the owner system
- `SetOwner(Guid ownerId)` — domain method to link a restaurant to an owner

### PayoutLedger (`Orders.Domain`)

One row per delivered order. Created by the `OrderDeliveredPayoutLedgerHandler` event handler.

```
PayoutLedger : BaseEntity
├── OwnerId        — which owner this earning belongs to
├── OutletId       — which specific restaurant outlet
├── OrderId        — the delivered order
├── OrderAmount    — gross order value (what customer paid for food)
├── GstAmount      — 5% GST on food (Section 9(5) CGST Act)
├── CommissionPercentage — Rally's commission rate (e.g., 20%)
├── CommissionAmount     — OrderAmount * CommissionPercentage / 100
├── CommissionGst        — 18% GST on commission (service tax)
├── TdsAmount            — 1% TDS on (OrderAmount - Commission) per Section 194-O
├── NetAmount            — what the restaurant actually receives
├── Currency             — always "INR"
├── Status               — Pending → Batched → PaidOut
└── PayoutId             — FK to the Payout batch (set when batched)
```

### Payout (`Orders.Domain`)

Weekly aggregation of ledger entries for a single owner.

```
Payout : AggregateRoot
├── OwnerId
├── PeriodStart, PeriodEnd    — the Monday-Sunday week
├── OrderCount                — number of orders in this batch
├── GrossOrderAmount          — sum of all OrderAmounts
├── TotalGstCollected         — sum of all GstAmounts
├── TotalCommission           — sum of all CommissionAmounts
├── TotalCommissionGst        — sum of all CommissionGst
├── TotalTds                  — sum of all TdsAmounts
├── NetPayoutAmount           — what gets transferred to owner's bank
├── Status                    — Pending → Processing → Paid / Failed
├── BankAccountNumber, BankIfscCode — snapshot of owner's bank at time of batch
├── TransactionReference      — UTR/NEFT/IMPS ref after payment
├── PaidAt                    — timestamp of successful transfer
└── Notes                     — admin notes
```

---

## Financial Calculations

All calculations happen in `PayoutLedger.Create()` when an order is delivered:

```
Given: OrderAmount = 1000, CommissionPercentage = 20%

GST on Food (5%):
  GstAmount = 1000 * 0.05 = 50.00
  (Collected by Rally under Section 9(5) CGST Act — aggregator collects & remits)

Rally Commission:
  CommissionAmount = 1000 * 20 / 100 = 200.00

GST on Commission (18%):
  CommissionGst = 200 * 0.18 = 36.00
  (This is the service tax on Rally's commission — charged to restaurant)

Net before TDS:
  NetBeforeTds = 1000 - 200 = 800.00

TDS (1% under Section 194-O):
  TdsAmount = 800 * 0.01 = 8.00
  (E-commerce operator must deduct 1% TDS on net amount payable)

Net Payout:
  NetAmount = 800 - 8 = 792.00
  (This is what hits the restaurant owner's bank account)
```

### Tax Compliance Notes

| Tax | Rate | Section | Who Pays | Who Collects/Remits |
|-----|------|---------|----------|---------------------|
| GST on food | 5% | Section 9(5) CGST | Customer (included in price) | Rally (as aggregator) |
| GST on commission | 18% | Standard GST | Restaurant (deducted from payout) | Rally |
| TDS | 1% | Section 194-O IT Act | Restaurant (deducted from payout) | Rally (as e-commerce operator) |

---

## API Endpoints

### Restaurant Owner Endpoints (Auth: `Restaurant` policy)

All these resolve the `OwnerId` from the logged-in restaurant's `sub` claim via `IRestaurantQueryService`.

#### 1. GET `/api/restaurants/payouts/earnings`
**Purpose:** Current week's live earnings dashboard.

Returns all **pending** (not yet batched) ledger entries for the owner, with running totals.

```json
// Response: EarningsSummaryDto
{
  "orderCount": 47,
  "grossRevenue": 52300.00,
  "totalCommission": 10460.00,
  "totalTds": 418.40,
  "netEarnings": 41421.60,
  "periodStart": "2026-03-30",
  "periodEnd": "2026-04-05",
  "ledgerEntries": [
    {
      "id": "...",
      "outletId": "...",
      "orderId": "...",
      "orderAmount": 1200.00,
      "gstAmount": 60.00,
      "commissionPercentage": 20.0,
      "commissionAmount": 240.00,
      "commissionGst": 43.20,
      "tdsAmount": 9.60,
      "netAmount": 950.40,
      "status": "Pending",
      "payoutId": null,
      "createdAt": "2026-03-31T08:22:00Z"
    }
  ]
}
```

#### 2. GET `/api/restaurants/payouts?page=1&pageSize=20`
**Purpose:** Payout history — list of all weekly batches.

Returns paginated list of past payouts (most recent first).

```json
// Response: PayoutDto[]
[
  {
    "id": "...",
    "ownerId": "...",
    "periodStart": "2026-03-23",
    "periodEnd": "2026-03-29",
    "orderCount": 132,
    "grossOrderAmount": 145000.00,
    "totalGstCollected": 7250.00,
    "totalCommission": 29000.00,
    "totalCommissionGst": 5220.00,
    "totalTds": 1160.00,
    "netPayoutAmount": 114840.00,
    "status": "Paid",
    "transactionReference": "UTR123456789",
    "paidAt": "2026-03-30T06:00:00Z",
    "notes": null,
    "createdAt": "2026-03-30T00:30:00Z"
  }
]
```

#### 3. GET `/api/restaurants/payouts/{payoutId}`
**Purpose:** Drill into a specific payout to see every order that was included.

Returns the payout summary + all its ledger entries. Validates the payout belongs to the requesting owner.

```json
// Response: PayoutDetailDto
{
  "id": "...",
  "ownerId": "...",
  "periodStart": "2026-03-23",
  "periodEnd": "2026-03-29",
  "orderCount": 132,
  "grossOrderAmount": 145000.00,
  // ... all summary fields ...
  "status": "Paid",
  "transactionReference": "UTR123456789",
  "ledgerEntries": [
    {
      "id": "...",
      "outletId": "...",
      "orderId": "...",
      "orderAmount": 850.00,
      "gstAmount": 42.50,
      "commissionAmount": 170.00,
      "commissionGst": 30.60,
      "tdsAmount": 6.80,
      "netAmount": 673.20,
      "status": "PaidOut",
      "payoutId": "..."
    }
  ]
}
```

#### 4. GET `/api/restaurants/payouts/gst-summary?from=2026-03-01&to=2026-03-31`
**Purpose:** GST report for accounting/filing. Defaults to current month if no dates provided.

Shows total GST collected on orders (5%) and GST on commission (18%), with per-order breakdown.

```json
// Response: GstSummaryDto
{
  "fromDate": "2026-03-01",
  "toDate": "2026-03-31",
  "orderCount": 580,
  "grossOrderAmount": 625000.00,
  "totalGstOnOrders": 31250.00,
  "totalCommission": 125000.00,
  "totalCommissionGst": 22500.00,
  "lineItems": [
    {
      "orderId": "...",
      "outletId": "...",
      "orderAmount": 1200.00,
      "gstAmount": 60.00,
      "commissionAmount": 240.00,
      "commissionGst": 43.20,
      "createdAt": "2026-03-01T10:15:00Z"
    }
  ]
}
```

#### 5. GET `/api/restaurants/payouts/tds-summary?from=2026-03-01&to=2026-03-31`
**Purpose:** TDS report for income tax filing (Form 26AS reconciliation). Defaults to current month.

Shows TDS deducted per order under Section 194-O.

```json
// Response: TdsSummaryDto
{
  "fromDate": "2026-03-01",
  "toDate": "2026-03-31",
  "orderCount": 580,
  "grossOrderAmount": 625000.00,
  "totalCommission": 125000.00,
  "totalTdsDeducted": 5000.00,
  "netAfterTds": 495000.00,
  "lineItems": [
    {
      "orderId": "...",
      "outletId": "...",
      "orderAmount": 1200.00,
      "commissionAmount": 240.00,
      "tdsAmount": 9.60,
      "netAmount": 950.40,
      "createdAt": "2026-03-01T10:15:00Z"
    }
  ]
}
```

#### 6. POST `/api/restaurants/outlets`
**Purpose:** Restaurant owner adds a new outlet location.

Creates a new `Restaurant` entity linked to the owner. The new outlet gets its own login credentials and can receive orders independently.

```json
// Request
{
  "name": "Biryani House - Indiranagar",
  "phone": "+919876543210",
  "email": "indiranagar@biryanihouse.com",
  "password": "SecurePass123!",
  "addressLine": "100 Feet Road, Indiranagar, Bangalore",
  "latitude": 12.9716,
  "longitude": 77.6412,
  "fssaiNumber": "12345678901234"  // optional
}

// Response: 201 Created
{
  "id": "new-restaurant-guid"
}
```

**Validation:**
- Owner must exist and be active
- Email must not already be registered
- Coordinates must be within India bounds (lat 6-38, lng 68-98)
- FSSAI number 14-20 characters if provided
- Password minimum 8 characters

### Admin Endpoints (Auth: `Admin` policy)

#### 7. GET `/api/admin/payouts/pending?page=1&pageSize=50`
**Purpose:** Admin dashboard — see all payouts awaiting bank transfer.

#### 8. PUT `/api/admin/payouts/{payoutId}/process`
**Purpose:** Mark a payout as processed after completing bank transfer.

```json
// Request
{
  "transactionReference": "UTR123456789",
  "notes": "Processed via NEFT batch"
}
```

---

## Data Flow

### Order Delivery → Ledger Entry

```
1. Rider marks order as delivered
   └─> Order.MarkDelivered() raises OrderDeliveredDomainEvent

2. OrderDeliveredPayoutLedgerHandler catches the event
   ├─> Looks up restaurant via IRestaurantQueryService
   ├─> Gets OwnerId and CommissionPercentage from restaurant
   ├─> Calls PayoutLedger.Create(ownerId, outletId, orderId, orderAmount, commission%)
   │   └─> Calculates GST (5%), Commission, Commission GST (18%), TDS (1%), Net
   └─> Saves ledger entry via IPayoutLedgerRepository

3. Ledger entry sits in "Pending" status until weekly batch runs
```

### Weekly Batch → Payout

```
1. WeeklyPayoutBatchService fires every Monday at 00:30 IST
   ├─> Calculates previous week's date range (Monday 00:00 → Sunday 23:59 IST → UTC)
   ├─> Gets all owner IDs with pending ledger entries in that range
   └─> For each owner:
       ├─> Fetches all pending ledger entries
       ├─> Looks up owner's bank details via IRestaurantQueryService
       ├─> Creates Payout.CreateFromLedger(...) — aggregates all amounts
       ├─> Marks each ledger entry as "Batched" with the payout ID
       └─> Saves payout + updated ledger entries

2. Payout now shows in admin's pending queue

3. Admin reviews and triggers bank transfer manually
   └─> PUT /api/admin/payouts/{id}/process with UTR reference
       ├─> Payout status: Pending → Processing → Paid
       └─> PaidAt timestamp recorded
```

### GST/TDS Summary Query

```
1. Restaurant owner requests summary for a date range
2. Handler converts IST dates to UTC (since all timestamps stored in UTC)
   ├─> fromUtc = IST midnight of fromDate → UTC
   └─> toUtc = IST midnight of (toDate + 1 day) → UTC (exclusive upper bound)
3. Queries PayoutLedger table for owner's entries in that UTC range
4. Aggregates and returns summary + per-order line items
```

---

## Background Services

### WeeklyPayoutBatchService

- **Schedule:** Runs every Monday at 00:30 IST
- **Location:** `Orders.Infrastructure/BackgroundServices/`
- **What it does:**
  1. Calculates the previous Monday-Sunday window
  2. Finds all owners with pending ledger entries in that window
  3. For each owner, creates a `Payout` aggregate from their ledger entries
  4. Links ledger entries to the payout (status: Pending → Batched)
  5. Snapshots the owner's bank details onto the payout record
- **Failure handling:** Each owner is processed independently — one failure doesn't block others
- **Idempotency:** Checks for existing payout for the same owner+period before creating

---

## Database Schema

### Users Schema (`users.*`)

```sql
-- New table
CREATE TABLE users."RestaurantOwners" (
    "Id"                UUID PRIMARY KEY,
    "Name"              VARCHAR(255) NOT NULL,
    "Email"             VARCHAR(255) NOT NULL UNIQUE,
    "PasswordHash"      TEXT NOT NULL,
    "Phone"             VARCHAR(20) NOT NULL,
    "PanNumber"         VARCHAR(10),
    "GstNumber"         VARCHAR(15),
    "BankAccountNumber" VARCHAR(50),
    "BankIfscCode"      VARCHAR(11),
    "BankAccountName"   VARCHAR(255),
    "IsActive"          BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt"         TIMESTAMPTZ NOT NULL,
    "UpdatedAt"         TIMESTAMPTZ NOT NULL
);

-- Modified: added FK to RestaurantOwners
ALTER TABLE users."Restaurants"
    ADD COLUMN "OwnerId" UUID REFERENCES users."RestaurantOwners"("Id"),
    ADD COLUMN "FssaiNumber" VARCHAR(20);
```

### Orders Schema (`orders.*`)

```sql
CREATE TABLE orders."PayoutLedgers" (
    "Id"                    UUID PRIMARY KEY,
    "OwnerId"               UUID NOT NULL,
    "OutletId"              UUID NOT NULL,
    "OrderId"               UUID NOT NULL UNIQUE,
    "OrderAmount"           DECIMAL(18,2) NOT NULL,
    "GstAmount"             DECIMAL(18,2) NOT NULL,
    "CommissionPercentage"  DECIMAL(5,2) NOT NULL,
    "CommissionAmount"      DECIMAL(18,2) NOT NULL,
    "CommissionGst"         DECIMAL(18,2) NOT NULL,
    "TdsAmount"             DECIMAL(18,2) NOT NULL,
    "NetAmount"             DECIMAL(18,2) NOT NULL,
    "Currency"              VARCHAR(3) NOT NULL DEFAULT 'INR',
    "Status"                INTEGER NOT NULL,
    "PayoutId"              UUID REFERENCES orders."Payouts"("Id"),
    "CreatedAt"             TIMESTAMPTZ NOT NULL,
    "UpdatedAt"             TIMESTAMPTZ NOT NULL
);

CREATE TABLE orders."Payouts" (
    "Id"                    UUID PRIMARY KEY,
    "OwnerId"               UUID NOT NULL,
    "PeriodStart"           DATE NOT NULL,
    "PeriodEnd"             DATE NOT NULL,
    "OrderCount"            INTEGER NOT NULL,
    "GrossOrderAmount"      DECIMAL(18,2) NOT NULL,
    "TotalGstCollected"     DECIMAL(18,2) NOT NULL,
    "TotalCommission"       DECIMAL(18,2) NOT NULL,
    "TotalCommissionGst"    DECIMAL(18,2) NOT NULL,
    "TotalTds"              DECIMAL(18,2) NOT NULL,
    "NetPayoutAmount"       DECIMAL(18,2) NOT NULL,
    "Status"                INTEGER NOT NULL,
    "BankAccountNumber"     VARCHAR(50),
    "BankIfscCode"          VARCHAR(11),
    "TransactionReference"  VARCHAR(100),
    "PaidAt"                TIMESTAMPTZ,
    "Notes"                 TEXT,
    "CreatedAt"             TIMESTAMPTZ NOT NULL,
    "UpdatedAt"             TIMESTAMPTZ NOT NULL
);

-- Indexes
CREATE INDEX "IX_PayoutLedgers_OwnerId" ON orders."PayoutLedgers"("OwnerId");
CREATE INDEX "IX_PayoutLedgers_Status" ON orders."PayoutLedgers"("Status");
CREATE INDEX "IX_PayoutLedgers_PayoutId" ON orders."PayoutLedgers"("PayoutId");
CREATE INDEX "IX_Payouts_OwnerId" ON orders."Payouts"("OwnerId");
CREATE INDEX "IX_Payouts_Status" ON orders."Payouts"("Status");
```

---

## Cross-Module Communication

The payout system strictly respects module boundaries:

| Need | How It's Solved |
|------|----------------|
| Orders module needs restaurant's OwnerId and CommissionPercentage | `IRestaurantQueryService.GetByIdAsync()` returns `RestaurantDetails` (in SharedKernel) |
| Orders module needs owner's bank details for payout | `RestaurantDetails.OwnerId` used to query owner via SharedKernel abstraction |
| Payout endpoints need to resolve logged-in restaurant's owner | Endpoint resolves via `IRestaurantQueryService` at request time |
| Add Outlet endpoint creates a Restaurant in Users module | Stays entirely within Users module — no cross-module call needed |

No module directly references another module's `Domain` or `Application` projects. All communication goes through `SharedKernel.Abstractions`.
