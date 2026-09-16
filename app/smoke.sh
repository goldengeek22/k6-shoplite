#!/usr/bin/env bash
set -euo pipefail

BASE="${BASE_URL:-http://localhost:9063}"
SUFFIX="$(date +%s)$RANDOM"
PASSWORD="Passw0rd!"
JSON='Content-Type: application/json'

pass() { echo "  ✅ $1"; }
fail() { echo "  ❌ $1"; exit 1; }
expect() { # expected actual label
  if [[ "$1" == "$2" ]]; then pass "$3 ($2)"; else fail "$3: expected $1, got $2"; fi
}
status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

clean() { tr -d '\r'; }

echo "ShopLite smoke test against $BASE"

expect 200 "$(status "$BASE/health")" "Health"

ALICE="alice_$SUFFIX"; BOB="bob_$SUFFIX"
register() { status -X POST "$BASE/auth/register" -H "$JSON" -d "{\"username\":\"$1\",\"password\":\"$PASSWORD\"}"; }
## login()    { curl -s -X POST "$BASE/auth/login" -H "$JSON" -d "{\"username\":\"$1\",\"password\":\"$PASSWORD\"}" | jq -r .token; }
login()    { curl -s -X POST "$BASE/auth/login" -H "$JSON" -d "{\"username\":\"$1\",\"password\":\"$PASSWORD\"}" | jq -r .token | clean; }

expect 201 "$(register "$ALICE")" "Register alice"
expect 409 "$(register "$ALICE")" "Duplicate username rejected"
expect 201 "$(register "$BOB")"   "Register bob"
expect 400 "$(status -X POST "$BASE/auth/register" -H "$JSON" -d '{"username":"x","password":"1"}')" "Invalid registration rejected"

A_TOKEN="$(login "$ALICE")"; B_TOKEN="$(login "$BOB")"
[[ -n "$A_TOKEN" && "$A_TOKEN" != "null" ]] && pass "Login returns token" || fail "Login did not return a token"
expect 401 "$(status -X POST "$BASE/auth/login" -H "$JSON" -d "{\"username\":\"$ALICE\",\"password\":\"wrong\"}")" "Wrong password rejected"

#RESPONSE="$(curl -s "$BASE/products?page=0&size=5")"
#TOTAL="$(jq -r '.total // "MISSING"' <<<"$RESPONSE")"

RESPONSE="$(curl -s "$BASE/products?page=0&size=5")"
TOTAL="$(jq -r '.total // "MISSING"' <<<"$RESPONSE" | clean)"

if [[ "$TOTAL" =~ ^[0-9]+$ ]] && (( TOTAL >= 10000 )); then
  pass "Products seeded ($TOTAL)"
else
  fail "Expected >= 10000 products, got '$TOTAL'. Raw response: $RESPONSE"
fi
expect 200 "$(status "$BASE/products?q=Blue&size=5")" "Search by prefix"
expect 200 "$(status "$BASE/products/42")" "Product by id"
expect 404 "$(status "$BASE/products/999999999")" "Unknown product"
expect 400 "$(status "$BASE/products?size=1000")" "Oversized page rejected"

expect 401 "$(status "$BASE/cart")" "Cart requires auth"

A_AUTH=(-H "Authorization: Bearer $A_TOKEN")
B_AUTH=(-H "Authorization: Bearer $B_TOKEN")

expect 200 "$(status -X POST "$BASE/cart/items" "${A_AUTH[@]}" -H "$JSON" -d '{"productId":42,"quantity":2}')" "Add product 42"
expect 200 "$(status -X POST "$BASE/cart/items" "${A_AUTH[@]}" -H "$JSON" -d '{"productId":7,"quantity":1}')"  "Add product 7"
expect 404 "$(status -X POST "$BASE/cart/items" "${A_AUTH[@]}" -H "$JSON" -d '{"productId":999999999,"quantity":1}')" "Add unknown product"

LINES="$(curl -s "$BASE/cart" "${A_AUTH[@]}" | jq '.items | length' | clean)"
expect 2 "$LINES" "Cart has 2 lines"

ORDER_RESPONSE="$(curl -s -w '\n%{http_code}' -X POST "$BASE/orders" "${A_AUTH[@]}")"
expect 201 "$(tail -n1 <<<"$ORDER_RESPONSE")" "Create order"
#ORDER_ID="$(head -n1 <<<"$ORDER_RESPONSE" | jq .orderId)"
ORDER_ID="$(head -n1 <<<"$ORDER_RESPONSE" | jq .orderId | clean)"

expect 200 "$(status "$BASE/orders/$ORDER_ID" "${A_AUTH[@]}")" "Owner can read order $ORDER_ID"
expect 403 "$(status "$BASE/orders/$ORDER_ID" "${B_AUTH[@]}")" "Other user forbidden"
expect 400 "$(status -X POST "$BASE/orders" "${A_AUTH[@]}")" "Empty cart order rejected"

LINES_AFTER="$(curl -s "$BASE/cart" "${A_AUTH[@]}" | jq '.items | length' | clean)"
expect 0 "$LINES_AFTER" "Cart cleared after order"

if [[ "$(status "$BASE/admin/chaos")" == "200" ]]; then
  curl -s -o /dev/null -X POST "$BASE/admin/chaos" -H "$JSON" -d '{"latencyMs":0,"errorRate":1.0,"slowQuery":false}'
  expect 500 "$(status "$BASE/products/1")" "Chaos errorRate=1.0 injects 500"
  expect 200 "$(status "$BASE/health")" "Health excluded from chaos"
  curl -s -o /dev/null -X POST "$BASE/admin/chaos" -H "$JSON" -d '{"latencyMs":0,"errorRate":0,"slowQuery":false}'
  expect 200 "$(status "$BASE/products/1")" "Chaos reset"
fi

echo "All smoke checks passed."
