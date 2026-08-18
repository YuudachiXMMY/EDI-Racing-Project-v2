#!/bin/bash
#
# scripts/verify-webgl-docker.sh — Smoke test WebGL deployment via Docker
#
# What it does:
#   1. Runs verify-webgl-build.sh first (artifact check)
#   2. Builds Docker image (standalone, no compose dependencies)
#   3. Starts container on port 8099
#   4. Verifies HTTP 200 on index.html
#   5. Checks critical static assets served with correct MIME types
#   6. Tears down container
#
# Usage:
#   ./scripts/verify-webgl-docker.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

CONTAINER_NAME="edi-racing-verify-$$"
PORT=8099
ERRORS=0

check_pass() { echo -e "  ${GREEN}PASS${NC}  $1"; }
check_fail() { echo -e "  ${RED}FAIL${NC}  $1"; ERRORS=$((ERRORS + 1)); }

cleanup() {
  echo ""
  echo "── Cleanup ──"
  docker stop "$CONTAINER_NAME" >/dev/null 2>&1 && echo "  Stopped container" || true
  docker rm "$CONTAINER_NAME" >/dev/null 2>&1 && echo "  Removed container" || true
}
trap cleanup EXIT

echo ""
echo "=== EDI Racing — Docker Smoke Test ==="
echo ""

# ── Step 1: Run artifact verification ─────────────────────────────

echo "── Step 1: Artifact Verification ──"
if ! "$SCRIPT_DIR/verify-webgl-build.sh" "$PROJECT_DIR/Deploy/webgl-build"; then
  echo -e "${RED}Artifact verification failed — aborting Docker test${NC}"
  exit 1
fi

# ── Step 2: Build Docker image ────────────────────────────────────

echo ""
echo "── Step 2: Build Docker Image ──"
docker build -t edi-racing-verify -f "$PROJECT_DIR/Deploy/Dockerfile" "$PROJECT_DIR" -q
check_pass "Docker image built"

# ── Step 3: Start container ───────────────────────────────────────

echo ""
echo "── Step 3: Start Container ──"
docker run -d --name "$CONTAINER_NAME" -p "$PORT:80" -e PORT=3000 edi-racing-verify >/dev/null
check_pass "Container started on port $PORT"

# Wait for readiness
echo "  Waiting for server..."
READY=0
for i in $(seq 1 15); do
  if curl -sf "http://localhost:$PORT/" >/dev/null 2>&1; then
    READY=1
    break
  fi
  sleep 1
done

if [ $READY -eq 0 ]; then
  check_fail "Server did not respond within 15s"
  echo -e "${RED}Aborting${NC}"
  exit 1
fi
check_pass "Server responding"

# ── Step 4: Verify HTTP responses ─────────────────────────────────

echo ""
echo "── Step 4: HTTP Verification ──"

# index.html returns 200 and contains unity-canvas
HTTP_CODE=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$PORT/")
if [ "$HTTP_CODE" = "200" ]; then
  check_pass "GET / → $HTTP_CODE"
else
  check_fail "GET / → $HTTP_CODE (expected 200)"
fi

BODY=$(curl -s "http://localhost:$PORT/")
if echo "$BODY" | grep -q 'unity-canvas'; then
  check_pass "Response contains unity-canvas"
else
  check_fail "Response missing unity-canvas element"
fi

# Check loader.js is accessible
LOADER_FILE=$(curl -s "http://localhost:$PORT/" | grep -oE 'Build/[^"]+\.loader\.js' | head -1)
if [ -n "$LOADER_FILE" ]; then
  LOADER_CODE=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$PORT/$LOADER_FILE")
  if [ "$LOADER_CODE" = "200" ]; then
    check_pass "GET /$LOADER_FILE → $LOADER_CODE"
  else
    check_fail "GET /$LOADER_FILE → $LOADER_CODE (expected 200)"
  fi
else
  check_fail "Could not find loader.js reference in HTML"
fi

# Check .wasm MIME type
WASM_FILE=$(curl -s "http://localhost:$PORT/" | grep -oE 'Build/[^"]+\.wasm' | head -1)
if [ -n "$WASM_FILE" ]; then
  WASM_TYPE=$(curl -sI "http://localhost:$PORT/$WASM_FILE" | grep -i 'content-type' | tr -d '\r')
  if echo "$WASM_TYPE" | grep -qi 'application/wasm'; then
    check_pass "WASM Content-Type: application/wasm"
  else
    check_warn "WASM Content-Type: $WASM_TYPE (expected application/wasm)"
  fi
fi

# ── Summary ───────────────────────────────────────────────────────

echo ""
echo "────────────────────────────────────"
if [ $ERRORS -eq 0 ]; then
  echo -e "${GREEN}Docker smoke test passed${NC}"
  exit 0
else
  echo -e "${RED}$ERRORS check(s) failed${NC}"
  exit 1
fi
