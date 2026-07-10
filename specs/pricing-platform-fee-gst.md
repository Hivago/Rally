# Spec: Platform Fee + GST + New Delivery Tiers (config-driven)

> **Status**: PLAN — awaiting approval before implementation
> **Branch**: `feature/pricing-platform-fee-gst` (off `master`)
> **Priority**: P1 — production pricing / money
> **Constraint**: must be reliable — no regression from any existing mutation/usage combination.

---

## 1. Target pricing model

### Customer (paid on top of food subtotal)
| Component | Rule |
|---|---|
| Delivery fee | ₹20 for 0–3 km, then +₹15 per km (ceil) for 3–5 km. Max at 5 km = ₹50. |
| Platform fee | ₹10 flat |
| GST | 18% on **(delivery fee + platform fee)** only |

Example @1.42 km: delivery ₹20 + platform ₹10 + GST ₹5.40 = **₹35.40** delivery-side (food subtotal + 5% food GST are separate, unchanged).
Example @4.2 km: delivery ₹50 + platform ₹10 + GST ₹10.80 = **₹70.80**.

### Restaurant (deducted from payout, **replaces** commission flat fee)
| Component | Rule |
|---|---|
| Delivery charge | ₹35 flat |
| Platform charge | ₹15 flat |
| GST | 18% on (35 + 15) = ₹9 |
| **Total restaurant charge** | **₹59** |

Net payout = food subtotal − ₹50 − ₹9 GST − 1% TDS. (5% food GST info line unchanged.)

## 2. Config knobs (all Railway-overridable)
```
Delivery__Pricing__BaseFee              = 20
Delivery__Pricing__PerKmRate            = 15
Delivery__Pricing__BaseDistanceKm       = 3
Delivery__Pricing__CustomerPlatformFee  = 10
Delivery__Pricing__GstPercent           = 18
Delivery__RestaurantCharge__DeliveryFee = 35
Delivery__RestaurantCharge__PlatformFee = 15
Delivery__RestaurantCharge__GstPercent  = 18
```
New options classes bound from config sections → change values in Railway without a redeploy of code.

## 2b. Uniform customer pricing across fleets (resolved)
The customer always pays **our** pricing — delivery tier + platform + GST — whether the order is fulfilled by own fleet or 3PL. The 3PL provider's quoted price is **our cost only** (margin tracking), never the customer's delivery fee. So `GetQuote` computes the customer delivery fee from our tiers for BOTH paths; the 3PL provider price is recorded as cost, not shown to the customer.

## 3. Critical invariant (reliability)
**`DeliveryQuote.FinalFee` MUST stay = delivery fee only.** It is the rider-earnings basis (`CalculateEarnings(QuotedPrice)`) and the 3PL `OrderAmount`. Platform fee + GST are **separate additive fields** — never folded into FinalFee, or riders would be paid a % of the platform fee/GST.

## 4. Files to change (grouped)

### A. Customer quote — Pricing module
- `DeliveryPricingOptions`: BaseFee 30→20, PerKmRate 10→15; **add** `CustomerPlatformFee`, `GstPercent`.
- `DeliveryPriceResult`: **add** `PlatformFee`, `GstAmount`, `TotalPayable` (additive, non-breaking).
- `DeliveryPricingCalculator`: compute platform + GST, add breakdown lines; **FinalFee unchanged (delivery only)**.
- `DeliveryQuoteDto` + quote endpoint mapping: return `platformFee`, `gst`, `totalPayable` (additive).

### B. Quote persistence — Delivery module
- `DeliveryQuote` entity: **add** `PlatformFee`, `GstAmount` (nullable, default 0) → **migration**.
- `CreateOwnFleet` / `CreateThirdParty` factories + `GetQuoteCommandHandler`: populate + map them.

### C. Order bill — Orders module
- `OrderPricing`: **add** `PlatformFee`, `DeliveryGst` (optional, default 0); `Total` includes them → **migration**.
- PlaceOrder request DTO: **add optional** `PlatformFee`, `DeliveryGst`.
- Customer bill/label DTO: show the two new lines.

### D. Restaurant payout — Orders module
- New `RestaurantChargeOptions` (config).
- `OrderDeliveredPayoutLedgerHandler`: source the charge from config (`DeliveryFee + PlatformFee`) instead of `restaurant.CommissionFlatFee`.
- `PayoutLedger.Create`: charge = ₹50, GST = 18% of charge; make the rate config-driven.

## 5. Migrations (all additive, safe)
- `DeliveryQuote`: `PlatformFee`, `GstAmount` — nullable.
- `Order` (OrderPricing owned type): `PlatformFee`, `DeliveryGst` — nullable/default 0.
- No column drops, no type changes, no data rewrite. Apply on Railway **before** merge (deploy-safety rule).

## 6. Reliability / regression analysis — "won't break other usages"
| Area | Impact | Why safe |
|---|---|---|
| Rider earnings | none | FinalFee still delivery-only |
| 3PL `OrderAmount` | none | uses QuotedPrice = FinalFee |
| Existing orders/quotes | none | new columns nullable/default 0 |
| Old frontend app (doesn't send new fields) | none | new order fields default 0 → identical Total as today |
| Existing PayoutLedger rows | none | only new rows use new formula |
| Dispatch / early-dispatch | none | pricing change is orthogonal |
| **Restaurant economics** | **CHANGES** | flat ₹50+GST replaces per-restaurant `CommissionFlatFee` for all new deliveries — intended, but a real behavior change to confirm |
| PricingEngineTests / integration | update needed | expected fee values change |

## 7. Decisions (resolved)
1. **Backend-authoritative pricing — YES (safest for money).** At placement, the delivery-side charges (delivery fee + platform fee + GST) are taken from the **stored `DeliveryQuote`** (loaded by `QuoteId`), NOT from the frontend's `pricing` object. The frontend's delivery/platform/GST values are ignored/overridden. If a delivery order has no resolvable quote → reject with a clear "refresh your quote" error. Food subtotal + 5% food tax path unchanged. Quote expiry does **not** block (the customer was shown that price); a missing/invalid `QuoteId` does.
2. **No free-delivery threshold.** Delivery is always charged; drop the `FreeDeliveryThreshold` from any logic. All amounts come from Railway config only.
3. **Restaurant charge = ₹50, kept as two config components (₹35 delivery + ₹15 platform) for clarity, summed to one commission charge.** Minimal code change: `OrderDeliveredPayoutLedgerHandler` sources `DeliveryFee + PlatformFee` from `RestaurantChargeOptions` (instead of `restaurant.CommissionFlatFee`) and passes the ₹50 sum into the existing `PayoutLedger.Create(commissionFlatFee:)`. The two components stay **separate in config** so the split is never a mystery; GST (18%) is already computed on the charge inside `PayoutLedger.Create`. No PayoutLedger schema change required (optional later: store the 35/15 split as two columns for audit).

## 8. Test plan
- Unit: delivery tiers (0–3 = ₹20; 3.5 = ₹35; 5 = ₹50); platform + GST math; FinalFee unchanged; OrderPricing total with platform+GST; PayoutLedger with ₹50 charge + ₹9 GST.
- Regression: existing dispatch/order/pricing suites green (FinalFee semantics unchanged).
- Integration: quote returns new breakdown; order stores platform+GST; payout ledger uses new charge.
