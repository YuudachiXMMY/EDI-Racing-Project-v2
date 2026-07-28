# Plan: WebGL 模板宏修复 + Brotli 压缩启用

## Summary

修复 `Assets/WebGLTemplates/EDIRacing/index.html` 使用的过时模板变量（Unity 2019 时代的 `UNITY_LOADER_URL` / `DATA_URL` 等，Unity 6 预处理器解析为空字符串），改用 Unity 6 官方宏 `{{{ LOADER_FILENAME }}}` 系列，使构建产物 index.html 直接可用，不再依赖 `Deploy/fix-webgl.sh` 事后修补。同时把 WebGL 压缩格式从 Disabled/Gzip 切换为 Brotli（保留 Decompression Fallback 以兼容课堂 HTTP 环境），将 279 MB 的 `.data` 压到预计 60–110 MB，大幅缩短学生浏览器加载时间。

## User Story

作为课堂教师（部署者），我希望 Unity 构建出的 WebGL 产物开箱即用且体积经过 Brotli 压缩，这样每次构建后无需手工修补 index.html，学生通过局域网浏览器加入时加载时间从数分钟降到可接受范围。

## Problem → Solution

| 现状 | 目标 |
|---|---|
| 模板用 `{{{ UNITY_LOADER_URL }}}`、`{{{ DATA_URL }}}` 等旧变量，Unity 6 解析为空 → 构建出的 index.html URL 全空，游戏卡在加载 | 模板用 Unity 6 官方宏 `Build/{{{ LOADER_FILENAME }}}` 等，构建即正确 |
| 靠 `Deploy/fix-webgl.sh` 用 sed 事后修补 URL | fix-webgl.sh 降级为纯校验+本地服务工具，不再修补 |
| 构建配置 `webGLCompressionFormat: 2`（Disabled）→ `.data` 279 MB 未压缩 | Brotli（=0）+ Decompression Fallback（已开启）→ `.data.unityweb` 预计 60–110 MB |

## Metadata

- **Complexity**: Medium
- **Source PRD**: N/A（自由文本输入）
- **PRD Phase**: N/A
- **Estimated Files**: 6
- **Related Plan**: `.claude/PRPs/plans/webgl-build-verification.plan.md`（其 NOT Building 明确将 Brotli 修复列为独立任务，即本计划）

---

## UX Design

### Before

```
学生打开 http://<教师机IP>:3900
  → 下载 279 MB .data（校园网可能 3–10 分钟）
  → （若构建后忘记跑 fix-webgl.sh：进度条卡 0%，控制台报空 URL 错误）
```

### After

```
学生打开 http://<教师机IP>:3900
  → 下载 ~60–110 MB .data.unityweb（时间缩短 60–75%）
  → loader 内置 JS 解压（Decompression Fallback），无需 HTTPS/服务器头配合
  → index.html 构建即正确，无需任何修补步骤
```

### Interaction Changes

