-- Backfill: release DeliveryRequests/riders orphaned by cancelled orders.
--
-- Root cause (fixed going forward by OrderCancelledIntegrationEventHandler,
-- added in Delivery.Application/EventHandlers/): cancelling an order never
-- notified the Delivery module. Every order cancelled while a delivery was
-- in flight left its delivery_requests row stuck at whatever status it was
-- in, and (if a rider had accepted) left riders.current_delivery_id
-- permanently pinned — blocking that rider from any new offers.
--
-- This script only repairs EXISTING orphaned rows created before the fix.
-- New cancellations are handled automatically now.
--
-- Run the SELECT first and read the output. Only run the UPDATE block
-- (still inside the same transaction) if the rows look like what you expect.
-- Nothing commits until the explicit COMMIT at the bottom — replace it with
-- ROLLBACK to abort after reviewing.

BEGIN;

-- ─────────────────────────────────────────────────────────────────────────
-- STEP 1 — DIAGNOSTIC: find orphaned delivery_requests
-- Order is Cancelled (100) or Rejected (90) but the delivery_request never
-- reached a terminal status (Cancelled/Delivered/Failed/RtoDelivered/RtoDisposed).
-- ─────────────────────────────────────────────────────────────────────────

SELECT
    dr.id                AS delivery_request_id,
    o.id                 AS order_id,
    o.order_number,
    o.status              AS order_status,          -- 90 = Rejected, 100 = Cancelled
    dr.status              AS delivery_status,        -- see DeliveryRequestStatus enum
    dr.rider_id,
    r.name                AS rider_name,
    r.current_delivery_id AS rider_current_delivery_id,
    dr.assigned_at,
    dr.picked_up_at,
    dr.updated_at          AS delivery_updated_at,
    o.updated_at           AS order_updated_at
FROM orders.orders o
JOIN delivery.delivery_requests dr ON dr.order_id = o.id
LEFT JOIN users.riders r ON r.id = dr.rider_id
WHERE o.status IN (90, 100)  -- Rejected, Cancelled
  AND dr.status NOT IN (70, 80, 90, 76, 77)  -- Delivered, Cancelled, Failed, RtoDelivered, RtoDisposed
ORDER BY dr.updated_at ASC;

-- ─────────────────────────────────────────────────────────────────────────
-- STEP 2 — FIX: mirror what OrderCancelledIntegrationEventHandler now does.
--
-- 2a. Cancel any orphaned delivery_request that never reached PickedUp (50).
--     Past PickedUp, leave delivery status alone — food may physically be
--     out with the rider; that needs a human call (MarkDelivered/MarkFailed
--     via the app), not a blind status flip. Those rows still show up in
--     the SELECT above for manual follow-up.
-- ─────────────────────────────────────────────────────────────────────────

UPDATE delivery.delivery_requests dr
SET status = 80,  -- Cancelled
    cancelled_at = now(),
    failure_notes = 'Backfill: order cancelled, delivery never reached pickup',
    updated_at = now()
FROM orders.orders o
WHERE dr.order_id = o.id
  AND o.status IN (90, 100)
  AND dr.status NOT IN (50, 55, 60, 65, 70, 80, 90, 76, 77)  -- below PickedUp and not already terminal
  AND dr.status >= 30;  -- RiderAssigned or later — Created/PendingDispatch/SearchingOwnFleet/Searching3PL
                         -- are already covered by DeliveryDispatchRecoveryService's stuck-recovery sweep.

-- ─────────────────────────────────────────────────────────────────────────
-- 2b. Release any rider still pinned to one of these orphaned deliveries
--     (covers both the ones just cancelled above AND the past-pickup ones
--     left for manual delivery-status resolution — the rider should not
--     stay blocked either way).
-- ─────────────────────────────────────────────────────────────────────────

UPDATE users.riders r
SET current_delivery_id = NULL,
    current_delivery_assigned_at = NULL,
    updated_at = now()
FROM delivery.delivery_requests dr
JOIN orders.orders o ON o.id = dr.order_id
WHERE r.id = dr.rider_id
  AND r.current_delivery_id = dr.id
  AND o.status IN (90, 100);

-- ─────────────────────────────────────────────────────────────────────────
-- STEP 3 — verify, then COMMIT (or ROLLBACK to abort)
-- ─────────────────────────────────────────────────────────────────────────

-- Re-run the STEP 1 SELECT here before committing to confirm the expected rows changed.

COMMIT;
-- ROLLBACK;
