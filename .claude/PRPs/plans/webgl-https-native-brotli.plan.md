# Plan: 上 HTTPS + 关闭 Decompression Fallback + 原生 Brotli（本地 Caddy 测试 → 生产 Traefik）

## Summary
把 WebGL 从「JS 解压 fallback（`.unityweb`）」升级到「服务器原生 Brotli（`.br`）」，让浏览器原生解压那 179MB 的 `.data`，显著缩短加载时间。核心前提：**原生 Brotli 要求 HTTPS**（含本地——`http://localhost` 不可靠）。本地用临时 **Caddy** 反代提供自动 HTTPS + 预压缩 `.br` 服务来验证；生产沿用现有 **Traefik + Let's Encrypt** 终结 TLS。同时修复 `nginx.conf` 中一个关闭 fallback 后才会暴露的 location 顺序 bug。

## User Story
As a EDI Racing 的开发/运维，
I want WebGL 资源经 HTTPS 原生 Brotli 传输、浏览器原生解压，
So that 课堂学生浏览器加载 179MB 赛车资源更快，且本地能一键复现验证。

## Problem → Solution
**现状**：`webGLDecompressionFallback: 1` → 产物 `.unityweb` → loader.js 里 JS Brotli 解压器在客户端解压（慢）；nginx 的 `.br` 块是死代码。
**目标**：关 fallback → 产物 `.br` → 经 HTTPS 由服务器带 `Content-Encoding: br` 传输 → 浏览器原生解压（快）。本地 Caddy 验证，生产 Traefik TLS。

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A（源自本会话排查 + 用户选定「方案 B」）
- **PRD Phase**: N/A（standalone）
- **Estimated Files**: 3 配置改 + 1 nginx 修 + 2 新增（Caddyfile、本地脚本）+ 1 次 Unity 重建 + 生产 Traefik labels

---

## 关键技术约束（已联网核实，不可绕过）

| 约束 | 证据 | 影响 |
|---|---|---|
| Chrome/Firefox 只在 **HTTPS** 下才在 `Accept-Encoding` 广告 `br` | Unity 官方文档 + Chrome/Mozilla issue | 明文 HTTP 服务 `.br` → 浏览器报 `Unable to parse ....br` 加载失败 |
| `http://localhost` **不可靠** | Firefox bugzilla 1670675：localhost 明文不发 `Accept-Encoding: br` | 本地也必须 HTTPS，不能用 `python -m http.server` |
| 原生解压比 JS fallback 快 | Unity 官方文档 | 这正是升级动机 |
| `localhost`/`*.localhost` 是可信来源，Caddy/mkcert 可签本地受信证书 | — | 本地 HTTPS 可零警告实现 |

> GOTCHA: 关闭 fallback 后，产物**只能**由发送 `Content-Encoding: br` 的 HTTPS 服务器提供。任何明文 HTTP、`file://`、`python -m http.server`、`npx serve` 都会加载失败——这是取舍，非缺陷。

---

## UX Design

### Before
```
浏览器 --HTTP--> nginx:80 返回 *.data.unityweb (Brotli 字节, 无编码头)
                          │
                loader.js 内置 JS Brotli 解压器解压 (慢)
                          ▼
                     Unity 启动
```

### After（本地）
```
浏览器 --HTTPS(广告 br)--> Caddy(本地内部CA) 返回 *.data.br + Content-Encoding: br
                                     │
                          浏览器原生解压 (快)
                                     ▼
                                Unity 启动
```