| Touchpoint | Before | After | Notes |
|---|---|---|---|
| 构建后手工步骤 | 必须跑 `fix-webgl.sh` 修补 | 无需修补 | 脚本保留为校验/本地服务工具 |
| 学生加载时长 | 279 MB 全量下载 | ~1/3–1/4 体积 | Brotli 压缩比对 Unity .data 通常 20–40% |
| 加载失败模式 | URL 空 → 无提示卡死 | BuildScript 构建时即校验失败退出 | 失败左移到构建阶段 |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/WebGLTemplates/EDIRacing/index.html` | all (69 行) | 待修复的模板本体，保留其自定义 loading overlay 样式 |
| P0 | `/Applications/Unity/Hub/Editor/6000.3.19f1/PlaybackEngines/WebGLSupport/BuildTools/WebGLTemplates/Base/Minimal/index.html` | all | Unity 6.3 官方宏格式的权威参照（本机已装） |
| P1 | `Assets/Scripts/Editor/BuildScript.cs` | 58–123 | VerifyArtifacts 现有校验逻辑，需扩展空 URL 检查 |
| P1 | `Assets/Settings/Build Profiles/Web - Desktop - Development.asset` | 852, 856, 998 | `webGLCompressionFormat: 2`、`webGLDecompressionFallback: 1`、`m_Development: 1` |
| P1 | `Assets/Settings/Build Profiles/Web - Mobile - Development.asset` | 836, 981 | 同上，Mobile 配置 |
| P1 | `ProjectSettings/ProjectSettings.asset` | 809–818 | `webGLCompressionFormat: 1`（Gzip，CLI 构建走此路径）、`webGLTemplate: PROJECT:EDIRacing` |
| P2 | `Deploy/fix-webgl.sh` | all | 将被降级为校验模式的脚本 |
| P2 | `Deploy/nginx/nginx.conf` | 39–73 | `.br`/`.unityweb` 的 MIME 与缓存配置（已就绪，无需改） |
| P2 | `Deploy/Dockerfile` | 17–23 | 产物校验用 `*.data*` 通配，兼容 `.unityweb` 后缀 |
| P2 | `scripts/build-webgl.sh` | all | CLI 构建入口，调用 `BuildScript.BuildWebGL` |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity 6 模板宏 | 本机 `Base/Minimal/index.html`（比在线文档更权威，与 6000.3.19f1 完全一致） | 宏为 `LOADER_FILENAME`/`DATA_FILENAME`/`FRAMEWORK_FILENAME`/`CODE_FILENAME`，路径前缀 `Build/` 写在模板里；字符串值用 `{{{ JSON.stringify(COMPANY_NAME) }}}`；条件块 `#if USE_WASM`、`#if USE_THREADS`、`#if SYMBOLS_FILENAME` |
| WebGLCompressionFormat 枚举 | Unity ScriptReference | `0 = Brotli, 1 = Gzip, 2 = Disabled` |
| Brotli over HTTP | 浏览器行为 | 浏览器仅在 HTTPS 下接受 `Content-Encoding: br`；课堂走 HTTP（端口 3900），**必须保留 Decompression Fallback**（loader 内置 JS 解压，产物后缀 `.unityweb`，不依赖服务器响应头） |

---

## Patterns to Mirror

### TEMPLATE_MACROS（Unity 6 官方写法）

```html
<!-- SOURCE: Unity 6000.3.19f1 Base/Minimal/index.html:19,45-61 -->
<script src="Build/{{{ LOADER_FILENAME }}}"></script>
<script>
  createUnityInstance(document.querySelector("#unity-canvas"), {
    arguments: [],
    dataUrl: "Build/{{{ DATA_FILENAME }}}",
    frameworkUrl: "Build/{{{ FRAMEWORK_FILENAME }}}",
#if USE_THREADS
    workerUrl: "Build/{{{ WORKER_FILENAME }}}",
#endif
#if USE_WASM
    codeUrl: "Build/{{{ CODE_FILENAME }}}",
#endif
#if SYMBOLS_FILENAME
    symbolsUrl: "Build/{{{ SYMBOLS_FILENAME }}}",
#endif
    streamingAssetsUrl: "StreamingAssets",
    companyName: {{{ JSON.stringify(COMPANY_NAME) }}},
    productName: {{{ JSON.stringify(PRODUCT_NAME) }}},
    productVersion: {{{ JSON.stringify(PRODUCT_VERSION) }}},
  })
```

### ERROR_HANDLING（BuildScript 校验风格）

```csharp
// SOURCE: Assets/Scripts/Editor/BuildScript.cs:96-113
string html = File.ReadAllText(indexPath);
var refs = Regex.Matches(html, @"Build/[^""]+");
foreach (Match m in refs)
{
    string refPath = Path.Combine(buildPath, m.Value);
    if (!File.Exists(refPath))
    {
        Debug.LogError($"[BuildScript] index.html references missing file: {m.Value}");
        errors++;
    }
}
```

### LOGGING_PATTERN

```csharp
// SOURCE: Assets/Scripts/Editor/BuildScript.cs:84,103
Debug.Log($"[BuildScript] Found: {info.Name} ({info.Length} bytes)");
Debug.LogError($"[BuildScript] index.html references missing file: {m.Value}");
// 统一 [BuildScript] 前缀；错误累加 errors 计数，最后统一判定
```

### SHELL_SCRIPT_STYLE（fix-webgl.sh 风格）

```bash
# SOURCE: Deploy/fix-webgl.sh:42-63
LOADER=$(find "$BUILD_DIR" -maxdepth 1 -name '*.loader.js' | head -1)
# ...彩色输出 GREEN/RED/YELLOW，MISSING 计数后统一 exit 1
```

### BUILD_PROFILE_YAML（配置文件内嵌序列化格式 — 直接编辑时的形态）

