#!/bin/bash
# Scan Deploy/webgl-build/Build/ for Unity WebGL artifacts
# and patch index.html with the correct file names.
#
# Usage: ./scripts/patch-index-html.sh

set -e

BUILD_DIR="$(cd "$(dirname "$0")/.." && pwd)/Deploy/webgl-build/Build"
INDEX="$(cd "$(dirname "$0")/.." && pwd)/Deploy/webgl-build/index.html"

if [ ! -d "$BUILD_DIR" ]; then
  echo "Error: Build directory not found: $BUILD_DIR"
  exit 1
fi

LOADER=$(ls "$BUILD_DIR"/*.loader.js 2>/dev/null | head -1 | xargs basename)
FRAMEWORK=$(ls "$BUILD_DIR"/*.framework.js 2>/dev/null | head -1 | xargs basename)
DATA=$(ls "$BUILD_DIR"/*.data 2>/dev/null | head -1 | xargs basename)
WASM=$(ls "$BUILD_DIR"/*.wasm 2>/dev/null | head -1 | xargs basename)

if [ -z "$LOADER" ] || [ -z "$FRAMEWORK" ] || [ -z "$DATA" ] || [ -z "$WASM" ]; then
  echo "Error: Missing build artifacts in $BUILD_DIR"
  echo "  loader.js:    ${LOADER:-MISSING}"
  echo "  framework.js: ${FRAMEWORK:-MISSING}"
  echo "  .data:        ${DATA:-MISSING}"
  echo "  .wasm:        ${WASM:-MISSING}"
  exit 1
fi

echo "Detected build artifacts:"
echo "  loader:    $LOADER"
echo "  framework: $FRAMEWORK"
echo "  data:      $DATA"
echo "  wasm:      $WASM"

# Patch index.html using sed
# Replace src="" or src="Build/..." for the loader script tag
sed -i '' -E "s|<script src=\"[^\"]*loader[^\"]*\"></script>|<script src=\"Build/$LOADER\"></script>|" "$INDEX"
sed -i '' -E "s|<script src=\"\"></script>|<script src=\"Build/$LOADER\"></script>|" "$INDEX"

# Replace dataUrl, frameworkUrl, codeUrl values
sed -i '' -E "s|dataUrl: \"[^\"]*\"|dataUrl: \"Build/$DATA\"|" "$INDEX"
sed -i '' -E "s|frameworkUrl: \"[^\"]*\"|frameworkUrl: \"Build/$FRAMEWORK\"|" "$INDEX"
sed -i '' -E "s|codeUrl: \"[^\"]*\"|codeUrl: \"Build/$WASM\"|" "$INDEX"

echo "Patched: $INDEX"