### After（生产）
```
浏览器 --HTTPS(广告 br)--> Traefik(Let's Encrypt, 终结TLS) --HTTP,透传Accept-Encoding--> nginx:80
                                                                        │ 命中 *.br 块，回传预压缩字节 + Content-Encoding: br
                                                          浏览器原生解压 ▼
                                                                   Unity 启动
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| 产物文件名 | `*.data.unityweb` | `*.data.br` | Unity 模板宏自动生成正确 index.html 引用 |
| 本地测试 | `fix-webgl.sh --serve`（Python HTTP） | `Deploy/serve-local-https.sh`（Caddy HTTPS） | Python 方案对 `.br` 无效，已弃用于此场景 |
| 生产传输 | HTTP + JS 解压 | HTTPS + 原生解压 | Traefik 加 TLS，nginx 修 `.br` location |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `ProjectSettings/ProjectSettings.asset` | 812-816 | 全局 `webGLDecompressionFallback`（改 `1→0`） |
| P0 | `Assets/Settings/Build Profiles/Web - Desktop - Development.asset` | 852-856 | profile 覆盖（YAML 字符串行，格式特殊） |
| P0 | `Assets/Settings/Build Profiles/Web - Mobile - Development.asset` | 836-840 | 同上，Mobile |
| P0 | `Deploy/nginx/nginx.conf` | 39-73 | `.br` location 顺序 bug + 缓存/MIME 需重构 |
| P1 | `Deploy/docker-compose.yml` | 27-33 | Traefik labels（生产加 TLS） |
| P1 | `Deploy/fix-webgl.sh` | 111-133 | `--serve` Python 方案（标注对 `.br` 无效） |
| P2 | `Assets/Scripts/Editor/BuildScript.cs` | 58-100 | 产物校验通配符已兼容 `.br`，不改 |
| P2 | `scripts/verify-webgl-build.sh` | 60-68 | 已支持 `.br`（本会话已改），不改 |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Brotli 需 HTTPS | Unity Manual: WebGL compressed builds | 关 fallback 后必须 HTTPS + `Content-Encoding: br` |
| localhost 不发 br | Firefox bugzilla 1670675 | 本地也要真 HTTPS，不能靠 `http://localhost` |
| Caddy 预压缩/内部CA | Caddy `file_server` / `local_certs` / `caddy trust` | localhost 自动 HTTPS；但 `.br` 直接请求需手动加 `Content-Encoding` |
| Traefik TLS | Traefik router labels `tls`/`certresolver` | 生产在 Traefik 层加证书，nginx 不终结 TLS |

---

## Patterns to Mirror

### CONFIG_GLOBAL（ProjectSettings.asset:816，2 空格缩进，普通 YAML）
```
  webGLDecompressionFallback: 1
```

### CONFIG_PROFILE（两个 build profile，m_PlayerSettingsYaml 字符串行；4 空格 + `- line: '|   ` 前缀 + 单引号）
```
    - line: '|   webGLDecompressionFallback: 1'
```

### NGINX_BR_CURRENT（nginx.conf:39-73，当前有 bug 的段落——通用 `\.br$` 抢先匹配，且各 add_header 因「赢者通吃」无法叠加）
```nginx
        # Unity WebGL Brotli-compressed files
        location ~ \.br$ {
            add_header Content-Encoding br;
            default_type application/octet-stream;
        }
        location ~ \.js\.br$ {
            add_header Content-Encoding br;
            default_type application/javascript;
        }
        location ~ \.wasm\.br$ {
            add_header Content-Encoding br;
            default_type application/wasm;
        }
        location ~ \.data\.br$ {
            add_header Content-Encoding br;
            default_type application/octet-stream;
        }

        # Correct MIME types for uncompressed Unity files
        location ~ \.wasm$ {
            default_type application/wasm;
        }

        location ~ \.data$ {
            default_type application/octet-stream;
        }

        # Cache static assets
        location ~* \.(js|wasm|data|br|unityweb)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `ProjectSettings/ProjectSettings.asset` | UPDATE | 全局 `webGLDecompressionFallback: 1→0` |
| `Assets/Settings/Build Profiles/Web - Desktop - Development.asset` | UPDATE | profile 覆盖同步 `0` |
| `Assets/Settings/Build Profiles/Web - Mobile - Development.asset` | UPDATE | 同上 |
| `Deploy/nginx/nginx.conf` | UPDATE | 修 `.br` location 顺序 + 每块内叠加 Content-Encoding/MIME/Cache |
| `Deploy/Caddyfile` | CREATE | 本地 HTTPS 测试反代（自动内部 CA + `.br` 头） |
| `Deploy/serve-local-https.sh` | CREATE | 本地一键起 Caddy 的便捷脚本 |
| `Deploy/fix-webgl.sh` | UPDATE | `--serve` 标注对 `.br` 无效，指向 Caddy 脚本 |
| `Deploy/docker-compose.yml` | UPDATE（生产阶段） | Traefik router 加 `tls`/`certresolver`/`websecure` |
| WebGL 构建产物 | REBUILD（手动） | 产物 `.unityweb → .br` |

## NOT Building

- **不改 `BuildScript.cs`**——artifact pattern 用通配符 `*.wasm*`/`*.data*`/`*.framework.js*`，已兼容 `.br`；无压缩告警 `GetFiles("*.data")` 不匹配 `.data.br`，安全。
- **不改 `scripts/verify-webgl-build.sh`**——本会话已补 `.br` 匹配。
- **不改压缩格式**——`webGLCompressionFormat` 保持 `0`（Brotli）。
- **不改 `Deploy/Dockerfile`**——artifact 校验 `*.framework.js*`/`*.wasm*`/`*.data*` 通配，兼容 `.br`。
- **不在 nginx 终结 TLS**——生产由 Traefik 终结；本地由 Caddy 终结。nginx 始终 listen 80。
- **不搭建生产 Let's Encrypt 本身**——它在 repo 外的全局 Traefik 实例里；本计划只给 docker-compose labels 指引，标注前提。

---

## Step-by-Step Tasks

### Task 1: 关闭全局 ProjectSettings 的 Decompression Fallback
- **ACTION**: Edit `ProjectSettings/ProjectSettings.asset`
- **IMPLEMENT**: `  webGLDecompressionFallback: 1` → `  webGLDecompressionFallback: 0`
- **MIRROR**: `CONFIG_GLOBAL`
- **GOTCHA**: 文件内唯一出现，保持 2 空格缩进与冒号后空格。
- **VALIDATE**: `grep -n webGLDecompressionFallback ProjectSettings/ProjectSettings.asset` → `: 0`

### Task 2: 同步 Desktop build profile
- **ACTION**: Edit `Assets/Settings/Build Profiles/Web - Desktop - Development.asset`
- **IMPLEMENT**: `    - line: '|   webGLDecompressionFallback: 1'` → `...0'`
- **MIRROR**: `CONFIG_PROFILE`
- **GOTCHA**: 这是 `m_PlayerSettingsYaml` 序列化字符串行，保留单引号与 `|   ` 前缀，只改末尾 `1→0`。profile 若不同步，用它构建会覆盖全局、fallback 仍开。
- **VALIDATE**: `grep -n webGLDecompressionFallback "Assets/Settings/Build Profiles/Web - Desktop - Development.asset"` → `...0'`

