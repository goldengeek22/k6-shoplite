# Lab 2 — Baseline Report

Target: ShopLite API (`GET /health`, `GET /products`, `GET /products/{id}`)
Script: `perf/tests/baseline.js`

```bash
k6 run -e PROFILE=smoke --summary-export=reports/baseline-1.json perf/tests/baseline.js
k6 run -e PROFILE=load  --summary-export=reports/baseline-2.json perf/tests/baseline.js
k6 run -e PROFILE=load -e CHAOS_LATENCY_MS=300 --summary-export=reports/baseline-3.json perf/tests/baseline.js
k6 run -e PROFILE=load -e CHAOS_SLOW_QUERY=true -e SEARCH_QUERY=a --summary-export=reports/baseline-4.json perf/tests/baseline.js
```

## Results

| Metric | Run 1: 1 VU, 30s | Run 2: 50 VUs, 2m | Run 3: 50 VUs, 2m, +300ms latency | Run 4: 50 VUs, 2m, slowQuery + q=a |
|---|---|---|---|---|
| `GetHealth` p95 | 3.29 ms | 104.25 ms | 14.30 ms | 33.66 ms |
| `ListProducts` p95 | 21.39 ms | 604.08 ms | 371.95 ms | — (not exercised) |
| `SearchProducts` p95 | — (not exercised) | — (not exercised) | — (not exercised) | 5820.41 ms |
| `GetProductById` p95 | 4.22 ms | 107.51 ms | 318.60 ms | 34.14 ms |
| Overall `http_req_duration` p95 | 18.28 ms | 458.13 ms | 353.54 ms | 5360.68 ms |
| `http_req_failed` rate | 0.00 % | 0.00 % | 0.00 % | 0.00 % |
| `http_reqs`/s (throughput) | 129.07 | 397.84 | 211.00 | 35.36 |
| Total requests | 3,878 | 47,756 | 25,457 | 4,247 |
| Thresholds | all met → exit 0 | breached (`GetHealth`, `ListProducts`) → non-zero | all met → exit 0 | breached (`SearchProducts`) → non-zero |

A few things worth noting while reading this table:

- **Run 2 vs Run 3 throughput:** adding 300ms of latency *dropped* throughput from 397.84 req/s to 211.00 req/s, even though VUs stayed at 50. That's the closed-model effect from Module 6 of the course: with a fixed VU pool, each VU can only start its next iteration once the current one finishes, so added per-request latency directly caps how many iterations/second 50 VUs can produce.
- **Run 2's `GetHealth` p95 (104.25ms) breached its own 100ms threshold** purely from concurrency — `/health` is excluded from the chaos middleware, so this is queueing/contention under 50 VUs, not injected latency.
- **Run 3 stayed under all thresholds** despite the added 300ms latency, because the thresholds were set loosely (500ms) relative to that fixed delay.

## Why `http_req_duration` grew in run 4 but errors didn't

`SearchProducts` p95 jumped to 5,820.41ms in run 4 (average 3,906ms, max 7,044.80ms), a massive increase over anything seen in runs 1–3 — yet `http_req_failed` stayed at exactly 0.00% across all four runs, and every one of the 4,247 requests in run 4 still passed its check. This is exactly the behavior `slowQuery: true` is designed to produce: when a search term is present, ShopLite switches from a prefix match that can use the index on `products.name` to a case-insensitive `ILIKE '%a%'` scan across the unindexed `description` column, forcing PostgreSQL to read and filter far more rows per request. That extra work shows up entirely as added latency, not failure — the query still eventually completes and returns a valid `200` response with matching results. It's also worth noting that the slowdown was isolated to exactly the code path that was supposed to be affected: `GetHealth` (33.66ms) and `GetProductById` (34.14ms) stayed fast in the same run, confirming the chaos setting degraded the search query specifically rather than the server as a whole. Errors would only have appeared here if the added latency had pushed requests past a timeout, exhausted the database connection pool under concurrent load, or the database had run out of capacity to keep up — none of which happened at this VU count. Slowness and failure are separate axes: a query can be arbitrarily slow and still succeed, right up until something with a hard limit (a timeout, a pool, a queue) gets involved.

## `http_req_waiting` vs. `http_req_duration`, in my own words

`http_req_duration` is the total time k6 counts for one HTTP request, from when it starts sending the request to when it finishes receiving the response. `http_req_waiting` is one piece inside that total: specifically the time between k6 finishing sending the request and the first byte of the response coming back — commonly called time to first byte (TTFB). In practice, `http_req_waiting` is mostly a measurement of how long the *server* took to process the request (run the handler, query the database, build the response), while the rest of `http_req_duration` (connecting, TLS handshake, and receiving the body) is mostly about the network and the response size.

Roughly: `http_req_duration ≈ http_req_connecting + http_req_tls_handshaking + http_req_waiting + time to receive the body`. `http_req_waiting` is the metric to watch first when diagnosing a slow backend, because it isolates server-side processing time from everything else in the request/response round trip.
