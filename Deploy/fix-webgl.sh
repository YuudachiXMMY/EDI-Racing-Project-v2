#!/bin/bash
#
# Deploy/fix-webgl.sh — Validate WebGL build output (and optionally serve it)
#
# What it does:
#   1. Validates that Build artifacts exist in Deploy/webgl-build/Build/
#   2. Verifies index.html references match the real artifact filenames and are
#      non-empty. It does NOT patch index.html — the WebGL template
#      (Assets/WebGLTemplates/EDIRacing/index.html) now uses Unity 6 macros
#      ({{{ LOADER_FILENAME }}} etc.), so a correct build needs no patching.
#      A mismatch here means the template regressed; fix the template and rebuild.
#   3. Optionally starts a local HTTP server on port 8080 for testing
#
# Usage:
#   ./Deploy/fix-webgl.sh          # validate only
#   ./Deploy/fix-webgl.sh --serve  # validate + start local server on :8080

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/webgl-build/Build"
INDEX="$SCRIPT_DIR/webgl-build/index.html"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo ""
echo "=== EDI Racing — WebGL Build Validator ==="
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

# Match compressed variants too: Brotli w/ decompression fallback -> *.unityweb
LOADER=$(find "$BUILD_DIR" -maxdepth 1 -name '*.loader.js' | head -1)
FRAMEWORK=$(find "$BUILD_DIR" -maxdepth 1 \( -name '*.framework.js' -o -name '*.framework.js.br' -o -name '*.framework.js.gz' -o -name '*.framework.js.unityweb' \) | head -1)
DATA=$(find "$BUILD_DIR" -maxdepth 1 \( -name '*.data' -o -name '*.data.br' -o -name '*.data.gz' -o -name '*.data.unityweb' \) | head -1)
WASM=$(find "$BUILD_DIR" -maxdepth 1 \( -name '*.wasm' -o -name '*.wasm.br' -o -name '*.wasm.gz' -o -name '*.wasm.unityweb' \) | head -1)

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

# ── Step 2: Validate index.html references ──────────────────────

echo ""
echo "Checking index.html references..."

# Empty URLs indicate the template macros failed to resolve.
if grep -q 'src=""' "$INDEX" || grep -Eq '(dataUrl|frameworkUrl|codeUrl): ""' "$INDEX"; then
  echo -e "${RED}Error: index.html contains empty URLs — template macros failed to resolve.${NC}"
  echo "  Check Assets/WebGLTemplates/EDIRacing/index.html (must use Unity 6 macros"
  echo "  like {{{ LOADER_FILENAME }}}) and rebuild."
  exit 1
fi

LOADER_NAME=$(basename "$LOADER")
FRAMEWORK_NAME=$(basename "$FRAMEWORK")
DATA_NAME=$(basename "$DATA")
WASM_NAME=$(basename "$WASM")

MISMATCH=0
for NAME_LABEL in "$LOADER_NAME:loader" "$FRAMEWORK_NAME:framework" "$DATA_NAME:data" "$WASM_NAME:wasm"; do
  NAME="${NAME_LABEL%%:*}"
  LABEL="${NAME_LABEL#*:}"
  if grep -q "$NAME" "$INDEX"; then
    echo -e "  ${GREEN}OK${NC}      $LABEL -> $NAME"
  else
    echo -e "  ${RED}MISMATCH${NC}  $LABEL — '$NAME' not referenced in index.html"
    MISMATCH=1
  fi
done

if [ $MISMATCH -eq 1 ]; then
  echo ""
  echo -e "${RED}index.html does not match the build artifacts.${NC}"
  echo "  Check the WebGL template (Assets/WebGLTemplates/EDIRacing/index.html) and rebuild."
  echo "  This script no longer patches index.html — the template must produce correct output."
  exit 1
fi

echo ""
echo -e "${GREEN}index.html references are valid.${NC}"

# ── Step 3: Optionally serve locally ────────────────────────────

if [ "$1" = "--serve" ]; then
  PORT="${2:-8080}"

  if ls "$SCRIPT_DIR/webgl-build/Build/"*.br >/dev/null 2>&1; then
    echo -e "${YELLOW}检测到 .br 产物：原生 Brotli 需 HTTPS，Python HTTP 无法正确测试。${NC}"
    echo "  请改用: ./Deploy/serve-local-https.sh (Caddy HTTPS)"
  fi

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