### Task 3: 同步 Mobile build profile
- **ACTION**: Edit `Assets/Settings/Build Profiles/Web - Mobile - Development.asset`
- **IMPLEMENT / MIRROR / GOTCHA**: 同 Task 2（目标行文本相同，按文件分别 Edit）
- **VALIDATE**: `grep -n webGLDecompressionFallback "Assets/Settings/Build Profiles/Web - Mobile - Development.asset"` → `...0'`

### Task 4: 在 Unity 重新构建 WebGL
- **ACTION**: 构建 WebGL，输出 `Deploy/webgl-build`（`BuildScript.cs:14` BuildPath）
- **IMPLEMENT**: 优先 UnitySkills API（`http://localhost:8090`）触发 `BuildScript.PerformWebGLBuild`；否则 Unity `File > Build Profiles` 选 `Web - Desktop - Development` → Build。构建前确认 Editor 已 reimport 改动后的 `.asset`（必要时重启 Editor）。
- **GOTCHA**: 旧 `.unityweb` 会被覆盖为 `.br`；若仍见 `.unityweb`，说明设置未生效（Editor 未 reimport）。
- **VALIDATE**: `ls Deploy/webgl-build/Build/` → 出现 `*.wasm.br`/`*.data.br`/`*.framework.js.br`，无 `*.unityweb`

### Task 5: 新建本地 Caddy 反代配置（本地测试核心）
- **ACTION**: Create `Deploy/Caddyfile`
- **IMPLEMENT**:
  ```caddyfile
  {
      # 本地内部 CA 给所有站点签证书（避免对 .localhost 走 ACME）
      local_certs
  }

  # https://localhost 一定可用；edi-racing.localhost 需 hosts 或浏览器 .localhost 解析
  localhost, edi-racing.localhost {
      root * {$WEBGL_ROOT:./webgl-build}

      # 关闭 fallback 后 Unity loader 直接请求 *.br URL。
      # 必须手动回传 Content-Encoding + 解压后的真实 Content-Type。
      # （Caddy 的 precompressed 指令是给「无后缀请求自动挑 .br」用的，不适用带 .br 后缀的直接请求。）
      @anybr  path *.br
      @wasmbr path *.wasm.br
      @jsbr   path *.framework.js.br *.js.br
      @databr path *.data.br

      header @anybr  Content-Encoding br
      header @wasmbr Content-Type application/wasm
      header @jsbr   Content-Type application/javascript
      header @databr Content-Type application/octet-stream

      @immutable path *.br *.loader.js
      header @immutable Cache-Control "public, immutable, max-age=31536000"

      file_server
  }
  ```
