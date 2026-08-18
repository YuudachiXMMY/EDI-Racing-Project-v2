# Plan: WebGL 构建验证

## Summary
创建一套自动化 WebGL 构建验证流水线，包括构建产物完整性检查脚本、Docker 部署冒烟测试、以及 GitHub Actions CI 工作流，确保每次构建后 WebGL 应用能正确加载并运行。

## User Story
As a developer/instructor,
I want automated verification that the WebGL build works correctly,
So that I can catch build issues before deployment and avoid broken releases.

## Problem → Solution
**当前状态**：构建验证完全手动 — 开发者需要执行 `build-webgl.sh`、手动运行 Docker、打开浏览器检查。没有自动化检查，没有 CI，构建产物与 nginx 配置可能不匹配（压缩格式差异）。

**目标状态**：一键验证脚本 + CI 流水线自动检测构建产物完整性、Docker 容器健康状态、WebGL 页面加载成功。构建中的任何问题在合并前就能发现。

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A
- **PRD Phase**: N/A
- **Estimated Files**: 6

---

## UX Design

N/A — internal change (开发者工具链改进，无用户界面变更)

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/Editor/BuildScript.cs` | all | 构建入口点，定义了场景和输出路径 |
| P0 (critical) | `scripts/build-webgl.sh` | all | 现有构建脚本，需要与验证脚本协调 |
| P0 (critical) | `Deploy/fix-webgl.sh` | all | 现有 WebGL 修复脚本，验证逻辑可复用 |
| P0 (critical) | `Deploy/Dockerfile` | all | Docker 构建上下文和产物路径 |
| P0 (critical) | `Deploy/docker-compose.yml` | all | 服务编排和端口映射 |
| P1 (important) | `Deploy/nginx/nginx.conf` | all | Brotli/MIME 配置，需与构建产物匹配 |
| P1 (important) | `Assets/WebGLTemplates/EDIRacing/index.html` | all | WebGL 模板，loader URL 格式 |
| P2 (reference) | `Deploy/webgl-build/index.html` | all | 实际构建输出的 index.html |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity WebGL build | Unity 6 docs | WebGL 构建输出包含 `.loader.js`, `.framework.js`, `.data`, `.wasm`，可选 Brotli (`.br`) 或 Gzip (`.gz`) 压缩 |
| game-ci/unity-test-runner | GitHub | Unity CI 使用 `game-ci/unity-test-runner@v4` action |
| Docker healthcheck | Docker docs | `healthcheck` 指令可在 compose 中验证容器健康 |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
// SOURCE: scripts/build-webgl.sh:1-2, Deploy/fix-webgl.sh:1-3
```bash
#!/bin/bash
# [Deploy/script-name.sh] — [Description]
#
# What it does:
#   [numbered list]
#
# Usage:
#   ./Deploy/script-name.sh [options]
```
Shell 脚本使用 `set -e`，颜色变量 `RED/GREEN/YELLOW/NC`，`echo -e` 输出。

### ERROR_HANDLING
// SOURCE: Deploy/fix-webgl.sh:30-35
```bash
if [ ! -d "$BUILD_DIR" ]; then
  echo -e "${RED}Error: Build directory not found: $BUILD_DIR${NC}"
  echo "  Run a WebGL build in Unity first..."
  exit 1
fi
```
脚本使用带颜色的错误消息，明确说明下一步操作。

### LOGGING_PATTERN
// SOURCE: Assets/Scripts/Editor/BuildScript.cs:27-28
```csharp
Debug.LogError($"[BuildScript] WebGL build failed: {report.summary.totalErrors} errors");
Debug.Log($"[BuildScript] WebGL build succeeded: {buildPath}");
```
日志使用 `[ClassName]` 前缀。

### BUILD_ARTIFACT_VALIDATION
// SOURCE: Deploy/fix-webgl.sh:42-57
```bash
LOADER=$(find "$BUILD_DIR" -maxdepth 1 -name '*.loader.js' | head -1)
FRAMEWORK=$(find "$BUILD_DIR" -maxdepth 1 -name '*.framework.js' -o -name '*.framework.js.br' | head -1)
DATA=$(find "$BUILD_DIR" -maxdepth 1 \( -name '*.data' -o -name '*.data.br' \) | head -1)
WASM=$(find "$BUILD_DIR" -maxdepth 1 \( -name '*.wasm' -o -name '*.wasm.br' \) | head -1)
```
构建产物查找支持压缩和非压缩两种变体。

### TEST_STRUCTURE
// SOURCE: Assets/Tests/EditMode/ResultsExporterTests.cs:1-15
```csharp
using NUnit.Framework;