```yaml
# SOURCE: Assets/Settings/Build Profiles/Web - Desktop - Development.asset:852
    - line: '|   webGLCompressionFormat: 2'
# 注意：profile 把 PlayerSettings 存成带前缀的文本行数组（m_PlayerSettingsYaml），
# 只改行尾数字，勿动缩进与 '|   ' 前缀
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/WebGLTemplates/EDIRacing/index.html` | UPDATE | 旧变量 → Unity 6 宏（根因修复） |
| `Assets/Settings/Build Profiles/Web - Desktop - Development.asset` | UPDATE | `webGLCompressionFormat: 2 → 0`（编辑器内构建走此配置，当前部署产物即出自它） |
| `Assets/Settings/Build Profiles/Web - Mobile - Development.asset` | UPDATE | 同上（`:836`） |
| `ProjectSettings/ProjectSettings.asset` | UPDATE | `webGLCompressionFormat: 1 → 0`（CLI 批处理构建 `BuildPipeline.BuildPlayer` 走共享 PlayerSettings） |
| `Assets/Scripts/Editor/BuildScript.cs` | UPDATE | VerifyArtifacts 增加空 URL 检查 + 未压缩 .data 告警，把失败左移到构建时 |
| `Deploy/fix-webgl.sh` | UPDATE | 移除 sed 修补逻辑，降级为校验 + `--serve` 工具；find 通配补 `.unityweb` |

**无需改动**（已兼容，避免误改）：
- `Deploy/nginx/nginx.conf` — 全局 `default_type application/octet-stream` 覆盖 `.unityweb`；缓存正则 `(js|wasm|data|br|unityweb)` 已含 unityweb；`.br` location 块保留（未来 HTTPS 原生 Brotli 可直接用）
- `Deploy/Dockerfile` — 产物校验用 `*.data*` 等通配，天然匹配 `.data.unityweb`
- `scripts/build-webgl.sh` — 构建入口不变

## NOT Building

- 不新建独立的 Production Build Profile（`m_Development: 1` 维持现状；若日后 Brotli 拖慢迭代构建，再拆 Dev/Prod 双 profile）
- 不配置 HTTPS / 原生 `Content-Encoding: br` 路径（课堂 HTTP 环境下浏览器拒绝原生 br，走 Decompression Fallback）
- 不开启 `webGLDataCaching`（IndexedDB 缓存可再省学生二次加载时间，但属独立优化，另行评估）
- 不缩减 .data 本体资产体积（贴图/模型压缩属资产优化范畴，非本次范围）
- 不删除 `Deploy/fix-webgl.sh`（保留其校验与本地服务能力）

---

## Step-by-Step Tasks

### Task 1: 重写 WebGL 模板为 Unity 6 宏格式

- **ACTION**: 更新 `Assets/WebGLTemplates/EDIRacing/index.html`，保留现有视觉设计（loading overlay、进度条、错误提示、全屏 canvas 样式）不变，仅替换模板变量层。
- **IMPLEMENT**:
  - `<script src="{{{ UNITY_LOADER_URL }}}">` → `<script src="Build/{{{ LOADER_FILENAME }}}">`
  - `dataUrl: "{{{ DATA_URL }}}"` → `dataUrl: "Build/{{{ DATA_FILENAME }}}"`
  - `frameworkUrl: "{{{ FRAMEWORK_URL }}}"` → `frameworkUrl: "Build/{{{ FRAMEWORK_FILENAME }}}"`
  - `codeUrl: "{{{ CODE_URL }}}"` → 包在 `#if USE_WASM` 内的 `codeUrl: "Build/{{{ CODE_FILENAME }}}"`
  - 删除 `#if MEMORY_FILENAME ... memoryUrl ...` 块（Unity 6 已无此变量）
  - `symbolsUrl: "{{{ SYMBOLS_URL }}}"` → `symbolsUrl: "Build/{{{ SYMBOLS_FILENAME }}}"`（保留 `#if SYMBOLS_FILENAME` 守卫）
  - 在 frameworkUrl 后新增 `#if USE_THREADS` / `workerUrl: "Build/{{{ WORKER_FILENAME }}}"` / `#endif`
  - `companyName: "{{{ COMPANY_NAME }}}"` 等三项 → `{{{ JSON.stringify(COMPANY_NAME) }}}` 形式（免引号转义问题）
  - 保留现有 progress 回调、`.then` 隐藏 overlay、`.catch` 红字报错逻辑原样
