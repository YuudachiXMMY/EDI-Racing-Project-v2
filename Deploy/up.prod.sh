#!/usr/bin/env bash
# Production deploy: the SAME single-origin nginx edge as up.sh, but LAYERS
# docker-compose.prod.yml on top of the base file so the production-only overrides apply —
# most importantly REQUIRE_HOST_TOKEN=true, which authenticates /ws room creation.
#
#   INTERNAL_SECRET=$(openssl rand -hex 32) ./Deploy/up.prod.sh -d
#
# WHY A DEDICATED SCRIPT: bare `up.sh` runs the base docker-compose.yml only, where
# REQUIRE_HOST_TOKEN defaults to false — safe for local dev, but in production it would leave
# WebSocket room creation unauthenticated (the /game/ cookie gate only protects static assets,
# not /ws). Never run up.sh in production; run this instead.
#
# INTERNAL_SECRET is interpolated into BOTH the game and web-app services from this single
# variable, so the two are always identical — the /game/ access gate and the WS host-token
# verify share one secret. Compose fails fast (`:?`) if it is unset.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

if [ ! -d "./webgl-build/Build" ]; then
  echo "未找到 webgl-build/Build —— 请先在 Unity 重新构建 WebGL 再运行本脚本。" >&2
  exit 1
fi

# Fail early with a clear message rather than a raw compose interpolation error.
if [ -z "${INTERNAL_SECRET:-}" ] && ! grep -qE '^[[:space:]]*INTERNAL_SECRET=' .env 2>/dev/null; then
  echo "INTERNAL_SECRET 未设置 —— 生产部署必须提供该密钥。" >&2
  echo "  例如：export INTERNAL_SECRET=\$(openssl rand -hex 32)" >&2
  echo "该密钥会同时注入 game 与 web-app 两个服务，保证 /game/ 访问门禁与 WS host-token 校验使用一致的密钥。" >&2
  exit 1
fi

echo "启动生产部署（叠加 docker-compose.prod.yml：REQUIRE_HOST_TOKEN=true）"
echo "打开 http://localhost:${GAME_PORT:-3900}  (Ctrl+C 停止)"
exec docker compose \
  -f "$SCRIPT_DIR/docker-compose.yml" \
  -f "$SCRIPT_DIR/docker-compose.prod.yml" \
  up --build "$@"
