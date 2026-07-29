# UI Implementation Guide — Payout & Order Status Fixes (2026-07-28)

Backend changes for 3 pieces of feedback: flexible payout history, customer "ready for pickup" status, admin order-level payout breakdown. No breaking changes — all existing calls keep working with old behavior if you don't pass new params.

---

## 1. Flexible payout/earnings date range

**Endpoint:** `GET /api/restaurants/payouts/earnings`
**Auth:** Restaurant

**What changed:** Now accepts optional `from` / `to` query params (date, `YYYY-MM-DD`). Omit both to get the old "current week" behavior — nothing breaks if you don't touch this yet.

```
GET /api/restaurants/payouts/earnings                          # current week (old behavior)
GET /api/restaurants/payouts/earnings?from=2026-06-01&to=2026-07-28   # any range
```

**Response** (`EarningsSummaryDto`, unchanged shape):
```json
{
  "orderCount": 42,
  "grossRevenue": 15230.50,
  "totalCommission": 1523.05,
  "totalTds": 152.30,
  "netEarnings": 13555.15,
  "periodStart": "2026-06-01",
  "periodEnd": "2026-07-28",
  "ledgerEntries": [ /* per-order lines */ ]
}
```

**Steps:**
1. Add a date-range picker to the restaurant payout/earnings screen.
2. Pass `from`/`to` when the user picks a custom range; call with no params for "this week" (default tab).
3. This is separate from the payout **history** endpoint (`GET /api/restaurants/payouts/`), which already returns all past weekly payout batches, paginated — use that for the "past payouts" list, this `/earnings` endpoint is for the live/in-progress earnings widget.

Same param pattern applies if the **admin panel** wants a flexible restaurant-earnings view — ask backend if you need an admin-facing version of this same endpoint.

---

## 2. Customer order status: "ready, waiting for rider"

**Channel:** SignalR hub `/hubs/notifications`, group `customer_{customerId}` (already joined automatically on connect), event name **`OrderStatusUpdate`** (same event you already listen to for Confirmed/PickedUp/Delivered — no new event name, just new `status` values).

**New payloads you'll now receive:**

```json
// status: "Preparing"
{
  "orderId": "...",
  "orderNumber": "RLY-1234",
  "status": "Preparing",
  "message": "Spice Garden is preparing your order"
}

// status: "ReadyForPickup" (delivery order)
{
  "orderId": "...",
  "orderNumber": "RLY-1234",
  "status": "ReadyForPickup",
  "message": "Your order is ready! Waiting for a delivery partner to pick it up."
}

// status: "ReadyForPickup" (self-pickup order — FulfillmentType = Pickup)
{
  "orderId": "...",
  "orderNumber": "RLY-1234",
  "status": "ReadyForPickup",
  "message": "Your order is ready for pickup at the restaurant!"
}
```

**Steps:**
1. In your order-tracking status stepper/timeline component, add a step for `"Preparing"` and `"ReadyForPickup"` if you don't already render every status value generically.
2. If your tracker already just displays whatever `message` string comes through, **no code change needed** — you'll automatically start seeing these two new messages fill the gap that used to jump straight from "Confirmed" to "Rider assigned."
3. Double-check you're not filtering/switching on a hardcoded whitelist of status strings that would silently drop the new ones.

---

## 3. Admin: order-level breakdown per payout

**Endpoint:** `GET /api/admin/payouts/restaurant/{payoutId}`
**Auth:** Admin

Previously only restaurant owners could see which orders make up a payout. This is the same view, now available to admin for any restaurant's payout.

**Response** (`AdminPayoutDetail`):
```json
{
  "payoutId": "...",
  "ownerId": "...",
  "displayName": "Spice Garden",
  "cycleStart": "2026-07-13",
  "cycleEnd": "2026-07-19",
  "orderCount": 87,
  "grossOrderAmount": 45210.00,
  "totalGstCollected": 2260.50,
  "totalCommission": 4521.00,
  "totalCommissionGst": 813.78,
  "totalTds": 452.10,
  "netPayoutAmount": 38677.72,
  "status": "Pending",
  "transactionReference": null,
  "paidAtUtc": null,
  "notes": null,
  "createdAtUtc": "2026-07-20T00:30:00Z",
  "ledgerEntries": [
    {
      "id": "...",
      "outletId": "...",
      "orderId": "...",
      "orderNumber": "RLY-1234",
      "orderAmount": 520.00,
      "gstAmount": 26.00,
      "commissionPercentage": 10,
      "commissionFlatFee": null,
      "commissionAmount": 52.00,
      "commissionGst": 9.36,
      "tdsAmount": 5.20,
      "netAmount": 479.44,
      "status": "AssignedToPayout",
      "createdAtUtc": "2026-07-15T12:10:00Z"
    }
    // ... one row per order in this payout
  ]
}
```

**Steps:**
1. On the admin payout list/table (from `GET /api/admin/payouts/restaurant`), make each row clickable/expandable.
2. On click, call `GET /api/admin/payouts/restaurant/{payoutId}` using the row's `payoutId`.
3. Render `ledgerEntries` as a table: order number, order amount, commission, TDS, net amount — this gives admin/investor the per-order drill-down they asked for.
4. 404 means the payout doesn't exist — handle same as any other admin detail 404.

---

## Rollout notes

- All three changes are backward compatible — no existing integration breaks if left untouched.
- No new auth scopes, no breaking response shape changes to existing endpoints.
- Backend build + tests verified before handoff (see commit for test run details).