- **MIRROR**: TEMPLATE_MACROS 模式（严格对照本机 Base/Minimal/index.html）
- **IMPORTS**: N/A
- **GOTCHA**: ① `#if` 预处理指令必须顶格写在行首；② `{{{ }}}` 内是 JS 表达式求值，旧变量名不报错、静默输出空串——这正是原 bug 成因；③ `PRODUCT_NAME` 用于 `<title>` 的插值写法 `{{{ PRODUCT_NAME }}}` 在 Unity 6 仍有效，不必改。
- **VALIDATE**: 跑 `./scripts/build-webgl.sh` 后检查 `Deploy/webgl-build/index.html`：`grep -c 'src=""\|dataUrl: ""' index.html` 应为 0，且四个 URL 均指向 `Build/` 下真实存在的文件。

### Task 2: 两个 Build Profile 压缩格式改为 Brotli

- **ACTION**: 把两份 profile 的 `webGLCompressionFormat` 从 `2`（Disabled）改为 `0`（Brotli）。
- **IMPLEMENT**: **优先通过 UnitySkills API（`http://localhost:8090`）执行**（项目规定 Unity 编辑器操作优先走 API）；若 API 不支持 Build Profile 覆盖项修改，回退为直接编辑文件——精确替换 `- line: '|   webGLCompressionFormat: 2'` → `- line: '|   webGLCompressionFormat: 0'`（Desktop `:852`，Mobile `:836`）。
- **MIRROR**: BUILD_PROFILE_YAML 模式
- **IMPORTS**: N/A
- **GOTCHA**: ① 直接编辑文件前必须**关闭 Unity 编辑器**（或确认编辑器会重载），否则编辑器内存态可能反向覆盖；② profile 的 YAML 是 `m_PlayerSettingsYaml` 内嵌文本行数组，缩进与 `'|   '` 前缀一个字符都不能动；③ `webGLDecompressionFallback: 1` 已开启，**保持不变**——课堂 HTTP 环境全靠它；④ Brotli 压缩 279 MB 数据会使构建时间增加数分钟，属预期。
- **VALIDATE**: `grep -n "webGLCompressionFormat" "Assets/Settings/Build Profiles/"*.asset` 两处均为 `0`；在 Unity 编辑器 Build Profiles 窗口确认显示 Brotli。

### Task 3: ProjectSettings 压缩格式改为 Brotli

- **ACTION**: `ProjectSettings/ProjectSettings.asset:812` 的 `webGLCompressionFormat: 1` → `0`。
- **IMPLEMENT**: 优先 UnitySkills API（等价于 `PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli`）；回退为直接编辑该行。
- **MIRROR**: N/A（单行数值）
- **IMPORTS**: N/A
- **GOTCHA**: CLI 构建（`BuildScript.BuildWebGL` 用经典 `BuildPlayerOptions`）走共享 PlayerSettings 而非 Build Profile 覆盖，所以此处必须同步改，否则命令行构建仍是 Gzip。
- **VALIDATE**: `grep -n "webGLCompressionFormat" ProjectSettings/ProjectSettings.asset` 为 `0`。

### Task 4: BuildScript.VerifyArtifacts 增加空 URL 与压缩校验

- **ACTION**: 扩展 `Assets/Scripts/Editor/BuildScript.cs` 的 `VerifyArtifacts`。
- **IMPLEMENT**: 在现有 index.html 校验段后追加：
  1. 空 URL 检查：`html.Contains("src=\"\"")` 或 `Regex.IsMatch(html, @"(dataUrl|frameworkUrl|codeUrl):\s*""""")` → `Debug.LogError("[BuildScript] index.html contains empty URLs — template macros failed to resolve"); errors++;`
  2. 压缩告警：若 `Build/` 下存在裸 `*.data` 文件（无 `.br`/`.gz`/`.unityweb` 后缀）且大小 > 100 MB → `Debug.LogWarning("[BuildScript] .data is uncompressed ({size} MB) — check webGLCompressionFormat")`（告警不计入 errors，避免本地快速迭代被卡）。
- **MIRROR**: ERROR_HANDLING + LOGGING_PATTERN（`[BuildScript]` 前缀、errors 累加）
- **IMPORTS**: 已有 `System.IO`、`System.Text.RegularExpressions`、`UnityEngine`，无需新增
- **GOTCHA**: 公共方法需按编码规范补 doc comment；现有 `Regex.Matches(html, @"Build/[^""]+")` 在宏解析为空时匹配不到任何引用（这正是旧 bug 静默通过校验的原因），所以空 URL 必须显式独立检查。
- **VALIDATE**: 手工构造一个 `src=""` 的 index.html 放入构建目录，跑 `Unity -batchmode -executeMethod BuildScript.VerifyBuildArtifacts` 应退出码非 0。

### Task 5: fix-webgl.sh 降级为校验 + 本地服务工具

