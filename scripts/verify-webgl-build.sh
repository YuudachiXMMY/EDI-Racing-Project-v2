#!/bin/bash
#
# scripts/verify-webgl-build.sh — Verify WebGL build artifacts are complete and valid
#
# Checks:
#   1. Build directory exists with required artifacts
#   2. File sizes are reasonable (not empty/corrupt)
#   3. index.html references correct file paths
#   4. All referenced files actually exist
#   5. WebGL template was applied correctly
#
# Usage:
#   ./scripts/verify-webgl-build.sh [BUILD_DIR]
#   BUILD_DIR defaults to Deploy/webgl-build

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="${1:-$PROJECT_DIR/Deploy/webgl-build}"
BUILD_SUBDIR="$BUILD_DIR/Build"
INDEX="$BUILD_DIR/index.html"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

ERRORS=0
WARNINGS=0

check_pass() { echo -e "  ${GREEN}PASS${NC}  $1"; }
check_fail() { echo -e "  ${RED}FAIL${NC}  $1"; ERRORS=$((ERRORS + 1)); }
check_warn() { echo -e "  ${YELLOW}WARN${NC}  $1"; WARNINGS=$((WARNINGS + 1)); }

echo ""
echo "=== EDI Racing — WebGL Build Verification ==="
echo "Build dir: $BUILD_DIR"
echo ""

# ── Check 1: Build directory and required artifacts ────────────────

echo "── Artifact Existence ──"

if [ ! -d "$BUILD_DIR" ]; then
  check_fail "Build directory not found: $BUILD_DIR"
  echo "  Run a WebGL build first: ./scripts/build-webgl.sh"
  echo ""
  echo -e "${RED}1 check failed${NC}"
  exit 1
fi

if [ ! -d "$BUILD_SUBDIR" ]; then
  check_fail "Build/  subdirectory not found"
  echo ""
  echo -e "${RED}1 check failed${NC}"
  exit 1
fi

LOADER=$(find "$BUILD_SUBDIR" -maxdepth 1 -name '*.loader.js' | head -1)
FRAMEWORK=$(find "$BUILD_SUBDIR" -maxdepth 1 \( -name '*.framework.js' -o -name '*.framework.js.br' -o -name '*.framework.js.gz' \) | head -1)
DATA=$(find "$BUILD_SUBDIR" -maxdepth 1 \( -name '*.data' -o -name '*.data.br' -o -name '*.data.gz' \) | head -1)
WASM=$(find "$BUILD_SUBDIR" -maxdepth 1 \( -name '*.wasm' -o -name '*.wasm.br' -o -name '*.wasm.gz' \) | head -1)

[ -n "$LOADER" ]    && check_pass "loader.js:    $(basename "$LOADER")"    || check_fail "loader.js not found"
[ -n "$FRAMEWORK" ] && check_pass "framework.js: $(basename "$FRAMEWORK")" || check_fail "framework.js not found"
[ -n "$DATA" ]      && check_pass "data:         $(basename "$DATA")"      || check_fail ".data file not found"
[ -n "$WASM" ]      && check_pass "wasm:         $(basename "$WASM")"      || check_fail ".wasm file not found"
[ -f "$INDEX" ]     && check_pass "index.html"                             || check_fail "index.html not found"

if [ $ERRORS -gt 0 ]; then
  echo ""
  echo -e "${RED}$ERRORS checks failed${NC} — build artifacts incomplete"
  exit 1
fi

# ── Check 2: File sizes ────────────────────────────────────────────

echo ""
echo "── File Sizes ──"

# Cross-platform file size (works on macOS and Linux)
file_size() { wc -c < "$1" | tr -d ' '; }

check_min_size() {
  local file="$1" label="$2" min_bytes="$3"
  local size
  size=$(file_size "$file")
  if [ "$size" -lt "$min_bytes" ]; then
    check_fail "$label: ${size} bytes (expected >= ${min_bytes})"
  else
    local human
    if [ "$size" -gt 1048576 ]; then
      human="$(( size / 1048576 )) MB"
    elif [ "$size" -gt 1024 ]; then
      human="$(( size / 1024 )) KB"
    else
      human="${size} B"
    fi
    check_pass "$label: $human"
  fi
}

check_min_size "$LOADER"    "loader.js"    1024        # > 1 KB
check_min_size "$FRAMEWORK" "framework.js" 10240       # > 10 KB
check_min_size "$WASM"      "wasm"         1048576     # > 1 MB
check_min_size "$DATA"      "data"         1048576     # > 1 MB
check_min_size "$INDEX"     "index.html"   200         # > 200 B

# ── Check 3: index.html references valid paths ────────────────────

echo ""
echo "── HTML References ──"

# Extract referenced Build/ paths from index.html
REFS=$(grep -oE 'Build/[^"]+' "$INDEX" || true)

if [ -z "$REFS" ]; then
  check_fail "index.html contains no Build/ references"
else
  REF_ERRORS=0
  while IFS= read -r ref; do
    FULL_PATH="$BUILD_DIR/$ref"
    if [ -f "$FULL_PATH" ]; then
      check_pass "ref: $ref"
    else
      check_fail "ref: $ref (file not found)"
      REF_ERRORS=$((REF_ERRORS + 1))
    fi
  done <<< "$REFS"

  if [ $REF_ERRORS -gt 0 ]; then
    check_warn "Run Deploy/fix-webgl.sh to patch index.html references"
  fi
fi

# ── Check 4: Template validation ──────────────────────────────────

echo ""
echo "── Template ──"

if grep -q 'unity-canvas' "$INDEX"; then
  check_pass "Unity canvas element present"
else
  check_fail "Missing unity-canvas element"
fi

if grep -q 'createUnityInstance' "$INDEX"; then
  check_pass "createUnityInstance call present"
else
  check_fail "Missing createUnityInstance call"
fi

if grep -q 'loading-overlay\|progress-bar\|Loading' "$INDEX"; then
  check_pass "Loading UI present"
else
  check_warn "No loading overlay detected"
fi

# ── Check 5: Compression consistency ─────────────────────────────

echo ""
echo "── Compression ──"

HAS_BR=$(find "$BUILD_SUBDIR" -maxdepth 1 -name '*.br' | head -1)
HAS_GZ=$(find "$BUILD_SUBDIR" -maxdepth 1 -name '*.gz' | head -1)
HAS_RAW_WASM=$(find "$BUILD_SUBDIR" -maxdepth 1 -name '*.wasm' ! -name '*.br' ! -name '*.gz' | head -1)

if [ -n "$HAS_BR" ]; then
  check_pass "Brotli compressed artifacts detected"
elif [ -n "$HAS_GZ" ]; then
  check_pass "Gzip compressed artifacts detected"
elif [ -n "$HAS_RAW_WASM" ]; then
  check_warn "Uncompressed build — consider enabling Brotli for smaller downloads"
else
  check_warn "Could not determine compression format"
fi

# ── Summary ───────────────────────────────────────────────────────

echo ""
echo "────────────────────────────────────"
if [ $ERRORS -eq 0 ]; then
  echo -e "${GREEN}All checks passed${NC} ($WARNINGS warning(s))"
  exit 0
else
  echo -e "${RED}$ERRORS check(s) failed${NC} ($WARNINGS warning(s))"
  exit 1
fi
