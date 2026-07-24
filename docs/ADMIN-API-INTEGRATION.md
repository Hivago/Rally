# Hivago Admin Panel — API Integration Guide

> Last updated: 2026-04-29
> Branch: `feat/pickup-orders` @ `77b9fc0`
> For: React admin panel developer
>
> This doc covers every admin-panel endpoint that's currently live on the branch.
> Page 4B (rider payouts) is in progress and not yet shipped — this doc will
> be amended when it lands.

---

## Table of contents

1. [Authentication](#authentication)
2. [Conventions](#conventions)
3. [Error responses](#error-responses)
4. [Page 6 — Restaurant Management](#page-6--restaurant-management)
5. [Page 1 — Live Operations Dashboard](#page-1--live-operations-dashboard)
6. [Page 3 — Orders Management & Escalation Modal](#page-3--orders-management--escalation-modal)
7. [Page 4A — Restaurant Payouts](#page-4a--restaurant-payouts)
8. [Appendix — legacy endpoints to be aware of](#appendix--legacy-endpoints)

---

## Authentication

All admin endpoints require a JWT access token. Login flow:

```
POST /api/admins/login
Content-Type: application/json

{ "email": "admin@rally.com", "password": "..." }
```

**Response 200:**
```json
{
  "adminId": "790c2762-73b1-40b4-bcba-3a7a87c1e702",
  "name": "Rally Admin",
  "role": "SuperAdmin",
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "...",
  "accessTokenExpiresAt": "2026-04-29T10:15:00Z"
}
```

Attach the access token to every admin-panel request:

```
Authorization: Bearer <accessToken>
```

When the access token is close to expiring, exchange the refresh token via the
existing `POST /api/auth/refresh` endpoint. (Refresh flow is unchanged from the
current customer/restaurant flow — refer to the existing auth doc.)

### Authorization rules

- All endpoints under `/api/admin/*` require role `Admin` (any of `Support`,
  `Operations`, `SuperAdmin`).
- `403 Forbidden` is returned when the token is valid but role is wrong (e.g.
  customer token).
- `401 Unauthorized` is returned when no token, expired token, or invalid token.

---

## Conventions

### URL prefix

| Prefix | Use |
|---|---|
| `/api/admin/...` (singular) | All endpoints in this guide. New admin panel work goes here. |
| `/api/admins/...` (plural) | Legacy endpoints (login, original create-restaurant, list-riders, etc.). Stays as-is — not consumed by the new admin panel except for `login`. |

### Pagination

List endpoints use a consistent shape:

```json
{
  "items": [ ... ],
  "totalCount": 156,
  "page": 1,
  "pageSize": 20
}
```

Some list endpoints also include extra metadata (e.g. tab counts on
`/api/admin/orders`). Defaults are usually `page=1`, `pageSize=20`. Maximum
`pageSize` is 100.

### Dates and times

- All timestamps are ISO 8601 UTC: `"2026-04-29T09:18:21.536940Z"`.
- Date-only fields (e.g. payout cycle dates) are `YYYY-MM-DD`.
- Pass UTC datetimes in query params: `?from=2026-04-21T00:00:00Z&to=2026-04-28T00:00:00Z`.

### Money

All amounts are decimal numbers in INR. No string formatting, no symbols. Format
on the client side. Example: `"netPayable": 70793.10`.

---

## Error responses

Every error returns the same shape. Status code carries the category, body
gives the detail:

```json
{
  "error": "Validation.Error",
  "message": "Owner ID is required. Pick an existing owner or create one first.",
  "details": [
    { "field": "OwnerId", "message": "Owner ID is required." }
  ]
}
```

| Status | When | Example `error` codes |
|---|---|---|
| 400 | Validation / bad request | `Validation.Error`, `Order.RejectionReasonRequired` |
| 401 | No token / invalid token | `(handled by middleware)` |
| 403 | Wrong role | `(handled by middleware)` |
| 404 | Resource not found | `Order.NotFound`, `Admin.NotFound`, `Payout.NotFound` |
| 409 | Conflict (state guard hit) | `Conflict.Error`, `Order.CannotCancel`, `Payment.NotRefundable` |
| 429 | Rate limit (CSV export only today) | `(no body)` |
| 500 | Unexpected — surface the `traceId` for backend debugging | `InternalError` |

### "Force" flags on cancel and refund

Two endpoints accept a `forceCancel` / `forceRefund` flag that bypasses the
normal status guard. Default is `true` on the admin endpoints (admin almost
always wants to bypass) — pass `false` if you want the normal customer-style
guard to apply. Detail in the per-endpoint sections.

---

## Page 6 — Restaurant Management

The restaurant table on the admin panel is powered by these endpoints. Owner
support is built in: every new restaurant must be linked to an existing
`RestaurantOwner` row.

### `GET /api/admin/restaurants` — list restaurants

**Query params (all optional):**

| Param | Type | Default | Notes |
|---|---|---|---|
| `isActive` | bool | (all) | `true` for active only, `false` for inactive only |
| `search` | string | — | Substring match on restaurant `name` (case-insensitive) |
| `page` | int | 1 | |
| `pageSize` | int | 20 | Max 100 |

**Response 200:**
```json
{
  "restaurants": [
    {
      "id": "26a0a40c-3f7c-40d7-90e7-8df60d15053c",
      "rstCode": "RST002",
      "name": "Smoke Test Outlet",
      "phone": "9876543210",
      "email": "owner@example.com",
      "isActive": true,
      "isAcceptingOrders": false,
      "commissionPercentage": 20.00,
      "commissionFlatFee": 30.00,
      "ownerId": "6c4d6607-79cd-41b2-bd0f-68a36f0324ab",
      "totalOrderCount": 0,
      "operatingHoursSummary": "09:00 – 22:00",
      "createdAt": "2026-04-28T09:27:42.292481Z"
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 20
}
```

`operatingHoursSummary` is `"HH:mm – HH:mm"` for fixed hours, or
`"Custom weekly schedule"` if the restaurant uses the per-day schedule slots.

### `GET /api/admin/restaurants/{id}` — detail

Full restaurant detail including the linked owner block, schedule, notification
preferences, FSSAI number, and dietary attributes.

**Response 200 (truncated):**
```json
{
  "id": "26a0a40c-...",
  "rstCode": "RST002",
  "name": "Smoke Test Outlet",
  "phone": "9876543210",
  "email": "owner@example.com",
  "addressLine": "123 Test St",
  "latitude": 12.93,
  "longitude": 77.62,
  "isActive": true,
  "isAcceptingOrders": false,
  "autoAcceptOrders": false,
  "avgPrepTimeMins": 20,
  "openingTime": "09:00",
  "closingTime": "22:00",
  "useCustomSchedule": false,
  "commissionPercentage": 20.00,
  "commissionFlatFee": 30.00,
  "minOrderAmount": 0.00,
  "fssaiNumber": null,
  "description": null,
  "logoUrl": null,
  "dietaryType": "Both",
  "deliveryMode": "Hivago",
  "isPureVeg": false,
  "isVeganFriendly": false,
  "hasJainOptions": false,
  "cuisineTypes": [],
  "ownerId": "6c4d6607-...",
  "owner": {
    "id": "6c4d6607-...",
    "name": "Test Owner",
    "email": "owner@example.com",
    "phone": "9876543210",
    "panNumber": null,
    "gstNumber": null,
    "bankAccountNumber": null,
    "bankIfscCode": null,
    "bankAccountName": null,
    "isActive": true
  },
  "notifications": {
    "emailAlerts": true,
    "browserNotifications": true,
    "orderSound": true
  },
  "schedule": [
    { "dayOfWeek": "Monday", "opensAt": "09:00", "closesAt": "22:00" }
  ],
  "createdAt": "2026-04-28T09:27:42.292481Z",
  "updatedAt": "2026-04-28T09:27:42.292481Z"
}
```

### `POST /api/admin/restaurants` — create restaurant

Creates a new restaurant linked to an existing owner. `rstCode` is
auto-generated server-side via a Postgres sequence.

**Request body:**
```json
{
  "ownerId": "6c4d6607-79cd-41b2-bd0f-68a36f0324ab",
  "name": "Pizza Paradise",
  "phone": "9876543210",
  "email": "pizza@paradise.com",
  "password": "InitialPassword!",
  "addressLine": "123 MG Road, Bangalore",
  "latitude": 12.9352,
  "longitude": 77.6245
}
```

**Response 201:**
```json
{ "restaurantId": "...", "rstCode": "RST003" }
```

**Errors:**
- `400` Validation: missing fields, bad coordinates, bad email
- `404 RestaurantOwner.NotFound` — the chosen owner doesn't exist
- `400` "Owner is inactive. Reactivate the owner before adding outlets."
- `409` "Restaurant with this email already exists."

### `PUT /api/admin/restaurants/{id}` — edit restaurant

All fields optional; only the ones supplied are updated.

**Request body (any subset):**
```json
{
  "name": "...",
  "phone": "...",
  "addressLine": "...",
  "commissionPercentage": 18.5,
  "commissionFlatFee": 25.00,
  "avgPrepTimeMins": 25,
  "cuisineTypes": ["Indian", "Chinese"],
  "isPureVeg": true,
  "isVeganFriendly": true,
  "hasJainOptions": false,
  "minOrderAmount": 99.00,
  "fssaiNumber": "12345678901234"
}
```

**Response 204** on success. Errors are 400 with field-level details.

### `PUT /api/admin/restaurants/{id}/status` — activate / deactivate

**Request body:**
```json
{ "isActive": true }
```

Pass `false` to deactivate, `true` to activate. Idempotent — sending the same
state again returns 204 with no error. Frontend should read the current
`isActive` and send the opposite when the toggle is clicked.

**Response 204.**

---

## Page 1 — Live Operations Dashboard

Three read-only endpoints power the dashboard.

### `GET /api/admin/dashboard/stats` — top-bar stats

**Response 200:**
```json
{
  "activeOrders": 1,
  "todayOrders": 0,
  "onlineRiders": 0,
  "pendingKyc": 1,
  "activeRestaurants": 2
}
```

- `activeOrders`: orders in `Paid`/`Confirmed`/`Preparing`/`ReadyForPickup`/`PickedUp`
- `todayOrders`: orders created since UTC midnight today
- `onlineRiders`: riders with `isOnline=true` and `isActive=true`
- `pendingKyc`: riders with KYC status = Pending
- `activeRestaurants`: restaurants with `isActive=true`

Refresh cadence on the panel: every 15–30 seconds is fine. SignalR push for
real-time is a future iteration.

### `GET /api/admin/orders/live` — live order feed

**Query params:**

| Param | Type | Default | Notes |
|---|---|---|---|
| `limit` | int | 200 | Max 500 |

**Response 200:**
```json
{
  "count": 1,
  "orders": [
    {
      "orderId": "fbfaaabb-...",
      "orderNumber": "ORD-20260313-00006",
      "customerName": "Customer",
      "restaurantName": "Vohuman Cafe",
      "riderName": null,
      "status": "Paid",
      "fulfillmentType": "Delivery",
      "itemCount": 2,
      "total": 222.00,
      "currency": "INR",
      "createdAt": "2026-03-13T14:08:53.058125Z"
    }
  ]
}
```

`riderName` is `null` for orders without an assigned rider (display as
"Unassigned"). Pickup orders have `fulfillmentType: "Pickup"` and never have a
rider — show "N/A" for those.

### `GET /api/admin/alerts` — operational alerts feed

**Query params:**

| Param | Type | Default | Notes |
|---|---|---|---|
| `limit` | int | 50 | Max 200 |

**Response 200:**
```json
{
  "count": 1,
  "alerts": [
    {
      "type": "escalated",
      "orderId": "fbfaaabb-...",
      "orderNumber": "ORD-20260313-00006",
      "message": "Smoke test escalation",
      "severity": "high",
      "raisedAt": "2026-04-28T09:37:16.797356Z"
    }
  ]
}
```

**Alert types currently emitted:**
- `escalated` — order escalated to admin (`isEscalated=true`, status=Paid)
- `stuck_payment` — order stuck in Pending state (payment not confirmed) for >10 minutes

**Not yet emitted (deferred until cross-module Delivery signals are wired up):**
- `dispatch_failed` — rider dispatch attempts all failed
- `stuck_in_dispatch` — order ReadyForPickup with no rider assigned

`severity` is `"high"` or `"medium"` today; treat anything else as low.

---

## Page 3 — Orders Management & Escalation Modal

### `GET /api/admin/orders` — paged list with filters

**Query params:**

| Param | Type | Default | Notes |
|---|---|---|---|
| `status` | string | `all` | `all` \| `active` \| `escalated` \| `failed` |
| `search` | string | — | If starts with `ORD-`: exact order number match. Otherwise substring on customer name + restaurant name |
| `from` | datetime | — | UTC ISO 8601, inclusive |
| `to` | datetime | — | UTC ISO 8601, exclusive |
| `page` | int | 1 | |
| `pageSize` | int | 20 | Max 100 |

**Status tab semantics:**
- `active`: Paid / Confirmed / Preparing / ReadyForPickup / PickedUp
- `escalated`: orders with `isEscalated=true` (any status)
- `failed`: Rejected / Cancelled / Failed / Refunding / Refunded
- `all`: no status filter

**Response 200:**
```json
{
  "items": [
    {
      "orderId": "...",
      "orderNumber": "ORD-20260313-00006",
      "customerName": "Customer",
      "restaurantName": "Vohuman Cafe",
      "riderName": null,
      "status": "Cancelled",
      "paymentStatus": "Paid",
      "fulfillmentType": "Delivery",
      "isEscalated": true,
      "itemCount": 2,
      "total": 222.00,
      "currency": "INR",
      "createdAt": "2026-03-13T14:08:53.058125Z"
    }
  ],
  "totalCount": 6,
  "page": 1,
  "pageSize": 20,
  "counts": {
    "all": 6,
    "active": 0,
    "escalated": 6,
    "failed": 6
  }
}
```

`counts` always reflects the **same** filter (date range + search) but ignores
the `status` tab — so the badge counts on each tab can be rendered from a
single response.

### `GET /api/admin/orders/{id}` — admin-scoped order detail

Used by both the order detail page and the escalation modal. Includes rider
phone, restaurant phone, delay duration, full pricing breakdown, and itemized
lines.

**Response 200 (truncated):**
```json
{
  "orderId": "fbfaaabb-...",
  "orderNumber": "ORD-20260313-00006",
  "status": "Paid",
  "statusDisplay": "Paid - Awaiting Restaurant",
  "paymentStatus": "Paid",
  "fulfillmentType": "Delivery",
  "isEscalated": true,
  "escalatedAt": "2026-04-28T09:37:16.797356Z",
  "escalationReason": "Restaurant not responding",
  "delayMinutes": 14,
  "customerId": "...",
  "customerName": "Customer",
  "customerPhone": "9403266823",
  "customerEmail": null,
  "restaurantId": "...",
  "restaurantName": "Vohuman Cafe",
  "restaurantPhone": "+919876543210",
  "riderId": null,
  "riderName": null,
  "riderPhone": null,
  "deliveryAddress": "Shiv Towers, Floor 3, ...",
  "subTotal": 140.00,
  "deliveryFee": 40.00,
  "tax": 7.00,
  "discount": 0.00,
  "total": 222.00,
  "currency": "INR",
  "itemCount": 2,
  "items": [
    {
      "itemName": "Bun Maska",
      "quantity": 2,
      "unitPrice": 40.00,
      "totalPrice": 80.00,
      "specialInstructions": ""
    }
  ],
  "cancellationReason": null,
  "cancellationNotes": null,
  "rejectionReason": null,
  "specialInstructions": "Ring bell once",
  "createdAt": "2026-03-13T14:08:53.058125Z",
  "confirmedAt": null,
  "preparingAt": null,
  "readyAt": null,
  "pickedUpAt": null,
  "deliveredAt": null,
  "cancelledAt": null
}
```

`delayMinutes` = how long the order has been in its current state (only set
for non-terminal active states). Use this in the escalation modal's "Time in
current state" indicator.

The "Contact Rider" / "Contact Restaurant" buttons are pure UI — they read
`riderPhone` / `restaurantPhone` from this response. No separate API call.

### `POST /api/admin/orders/{id}/escalate` — manual escalation

**Request body:**
```json
{ "reason": "Restaurant not responding to phone calls" }
```

**Response 204** on success. `400` if `reason` is empty or > 500 chars.
Idempotent at the domain level — re-escalating an already-escalated order is a
no-op (still returns 204 in practice).

### `POST /api/admin/orders/{id}/assign-rider` — manual rider assignment

Bypasses the dispatch retry loop and assigns a specific rider to the order.
Rider name and phone are looked up server-side; only `riderId` needs to be
passed.

**Request body:**
```json
{ "riderId": "bcdaa7ee-e30c-4bb9-b2d2-0f5701233124" }
```

**Response 204** on success.

**Errors:**
- `400` "Pickup orders cannot have a rider assigned."
- `404 Order.RiderNotFound` — rider doesn't exist
- `400` "Cannot assign an inactive rider."
- `400 Order.CannotModify` — order is in a terminal state

### `POST /api/admin/orders/{id}/cancel` — admin cancel

Admin can cancel orders in any non-terminal state when `forceCancel: true`.

**Request body:**
```json
{
  "reason": 1,
  "notes": "Customer requested via phone",
  "forceCancel": true
}
```

`reason` is an enum — pass either the int value or the name string. Values:

| Value | Name |
|---|---|
| 0 | `CustomerRequested` |
| 10 | `RestaurantUnavailable` |
| 20 | `ItemsOutOfStock` |
| 30 | `RestaurantClosed` |
| 40 | `NoRidersAvailable` |
| 50 | `PaymentFailed` |
| 60 | `DeliveryAddressIssue` |
| 70 | `Timeout` |
| 75 | `PaymentTimeout` |
| 80 | `SystemError` |
| 90 | `FraudSuspected` |
| 100 | `Other` |

`forceCancel: true` (default) lets admin cancel from `Preparing`,
`ReadyForPickup`, `PickedUp`. `forceCancel: false` enforces the normal
allowlist (`Pending`/`Paid`/`Confirmed` only).

**Response 200** with the full updated order DTO.

**Errors:**
- `400 Order.CannotCancel` — terminal state, or non-cancellable state with
  `forceCancel: false`
- `403 Order.Unauthorized` — should not happen for admin tokens

### `POST /api/admin/orders/{id}/refund` — admin refund

Triggers a PayU refund. `forceRefund: true` (default) bypasses the normal
"is the payment in a refundable status?" check.

**Request body:**
```json
{
  "amount": null,
  "forceRefund": true
}
```

`amount: null` means full refund. Pass a decimal for partial refund.

**Response 200:**
```json
{ "refundRequestId": "REFUND-...", "status": "Queued" }
```

**Errors:**
- `404 Payment.NotFound` — no payment record for this order
- `400 Payment.NotRefundable` — payment isn't in a refundable status and
  `forceRefund: false`
- `400 Payment.NoPayuId` — payment record missing PayU transaction ID
- `400 Payment.RefundExceedsAmount`
- `400 Payment.RefundFailed` — PayU returned an error

### `GET /api/admin/orders/export` — CSV export

Same filters as the list endpoint, no pagination. Streams rows as CSV.

**Rate limit:** 5 requests / minute per admin (development: 100 / minute).

**Query params:** same as `/api/admin/orders` (status, search, from, to). Page
and pageSize are ignored.

**Response 200:** `text/csv; charset=utf-8`

```
OrderNumber,CreatedAtUtc,Status,PaymentStatus,FulfillmentType,CustomerName,CustomerPhone,RestaurantName,RiderName,ItemCount,Total,Currency,IsEscalated,CancellationReason
ORD-20260313-00006,2026-03-13T14:08:53.0581250Z,Cancelled,Paid,Delivery,Customer,9403266823,Vohuman Cafe,,2,222.00,INR,true,RestaurantUnavailable
...
```

Includes a UTF-8 BOM and proper CSV escaping. Filename is
`orders-{timestamp}.csv` via `Content-Disposition`.

`429 Too Many Requests` if the rate limit is hit.

---

## Page 4A — Restaurant Payouts

The restaurant tab on the payouts page. Rider tab (Page 4B) coming soon.

### Status semantics

Restaurant payouts are settled **manually via ICICI bulk bank transfer**, not an automated
gateway. A payout transitions through:

```
Pending → Processing → Paid     (exported into the weekly ICICI file, then bank-reconciled)
Pending → OnHold → Pending      (admin pause + release)
Processing → Failed → Pending   (ICICI rejected the row; admin fixes bank details, retries)
```

| Value | Meaning |
|---|---|
| `Pending` | Awaiting the next weekly export or admin action |
| `Processing` | Included in an exported ICICI file, awaiting the bank statement upload that reconciles it |
| `Paid` | Reconciled against the ICICI bank statement — has a real `transactionReference` (UTR) and `paidAt` |
| `Failed` | ICICI rejected this row (e.g. bad account) — reason recorded, fixable via retry |
| `OnHold` | Admin paused — excluded from export until released |

There is no "pay-now" action — a payout can only become `Paid` by reconciling an actual
ICICI bank statement (`POST /api/admin/payouts/restaurant/reconcile`, see below), so
`transactionReference` is always a real bank-issued UTR, never a synthetic value.

### `GET /api/admin/payouts/restaurant/summary` — stats card

**Response 200:**
```json
{
  "pendingCount": 1,
  "totalPendingAmount": 70793.10,
  "failedAmount": 10502.30,
  "onHoldCount": 0,
  "onHoldAmount": 0.00,
  "platformProfit": 5026.80,
  "nextAutoRunAtUtc": "2026-05-04T00:30:00Z",
  "lastAutoRun": {
    "atUtc": "2026-04-28T00:00:00Z",
    "restaurantCount": 1,
    "totalAmount": 70793.10,
    "totalPaid": 0.00
  }
}
```

- `platformProfit` — lifetime sum of `commission + commissionGst` on `Paid` payouts
- `nextAutoRunAtUtc` — Monday 06:00 IST next week (= 00:30 UTC)
- `lastAutoRun.totalAmount` — sum of `netPayoutAmount` for all payouts created on the latest batch day
- `lastAutoRun.totalPaid` — same set, restricted to those that have since been Paid
- `lastAutoRun` is `null` until the auto-run job has fired at least once

### `GET /api/admin/payouts/restaurant` — paged list

**Query params:**

| Param | Type | Default | Notes |
|---|---|---|---|
| `from` | datetime | — | Filter by `createdAtUtc >= from` |
| `to` | datetime | — | Filter by `createdAtUtc < to` |
| `ownerId` | uuid | — | Filter to one owner |
| `status` | string | (all) | `Pending` \| `Processing` \| `Paid` \| `Failed` \| `OnHold` |
| `page` | int | 1 | |
| `pageSize` | int | 20 | Max 100 |

**Response 200:**
```json
{
  "items": [
    {
      "payoutId": "ef3ff53b-...",
      "ownerId": "6c4d6607-...",
      "displayName": "Pizza Paradise",
      "orderCount": 156,
      "gmv": 87450.00,
      "netPayable": 70793.10,
      "status": "Pending",
      "statusNote": null,
      "cycleStart": "2026-04-21",
      "cycleEnd": "2026-04-27",
      "createdAtUtc": "2026-04-28T09:18:21.536940Z",
      "paidAtUtc": null,
      "transactionReference": null
    }
  ],
  "totalCount": 3,
  "page": 1,
  "pageSize": 20
}
```

`displayName` is computed server-side based on outlet count:
- 1 outlet: that restaurant's name (e.g. `"Pizza Paradise"`)
- N outlets: `"Owner Name (N outlets)"` (e.g. `"Yash Vishwakarma (3 outlets)"`)

### Export & reconcile — ICICI manual payout flow

Replaces the old gateway-based "pay-now" button. Endpoints documented separately once built
(see `specs/icici-manual-payout-export.md`):
- `POST /api/admin/payouts/restaurant/export` — generates the weekly ICICI bulk-transfer
  `.xlsx`, flips included payouts `Pending → Processing`.
- `POST /api/admin/payouts/restaurant/reconcile` — upload the ICICI bank statement;
  matches rows by amount, applies `Processing → Paid` (with UTR) or `→ Failed` (with reason).

### `POST /api/admin/payouts/restaurant/{payoutId}/hold` — pause payout

Optional body:
```json
{ "reason": "Owner disputed last week's commission" }
```

`reason` (if provided) is stored in `statusNote`.

**Response 204** on success.

**Errors:**
- `404 Payout.NotFound`
- `409 Conflict.Error` — not in `Pending` (only Pending payouts can be paused)

### `POST /api/admin/payouts/restaurant/{payoutId}/release-hold` — release hold

No body.

**Response 204** on success.

**Errors:**
- `404 Payout.NotFound`
- `409 Conflict.Error` — not in `OnHold`

### `POST /api/admin/payouts/restaurant/{payoutId}/retry` — retry failed payout

No body. Moves a `Failed` payout back to `Pending` and clears `failureReason`.
Picked up by the next weekly ICICI export.

**Response 204** on success.

**Errors:**
- `404 Payout.NotFound`
- `409 Conflict.Error` — not in `Failed`

---

## Appendix — legacy endpoints

These exist on the backend but are **not** part of the new admin panel build.
Don't call them from the new screens. They stay so existing tooling and the
restaurant-side login keep working.

| Endpoint | What it is |
|---|---|
| `POST /api/admins/login` | Admin login. Used by the admin panel for auth. **Do call this.** |
| `POST /api/admins/restaurants` | Old admin restaurant create — does NOT require `ownerId`. Use `POST /api/admin/restaurants` (singular) instead. |
| `POST /api/admins/riders` | Old admin rider create. |
| `GET /api/admins/riders` | Old admin rider list. |
| `GET /api/admins/restaurants` | Old admin restaurant list. The new singular `/api/admin/restaurants` has the richer response. |
| `GET /api/admins/restaurants/{id}` | Old detail. Use `/api/admin/restaurants/{id}` instead. |
| `PUT /api/admins/restaurants/{id}` | Old edit. Use `/api/admin/restaurants/{id}` instead. |
| `PUT /api/admins/restaurants/{id}/status` | Status toggle — same path is being used by the new panel too; status request shape is identical. |
| `GET /api/admins/stats` | Old all-in-one stats endpoint. Page 1's `/api/admin/dashboard/stats` covers what the panel needs; the larger stats payload is still available here if needed. |
| `GET /api/admins/stats/orders` | Order-status breakdown across all-time. Useful for analytics screens, not Page 1. |
| `GET /api/admins/stats/revenue` | Revenue/commission by period. Useful for Page 5 (Money Analytics) when that's built. |
| `GET /api/admins/orders/escalated` | Old escalated orders feed. Replaced by `/api/admin/alerts` for Page 1 and `/api/admin/orders?status=escalated` for Page 3. |
| `PUT /api/admin/payouts/{id}/process` | Old manual payout processor. Replaced by the four restaurant payout mutation endpoints. Don't call this from the new panel. |

### Swagger UI

The full OpenAPI spec is auto-generated and available at
`https://your-deploy-host/swagger` (or `http://localhost:5000/swagger` locally).
Use it for try-it-out testing while building the screens.

---

## What's coming next

- **Page 4B — Rider Payouts.** Six endpoints under `/api/admin/payouts/rider/...`
  mirroring the restaurant tab. New `RiderPayoutLedger` table, weekly
  aggregation job. ETA: this iteration.
- **Page 5 — Money Analytics.** Three read-only aggregation endpoints
  (`/api/admin/analytics/financial`, `/gmv-over-time`, `/order-funnel`).
- **Cross-cutting:** audit log on every mutation under `/api/admin/*` (every
  POST/PUT records `{adminId, action, targetType, targetId, payload, at}`).

This doc will be amended as those land. The authoritative endpoint list is
always the Swagger spec on the deployed API.
