#!/bin/bash
# Build Unity WebGL and copy output to Deploy/webgl-build/
# Requires Unity 6 with an activated license.
#
# Usage:
#   ./scripts/build-webgl.sh
#   UNITY_PATH=/path/to/Unity ./scripts/build-webgl.sh

set -e

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_PATH="$PROJECT_PATH/Deploy/webgl-build"

# Auto-detect Unity on macOS
if [ -z "$UNITY_PATH" ]; then
  UNITY_PATH=$(ls -d /Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity 2>/dev/null | tail -1)
fi

if [ -z "$UNITY_PATH" ] || [ ! -f "$UNITY_PATH" ]; then
  echo "Error: Unity not found. Set UNITY_PATH environment variable."
  exit 1
fi

echo "Unity: $UNITY_PATH"
echo "Project: $PROJECT_PATH"
echo "Output: $BUILD_PATH"

rm -rf "$BUILD_PATH"
mkdir -p "$BUILD_PATH"

"$UNITY_PATH" \
  -batchmode \
  -nographics \
  -projectPath "$PROJECT_PATH" \
  -executeMethod BuildScript.BuildWebGL \
  -buildTarget WebGL \
  -logFile - \
  -quit

echo "Build complete: $BUILD_PATH"
