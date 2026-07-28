#!/bin/bash
# 本地 HTTPS 单域名网关：https://localhost 同时提供 Unity 游戏、问卷前端/API、对战 WS。
# 用于本地完整跑通「问卷配置 → 收集 → 开赛」全链路（与生产 Traefik 拓扑一致）。
#
# 需要先启动两个后端（各开一个终端）：
#   cd Server   && npm start   # 对战服务器  :8080 (WS + HTTP)
#   cd web-app  && npm start    # 问卷 API/前端 :3001 (含 /survey)
# 再运行本脚本，然后浏览器打开 https://localhost
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

# 后端可达性提醒（不阻断，仅提示）：/ws → :8080、/api /survey → :3001
for probe in "对战服务器(:8080)|http://localhost:8080/api/room-status/PROBE0" \
             "问卷 API(:3001)|http://localhost:3001/api/health"; do
  name="${probe%%|*}"; url="${probe##*|}"
  if ! curl -sf -o /dev/null --max-time 1 "$url"; then
    echo "提示：$name 未响应 —— 对应的 /ws 或 /api /survey 路由将不可用（请先启动该后端）。"
  fi
done

echo "首次运行需信任 Caddy 内部 CA（可能要求管理员密码）..."
caddy trust 2>/dev/null || true
echo "启动 https://localhost  (Ctrl+C 停止)"
WEBGL_ROOT="./webgl-build" caddy run --config Caddyfile --adapter caddyfile
