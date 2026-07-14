# Frontend Hand-off — Delivery Quote API (item cost + pickup support)

## TL;DR
`POST /api/delivery/quote` now returns the **full bill** (item cost + fees), not just delivery
charges, and it now supports **pickup orders**. Two things for the UI:
1. Use the new `itemTotal` / `grandTotal` fields to render the complete price summary.
2. For pickup, send `fulfillmentType: "Pickup"` and skip the drop location — you get back a
   quote with **zero delivery fee** (platform fee + GST only).

Existing fields are unchanged in meaning, so nothing you already built breaks.

---

## Endpoint
`POST /api/delivery/quote` — call at checkout, whenever items/address/fulfillment change.

### Request
```jsonc
{
  "restaurantId": "…",           // required
  "pickupLatitude": 12.9352,     // required (restaurant location)
  "pickupLongitude": 77.6245,    // required
  "pickupPincode": "560034",     // optional (reverse-geocoded if omitted)

  "dropLatitude": 12.9100,       // OPTIONAL now — required for delivery, omit for pickup
  "dropLongitude": 77.6400,      // OPTIONAL now — required for delivery, omit for pickup
  "dropPincode": "560102",       // optional
  "city": "Bengaluru",           // optional

  "orderAmount": 500,            // required — food subtotal (sum of item prices)
  "fulfillmentType": "Delivery"  // NEW — "Delivery" (default) or "Pickup"
}
```
- `fulfillmentType` is optional and defaults to `"Delivery"`, so your existing delivery call
  keeps working with **no change**.
- For **pickup**, set `"fulfillmentType": "Pickup"` and you may omit `dropLatitude`/`dropLongitude`.

### Response (`DeliveryQuoteDto`)
```jsonc
{
  "id": "…",                     // quote id — pass back as deliveryQuoteId when placing a DELIVERY order
  "fulfillmentType": "Delivery", // "Delivery" or "Pickup"

  "itemTotal": 500,              // NEW — food subtotal (echo of orderAmount)
  "deliveryFee": 40,             // delivery charge only (0 for pickup)
  "platformFee": 10,             // flat platform fee
  "gst": 9.00,                   // GST on (deliveryFee + platformFee)
  "totalPayable": 59.00,         // fees only = deliveryFee + platformFee + gst
  "grandTotal": 559.00,          // NEW — itemTotal + totalPayable  ← show this as the amount due

  "distanceKm": 3.2,             // 0 for pickup
  "estimatedMinutes": 32,        // 0 for pickup
  "surgeMultiplier": 1.0,
  "surgeReason": null,
  "expiresAt": "2026-07-14T10:30:00Z",
  "breakdown": [                 // line items that sum to totalPayable (NOT including itemTotal)
    { "name": "Delivery Fee",  "description": "Delivery charge",              "amount": 40 },
    { "name": "Platform Fee",  "description": "Platform service fee",         "amount": 10 },
    { "name": "GST",           "description": "GST on delivery + platform fee","amount": 9.00 }
  ]
}
```

> ⚠️ `totalPayable` is **fees only** — same as before. The amount the customer pays is
> `grandTotal`. Do **not** sum `breakdown` and add `itemTotal` yourself; use `grandTotal`.

---

## Step-by-step

### Step 1 — Update your quote type
Add the three new fields to your `DeliveryQuote` TS type:
```ts
interface DeliveryQuote {
  id: string;
  fulfillmentType: "Delivery" | "Pickup"; // new
  itemTotal: number;                       // new
  deliveryFee: number;
  platformFee: number;
  gst: number;
  totalPayable: number;                    // fees only
  grandTotal: number;                      // new — itemTotal + totalPayable
  distanceKm: number;
  estimatedMinutes: number;
  surgeMultiplier: number;
  surgeReason: string | null;
  expiresAt: string;
  breakdown: { name: string; description: string; amount: number }[];
}
```

### Step 2 — Send `fulfillmentType`
Add it to the request body. Drive it from the delivery/pickup toggle the user already picks.
```ts
const body = {
  restaurantId,
  pickupLatitude, pickupLongitude, pickupPincode,
  orderAmount: subTotal,
  fulfillmentType,                 // "Delivery" | "Pickup"
  ...(fulfillmentType === "Delivery"
      ? { dropLatitude, dropLongitude, dropPincode, city }
      : {}),                       // omit drop for pickup
};
```

### Step 3 — Render the bill from the response (don't recompute)
```
Item total                itemTotal
Delivery fee              deliveryFee     (hide the row when pickup / deliveryFee === 0)
Platform fee              platformFee
GST                       gst
────────────────────────
To pay                    grandTotal
```
A generic approach: render each `breakdown` row, show `itemTotal` above it, and `grandTotal`
as the total. Never add `itemTotal` into the `breakdown` list — it isn't part of it.

### Step 4 — Handle the pickup differences
When `fulfillmentType === "Pickup"`:
- `deliveryFee`, `distanceKm`, `estimatedMinutes` are all `0` — hide the delivery-fee row and
  the ETA/distance UI.
- `id` comes back as an all-zeros GUID (`00000000-0000-0000-0000-000000000000`). That's expected:
  pickup pricing is recomputed server-side at order time, so **you do not pass a
  `deliveryQuoteId` when placing a pickup order** (see Step 5).

### Step 5 — Placing the order (unchanged contract, just be aware)
- **Delivery order**: pass the quote's `id` as `deliveryQuoteId` in `POST /api/orders`. The
  backend re-reads the quote and bills exactly what you were quoted.
- **Pickup order**: send `"fulfillmentType": "Pickup"`, `"pricing": { …, "deliveryFee": 0 }`,
  and **no** `deliveryQuoteId`. The backend applies the platform fee + GST from config.

### Step 6 — Error handling (unchanged)
Delivery quotes still return `400` when the drop is outside the service radius (>5 km), e.g.
_"Delivery is only available within 5 km of the restaurant. This address is 7.3 km away."_
Show it inline on the address step. Pickup quotes never hit this check.

---

## Why the backend is authoritative
The customer always pays **our** price. The frontend must not compute delivery fee, platform
fee, or GST itself — read them from this endpoint and, at order time, the backend re-derives
them (from the stored quote for delivery, from config for pickup). Platform fee and GST% are
tuned from Railway config (`Delivery:Quote`), so the quote and the final order bill always match.

## Quick test cases
| Scenario | Request | Expect |
|----------|---------|--------|
| Delivery, in range | `fulfillmentType:"Delivery"` + drop coords ≤5 km | `deliveryFee>0`, `id` set, `grandTotal = itemTotal + totalPayable` |
| Delivery, out of range | drop coords >5 km | `400` with distance message |
| Pickup | `fulfillmentType:"Pickup"`, no drop | `deliveryFee:0`, `distanceKm:0`, `id:"000…0"`, `grandTotal = itemTotal + platformFee + gst` |
| Omit fulfillmentType | legacy body | treated as Delivery (backward compatible) |