- **MIRROR**: 头逻辑对应 nginx 生产块（同样 Content-Encoding + 解压后 MIME）
- **GOTCHA**: ① Caddy 的 `header` 是响应中间件、**多 matcher 会叠加**（不像 nginx 赢者通吃），所以 `@anybr` 的 Encoding 与 `@wasmbr` 的 Type 能同时作用于 `wasm.br`——这正是本地用 Caddy 的便利。② `wasm.br` 的 Content-Type 必须是 `application/wasm`，否则 WebAssembly streaming 编译失败。③ 首次需 `caddy trust` 让系统信任内部 CA。
- **VALIDATE**: `caddy validate --config Deploy/Caddyfile --adapter caddyfile` 通过

### Task 6: 新建本地一键起服务脚本
- **ACTION**: Create `Deploy/serve-local-https.sh`（`chmod +x`）
- **IMPLEMENT**:
  ```bash
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
  ```
- **GOTCHA**: `caddy trust` 在 macOS 需管理员权限；仅首次需要。若不 trust，浏览器会有证书警告但仍可点进去测试。
- **VALIDATE**: `bash -n Deploy/serve-local-https.sh` 通过

### Task 7: 本地端到端验证原生 Brotli
- **ACTION**: 运行本地服务并核对响应头 + 浏览器加载
- **IMPLEMENT / VALIDATE**:
  ```bash
  ./Deploy/serve-local-https.sh &   # 或前台另开终端
  # 核对 wasm.br 的编码与类型：
  WASM=$(ls Deploy/webgl-build/Build | grep '\.wasm\.br$')
  curl -skI -H 'Accept-Encoding: br' "https://localhost/Build/$WASM" | grep -iE 'content-encoding|content-type'
  ```
  EXPECT: `content-encoding: br` 且 `content-type: application/wasm`
- **浏览器**: 打开 `https://localhost` → DevTools Network → `.data.br` 请求响应头含 `content-encoding: br`、`content-type` 正确、无 `Unable to parse` 报错、游戏正常进入主场景。
- **GOTCHA**: 若浏览器仍报 br 解析失败，先确认是 `https://`（非 http）、地址栏无证书拦截（已 `caddy trust`）。

### Task 8: 标注 fix-webgl.sh 的 --serve 局限
- **ACTION**: Edit `Deploy/fix-webgl.sh` 的 `--serve` 分支
- **IMPLEMENT**: 在 Python server 启动前加提示：`.br` 产物需 HTTPS，Python HTTP 无法测试原生 Brotli，指向 `./Deploy/serve-local-https.sh`。保留 Python 逻辑用于纯静态/`.unityweb` 回滚场景。
  ```bash
  if ls "$SCRIPT_DIR/webgl-build/Build/"*.br >/dev/null 2>&1; then
    echo -e "${YELLOW}检测到 .br 产物：原生 Brotli 需 HTTPS，Python HTTP 无法正确测试。${NC}"
    echo "  请改用: ./Deploy/serve-local-https.sh (Caddy HTTPS)"
  fi
  ```
- **VALIDATE**: `bash -n Deploy/fix-webgl.sh` 通过

### Task 9: 修复 nginx `.br` location（生产阶段）
- **ACTION**: Edit `Deploy/nginx/nginx.conf`，替换 `NGINX_BR_CURRENT` 段落
- **IMPLEMENT**: 具体 `.br` 块在前、通用兜底在后，且每块内同时写 Content-Encoding + MIME + Cache（因 nginx 正则 location「赢者通吃」，必须在同一块叠加）：
  ```nginx
        # Unity WebGL Brotli — 具体后缀在前，各块自带编码/类型/缓存
        location ~ \.wasm\.br$ {
            add_header Content-Encoding br;
            add_header Cache-Control "public, immutable";
            expires 1y;
            default_type application/wasm;
        }
        location ~ \.framework\.js\.br$ {
            add_header Content-Encoding br;
            add_header Cache-Control "public, immutable";
            expires 1y;
            default_type application/javascript;
        }
        location ~ \.data\.br$ {
            add_header Content-Encoding br;
            add_header Cache-Control "public, immutable";
            expires 1y;
            default_type application/octet-stream;
        }
        # 兜底：其余 .br（如 .symbols.json.br）放最后
        location ~ \.br$ {
            add_header Content-Encoding br;
            add_header Cache-Control "public, immutable";
            expires 1y;
            default_type application/octet-stream;
        }

        # 未压缩 Unity 文件的 MIME（回滚/无压缩场景）
        location ~ \.wasm$ { default_type application/wasm; }
        location ~ \.data$ { default_type application/octet-stream; }

        # loader.js 等非 .br 静态资源缓存
        location ~* \.(js|data|wasm|unityweb)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
  ```
