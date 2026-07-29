# Plan: 上线前生产阻塞修复（归档端点密钥 + 影子路由 + 赛果浮层缺口）

## Summary
三项上线阻塞问题的单遍实施计划：**S1** — `POST /api/sessions/archive` 用硬编码公开默认密钥保护、且启动守卫不覆盖它，任何人可伪造任意教授账户的会话归档；**A1** — `responseRoutes`/`resultsRoutes` 被双前缀挂载，每条路由有两个可达 URL（含一条未测试的归档影子路径），扩大攻击面；**#2** — `RaceFinishPanel` 在出货场景零 GUID 引用、唯一实例化点是不在任何场景里的开发期回退脚手架 `RuntimeSetup.cs`，赛果浮层在出货构建可能根本不显示（功能缺口，需人工确认意图后走删除或补线两分支之一）。

> **与既有计划的关系**：`.claude/PRPs/plans/host-token-secret-guard.plan.md` 已交付 `checkSecretConfig` **host-token 铸造边界**的启动守卫（受 `REQUIRE_HOST_TOKEN` 门控，已在代码中）。本计划**互补**：它处理该守卫**明确未覆盖**的独立授权边界——归档端点（`REQUIRE_HOST_TOKEN=false` 生产默认下仍开放），以及 A1、#2。

## User Story
As a 部署 EDI Racing 的教师/运维,
I want 会话归档端点不能被公开默认密钥伪造、每条 API 只有一个可达 URL、赛果在出货构建里可靠呈现,
So that 生产环境不会泄露/污染教授数据，攻击面最小，且课堂结束时学生能看到比赛结果。

## Problem → Solution
- **S1**: 未设 `INTERNAL_SECRET` 时归档端点接受公开值 `'edi-internal-default'` 作为有效凭证，且 `REQUIRE_HOST_TOKEN=false`（生产默认）下启动守卫只 `warn` 不拦截 → 归档授权改为**独立于 `REQUIRE_HOST_TOKEN`**：密钥为默认/未设时端点 fail-closed（503），并在启动时明确告警该端点已禁用；同时消除重复字面量，从 `hostToken.js` 导入 `DEFAULT_INTERNAL_SECRET` 作为唯一真源。
- **A1**: 每个 router 挂两个前缀产生影子 URL → 把子路径烘焙进各路由，两个 router 各只在 `/api` 挂载一次，消除影子 URL。
- **#2**: 赛果浮层只由未入场景的开发脚手架构建 → 人工确认出货场景是否已用其它方式呈现赛果；**若是**则删除 `RaceFinishPanel.cs`(+`.meta`)，**若否**则把带 `RaceFinishPanel` 组件的 GameObject 接入出货场景并接线 `RaceManager`/`ScoreManager`。

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A（来自代码审查发现清单）
- **PRD Phase**: N/A
- **Estimated Files**: web-app 侧 2 个源文件 + 1~2 个测试文件；Unity 侧 1 个场景或 1 个脚本删除（取决于 #2 决策）

---

## UX Design

### Before
```
S1/A1 — 纯后端/安全，无用户可见 UX 变化。
#2 — 出货构建里比赛结束（GameState.Finished）：
┌───────────────────────────────┐
│  排行榜面板仍显示（烘焙进场景） │
│  事件日志面板仍显示             │
│  ？冠军/赛果浮层 —— 可能不出现  │  ← RaceFinishPanel 零场景引用
└───────────────────────────────┘
```

