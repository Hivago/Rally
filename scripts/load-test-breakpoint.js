// k6 breaking-point test — GET /api/catalog/restaurants (anonymous, uncached)
//
// Ramps far past the 200-VU number that stayed flat in the first run, and aborts
// automatically once the service actually starts failing (>10% error rate) or
// falls over on latency (p95 > 5s) — so it doesn't keep hammering staging for
// the full 5 minutes if it breaks early at, say, VU 400.
//
// Run: k6 run scripts/load-test-breakpoint.js --env BASE_URL=<staging-url>

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5023';
const TEST_LAT = Number(__ENV.TEST_LAT || 12.9352);
const TEST_LNG = Number(__ENV.TEST_LNG || 77.6245);

const browseLatency = new Trend('browse_restaurants_ms');

export const options = {
  scenarios: {
    find_breaking_point: {
      executor: 'ramping-vus',
      exec: 'browseRestaurants',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 200 },
        { duration: '20s', target: 400 },
        { duration: '20s', target: 600 },
        { duration: '20s', target: 800 },
        { duration: '20s', target: 1000 },
        { duration: '20s', target: 1500 },
        { duration: '20s', target: 2000 },
        { duration: '20s', target: 3000 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '15s',
    },
  },
  thresholds: {
    // abortOnFail stops the whole run early the moment the service is actually
    // breaking, instead of grinding through the full ramp after it's already dead.
    http_req_failed: [{ threshold: 'rate<0.10', abortOnFail: true, delayAbortEval: '10s' }],
    http_req_duration: [{ threshold: 'p(95)<5000', abortOnFail: true, delayAbortEval: '10s' }],
  },
};

export function browseRestaurants() {
  const res = http.get(
    `${BASE_URL}/api/catalog/restaurants?lat=${TEST_LAT}&lng=${TEST_LNG}&radiusKm=5&page=1&pageSize=20`,
    { tags: { name: 'browse_restaurants' } }
  );
  browseLatency.add(res.timings.duration);
  check(res, { 'status 200': (r) => r.status === 200 });
  sleep(0.5 + Math.random() * 0.5);
}
