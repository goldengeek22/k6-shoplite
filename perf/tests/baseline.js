import { check, sleep } from 'k6';
import http from 'k6/http';

// ---------------------------------------------------------------------------
// perf/tests/baseline.js - Lab 2: Baseline the API
// ---------------------------------------------------------------------------

const BASE_URL = __ENV.BASE_URL || 'http://localhost:9063';
const PRODUCT_ID_MAX = 10000;
const SEARCH_QUERY = __ENV.SEARCH_QUERY || '';

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

export const options = {
    ...profile,
    thresholds: {
        http_req_failed: ['rate<0.01'],
        'http_req_duration{name:GetHealth}': ['p(95)<100'],
        'http_req_duration{name:ListProducts}': ['p(95)<500'],
        'http_req_duration{name:SearchProducts}': ['p(95)<500'],
        'http_req_duration{name:GetProductById}': ['p(95)<500']
    }
};

export function setup() {
    const res = http.post(`${BASE_URL}/admin/chaos`, JSON.stringify(CHAOS), {
        headers: {
            'Content-Type': 'application/json'
        }
    });

    if (res.status !== 200) {
        console.warn(`Could not set chaos (status ${res.status})` +
            `Continuing with whatever chaos settings are already active on the server.`);
    } else {
        console.log(`Chaos set: latencyMs=${CHAOS.latencyMs}, errorRate=${CHAOS.errorRate}, slowQuery=${CHAOS.slowQuery}`);
    }
}

export default function () {

    // GET /health — exculded from chaos on the server side, so this is your
    // "is the box itself still alive" control metric across all four runs.
    const health = http.get(`${BASE_URL}/health`, { tags: { name: 'GetHealth' } });
    check(health, { 'health is 200': (r) => r.status === 200 });

    // GET /products — plain browse, or search when SEARCH_QUERY is set.
    // Tagged differently because ShopLite's slowQuery chaos only affects the
    // code path taken when a search term (q) is present.
    const isSearch = SEARCH_QUERY.length > 0;
    const listUrl = isSearch
        ? `${BASE_URL}/products?page=0&size=20&q=${encodeURIComponent(SEARCH_QUERY)}`
        : `${BASE_URL}/products?page=0&size=20`;

    const list = http.get(listUrl, { tags: { name: isSearch ? 'SearchProducts' : 'ListProducts' } });
    check(list, { 'product list is 200': (r) => r.status === 200 });

    // GET /products/{id} — random id in the seeded range.
    const id = Math.floor(Math.random() * PRODUCT_ID_MAX) + 1;
    const product = http.get(`${BASE_URL}/products/${id}`, { tags: { name: 'GetProductById' } });
    check(product, { 'product by id is 200': (r) => r.status === 200 });
}

export function teardown() {
    http.post(`${BASE_URL}/admin/chaos`,
        JSON.stringify({ latencyMs: 0, errorRate: 0, slowQuery: false }),
        { headers: { 'Content-Type': 'application/json' } }
    );
}