[TestFixture]
public class ExampleTests
{
    [Test]
    public void MethodName_Scenario_Expected()
    {
        // Arrange
        // Act
        // Assert
    }
}
```

### DOCKER_COMPOSE_PATTERN
// SOURCE: Deploy/docker-compose.yml:8-27
```yaml
services:
  edi-racing:
    build:
      context: ..
      dockerfile: Deploy/Dockerfile
    container_name: edi-racing-game
    ports:
      - "${GAME_PORT:-3900}:80"
    environment:
      - PORT=3000
    restart: unless-stopped
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `scripts/verify-webgl-build.sh` | CREATE | 构建产物完整性验证脚本（离线检查，不需要 Docker） |
| `scripts/verify-webgl-docker.sh` | CREATE | Docker 部署冒烟测试脚本（启动容器、检查健康、验证页面） |
| `.github/workflows/webgl-build-verify.yml` | CREATE | GitHub Actions CI 工作流，PR 时自动验证 |
| `Assets/Scripts/Editor/BuildScript.cs` | UPDATE | 添加构建后验证方法，检查产物完整性 |
| `Deploy/docker-compose.yml` | UPDATE | 添加 edi-racing 服务的 healthcheck |
| `Deploy/Dockerfile` | UPDATE | 添加构建产物验证步骤 |

## NOT Building

- Unity 构建本身的自动触发（需要 Unity license 和本地环境）
- PlayMode 测试（超出本任务范围）
- 浏览器端的 E2E 自动化测试（Playwright 集成是独立任务）
- Brotli 压缩修复（这是单独的优化任务）
- 生产环境部署脚本

---

## Step-by-Step Tasks

### Task 1: 创建构建产物验证脚本

- **ACTION**: 创建 `scripts/verify-webgl-build.sh`，验证 WebGL 构建输出的完整性
- **IMPLEMENT**:
  ```bash
  #!/bin/bash
  # scripts/verify-webgl-build.sh — Verify WebGL build artifacts are complete and valid
  #
  # Checks:
  #   1. Build directory exists with required artifacts
  #   2. index.html references correct file paths
  #   3. All referenced files actually exist
  #   4. File sizes are reasonable (not empty/corrupt)
  #   5. WebGL template was applied correctly
  #
  # Usage:
  #   ./scripts/verify-webgl-build.sh [BUILD_DIR]
  #   BUILD_DIR defaults to Deploy/webgl-build
  
  set -e
  
  SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
  PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
  BUILD_DIR="${1:-$PROJECT_DIR/Deploy/webgl-build}"
  
  RED='\033[0;31m'
  GREEN='\033[0;32m'
  YELLOW='\033[1;33m'
  NC='\033[0m'
  
  ERRORS=0
  WARNINGS=0
  
  check_pass() { echo -e "  ${GREEN}PASS${NC}  $1"; }
  check_fail() { echo -e "  ${RED}FAIL${NC}  $1"; ERRORS=$((ERRORS + 1)); }
  check_warn() { echo -e "  ${YELLOW}WARN${NC}  $1"; WARNINGS=$((WARNINGS + 1)); }
  
  echo ""
  echo "=== EDI Racing — WebGL Build Verification ==="
  echo "Build dir: $BUILD_DIR"
  echo ""
  
  # Check 1: Build directory exists
  echo "── Artifact Existence ──"
  [test conditions for loader, framework, data, wasm, index.html]
  
  # Check 2: File sizes
  echo "── File Sizes ──"
  [minimum size checks: loader >1KB, framework >10KB, wasm >1MB, data >1MB]
  
  # Check 3: index.html references
  echo "── HTML References ──"
  [grep index.html for valid Build/ paths, verify referenced files exist]
  
  # Check 4: Template validation
  echo "── Template ──"
  [check for EDI Racing branding, loading overlay, progress bar]
  
  # Summary
  echo ""
  if [ $ERRORS -eq 0 ]; then
    echo -e "${GREEN}All checks passed${NC} ($WARNINGS warnings)"
    exit 0
  else
    echo -e "${RED}$ERRORS checks failed${NC} ($WARNINGS warnings)"
    exit 1
  fi
  ```
