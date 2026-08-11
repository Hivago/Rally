// k6 load test — RallyAPI hot paths (restaurant browse, menu view, order placement)
//
// WHY these three: they're the only endpoints in the codebase that are unbounded/uncached
// on the read side (see docs/frontend-cicd or ask Claude for the scale audit), so they're
// the first things to fall over under real concurrency.
//
// Install k6: https://k6.io/docs/get-started/installation/ (winget install k6 --source winget)
//
// Run (browse + menu only, no auth needed):
//   k6 run scripts/load-test.js --env BASE_URL=https://<your-staging-url>
//
// Run (with order placement included — needs a real customer JWT + restaurant/menu-item IDs):
//   k6 run scripts/load-test.js `
//     --env BASE_URL=https://<your-staging-url> `
//     --env CUSTOMER_TOKEN=<jwt> `
//     --env RESTAURANT_ID=<guid> `
//     --env MENU_ITEM_ID=<guid> `
//     --env INCLUDE_ORDERS=true
//
// IMPORTANT: POST /api/orders is rate-limited to 60 req/min PER IP (shares the "login"
// policy — see OrderEndpoints.cs:57). If you run this from a single machine, you WILL
// start seeing 429s around ~1 req/sec on the order scenario long before Postgres is the
// bottleneck. That's expected — it's one of the findings, not a bug in this script.
// To actually stress the DB via order placement you'd need to distribute across many
// source IPs, which is a bigger exercise — for now this script proves out the two
// uncached/unbounded read paths, and shows you the rate-limit ceiling on writes.

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5023';
const CUSTOMER_TOKEN = __ENV.CUSTOMER_TOKEN || '';
const RESTAURANT_ID = __ENV.RESTAURANT_ID || '';
const MENU_ITEM_ID = __ENV.MENU_ITEM_ID || '';
const INCLUDE_ORDERS = (__ENV.INCLUDE_ORDERS || 'false').toLowerCase() === 'true';

// Bengaluru-ish coordinates for the lat/lng-filtered browse query — swap for real test data.
const TEST_LAT = Number(__ENV.TEST_LAT || 12.9352);
const TEST_LNG = Number(__ENV.TEST_LNG || 77.6245);

const browseLatency = new Trend('browse_restaurants_ms');
const menuLatency = new Trend('get_menu_ms');
const orderLatency = new Trend('place_order_ms');
const orderRateLimited = new Rate('order_429_rate');

export const options = {
  scenarios: {
    browse_restaurants: {
      executor: 'ramping-vus',
      exec: 'browseRestaurants',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '1m', target: 50 },
        { duration: '1m', target: 100 },
        { duration: '1m', target: 200 },
        { duration: '30s', target: 0 },
      ],
    },
    ...(RESTAURANT_ID
      ? {
          view_menu: {
            executor: 'ramping-vus',
            exec: 'viewMenu',
            startVUs: 0,
            stages: [
              { duration: '30s', target: 20 },
              { duration: '1m', target: 50 },
              { duration: '1m', target: 100 },
              { duration: '1m', target: 200 },
              { duration: '30s', target: 0 },
            ],
          },
        }
      : {}),
    ...(INCLUDE_ORDERS
      ? {
          place_orders: {
            executor: 'constant-arrival-rate',
            exec: 'placeOrder',
            rate: 1, // deliberately low — the 60/min-per-IP policy caps this anyway
            timeUnit: '1s',
            duration: '2m',
            preAllocatedVUs: 5,
            maxVUs: 10,
          },
        }
      : {}),
  },
  thresholds: {
    'browse_restaurants_ms': ['p(95)<800'],
    'get_menu_ms': ['p(95)<500'],
    http_req_failed: ['rate<0.05'],
  },
};

export function browseRestaurants() {
  const res = http.get(
    `${BASE_URL}/api/catalog/restaurants?lat=${TEST_LAT}&lng=${TEST_LNG}&radiusKm=5&page=1&pageSize=20`,
    { tags: { name: 'browse_restaurants' } }
  );
  browseLatency.add(res.timings.duration);
  check(res, {
    'browse: status 200': (r) => r.status === 200,
  });
  sleep(1 + Math.random());
}

export function viewMenu() {
  if (!RESTAURANT_ID) {
    console.warn('RESTAURANT_ID not set — skipping menu scenario iteration');
    sleep(1);
    return;
  }
  const res = http.get(`${BASE_URL}/api/catalog/restaurants/${RESTAURANT_ID}/menu`, {
    tags: { name: 'view_menu' },
  });
  menuLatency.add(res.timings.duration);
  check(res, {
    'menu: status 200': (r) => r.status === 200,
  });
  sleep(1 + Math.random());
}

export function placeOrder() {
  if (!CUSTOMER_TOKEN || !RESTAURANT_ID || !MENU_ITEM_ID) {
    console.warn('CUSTOMER_TOKEN/RESTAURANT_ID/MENU_ITEM_ID not set — skipping order iteration');
    return;
  }

  const payload = JSON.stringify({
    restaurantId: RESTAURANT_ID,
    fulfillmentType: 'Delivery',
    items: [{ menuItemId: MENU_ITEM_ID, quantity: 1 }],
    deliveryAddress: {
      addressLine: 'Load Test Address',
      latitude: TEST_LAT,
      longitude: TEST_LNG,
      label: 'Other',
    },
    pricing: { subTotal: 100, deliveryFee: 20, tax: 5, total: 125 },
    paymentId: `loadtest-${__VU}-${__ITER}-${Date.now()}`,
  });

  const res = http.post(`${BASE_URL}/api/orders`, payload, {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${CUSTOMER_TOKEN}`,
      'Idempotency-Key': `loadtest-${__VU}-${__ITER}-${Date.now()}`,
    },
    tags: { name: 'place_order' },
  });

  orderLatency.add(res.timings.duration);
  orderRateLimited.add(res.status === 429 ? 1 : 0);
  check(res, {
    'order: not 5xx': (r) => r.status < 500,
  });
}
