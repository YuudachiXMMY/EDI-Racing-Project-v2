#!/bin/bash
# 本地 HTTPS 测试原生 Brotli（关闭 Decompression Fallback 后的 .br 产物）。
# 用法: ./Deploy/serve-local-https.sh   然后浏览器打开 https://localhost
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

if ! command -v caddy >/dev/null 2>&1; then
  echo "需要 Caddy：brew install caddy"; exit 1
fi
if [ ! -d "./webgl-build/Build" ]; then
  echo "未找到 webgl-build/Build，请先在 Unity 重新构建 WebGL。"; exit 1
fi
if ls ./webgl-build/Build/*.unityweb >/dev/null 2>&1; then
  echo "警告：检测到 .unityweb 产物 —— Decompression Fallback 似乎仍开启。"
  echo "  原生 Brotli 需要 .br 产物；请确认已关闭 fallback 并重新构建。"
fi

echo "首次运行需信任 Caddy 内部 CA（可能要求管理员密码）..."
caddy trust 2>/dev/null || true
echo "启动 https://localhost  (Ctrl+C 停止)"
WEBGL_ROOT="./webgl-build" caddy run --config Caddyfile --adapter caddyfile