- **ACTION**: 移除 sed 修补段（`:81-106`），改为校验模式；保留产物存在性检查与 `--serve`。
- **IMPLEMENT**:
  - find 通配补 `.unityweb`：如 `-name '*.data' -o -name '*.data.br' -o -name '*.data.gz' -o -name '*.data.unityweb'`（framework/wasm 同理）
  - 修补段替换为：逐一 grep 确认 index.html 中的 loader/data/framework/code 引用与 Build/ 目录实际文件名一致；不一致或为空 → 红字报错 `index.html 与构建产物不匹配 — 请检查 Assets/WebGLTemplates/EDIRacing/index.html 模板并重新构建`，exit 1（**不再静默修补**，让模板回归问题在部署前暴露）
  - 头部注释同步更新用途说明
- **MIRROR**: SHELL_SCRIPT_STYLE（彩色输出、MISSING 计数、`set -e`）
- **IMPORTS**: N/A
- **GOTCHA**: sed 的 macOS/Linux 兼容 hack（temp file 轮转）可一并删除；`--serve` 段落原样保留。
- **VALIDATE**: 对新构建产物跑 `./Deploy/fix-webgl.sh` 应全绿通过；手工把 index.html 某 URL 改错再跑应 exit 1。

### Task 6: 全链路构建 + Docker 部署验证

- **ACTION**: 端到端验证两项改进。
- **IMPLEMENT**: 依次执行下方 Validation Commands 全部命令。
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: ① Brotli 使构建时间明显变长（279 MB 数据压缩需数分钟），不要误判为卡死；② 构建产物文件名是内容哈希（`webGLNameFilesAsHashes: 1`），改压缩格式后哈希全变，Docker 镜像需重新 build；③ 浏览器验证必须走 HTTP 服务（`--serve` 或 Docker），`file://` 协议不能加载 WebGL。
- **VALIDATE**: 见 Validation Commands。

---

## Testing Strategy

### Unit Tests

本次变更均在构建管线/模板层，无运行时 C# 逻辑变化，不新增 EditMode 单测。校验逻辑由 `BuildScript.VerifyBuildArtifacts`（批处理可独立调用）承担，属构建门禁而非单测。

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| VerifyBuildArtifacts 正常产物 | 新构建的 webgl-build/ | 退出码 0，日志 All build artifacts verified | 否 |
| VerifyBuildArtifacts 空 URL | 含 `src=""` 的 index.html | 退出码 1，报 empty URLs | 是 |
| VerifyBuildArtifacts 未压缩告警 | 裸 .data > 100 MB | Warning 日志，退出码仍 0 | 是 |
| fix-webgl.sh 校验模式 | URL 与产物不匹配 | exit 1 红字报错 | 是 |

### Edge Cases Checklist

- [ ] 构建目录缺失某产物（loader/framework/data/wasm）→ 构建校验与 fix-webgl.sh 均报错
- [ ] `.unityweb` 后缀产物被 Dockerfile `*.data*` 通配正确匹配
- [ ] 学生浏览器（Chrome/Edge/Safari）经 HTTP 加载 `.unityweb` 正常解压启动
- [ ] 移动端（Web - Mobile profile）构建同样产出 Brotli
- [ ] nginx 缓存头对 `.unityweb` 生效（`expires 1y` 正则已含 unityweb）

---

## Validation Commands

### Static Analysis

```bash
# 三处压缩配置统一检查
grep -n "webGLCompressionFormat" ProjectSettings/ProjectSettings.asset "Assets/Settings/Build Profiles/"*.asset
```
EXPECT: 三处均为 `0`

### Build Verification

```bash
./scripts/build-webgl.sh
ls -lh Deploy/webgl-build/Build/
grep -E 'src=""|dataUrl: ""' Deploy/webgl-build/index.html; echo "exit=$?"
```
EXPECT: 产物为 `*.unityweb` 后缀；`.data.unityweb` 体积 ≪ 279 MB（预计 60–110 MB）；grep 无匹配（exit=1）

### Standalone Artifact Verification

```bash
"$UNITY_PATH" -batchmode -nographics -projectPath . -executeMethod BuildScript.VerifyBuildArtifacts -logFile - -quit
./Deploy/fix-webgl.sh
```
EXPECT: 两者均通过，无 MISSING、无 empty URL

### Browser Validation

```bash
./Deploy/fix-webgl.sh --serve   # http://localhost:8080
```
EXPECT: 进度条正常推进至 100%，游戏启动，浏览器控制台无 404/解压错误；DevTools Network 面板确认 `.data.unityweb` 传输体积为压缩后大小