### After
```
#2 分支 A（赛果已由排行榜/别处呈现，确认冗余）：
┌───────────────────────────────┐
│  排行榜面板显示最终名次         │  ← 维持现状，删除死文件
└───────────────────────────────┘

#2 分支 B（确认缺口，补线）：
┌───────────────────────────────┐
│      RACE FINISHED!            │
│      Winner: <TeamName>        │  ← RaceFinishPanel 覆盖层
│      ── Final Standings ──     │
│      1. ...  2. ...  3. ...    │
└───────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| `POST /api/sessions/archive`（默认密钥部署） | 接受 `x-internal-secret: edi-internal-default` → 200 写库 | 返回 503（端点禁用），任何 header 都无法通过 | fail-closed |
| `GET /api/surveys/s/:code` 等影子 URL | 可达（未测试） | 404（不再存在） | 攻击面收敛 |
| 比赛结束浮层 | 出货构建可能无 | 分支 B：稳定显示；分支 A：由排行榜承担 | 取决于 #2 决策 |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/results.js` | 62-103 | S1 目标端点；当前硬编码密钥与比较逻辑 |
| P0 | `web-app/src/hostToken.js` | 19-52 | `DEFAULT_INTERNAL_SECRET` 与 `checkSecretConfig`（复用的纯决策函数） |
| P0 | `web-app/src/index.js` | 19-50 | 启动守卫 + 全部路由挂载点（S1 守卫、A1 挂载） |
| P0 | `web-app/src/routes/responses.js` | 全部 | A1：需重排路径的 router 之一 |
| P1 | `web-app/__tests__/host-token.test.js` | 1-108 | 测试模式（vitest、纯函数、确定性时钟、`checkSecretConfig` 矩阵） |
| P1 | `web-app/client/src/api.js` | 18-27, 100-210 | 客户端统一加 `/api` 前缀，调用规范 URL（A1 不会打断前端） |
| P1 | `Server/server.js` | 150-165, 615-625 | 归档端点唯一真实调用者（带 `x-internal-secret`）+ 镜像启动守卫 |
| P1 | `Assets/Scripts/RuntimeSetup.cs` | 1-30, 232-245 | #2：开发期回退脚手架，`BuildFinishPanel` 唯一实例化点，`Awake` 构建全部运行时 UI |
| P2 | `Assets/Scripts/UI/RaceFinishPanel.cs` | 全部 | #2：程序化构建覆盖层，订阅 `RaceManager.OnRaceFinished` |
| P2 | `.claude/PRPs/plans/host-token-secret-guard.plan.md` | Summary | 既有守卫计划——本计划补其未覆盖的归档边界，勿重复 host-token 部分 |

## External Documentation
无需外部研究 —— 全部使用已建立的内部模式（express Router、vitest、Unity MonoBehaviour/事件订阅）。

---

## Patterns to Mirror

### SINGLE_SOURCE_OF_TRUTH_SECRET
```js
// SOURCE: web-app/src/hostToken.js:22-24
export const DEFAULT_INTERNAL_SECRET = 'edi-internal-default';
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || DEFAULT_INTERNAL_SECRET;
```
→ S1 中 `results.js` 必须 `import { DEFAULT_INTERNAL_SECRET } from '../hostToken.js'`，不再重复字面量。

### PURE_DECISION_FUNCTION（可纯单测，不碰 process.env）
```js
// SOURCE: web-app/src/hostToken.js:34-52
export function checkSecretConfig({ secret, requireHostToken }) {
  const isDefault = !secret || secret === DEFAULT_INTERNAL_SECRET;
  if (!isDefault) return { level: 'ok', message: '' };
  ...
}
```
→ S1 新增的“归档密钥是否可用”判断应做成同样风格的纯函数，供路由与（可选）启动日志共用。

### EXPRESS_ROUTE_GUARD（早返回 + 标准结果契约）
```js
// SOURCE: web-app/src/routes/results.js:64-67
router.post('/sessions/archive', (req, res) => {
  if (req.headers['x-internal-secret'] !== INTERNAL_SECRET) {
    return res.status(403).json({ success: false, error: 'Forbidden' });
  }
```
→ 所有响应遵循 `{ success: boolean, error?: string, data?: ... }` 契约。

### BOOT_GUARD（listen 前决策，fatal→exit(1)，warn→console.warn）
```js
// SOURCE: web-app/src/index.js:21-32
const secretCheck = checkSecretConfig({ secret: process.env.INTERNAL_SECRET, requireHostToken: REQUIRE_HOST_TOKEN });
if (secretCheck.level === 'fatal') { console.error(`[Auth] FATAL: ${secretCheck.message}`); process.exit(1); }
if (secretCheck.level === 'warn')  { console.warn(`[Auth] WARNING: ${secretCheck.message}`); }
```
→ S1 在此追加归档端点相关告警。

