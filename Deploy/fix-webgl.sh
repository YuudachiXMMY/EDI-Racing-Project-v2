#!/bin/bash
#
# Deploy/fix-webgl.sh — Fix WebGL build stuck at loading
#
# What it does:
#   1. Validates that Build artifacts exist in Deploy/webgl-build/Build/
#   2. Patches index.html with correct filenames (loader, framework, data, wasm)
#   3. Optionally starts a local HTTP server on port 8080 for testing
#
# Usage:
#   ./Deploy/fix-webgl.sh          # patch only
#   ./Deploy/fix-webgl.sh --serve  # patch + start local server on :8080

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/webgl-build/Build"
INDEX="$SCRIPT_DIR/webgl-build/index.html"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo ""
echo "=== EDI Racing — WebGL Build Fixer ==="
echo ""

# ── Step 1: Validate build artifacts ────────────────────────────

if [ ! -d "$BUILD_DIR" ]; then
  echo -e "${RED}Error: Build directory not found: $BUILD_DIR${NC}"
  echo "  Run a WebGL build in Unity first, then copy output to Deploy/webgl-build/"
  exit 1
fi

if [ ! -f "$INDEX" ]; then
  echo -e "${RED}Error: index.html not found: $INDEX${NC}"
  exit 1
fi

LOADER=$(find "$BUILD_DIR" -maxdepth 1 -name '*.loader.js' | head -1)
FRAMEWORK=$(find "$BUILD_DIR" -maxdepth 1 -name '*.framework.js' -o -name '*.framework.js.br' -o -name '*.framework.js.gz' | head -1)
DATA=$(find "$BUILD_DIR" -maxdepth 1 \( -name '*.data' -o -name '*.data.br' -o -name '*.data.gz' \) | head -1)
WASM=$(find "$BUILD_DIR" -maxdepth 1 \( -name '*.wasm' -o -name '*.wasm.br' -o -name '*.wasm.gz' \) | head -1)

MISSING=0
for LABEL_FILE in "loader.js:$LOADER" "framework.js:$FRAMEWORK" ".data:$DATA" ".wasm:$WASM"; do
  LABEL="${LABEL_FILE%%:*}"
  FILE="${LABEL_FILE#*:}"
  if [ -z "$FILE" ]; then
    echo -e "  ${RED}MISSING${NC}  $LABEL"
    MISSING=1
  else
    echo -e "  ${GREEN}OK${NC}      $(basename "$FILE")"
  fi
done

if [ $MISSING -eq 1 ]; then
  echo ""
  echo -e "${RED}Some build artifacts are missing. Rebuild in Unity (File > Build Settings > WebGL > Build).${NC}"
  exit 1
fi

# ── Step 2: Patch index.html ────────────────────────────────────

LOADER_NAME=$(basename "$LOADER")
FRAMEWORK_NAME=$(basename "$FRAMEWORK")
DATA_NAME=$(basename "$DATA")
WASM_NAME=$(basename "$WASM")

# Check current state
if grep -q 'src=""' "$INDEX" || grep -q 'dataUrl: ""' "$INDEX"; then
  echo ""
  echo -e "${YELLOW}index.html has empty URLs — patching now...${NC}"
else
  echo ""
  echo "Checking index.html references are up-to-date..."
fi

# Use a temp file for sed portability (macOS vs Linux)
TMP="$INDEX.tmp"
cp "$INDEX" "$TMP"

# Patch loader script src
sed -E "s|<script src=\"[^\"]*\"></script>|<script src=\"Build/$LOADER_NAME\"></script>|" "$TMP" > "$INDEX"
cp "$INDEX" "$TMP"

# Patch dataUrl
sed -E "s|dataUrl: \"[^\"]*\"|dataUrl: \"Build/$DATA_NAME\"|" "$TMP" > "$INDEX"
cp "$INDEX" "$TMP"

# Patch frameworkUrl
sed -E "s|frameworkUrl: \"[^\"]*\"|frameworkUrl: \"Build/$FRAMEWORK_NAME\"|" "$TMP" > "$INDEX"
cp "$INDEX" "$TMP"

# Patch codeUrl
sed -E "s|codeUrl: \"[^\"]*\"|codeUrl: \"Build/$WASM_NAME\"|" "$TMP" > "$INDEX"

rm -f "$TMP"

echo -e "${GREEN}Patched index.html:${NC}"
echo "  loader:    Build/$LOADER_NAME"
echo "  framework: Build/$FRAMEWORK_NAME"
echo "  data:      Build/$DATA_NAME"
echo "  wasm:      Build/$WASM_NAME"

# ── Step 3: Optionally serve locally ────────────────────────────

if [ "$1" = "--serve" ]; then
  PORT="${2:-8080}"
  echo ""
  echo "Starting local server on http://localhost:$PORT ..."
  echo "Press Ctrl+C to stop."
  echo ""

  cd "$SCRIPT_DIR/webgl-build"

  # Prefer Python 3, fall back to Python 2, then Node
  if command -v python3 &>/dev/null; then
    python3 -m http.server "$PORT"
  elif command -v python &>/dev/null; then
    python -m SimpleHTTPServer "$PORT"
  elif command -v npx &>/dev/null; then
    npx serve -l "$PORT" .
  else
    echo -e "${RED}No HTTP server found. Install Python 3 or Node.js.${NC}"
    exit 1
  fi
fi