- **GOTCHA**: ① nginx 正则 location 按**书写顺序**首个匹配即用，`\.br$` 通用块必须放具体块**之后**。② 一个请求只命中一个 location，故 Content-Encoding 与 Cache-Control 必须写在**同一块**，不能分散。③ `add_header` 只是回传预压缩字节，**不需要** `ngx_brotli` 动态模块。④ nginx 不检查 `Accept-Encoding` 就发 `Content-Encoding: br`——在 Traefik(HTTPS) 后面浏览器总会广告 br，安全；但这也是为何整链必须 HTTPS。
- **VALIDATE**: `docker-compose -f Deploy/docker-compose.yml build` 成功（镜像启动时 nginx 校验配置）；或本地装了 nginx 时 `nginx -t -c`

### Task 10: 生产 Traefik 加 TLS（生产阶段，依赖外部 Traefik）
- **ACTION**: Edit `Deploy/docker-compose.yml` 的 `edi-racing` service labels
- **IMPLEMENT**: 追加（前提：外部全局 Traefik 已定义 `websecure` entrypoint 与名为 `letsencrypt` 的 certresolver）：
  ```yaml
      - "traefik.http.routers.edi-racing-game.entrypoints=websecure"
      - "traefik.http.routers.edi-racing-game.tls=true"
      - "traefik.http.routers.edi-racing-game.tls.certresolver=letsencrypt"
  ```
- **GOTCHA**: certresolver 名称、entrypoint 名称必须与外部 Traefik 静态配置一致——需向该 Traefik 实例的维护配置核对。真实域名（非 .localhost）才能签 Let's Encrypt。api service 同理可加。
- **VALIDATE**: 部署后浏览器访问生产 HTTPS 域名，DevTools 确认 `.data.br` 带 `content-encoding: br` 且证书有效

---

## Testing Strategy

### 校验矩阵
| 校验 | 命令 | 期望 |
|---|---|---|
| 三处配置 | `grep -rn webGLDecompressionFallback ProjectSettings/ "Assets/Settings/Build Profiles/"` | 全 `0` |
| 产物形态 | `ls Deploy/webgl-build/Build/` | `.br`，无 `.unityweb` |
| 产物完整性 | `./scripts/verify-webgl-build.sh` | All passed，压缩识别为 Brotli |
| Caddyfile | `caddy validate --config Deploy/Caddyfile --adapter caddyfile` | valid |
| 脚本语法 | `bash -n Deploy/serve-local-https.sh Deploy/fix-webgl.sh` | 通过 |
| 本地编码头 | `curl -skI -H 'Accept-Encoding: br' https://localhost/Build/<hash>.wasm.br` | `content-encoding: br` + `content-type: application/wasm` |
| 本地浏览器 | 打开 `https://localhost` | `.data.br` 原生解压、无报错、游戏启动 |
| nginx 构建 | `docker-compose -f Deploy/docker-compose.yml build` | 成功（镜像内 nginx 配置有效） |

### Edge Cases Checklist
- [ ] Editor 未 reimport → 仍出 `.unityweb`（Task 4 拦截）
- [ ] 漏改 profile → 用该 profile 构建 fallback 仍开（Task 2/3）
- [ ] wasm.br 的 Content-Type 非 application/wasm → streaming 编译失败（Caddy/nginx 都已覆盖）
- [ ] nginx `\.br$` 通用块排前 → wasm MIME 错、无缓存头（Task 9 重排修复）
- [ ] 未 `caddy trust` → 浏览器证书警告（可点通过，或先 trust）
- [ ] 用 http/Python server 测 `.br` → 加载失败（预期；Task 8 提示）
- [ ] Traefik certresolver 名不符 → 生产签证失败（Task 10 核对）

---

## Validation Commands

### Static
```bash
bash -n Deploy/serve-local-https.sh Deploy/fix-webgl.sh
caddy validate --config Deploy/Caddyfile --adapter caddyfile
grep -rn webGLDecompressionFallback ProjectSettings/ProjectSettings.asset "Assets/Settings/Build Profiles/"
```
EXPECT: 全部通过；三处均 `0`