### Docker Deployment Validation

```bash
cd Deploy && docker compose build edi-racing && docker compose up -d
curl -sI http://localhost:3900/ | head -5
curl -sI "http://localhost:3900/Build/$(ls webgl-build/Build/ | grep data)" | grep -iE 'content-length|cache-control'
```
EXPECT: 镜像构建通过产物校验；index 200；data 文件 Content-Length 为压缩后体积且带 `Cache-Control: public, immutable`

### Manual Validation

- [ ] 教师机部署后，用另一台设备经局域网 IP 加载，记录首次加载耗时（对比改造前）
- [ ] Unity 编辑器内 Build Profiles 窗口确认两 profile 显示 Brotli + Decompression Fallback 勾选

---

## Acceptance Criteria

- [ ] 构建产物 index.html 的 4 个 URL 全部非空且指向真实文件（不经任何修补）
- [ ] `.data` 以 Brotli 压缩（`.unityweb` 后缀），体积较 279 MB 显著下降
- [ ] HTTP（非 HTTPS）环境下浏览器可正常加载（Decompression Fallback 生效）
- [ ] `BuildScript.VerifyBuildArtifacts` 能拦截空 URL 产物
- [ ] fix-webgl.sh 不再修补、仅校验，且对新产物通过
- [ ] Docker 镜像构建与运行验证通过

## Completion Checklist

- [ ] 模板严格对照 Unity 6000.3.19f1 官方 Minimal 模板宏
- [ ] 保留 EDIRacing 模板自定义视觉（overlay/进度条/错误提示）
- [ ] BuildScript 新增校验遵循 `[BuildScript]` 日志前缀与 errors 累加模式
- [ ] 公共方法 doc comment 齐全（编码规范）
- [ ] 无硬编码文件名（全部走宏/通配）
- [ ] Commit 用 Conventional Commits（`fix: ...` / `perf: ...`），正文引用本计划路径
- [ ] 无范围外改动（nginx/Dockerfile/build-webgl.sh 不动）

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 直接编辑 profile YAML 时 Unity 编辑器开着，内存态覆盖改动 | 中 | 中 | 优先走 UnitySkills API；直接编辑前关闭 Unity 或改后触发编辑器重载并复核 |
| Brotli 大幅拉长迭代构建时间引发不满 | 中 | 低 | 已知取舍；如成为痛点，后续拆 Dev（Disabled）/Prod（Brotli）双 profile |
| 低端学生设备 JS 解压大体积数据耗时/内存压力 | 低 | 中 | Fallback 解压为流式；若实测慢，评估 HTTPS + 原生 br（nginx 配置已备好） |
| 模板宏拼写错误再次静默输出空串 | 低 | 高 | Task 4 的空 URL 构建门禁兜底，问题在构建时即失败 |
| CLI 与编辑器构建走不同设置源（PlayerSettings vs Profile）漏改一处 | 中 | 中 | Task 2+3 同时改三处并用 grep 统一验证 |

## Notes

- **根因链**：模板变量 `UNITY_LOADER_URL`/`DATA_URL` 是 Unity 2019 时代命名；Unity 6 预处理器把 `{{{ }}}` 当 JS 表达式求值，未定义变量静默输出空串 → URL 全空。而 `VerifyArtifacts` 的正则 `Build/[^""]+` 恰好匹配不到空引用，所以旧校验静默放行——这解释了为什么问题一直靠 fix-webgl.sh 兜底。
- **压缩产物命名**：因 `webGLDecompressionFallback: 1`，Brotli 产物为 `xxx.data.unityweb`（非 `.data.br`），loader 自带解压，与服务器响应头无关——这是课堂 HTTP 场景的关键。nginx 中现存 `.br` location 块是为未来 HTTPS 原生路径预留，不冲突。
- **当前部署产物出处推断**：`.data` 完全未压缩，与 ProjectSettings（Gzip=1）不符、与 profile（Disabled=2）相符，说明部署产物来自编辑器内 Build Profile 构建——因此 profile 与 PlayerSettings 必须同时改。
- **实际体积**：用户提到 265 MB，实测当前 `.data` 为 279 MB（`Deploy/webgl-build/Build/3e5b573e5cb1cefebb8920075c8bd752.data`）。
- 可选后续优化（本次不做）：`webGLDataCaching: 1` 让学生二次访问命中 IndexedDB 缓存，几乎零成本再提速。
