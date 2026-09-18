import { check, sleep } from 'k6';
import http from 'k6/http';
import exec from 'k6/execution';

// ---------------------------------------------------------------------------
// perf/tests/slo.js - Lab 3: SLOs as code.
//
// This takes the baseline test from Lab 2 and turns it into a real quality gate:
// meaningful checks on every request, thresholds that encode the service's SLOs,
// a health check that aborts the test before it starts if the app is down,
// and automatic chaos reset in teardown().
//
// Usage:
//
//      Clean run (should pass, exit code 0):
//          k6 run -e PROFILE=load perf/tests/slo.js
//
//      Inject a 10% error rate (should abort early, non-zero exit code):
//          k6 run -e PROFILE=load -e CHAOS_ERROR_RATE=0.1 perf/tests/slo.js
//
//      With the app stopped (setup() should abort immediately with a clear message, before any load is generated):
//          k6 run -e PROFILE=smoke perf/test/slo.js
//
// Override -e BASE_URL if the API isn't on the default port.
// Check the exit code afterward with `echo $?` (bash) or `$LASTEXITCODE` (Powershell) to confirm pass/fail.
// ---------------------------------------------------------------------------

const BASE_URL = __ENV.BASE_URL || 'http://localhost:9063';
const PRODUCT_ID_MAX = 10000;
const SEARCH_QUERY = __ENV.SEARCH_QUERY || '';
const MISSING_PRODUCT_ID = 999999999; // guaranteed not to exist - used fr the expected 404 check

const CHAOS = {
    latencyMs: Number(__ENV.CHAOS_LATENCY_MS || 0),
    errorRate: Number(__ENV.CHAOS_ERROR_RATE || 0),
    slowQuery: (__ENV.CHAOS_SLOW_QUERY || 'false') === 'true'
};

const PROFILES = {
    smoke: {
        vus: 1,
        duration: '30s',
    },
    load: {
        stages: [
            { duration: '10s', target: 50 },
            { duration: '1m40s', target: 50 },
            { duration: '10s', target: 0 },
        ],
    },
};

const profileName = __ENV.PROFILE || 'smoke';
const profile = PROFILES[profileName];

if (!profile) {
    throw new Error(`Unknown PROFILE "${profileName}". Use "smoke" or "load"`);
}

// ---------------------------------------------------------------------------
// Requirement 1: SLOs as code.
// ---------------------------------------------------------------------------

export const options = {
    ...profile,
    thresholds: {
        http_req_failed: [
            { threshold: 'rate<0.01', abortOnFail: true, delayAbortEval: '10s' },
        ],
        'http_req_duration{name:GetProductById}': ['p(95)<200'],
        'http_req_duration{name:SearchProducts}': ['p(95)<400'],
        checks: ['rate>0.99'],
    },
};

export function setup() {

    const health = http.get(`${BASE_URL}/health`);

    // ---------------------------------------------------------------------------
    // Requirement 3: verify the app is healthy before generating any load, and
    // abort with a clear message if it isn't.
    // --------------------------------------------------------------------------
    if (health.status !== 200) {
        exec.test.abort(
            `ShopLite is not healthy (status=${health.status})` +
            `${health.error ? `, error="${health.error}"` : ''}` +
            `Aborting before generating any load.`
        );
    }

    const chaosRes = http.post(`${BASE_URL}/admin/chaos`, JSON.stringify(CHAOS), {
        headers: {
            'Content-Type': 'application/json'
        }
    });

    if (chaosRes.status !== 200) {
        console.warn(`Could not set chaos (status ${res.status})` +
            `Continuing with whatever chaos settings are already active on the server.`);
    } else {
        console.log(`Chaos set: latencyMs=${CHAOS.latencyMs}, errorRate=${CHAOS.errorRate}, slowQuery=${CHAOS.slowQuery}`);
    }
}

export default function () {

    // ---------------------------------------------------------------------
    // Requirement 2: meaningful checks (status + a body assertion) on every
    // request, not just a status-code check.
    // ---------------------------------------------------------------------

    const health = http.get(`${BASE_URL}/health`, { tags: { name: 'GetHealth' } });
    check(health,
        {
            'health is 200': (r) => r.status === 200,
            'health body reports UP': (r) => r.json('status') === 'UP',
        });

    const searchRes = http.get(
        `${BASE_URL}/products?page=0&size=20&q=${encodeURIComponent(SEARCH_QUERY)}`,
        { tags: { name: 'SearchProducts' } }
    );
    check(searchRes, {
        'search status is 200': (r) => r.status === 200,
        'search returns an items array': (r) => Array.isArray(r.json('items')),
    });


    const id = Math.floor(Math.random() * PRODUCT_ID_MAX) + 1;
    const product = http.get(`${BASE_URL}/products/${id}`, { tags: { name: 'GetProductById' } });
    check(product,
        {
            'product by id is 200': (r) => r.status === 200,
            'product id matches request': (r) => r.json('id') === id,
        });

    // -----------------------------------------------------------------------
    // Requirement 5: one intentionally expected 404 that does NOT count as a
    // failure. expectedStatuses tells k6 that both 200 and 404 are valid
    // outcomes for this request, so http_req_failed (and therefore the
    // abortOnFail threshold above) isn't polluted by a 404 we caused on
    // purpose. A real 500 from injected chaos would still be marked failed,
    // since 500 isn't in the expected set — only 200/404 are exempted.
    // -----------------------------------------------------------------------
    const missingRes = http.get(`${BASE_URL}/products/${MISSING_PRODUCT_ID}`, {
        tags: { name: 'GetMissingProduct' },
        responseCallback: http.expectedStatuses(200, 404),
    });
    check(missingRes, {
        'missing product returns 404': (r) => r.status === 404,
        'missing product has an error message': (r) => !!r.json('error'),
    });
}

// ---------------------------------------------------------------------------
// Requirement 4: always leave the server clean, including after an
// abortOnFail threshold breach or an exec.test.abort() in setup() — k6 still
// runs teardown() in both cases.
// ---------------------------------------------------------------------------
export function teardown() {
    http.post(`${BASE_URL}/admin/chaos`,
        JSON.stringify({ latencyMs: 0, errorRate: 0, slowQuery: false }),
        { headers: { 'Content-Type': 'application/json' } }
    );
}
