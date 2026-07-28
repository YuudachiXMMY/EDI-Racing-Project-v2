#!/usr/bin/env bash
# One-command local deploy: build + run Game + WebSocket Server + Web App behind
# the single-origin nginx edge. Mirrors serve-local-https.sh ergonomics.
#
#   ./Deploy/up.sh              # foreground, build + up
#   ./Deploy/up.sh -d           # detached
# Then open http://localhost:${GAME_PORT:-8080}
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

if [ ! -d "./webgl-build/Build" ]; then
  echo "未找到 webgl-build/Build —— 请先在 Unity 重新构建 WebGL 再运行本脚本。" >&2
  exit 1
fi

echo "启动统一部署：Game + WebSocket Server + Web App"
echo "打开 http://localhost:${GAME_PORT:-8080}  (Ctrl+C 停止)"
exec docker compose -f "$SCRIPT_DIR/docker-compose.yml" up --build "$@"