### Build（重建后）
```bash
ls -la Deploy/webgl-build/Build/
./scripts/verify-webgl-build.sh
```
EXPECT: `.br` 产物、无 `.unityweb`；校验全绿

### Local HTTPS（本地重点）
```bash
./Deploy/serve-local-https.sh          # 首次会 caddy trust
# 另一终端：
WASM=$(ls Deploy/webgl-build/Build | grep '\.wasm\.br$')
curl -skI -H 'Accept-Encoding: br' "https://localhost/Build/$WASM" | grep -iE 'content-encoding|content-type'
```
EXPECT: `content-encoding: br` 且 `content-type: application/wasm`；浏览器 `https://localhost` 游戏正常启动

### Production nginx smoke（可选）
```bash
docker-compose -f Deploy/docker-compose.yml build
```
EXPECT: 构建成功，nginx 配置有效

### Manual Validation
- [ ] 三处配置 grep 为 0
- [ ] Unity 重建产物只见 `.br`
- [ ] `verify-webgl-build.sh` 全绿
- [ ] `https://localhost` DevTools 确认 `.data.br` 原生 `content-encoding: br`、游戏进入主场景
- [ ] （生产时）Traefik HTTPS 域名同样确认原生 br

---

## Acceptance Criteria
- [ ] 三处 `webGLDecompressionFallback` 为 `0`
- [ ] 重建产物为 `.br`，无 `.unityweb`
- [ ] `Deploy/Caddyfile` + `serve-local-https.sh` 可本地 HTTPS 起服务
- [ ] 本地 `https://localhost` 原生 Brotli 加载成功（`content-encoding: br`，wasm MIME 正确，游戏启动）
- [ ] nginx `.br` location 顺序/叠加修复，`docker-compose build` 通过
- [ ] docker-compose Traefik TLS labels 就位（生产，标注前提）

## Completion Checklist
- [ ] 配置改动逐字匹配现有 YAML（含 profile 字符串行前缀）
- [ ] Caddy/nginx 的 `.br` 头逻辑一致（Content-Encoding + 解压后 MIME + Cache）
- [ ] 未改 BuildScript.cs / verify 脚本 / Dockerfile（确认无需）
- [ ] 无硬编码新值
- [ ] `.unityweb` 兼容逻辑保留（回滚安全）
- [ ] 生产 Traefik 前提已在 GOTCHA 标注
- [ ] 无越界改动

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 生产/本地误用 HTTP 托管 `.br` | 中 | 高 | 本地强制 Caddy HTTPS；生产 Traefik TLS；文档警示 |
| Editor 未 reimport，构建仍 `.unityweb` | 中 | 中 | Task 4 VALIDATE + serve 脚本检测告警 |
| profile 字符串行 Edit 破坏 YAML | 低 | 高 | 逐字匹配 MIRROR，仅改末尾数字；Editor 打开 profile 确认无报错 |
| nginx location 顺序未修 → wasm MIME 错 | 中 | 中 | Task 9 重排 + 同块叠加 |
| Traefik certresolver 名不符外部配置 | 中 | 中 | Task 10 核对外部 Traefik 静态配置 |
| Caddy 内部 CA 未信任 → 证书警告 | 低 | 低 | `caddy trust`；或测试时手动放行 |
| 失去「任意静态服务器可跑」便利 | 已知 | 低 | 方案 B 固有取舍，用户已确认 |

## Notes
- 源自本会话：`verify-webgl-build.sh` 误报 → 发现 `.unityweb` → 发现 nginx `.br` 死代码 → 用户先选方案 A → 联网核实 Brotli 需 HTTPS、方案 A 破坏 HTTP 部署 → 用户改选方案 B（上 HTTPS）+ 本地 Caddy。
- `webGLCompressionFormat` 已是 Brotli（`0`），本计划只关 fallback、不动压缩格式。
- 本地 Caddy 与生产 Traefik 是两套独立 TLS 终结，互不干扰；nginx 始终不终结 TLS（listen 80）。
- Caddy 的 `header` 多 matcher 叠加、nginx 正则 location 赢者通吃——这是两者 `.br` 配置写法不同的根因。
- 回滚：三处 `0→1` + 重建 → 恢复 `.unityweb`（可 HTTP 部署）；Caddy/脚本/nginx 改动对 `.unityweb` 无害。
- 关联：`.claude/PRPs/plans/webgl-template-macros-and-brotli.plan.md`（原 Brotli+保留fallback 决策，本计划在其上升级到 HTTPS 原生路径）。