### ROUTER_PREFIX_BAKING（每 router 挂一次，子路径写进路由）
```js
// SOURCE: web-app/src/routes/surveys.js:24
router.get('/:id/responses/count', requireAuth, (req, res) => { ... });
// 挂载：app.use('/api/surveys', surveyRoutes)  → /api/surveys/:id/responses/count
```
→ A1 采用同一原则，但把两个混合前缀的 router 统一挂到 `/api`，用完整子路径区分。

### VITEST_PURE_TEST（确定性、无共享全局、显式输入）
```js
// SOURCE: web-app/__tests__/host-token.test.js:78-93
it('is fatal when enforcement is on and secret is the default', () => {
  expect(checkSecretConfig({ secret: DEFAULT_INTERNAL_SECRET, requireHostToken: true }).level).toBe('fatal');
});
```

### UNITY_EVENT_SUBSCRIPTION（OnEnable 订阅 / OnDisable 退订 + Inspector 可赋值引用）
```csharp
// SOURCE: Assets/Scripts/UI/RaceFinishPanel.cs:11-28
public RaceManager RaceManager;
public ScoreManager ScoreManager;
private void OnEnable()  { if (RaceManager != null) RaceManager.OnRaceFinished += ShowFinish; }
private void OnDisable() { if (RaceManager != null) RaceManager.OnRaceFinished -= ShowFinish; }
```
→ #2 分支 B 若在场景中放置，须在 Inspector 里赋 `RaceManager`/`ScoreManager`（镜像 `RuntimeSetup.BuildFinishPanel` 的接线）。

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/routes/results.js` | UPDATE | S1：导入 `DEFAULT_INTERNAL_SECRET`；归档端点在默认/未设密钥时 fail-closed（503）；消除重复字面量。A1：把 `/:id/results` 改为 `/surveys/:id/results` |
| `web-app/src/routes/responses.js` | UPDATE | A1：把 `/:id/responses` 改为 `/surveys/:id/responses`（`/s/*` 保持不变） |
| `web-app/src/index.js` | UPDATE | A1：两 router 各只挂 `/api` 一次，删除双前缀。S1：启动日志追加归档端点禁用告警（可选，复用纯函数） |
| `web-app/__tests__/results-archive.test.js` | CREATE | S1：归档密钥可用性纯函数的确定性矩阵测试 |
| `web-app/__tests__/route-mounting.test.js` | CREATE（可选） | A1：断言规范 URL 命中、影子 URL 404（需引入 supertest，见 GOTCHA） |
| `Assets/Scenes/complete_track_demo.unity` **或** `Assets/Scripts/UI/RaceFinishPanel.cs`(+`.meta`) | UPDATE **或** DELETE | #2：分支 B 补线到出货场景 / 分支 A 删除死文件（**待人工确认后二选一**） |

## NOT Building
- 不改 `hostToken.js` 的令牌线格式或 `checkSecretConfig` 决策逻辑（与 `Server/server.js` 逐字节镜像，动它需两处 lockstep）。
- 不重做 host-token 铸造边界的启动守卫（已由 `host-token-secret-guard.plan.md` 交付）。
- 不把 `REQUIRE_HOST_TOKEN` 默认改为 `true`（那是独立的部署策略决定，超出本次范围）。
- 不改 `Server/server.js` 调用归档的方式（它已带 `x-internal-secret: INTERNAL_SECRET`；生产设了强密钥即正常）。
- 不重构其它 router（auth/surveys/export/templates/game-status）的挂载。
- 不给 `RaceFinishPanel` 加新功能/换 UI Toolkit —— 仅接线或删除。
- 不引入 DOTS/UI Toolkit/Addressables 等新体系。

---

## Step-by-Step Tasks

### Task 1: S1 — 归档端点消除硬编码默认密钥并 fail-closed
- **ACTION**: 修改 `web-app/src/routes/results.js`。
- **IMPLEMENT**:
  1. 顶部 `import { DEFAULT_INTERNAL_SECRET } from '../hostToken.js';`
  2. 用纯函数判断密钥是否可作归档凭证：
     ```js
     const INTERNAL_SECRET = process.env.INTERNAL_SECRET || DEFAULT_INTERNAL_SECRET;
     // 归档是独立于 REQUIRE_HOST_TOKEN 的授权边界：公开默认值绝不可作为有效凭证。
     export function archiveSecretUsable(secret) {
       return typeof secret === 'string' && secret.length > 0 && secret !== DEFAULT_INTERNAL_SECRET;
     }
     const ARCHIVE_ENABLED = archiveSecretUsable(process.env.INTERNAL_SECRET);
     ```
  3. 归档路由改为先 fail-closed，再做常量时间比较：
     ```js
     router.post('/sessions/archive', (req, res) => {
       if (!ARCHIVE_ENABLED) {
         return res.status(503).json({ success: false, error: 'Session archiving is disabled: set a strong INTERNAL_SECRET.' });
       }
       const provided = req.headers['x-internal-secret'] || '';
       const a = Buffer.from(provided);
       const b = Buffer.from(INTERNAL_SECRET);
       if (a.length !== b.length || !timingSafeEqual(a, b)) {
         return res.status(403).json({ success: false, error: 'Forbidden' });
       }
       // ...原有写库逻辑不变...
     });
     ```
- **MIRROR**: SINGLE_SOURCE_OF_TRUTH_SECRET、PURE_DECISION_FUNCTION、EXPRESS_ROUTE_GUARD。
- **IMPORTS**: `import { timingSafeEqual } from 'crypto';`（若加常量时间比较）；`import { DEFAULT_INTERNAL_SECRET } from '../hostToken.js';`
- **GOTCHA**:
  - `x-internal-secret` 缺失时 `req.headers[...]` 为 `undefined`，`Buffer.from(undefined)` 会抛错 —— 先 `|| ''`。
  - `timingSafeEqual` 要求等长，长度不等直接判失败（勿把长度差异喂给它）。
  - `export` 该纯函数以便测试；`results.js` 是 `export default router`，命名导出与默认导出可共存。
  - 生产默认 `REQUIRE_HOST_TOKEN=false` 下，若同时没设 `INTERNAL_SECRET`，归档将被禁用 —— 这是预期的 fail-closed（要归档就必须设强密钥）。
- **VALIDATE**: `archiveSecretUsable('edi-internal-default') === false`；`archiveSecretUsable(undefined) === false`；`archiveSecretUsable('strong-random') === true`。

### Task 2: S1 — 启动时明确告警归档端点状态（可选但推荐）
- **ACTION**: 在 `web-app/src/index.js` 启动守卫块后追加一行归档状态日志。
- **IMPLEMENT**:
  ```js
  import resultsRoutes, { archiveSecretUsable } from './routes/results.js';
  ...
  if (!archiveSecretUsable(process.env.INTERNAL_SECRET)) {
    console.warn('[Auth] WARNING: /api/sessions/archive is DISABLED — INTERNAL_SECRET is unset or the public default. Set a strong secret to enable session archiving.');
  }
  ```
- **MIRROR**: BOOT_GUARD。
- **IMPORTS**: 从 `./routes/results.js` 追加命名导入 `archiveSecretUsable`。
- **GOTCHA**: 保持默认导入 `resultsRoutes` 不变，追加命名导入即可（`import def, { named } from '...'`）。
- **VALIDATE**: 不设 `INTERNAL_SECRET` 启动 → 控制台出现该 WARNING；设强密钥 → 无该行。

### Task 3: A1 — 消除响应/结果路由的影子 URL
- **ACTION**: 修改 `responses.js`、`results.js` 的路由路径，并把 `index.js` 挂载改为每 router 一次。
- **IMPLEMENT**:
  - `web-app/src/routes/responses.js`：`router.get('/:id/responses', ...)` → `router.get('/surveys/:id/responses', ...)`（`/s/:shareCode`、`/s/:shareCode/respond` 保持不变）。
  - `web-app/src/routes/results.js`：`router.post('/:id/results', ...)` 与 `router.get('/:id/results', ...)` → `'/surveys/:id/results'`（`/sessions/archive`、`/sessions` 保持不变）。
  - `web-app/src/index.js` 第 46-49 行由四行改两行：
    ```js
    app.use('/api', responseRoutes);   // /api/s/*, /api/surveys/:id/responses
    app.use('/api', resultsRoutes);    // /api/surveys/:id/results, /api/sessions*, /api/sessions/archive
    ```
    删除 `app.use('/api/surveys', responseRoutes)` 与 `app.use('/api/surveys', resultsRoutes)`。
- **MIRROR**: ROUTER_PREFIX_BAKING。
- **IMPORTS**: 无新增。
- **GOTCHA**:
  - 客户端 `api.js` 的 `request()` 统一前缀 `/api`（第 25 行 `fetch(\`/api${path}\`)`），调用的是 `/surveys/:id/responses`、`/surveys/:id/results`、`/sessions`、`/api/s/...` —— 全部命中新规范 URL，**不会打断前端**（已核对）。
  - `surveys.js` 仍挂 `/api/surveys` 且有 `/:id/responses/count`；与 responseRoutes 的 `/surveys/:id/responses` **不冲突**（express 精确匹配完整路径，`.../responses/count` 多一段不会误命中）。挂载顺序上 `surveyRoutes`(第43行) 在前，无覆盖问题。
  - 别漏改 `results.js` 里的两个 `/:id/results`（POST 和 GET 各一条）。
- **VALIDATE**: 见 Task 6 手动 curl 矩阵；规范 URL 200/403、旧影子 URL（`/api/surveys/s/x`、`/api/5/results`、`/api/surveys/sessions/archive`）返回 404。

### Task 4: S1 测试 — 归档密钥可用性纯函数矩阵
- **ACTION**: 新建 `web-app/__tests__/results-archive.test.js`。
- **IMPLEMENT**:
  ```js
  import { describe, it, expect } from 'vitest';
  import { archiveSecretUsable } from '../src/routes/results.js';
  import { DEFAULT_INTERNAL_SECRET } from '../src/hostToken.js';

  describe('archiveSecretUsable', () => {
    it('rejects the public default secret', () => {
      expect(archiveSecretUsable(DEFAULT_INTERNAL_SECRET)).toBe(false);
    });
    it('rejects an unset (undefined) secret', () => {
      expect(archiveSecretUsable(undefined)).toBe(false);
    });
    it('rejects an empty string secret', () => {
      expect(archiveSecretUsable('')).toBe(false);
    });
    it('accepts a strong non-default secret', () => {
      expect(archiveSecretUsable('phase7-adversarial-qa-secret-0123456789abcdef')).toBe(true);
    });
  });
  ```
- **MIRROR**: VITEST_PURE_TEST（对照 `host-token.test.js` 的 `checkSecretConfig` 矩阵）。
- **IMPORTS**: 见片段。
- **GOTCHA**: 导入 `../src/routes/results.js` 会执行该模块顶层（创建 router、读 `process.env.INTERNAL_SECRET`）——纯函数不依赖模块级 `ARCHIVE_ENABLED`，所以对 env 无要求；保持函数纯（参数进、布尔出）。
- **VALIDATE**: `cd web-app && npm test` 该文件 4 条全绿。

### Task 5: A1 测试 — 路由挂载 URL 断言（可选）
- **ACTION**: 新建 `web-app/__tests__/route-mounting.test.js`（若引入 supertest）。
- **IMPLEMENT**: 用 supertest 挂载一个仅含 responseRoutes/resultsRoutes 的最小 express app，断言规范 URL 命中处理器、影子 URL 得 404。
  ```js
  import express from 'express';
  import request from 'supertest';
  import responseRoutes from '../src/routes/responses.js';
  import resultsRoutes from '../src/routes/results.js';
  const app = express();
  app.use('/api', responseRoutes);
  app.use('/api', resultsRoutes);
  // 例：影子路径应 404
  it('has no shadow archive path', async () => {
    const r = await request(app).post('/api/surveys/sessions/archive');
    expect(r.status).toBe(404);
  });
  ```
- **MIRROR**: 现有 vitest 结构。
- **IMPORTS**: 需 `npm i -D supertest`（当前无此依赖）。
- **GOTCHA**: 需真实 DB 的路由（`/responses`、`/results`、`/sessions`）会触碰 `getDb()`；若不想连库，仅测“路由是否存在/404”即可（404 由 express 路由层给出，处理器不执行）。若嫌引入 supertest 过重，可跳过本任务，改用 Task 6 的手动 curl 验证。
- **VALIDATE**: 影子 URL 全 404；规范 URL 非 404。

### Task 6: A1/S1 手动验证矩阵（curl）
- **ACTION**: 本地起 web-app 后跑一组 curl，确认规范 URL 可达、影子 URL 消失、归档 fail-closed。
- **IMPLEMENT**: 见下方 “Browser/Manual Validation”。
- **VALIDATE**: 全部符合期望表。

### Task 7: #2 — 人工确认赛果呈现方式（决策门，先于 7A/7B）
- **ACTION**: 向用户确认：出货场景 `complete_track_demo.unity` 里，比赛结束（`GameState.Finished`）时赛果由什么呈现？（排行榜面板在 Finished 仍显示——见 `RuntimeSetup.OnStateChanged`；是否已足够？还是设计上需要独立的冠军/赛果浮层？）
- **背景（已核实）**:
  - `RaceFinishPanel` 的 GUID `56377efdab6f34add88f03df15f3a432` 在 `Assets` 下**零** `.unity`/`.prefab` 引用。
  - 唯一实例化点 `RuntimeSetup.BuildFinishPanel`（`Assets/Scripts/RuntimeSetup.cs:234-242`）。
  - `RuntimeSetup` GUID `6bd588b065ed04ba7a0330e01b74b545` **不在任何场景**；其类注释：“Auto-wires ... at runtime when scene objects are not yet configured. Attach this to the RaceManager GameObject. Safe to remove once the scene is fully set up in the Editor.” → 它是**开发期回退脚手架**，靠 `Awake()`（非 `RuntimeInitializeOnLoadMethod`），会程序化构建整套运行时 UI（相机管理、EventSystem、Canvas、排行榜、控制面板、事件日志、FinishPanel）。
  - 出货场景引用了约 80 个 GUID 但不含 `RuntimeSetup` → 场景已“在编辑器中完整搭好”，运行时 UI 应为烘焙进场景的实体对象；缺的只是 `RaceFinishPanel` 这一程序化覆盖层。
  - 因此 `RaceFinishPanel` 在出货构建里确实**不会被实例化**，除非补线。
- **DECISION 输出**: 走 7A（删除）或 7B（补线）。**这是唯一需要人工输入的门；其余任务无需再问。**

### Task 7A: #2 分支 A — 赛果已由别处呈现 → 删除死文件
- **ACTION**: 删除 `Assets/Scripts/UI/RaceFinishPanel.cs` 与 `Assets/Scripts/UI/RaceFinishPanel.cs.meta`；同步移除 `RuntimeSetup.cs` 中对 `RaceFinishPanel` 的引用（字段 `finishPanel`、`BuildFinishPanel`、其调用点及 `OnStateChanged` 里对 finishPanel 的 Show/Hide）。
- **MIRROR**: 保持 `RuntimeSetup` 其余脚手架逻辑不变。
- **GOTCHA**: 优先用 UnitySkills API（`http://localhost:8090`，`/unity-skills` skill）删除资产以让 Unity 正确处理 `.meta`；API 不支持再直接删两个文件。删除后须确认 `RuntimeSetup.cs` 仍能编译（移除所有 `RaceFinishPanel` 符号引用，否则报 CS0246）。
- **VALIDATE**: Unity 编译零错误；`grep -rn RaceFinishPanel Assets/Scripts` 无残留引用；出货场景比赛结束仍由排行榜显示名次。

### Task 7B: #2 分支 B — 存在缺口 → 补线到出货场景
- **ACTION**: 在 `Assets/Scenes/complete_track_demo.unity` 中放置一个挂 `RaceFinishPanel` 组件的 GameObject（置于运行时 UI 的 Canvas 下），并在 Inspector 赋值 `RaceManager`、`ScoreManager` 引用。
- **IMPLEMENT**: 优先经 UnitySkills API 完成（创建 GameObject、AddComponent、设置引用）；镜像 `RuntimeSetup.BuildFinishPanel`（`RaceFinishPanel.cs` 会在 `OnEnable` 订阅 `RaceManager.OnRaceFinished`，并在首次触发时程序化构建覆盖层——无需预制体，但组件必须真实存在于场景并激活）。
- **MIRROR**: UNITY_EVENT_SUBSCRIPTION；接线参照 `RuntimeSetup.cs:238-240`（`finishPanel.RaceManager = raceManager; finishPanel.ScoreManager = scoreManager;`）。
- **GOTCHA**:
  - `RaceFinishPanel` 需要位于某个 `Canvas` 之下（`ShowFinish`→`BuildPanel` 取 `GetComponentInParent<Canvas>()`）；放到出货场景已有的运行时 UI Canvas 下。
  - GameObject 若默认 inactive，`OnEnable` 不会执行、就不会订阅事件——确保它 active（`RuntimeSetup` 里 FinishPanel 自身 active、只是内部 overlay `SetActive(false)`）。
  - `RaceManager`/`ScoreManager` 引用必须是场景内实例的 GUID 指向，别留空（否则 `OnEnable` 的 null 检查会静默跳过订阅——功能仍缺）。
- **VALIDATE**: 出货场景播放一局至结束 → 冠军/名次覆盖层出现；截图存 `production/qa/evidence/`（Visual/UI 类为 ADVISORY，按 coding-standards 需截图 + lead sign-off）。

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `archiveSecretUsable(DEFAULT_INTERNAL_SECRET)` | 公开默认值 | `false` | 是（核心漏洞） |
| `archiveSecretUsable(undefined)` | 未设 | `false` | 是 |
| `archiveSecretUsable('')` | 空串 | `false` | 是 |
| `archiveSecretUsable('strong-random')` | 强密钥 | `true` | 否 |
| （可选 supertest）`POST /api/surveys/sessions/archive` | 影子路径 | 404 | 是 |
| （可选 supertest）`GET /api/5/results` | 影子路径 | 404 | 是 |

### Edge Cases Checklist
- [x] 空/缺失 `x-internal-secret` header → Buffer.from(undefined) 不得抛错（`|| ''`）
- [x] 默认密钥 → 端点 503（fail-closed），而非 200
- [x] 长度不等的密钥 → 不喂给 `timingSafeEqual`
- [x] 影子 URL → 404
- [x] 客户端规范 URL 全部仍可达（api.js 已核对）
- [ ] #2 分支 B：inactive GameObject / 空引用导致静默不订阅（GOTCHA 已列）

---

## Validation Commands

### Static Analysis / 编译
```bash
# web-app 无独立类型检查（纯 JS）；靠测试与启动即为静态验证
cd web-app && node --check src/routes/results.js && node --check src/routes/responses.js && node --check src/index.js
```
EXPECT: 三个文件语法 OK，无输出错误。

### Unit Tests
```bash
cd web-app && npm test
```
EXPECT: 全部通过，含新增 `results-archive.test.js` 4 条；既有 `host-token.test.js`、`adversarial-ws.test.js` 等无回归。

### 启动 fail-closed 冒烟（S1）
```bash
cd web-app
# 不设 INTERNAL_SECRET 启动 → 应见归档禁用 WARNING
node src/index.js &   # 观察日志后 kill
# 期望日志含: [Auth] WARNING: /api/sessions/archive is DISABLED ...
```
EXPECT: 出现归档禁用告警；进程仍正常 listen（非 fatal）。

### 手动 URL 矩阵（A1 + S1，本地起服后）
```bash
BASE=http://localhost:3001
# 规范 URL（存在；未带 header/auth 时为 403/401，非 404）
curl -s -o /dev/null -w "%{http_code}\n" -X POST $BASE/api/sessions/archive         # 期望 503(默认密钥) 或 403
curl -s -o /dev/null -w "%{http_code}\n"        $BASE/api/s/NONEXISTENT             # 期望 404(业务)——即路由存在
# 影子 URL（应彻底消失 → 404 路由层）
curl -s -o /dev/null -w "%{http_code}\n" -X POST $BASE/api/surveys/sessions/archive # 期望 404
curl -s -o /dev/null -w "%{http_code}\n"        $BASE/api/5/results                 # 期望 404
curl -s -o /dev/null -w "%{http_code}\n"        $BASE/api/surveys/s/CODE            # 期望 404
```
EXPECT: 归档默认密钥下 503；影子路径全 404。

### Unity 验证（#2）
```
分支 A：Unity 编译零错误；grep -rn RaceFinishPanel Assets/Scripts 无残留。
分支 B：播放出货场景一局 → 结束浮层出现 → 截图存 production/qa/evidence/。
```

### Manual Validation
- [ ] S1：设强 `INTERNAL_SECRET` 后归档 200；不设/默认时归档 503；伪造默认值 header 无法通过。
- [ ] A1：前端“查看结果/会话列表/学生填答”功能回归正常（走规范 URL）。
- [ ] #2：按 7A 或 7B 达成对应期望。

---

## Acceptance Criteria
- [ ] S1：`results.js` 不再出现字面量 `'edi-internal-default'`；默认/未设密钥时归档端点 503；比较为常量时间；启动有归档禁用告警。
- [ ] A1：`index.js` 中 responseRoutes/resultsRoutes 各只挂载一次；影子 URL 全部 404；前端功能无回归。
- [ ] #2：决策已确认；分支 A 死文件删净且 RuntimeSetup 仍编译 / 分支 B 出货场景结束时赛果浮层可见。
- [ ] 所有 validation 命令通过；新增单测全绿；无既有测试回归。

## Completion Checklist
- [ ] 遵循发现的模式（单一密钥真源、纯决策函数、路由前缀烘焙、Unity 事件订阅）
- [ ] 错误处理沿用 `{ success, error }` 契约与早返回
- [ ] 测试遵循 vitest 纯函数 + 确定性风格
- [ ] 无新增硬编码值（密钥来自 `hostToken.js` 唯一真源）
- [ ] `Server/server.js` 无需改动已确认（生产设强密钥即协同工作）
- [ ] 未越界扩范围（NOT Building 全部遵守）
- [ ] 计划自足 —— 实施期除 #2 决策门外无需再检索代码

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 默认密钥部署原本依赖归档，改后归档静默失效 | 中 | 中 | 启动 WARNING 明示端点禁用；文档要求生产设强密钥；`Server/server.js` 的 archive 本就是 fire-and-forget，失败不阻断对局 |
| A1 改路径打断某个未核对的调用方 | 低 | 高 | 已 grep 客户端与 Server 全部调用点，均走规范 URL；curl 矩阵 + 前端回归双重把关 |
| #2 分支 B 补线后引用空/对象 inactive 致静默不订阅 | 中 | 中 | GOTCHA 明列 active + 非空引用；播放实测 + 截图验证 |
| 引入 supertest 拖慢/复杂化测试 | 低 | 低 | Task 5 标为可选；核心 S1 用纯函数单测覆盖，A1 用 curl 矩阵兜底 |
| Unity 资产删除/接线绕过 UnitySkills API 破坏 .meta/场景 | 低 | 中 | 优先经 `http://localhost:8090` API；不支持再回退文件编辑并复核编译 |

## Notes
- **S1 设计取舍**：把归档授权定义为**独立于 `REQUIRE_HOST_TOKEN` 的边界**，因为它可写任意教授账户的会话记录——公开默认值绝不能是有效凭证。选择“端点 fail-closed 503 + 启动 WARNING”而非“默认密钥即 fatal 退出”，是为不误伤合法的 `REQUIRE_HOST_TOKEN=false` 且不使用归档的部署（该模式下默认密钥对 host-token 仍是被现有 `checkSecretConfig` 判为 `warn` 的“可接受”状态）。若运维策略希望更严格，可将 Task 2 的 WARNING 升级为 fatal（备选，未采纳以免扩大 boot 语义变更）。
- **A1 设计取舍**：采用“子路径烘焙进路由 + 每 router 挂一次 `/api`”，比拆成多个 Router 实例改动更小，且与 `surveys.js` 现有 `/:id/responses/count` 的挂载风格一致。
- **#2 本质**：不是“浮层坏了”，而是“出货场景根本没放这个组件；它只由开发期脚手架 `RuntimeSetup` 程序化构建，而脚手架不在出货场景”。因此必须先由人确认赛果的既定呈现方式，再决定删除还是补线——这是审查清单里唯一标注“人工确认”的项。

---

## Confidence: 8/10
S1、A1 上下文完全闭合（调用方、测试模式、镜像守卫均已核实），可单遍实施。#2 含一个明确的人工决策门（Task 7），门后两分支各自自足；扣分仅因该门需用户输入，且 Unity 侧接线/删除依赖 UnitySkills API 的实际可用性。