- **MIRROR**: `Deploy/fix-webgl.sh` — 颜色输出、产物查找模式、错误消息风格
- **IMPORTS**: N/A (纯 bash)
- **GOTCHA**: macOS 和 Linux 的 `stat` 命令语法不同（`-f %z` vs `-c %s`），需要兼容处理
- **VALIDATE**: 
  - 对现有构建运行脚本，应全部通过
  - 删除一个构建文件后运行，应报错退出码 1
  - 创建空文件替换 .wasm 后运行，应报文件大小警告

### Task 2: 增强 BuildScript.cs 添加构建后验证

- **ACTION**: 在 `Assets/Scripts/Editor/BuildScript.cs` 中添加 `VerifyBuildArtifacts` 方法
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Post-build verification: checks that all required WebGL artifacts exist
  /// and index.html references valid files. Called after BuildWebGL succeeds.
  /// Can also be called standalone via:
  ///   Unity -batchmode -executeMethod BuildScript.VerifyBuildArtifacts
  /// </summary>
  public static void VerifyBuildArtifacts()
  {
      string buildPath = "Deploy/webgl-build";
      string buildDir = Path.Combine(buildPath, "Build");
      string indexPath = Path.Combine(buildPath, "index.html");
      
      // Verify Build/ directory
      // Verify index.html exists
      // Verify *.loader.js, *.framework.js, *.wasm, *.data files exist
      // Verify index.html references match actual files
      // Log results with [BuildScript] prefix
  }
  ```
  在 `BuildWebGL()` 成功后调用 `VerifyBuildArtifacts()`。
- **MIRROR**: `BuildScript.cs` — 现有的日志格式 `[BuildScript]`，`EditorApplication.Exit(1)` 错误退出
- **IMPORTS**: `using System.IO;`, `using System.Linq;`, `using System.Text.RegularExpressions;`
- **GOTCHA**: `Directory.GetFiles()` 路径使用正斜杠，Unity 的路径在不同 OS 上需要 `Path.Combine`
- **VALIDATE**: 在 Unity Editor 中通过菜单或命令行调用 `VerifyBuildArtifacts`，验证通过

### Task 3: 创建 Docker 部署冒烟测试脚本

- **ACTION**: 创建 `scripts/verify-webgl-docker.sh`，启动 Docker 容器并验证 WebGL 页面可访问
- **IMPLEMENT**:
  ```bash
  #!/bin/bash
  # scripts/verify-webgl-docker.sh — Smoke test WebGL deployment via Docker
  #
  # What it does:
  #   1. Runs verify-webgl-build.sh first (artifact check)
  #   2. Builds and starts Docker container
  #   3. Waits for container to be healthy
  #   4. Verifies HTTP 200 on index.html
  #   5. Verifies WebSocket server responds
  #   6. Checks critical static assets are served with correct MIME types
  #   7. Tears down container
  #
  # Usage:
  #   ./scripts/verify-webgl-docker.sh
  
  # Step 1: Run artifact verification
  "$SCRIPT_DIR/verify-webgl-build.sh" || exit 1
  
  # Step 2: Build Docker image (without compose dependencies)
  docker build -t edi-racing-verify -f Deploy/Dockerfile .
  
  # Step 3: Start container
  CONTAINER_ID=$(docker run -d -p 8099:80 -e PORT=3000 edi-racing-verify)
  
  # Step 4: Wait for readiness (poll with timeout)
  # curl http://localhost:8099/ — expect 200
  
  # Step 5: Verify responses
  # - GET / → 200, contains "unity-canvas"
  # - GET /Build/*.loader.js → 200, Content-Type: application/javascript
  # - GET /Build/*.wasm → 200, Content-Type: application/wasm
  
  # Step 6: Cleanup
  docker stop $CONTAINER_ID && docker rm $CONTAINER_ID
  ```
- **MIRROR**: `Deploy/fix-webgl.sh` — 脚本风格、颜色输出
- **IMPORTS**: N/A
- **GOTCHA**: 
  - Docker 冒烟测试不包含 `web-app` 依赖 — 使用独立 `docker run` 而非 `docker-compose`
  - 端口 8099 避免与开发服务器冲突
  - 需要在清理阶段使用 `trap` 确保容器被停止
- **VALIDATE**: 
  - 有正确构建产物时运行：所有检查通过
  - 构建产物缺失时运行：在 Step 1 就失败

### Task 4: 添加 Docker healthcheck

- **ACTION**: 为 `Deploy/docker-compose.yml` 中的 `edi-racing` 服务添加 healthcheck
- **IMPLEMENT**:
  ```yaml
  edi-racing:
    # ... existing config ...
    healthcheck:
      test: ["CMD", "wget", "-q", "--spider", "http://localhost:80"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 5s
  ```
- **MIRROR**: `docker-compose.yml:43-47` — web-app 服务已有 healthcheck，使用相同格式
- **IMPORTS**: N/A
- **GOTCHA**: nginx:alpine 镜像自带 `wget`，不需要额外安装
- **VALIDATE**: `docker-compose up -d && docker-compose ps` 显示 `healthy` 状态

### Task 5: 增强 Dockerfile 添加构建产物验证

- **ACTION**: 在 `Deploy/Dockerfile` 的 COPY 步骤后添加产物验证 RUN 指令
- **IMPLEMENT**:
  ```dockerfile
  # Copy WebGL build output
  COPY Deploy/webgl-build/ /usr/share/nginx/html/
  
  # Verify build artifacts are present (fail fast if build is incomplete)
  RUN test -f /usr/share/nginx/html/index.html \
      && ls /usr/share/nginx/html/Build/*.loader.js >/dev/null 2>&1 \
      && ls /usr/share/nginx/html/Build/*.wasm >/dev/null 2>&1 \
      && ls /usr/share/nginx/html/Build/*.data >/dev/null 2>&1 \
      && ls /usr/share/nginx/html/Build/*.framework.js >/dev/null 2>&1 \
      || (echo "ERROR: WebGL build artifacts incomplete" && exit 1)
  ```
- **MIRROR**: Dockerfile 现有风格 — 简洁的 RUN 指令
- **IMPORTS**: N/A
- **GOTCHA**: `.br` 变体需要在通配符中兼容（`*.framework.js*`），但当前构建未压缩
- **VALIDATE**: `docker build -t test -f Deploy/Dockerfile .` 成功；删除 `.wasm` 后构建失败

### Task 6: 创建 GitHub Actions CI 工作流

- **ACTION**: 创建 `.github/workflows/webgl-build-verify.yml`
- **IMPLEMENT**:
  ```yaml
  name: WebGL Build Verification
  
  on:
    pull_request:
      paths:
        - 'Assets/**'
        - 'ProjectSettings/**'
        - 'Packages/**'
        - 'Deploy/**'
        - 'Server/**'
  
  jobs:
    verify-artifacts:
      name: Verify Build Artifacts
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        
        - name: Verify WebGL build artifacts
          run: |
            chmod +x scripts/verify-webgl-build.sh
            ./scripts/verify-webgl-build.sh
    
    verify-docker:
      name: Docker Smoke Test
      runs-on: ubuntu-latest
      needs: verify-artifacts
      steps:
        - uses: actions/checkout@v4
        
        - name: Build and test Docker image
          run: |
            chmod +x scripts/verify-webgl-docker.sh
            ./scripts/verify-webgl-docker.sh
    
    unity-tests:
      name: Unity EditMode Tests
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
          with:
            lfs: true
        
        - uses: game-ci/unity-test-runner@v4
          env:
            UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          with:
            projectPath: .
            testMode: EditMode
            unityVersion: 6000.1.3f1
  ```
- **MIRROR**: `.claude/docs/coding-standards.md` — "Automated test suite runs on every push to main and every PR"
- **IMPORTS**: N/A
- **GOTCHA**: 
  - `game-ci/unity-test-runner@v4` 需要 `UNITY_LICENSE` secret 配置
  - Unity 版本需要与项目匹配（6000.x 对应 Unity 6.x）
  - 构建产物已提交到 git 则 `verify-artifacts` 可行；若 .gitignore 忽略了 Deploy/webgl-build/，需调整策略
- **VALIDATE**: Push 到分支创建 PR 后检查 Actions 运行状态

---

## Testing Strategy

### Unit Tests

本任务不涉及新的 C# 游戏逻辑，但 `BuildScript.VerifyBuildArtifacts` 可通过以下方式验证：

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| 完整构建产物验证 | 现有 Deploy/webgl-build/ | 通过 (exit 0) | No |
| 缺失 .wasm 文件 | 删除 .wasm 后运行 | 失败 (exit 1) | Yes |
| 空 index.html | 清空 index.html 内容 | 失败 (exit 1) | Yes |
| index.html 引用不存在的文件 | 修改 index.html 中路径 | 失败 (exit 1) | Yes |
| 文件大小为 0 | 创建空的 .wasm 文件 | 警告 | Yes |
| Brotli 压缩构建 | .br 文件变体 | 通过 (exit 0) | Yes |

### Edge Cases Checklist
- [x] 空构建目录
- [x] 部分文件缺失
- [x] 文件大小为零（损坏的构建）
- [x] index.html 路径引用不匹配
- [x] Brotli 压缩 vs 未压缩构建
- [ ] 并发访问 (不适用)
- [x] Docker 启动失败（端口冲突）
- [x] 网络超时（Docker healthcheck）

---

## Validation Commands

### Static Analysis
```bash
# Verify shell scripts have no syntax errors
bash -n scripts/verify-webgl-build.sh
bash -n scripts/verify-webgl-docker.sh
```
EXPECT: No output (clean parse)

### Build Artifact Verification
```bash
# Run the artifact verification script
./scripts/verify-webgl-build.sh
```
EXPECT: All checks pass, exit code 0

### Docker Smoke Test
```bash
# Run full Docker deployment verification
./scripts/verify-webgl-docker.sh
```
EXPECT: Container starts, HTTP 200 returned, all MIME types correct

### Unity Build Verification (manual)
```bash
# Run Unity build verification method
/Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath . \
  -executeMethod BuildScript.VerifyBuildArtifacts \
  -logFile - -quit
```
EXPECT: "[BuildScript] All build artifacts verified" in output

### CI Workflow Validation
```bash
# Validate GitHub Actions YAML syntax
gh workflow view webgl-build-verify.yml 2>/dev/null || echo "Workflow not yet pushed"
```
EXPECT: Valid workflow definition

### Manual Validation
- [ ] 运行 `./scripts/verify-webgl-build.sh` 对现有构建输出 — 全部通过
- [ ] 手动删除 `Deploy/webgl-build/Build/*.wasm` 后运行脚本 — 报错退出
- [ ] 恢复文件后运行 `./scripts/verify-webgl-docker.sh` — Docker 容器启动并通过冒烟测试
- [ ] 浏览器访问 `localhost:8099` — 看到 EDI Racing 加载页面
- [ ] `docker-compose up -d && docker-compose ps` — edi-racing 服务显示 healthy

---

## Acceptance Criteria
- [ ] `scripts/verify-webgl-build.sh` 脚本完成并可执行
- [ ] `scripts/verify-webgl-docker.sh` 脚本完成并可执行
- [ ] `BuildScript.cs` 包含 `VerifyBuildArtifacts()` 方法
- [ ] `Dockerfile` 包含构建产物验证步骤
- [ ] `docker-compose.yml` 的 edi-racing 服务包含 healthcheck
- [ ] `.github/workflows/webgl-build-verify.yml` 语法正确
- [ ] 所有验证脚本对现有构建通过
- [ ] 所有验证脚本对缺损构建正确报错

## Completion Checklist
- [ ] 脚本遵循 `Deploy/fix-webgl.sh` 的风格模式（颜色输出、错误消息）
- [ ] 错误处理使用 `set -e` + 明确的错误消息
- [ ] 日志遵循 `[ClassName]` 前缀格式
- [ ] Shell 脚本通过 `bash -n` 语法检查
- [ ] 没有硬编码路径（使用变量和 PROJECT_DIR 计算）
- [ ] 文档已在脚本头部注释中完善
- [ ] 没有超出范围的额外功能
- [ ] 自包含 — 实施时无需额外搜索代码库

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| WebGL 构建产物未提交到 git | Medium | High | 检查 .gitignore，如果忽略则 CI 中需要先构建（需要 Unity license） |
| Docker 冒烟测试在 CI 中端口冲突 | Low | Low | 使用随机高端口 (8099) 并在 cleanup 中确保释放 |
| `game-ci/unity-test-runner` 版本不支持 Unity 6 | Medium | Medium | 固定版本 v4，检查兼容性表 |
| macOS vs Linux 的 `stat` 命令差异 | High | Low | 使用 `wc -c < file` 替代 `stat` 获取文件大小 |
| 大文件 (278MB .data) 导致 CI 缓慢 | Medium | Low | 产物验证脚本仅检查存在性和大小，不上传 |

## Notes
- **当前构建产物是未压缩的**：`Deploy/webgl-build/Build/` 中的文件没有 `.br` 后缀，但 `ProjectSettings` 中 `webGLCompressionFormat: 1` 表示配置了 Brotli。建议后续任务调查压缩配置是否正确。
- **Server/ 依赖**：Docker 冒烟测试使用独立 `docker run`（不经过 compose），因此不需要 `web-app` 服务。WebSocket server 内置在同一容器中。
- **CI 成本**：Unity license 是 CI 中运行 Unity 测试的前置条件。如果暂时没有 license secret，可以先只运行构建产物验证和 Docker 冒烟测试。
