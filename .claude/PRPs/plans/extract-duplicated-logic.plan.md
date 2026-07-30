# Plan: Extract Duplicated Logic (Backend / Frontend / Unity C#)

## Summary
消除三个代码库（Node/Express 后端、React 前端、Unity C# 运行时）中已识别的 12 处重复逻辑，将其提取为共享辅助函数、中间件、集中化配置、展示组件与扩展方法。纯粹的**行为保持型重构**——不改变任何对外契约或用户可见行为，只降低重复、集中真相来源。

## User Story
As a 维护 EDI Racing 三端代码库的开发者，
I want 把逐字重复的 WS 端点 / 归属校验 / 配置常量 / 表格 JSX / 属性查找 / 默认规则集提取为单一真相来源，
So that 后续改动只需改一处、减少漂移风险、降低回归面。

## Problem → Solution
**现状**：同一段逻辑散落多处、已出现细微漂移（如默认规则集三副本条数不一致、金牌颜色两种表示、Blob 下载两种签名）→ **目标**：每类逻辑收敛到一个命名良好的单元，所有调用点改为引用它，行为逐字不变。

## Metadata
- **Complexity**: XL（12 项重构 × 3 语言/领域，跨 ~30 文件）
- **Source PRD**: N/A（free-form 重构清单）
- **PRD Phase**: N/A
- **Estimated Files**: 后端 ~10、前端 ~9、Unity ~17（新建 + 修改）
- **建议执行方式**: 三个 workstream **相互独立**，可分别实施与验证、分别提 PR。若要拆分，按 Backend / Frontend / Unity 各成一个 `/prp-implement` 会话。

---

## ⚠️ 与原始清单的关键偏差（实施前必读）

探索阶段发现原始需求描述中有若干不精确处，已在下方任务中修正：

| 原描述 | 实际情况 | 影响 |
|---|---|---|
| WS 端点在 `index.js` | 实际在 `web-app/src/routes/export.js:252-419` | 定位修正 |
| 归属校验重复 14 处 | 14 处命中：**11 处** SELECT-then-404 可用中间件，**3 处** 用 `result.changes===0`（link-room×2 + DELETE），需改为 load→操作两步或保留 | 中间件覆盖范围 |
| `game_sessions` 重复 2 处 | **3 处**：`schema.sql` + `db.js` + `__tests__/test-helpers.js`（第 3 份易漏，会导致重构后仍漂移） | 必须一并处理 |
| `gameLaunch.js` 在 `utils/` | 实际在 `web-app/client/src/gameLaunch.js`（src 根，非 utils/），有 2 处 import | 需决定移动 or 就地扩展 |
| Blob 下载 4 处 | **3 处**原始实现；`csvExport.downloadBlob` **已是**共享工具，但签名 `(content, filename, mime)` 与 EditorPage 的 `(blob, filename)` **冲突** | 合并需兼容两种入参 |
| Unity 用 `KeyCode.Digit1-9` | 实际用 `UnityEngine.InputSystem.Key.Digit1-9` | 类型修正 |
| 属性查找重复 ~6 处 | 实际 **8 处** | 扩展方法覆盖范围 |
| 默认规则集 3 处「数据副本」 | 三处**并不完全一致**：`EventSchedule` **8 条**（含 Sunset，强类型 `EventRule` + `TriggerKey`）；两模板各 **7 条**（无 Sunset，`SavedEventRule` + `(int)` 枚举，无 TriggerKey）| **不是纯提取**，需先决定 Sunset 归属与类型调和 |
| 项目已有扩展方法可参照 | **无任何扩展方法**，`AttributeEntry[].Get()` 将是首个 | 需新建承载 static class |

---

## UX Design

### Before / After
**N/A — 纯内部重构。** 无任何用户可见行为、API 响应体、渲染输出的变化。验收标准即"行为逐字不变"。

### Interaction Changes
无。所有 HTTP 响应信封、WS 消息类型、渲染的表格、排行榜颜色、事件规则行为均保持不变。

---

## Mandatory Reading

### Backend
| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/export.js` | 1-419 | 两个 WS 端点全文 + WS_GAME_URL 定义 + 5 处归属校验 |
| P0 | `web-app/src/middleware/auth.js` | 1-28 | 中间件写法模板（提取 `loadOwnedSurvey` 照此） |
| P0 | `web-app/src/routes/surveys.js` | 1-175 | 归属校验主战场 + share code 生成 + roomCode 校验 |
| P0 | `web-app/src/db.js` | 1-70 | schema 加载 + ALTER 迁移 + game_sessions 内联重建 |
| P0 | `web-app/src/schema.sql` | 60-76 | game_sessions 权威建表 |
| P1 | `web-app/__tests__/test-helpers.js` | 1-55 | game_sessions 第 3 副本 + 测试 setup 模板 |
| P1 | `web-app/src/routes/responses.js` | 1-95 | WS_GAME_URL/GAME_HTTP_URL 第 2 处 + 归属校验 |
| P1 | `web-app/src/routes/game-status.js` | 1-60 | WS_GAME_URL/GAME_HTTP_URL 第 3 处 + roomCode 规范化 |
| P1 | `web-app/src/routes/results.js` | 1-110 | 2 处归属校验 + roomCode 校验变体 |
| P2 | `web-app/src/index.js` | 1-91 | 应用装配、路由挂载点、全局错误处理器 |
| P2 | `web-app/__tests__/db.test.js` | 1-30 | 测试文件结构范例 |

### Frontend
| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/client/src/gameLaunch.js` | 1-27 | 现有 URL builder 模板 + 分享链接落点 |
| P0 | `web-app/client/src/utils/csvExport.js` | 1-62 | 已有 `downloadBlob` 共享工具（签名冲突点） |
| P0 | `web-app/client/src/components/ResultsTab.jsx` | 1-120 | 赛果表 + 事件日志表 JSX 源 1 |
| P0 | `web-app/client/src/pages/HistoryPage.jsx` | 80-150 | 赛果表 + 事件日志表 JSX 源 2（近逐字重复） |
| P1 | `web-app/client/src/pages/EditorPage.jsx` | 90-125 | Blob 下载重复 2 处（不同签名） |
| P1 | `web-app/client/src/pages/DashboardPage.jsx` | 4, 50-54, 137-143 | 分享链接硬编码 2 处 + import |
| P1 | `web-app/client/src/components/SharePanel.jsx` | 1-30 | 分享链接硬编码第 3 处 |
| P2 | `web-app/client/src/components/LiveLeaderboard.jsx` | 1-26 | 纯展示表格组件模板 |

### Unity C#
| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Data/CarData.cs` | 1-90 | `AttributeEntry` 定义 + 2 处查找 + ToDictionary 参考 |
| P0 | `Assets/Scripts/Data/SurveyResponseMapper.cs` | 1-95 | static class 模板 + 2 处查找 |
| P0 | `Assets/Scripts/Data/SurveyConfigManager.cs` | 95-120 | Digit 数组 + ToRule 循环源 A |
| P0 | `Assets/Scripts/Race/RaceManager.cs` | 75-95 | Digit 数组 + ToRule 循环源 B |
| P0 | `Assets/Scripts/Events/EventSchedule.cs` | 1-111 | 默认规则集副本 1（8 条，权威） |
| P0 | `Assets/Scripts/Data/SurveyTemplates.cs` | 37-116, 395-404 | 默认规则集副本 2、3（各 7 条） |
| P1 | `Assets/Scripts/UI/LeaderboardPanel.cs` | 60-75 | 排行榜格式化 + RGB 三色源 1 |
| P1 | `Assets/Scripts/UI/RaceFinishPanel.cs` | 95-110 | 名次 hex 三色源 2 |
| P1 | `Assets/Scripts/Car/CarIdentity.cs` | 35-65 | 属性查找镜像 2 处 |
| P1 | `Assets/Scripts/Data/SessionData.cs` | 180-195 | `SavedEventRule.FromRule/ToRule` + ColorIndex 查找 |
| P2 | `Assets/Tests/EditMode/SurveyResponseMapperTests.cs` | 1-15 | NUnit 测试结构范例 |

## External Documentation
| Topic | Source | Key Takeaway |
|---|---|---|
| — | — | 无外部研究需要 —— 全部使用已建立的内部模式（Express 中间件、React 组件、C# 扩展方法均为标准语言特性）。 |

---

## Patterns to Mirror

### BACKEND_MIDDLEWARE — 提取 loadOwnedSurvey 照此写
```js
// SOURCE: web-app/src/middleware/auth.js (全文)
export function requireAuth(req, res, next) {
  const header = req.headers.authorization;
  if (!header || !header.startsWith('Bearer ')) {
    return res.status(401).json({ success: false, error: 'Authentication required' });
  }
  const token = header.slice(7);
  const session = sessions.get(token);
  if (!session) {
    return res.status(401).json({ success: false, error: 'Invalid or expired session' });
  }
  req.user = session;
  next();
}
```
要点：`middleware/` 目录、具名 `export function`、`(req,res,next)` 签名、卫语句 + `return res.status().json({ success:false, error })`、成功挂 `req.<x>` 后 `next()`。

### BACKEND_ERROR_ENVELOPE
```js
// SOURCE: web-app/src/routes/surveys.js:37-43 等 14 处
if (!survey) {
  return res.status(404).json({ success: false, error: 'Survey not found' });
}
// 成功: res.json({ success: true, data: ... })
```

### BACKEND_ROUTE_MODULE
```js
// SOURCE: web-app/src/routes/*.js
import { Router } from 'express';
const router = Router();
router.get('/:id', requireAuth, (req, res) => { /* ... */ });
export default router;
```
常量用 UPPER_SNAKE，函数 camelCase，DB 列 snake_case，发 Unity 的 payload PascalCase。日志前缀 `[Tag]`（`[DB]`/`[API]`/`[Auth]`）。

### BACKEND_TEST
```js
// SOURCE: web-app/__tests__/db.test.js:1-10
import { describe, it, expect, beforeEach } from 'vitest';
import { createTestDb, createTestUser } from './test-helpers.js';

describe('Database Schema', () => {
  let db;
  beforeEach(() => { db = createTestDb(); });
  it('creates game_sessions table', () => { /* expect(...).toBe(...) */ });
});
```

### FRONTEND_UTIL — named-export function（gameLaunch.js / csvExport.js 风格）
```js
// SOURCE: web-app/client/src/gameLaunch.js:1-15
const GAME_ROOT = import.meta.env?.VITE_GAME_URL || '/';
export function buildHostLaunchUrl(token, surveyId) { /* ... */ }
export function buildStudentPlayUrl(roomCode) { /* ... */ }
```
全 named export 普通 function，无 default export，带注释。

### FRONTEND_PRESENTATIONAL_COMPONENT — 抽表格组件照此
```jsx
// SOURCE: web-app/client/src/components/LiveLeaderboard.jsx:1-26
export default function LiveLeaderboard({ rankings }) {
  if (!rankings || rankings.length === 0) {
    return <div className="live-leaderboard empty">Waiting for race data...</div>;
  }
  return (
    <table className="response-table">
      <thead><tr><th>#</th><th>Team</th></tr></thead>
      <tbody>
        {rankings.map((entry, i) => (
          <tr key={i} className="response-row">
            <td className={entry.rank <= 3 ? `rank-${entry.rank}` : ''}>{entry.rank}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
```
要点：`export default function`，props 解构，空状态提前 return，`key={i}`，class `response-table`/`response-row`/`rank-{n}`。**注意赛果/事件表数据字段为 PascalCase**（`car.Rank`、`e.EventName`，来自 C# 序列化），与 LiveLeaderboard 的小写字段不同——抽的组件按 PascalCase 读 props。

### CSHARP_STATIC_UTIL — 承载扩展方法照此（项目无现存扩展方法）
```csharp
// SOURCE: Assets/Scripts/Data/SurveyResponseMapper.cs:8
/// <summary>Pure static utility — no MonoBehaviour, no state.</summary>
public static class SurveyResponseMapper
{
    public static CarData MapResponses(...) { ... }
    private static string FindResponse(AttributeEntry[] responses, string questionId) { ... }
}
```
全局命名空间（无 namespace），`public static class`，放 `Assets/Scripts/Data/`。

### CSHARP_ATTRIBUTE_LOOKUP — 8 处重复的目标模式
```csharp
// SOURCE: Assets/Scripts/Data/CarData.cs:45-49
if (Attributes == null) return defaultValue;
for (int i = 0; i < Attributes.Length; i++)
    if (string.Equals(Attributes[i].Key, key, StringComparison.OrdinalIgnoreCase))
        return Attributes[i].Value;
return defaultValue;
```

### CSHARP_NAMING
- 类/公共字段/公共属性/枚举/常量：PascalCase（公共字段用 PascalCase 是 Unity Inspector 惯例）
- 私有字段：camelCase 无前缀（无 `_`/`m_`）
- 常量用 `static readonly`（项目未用 `const`）
- 事件：`public event Action<T> OnXxx;`，触发 `OnXxx?.Invoke(...)`
- 日志：`Debug.Log($"[ClassName] ...")`

### CSHARP_TEST
```csharp
// SOURCE: Assets/Tests/EditMode/SurveyResponseMapperTests.cs:1-15
using System;
using NUnit.Framework;

[TestFixture]
public class SurveyResponseMapperTests
{
    [Test]
    public void MapResponses_NoMappings_ReturnsEmptyAttributes()
    {
        var responses = new AttributeEntry[] { new AttributeEntry { Key = "q1", Value = "hello" } };
        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, Array.Empty<AttributeMapping>());
        Assert.AreEqual("Team1", result.TeamName);
    }
}
```
命名 `Method_Scenario_ExpectedResult`，经典 NUnit `Assert.AreEqual/IsTrue/IsNull`。（注意：`.claude/rules/test-standards.md` 写的是 GDScript 蛇形约定，**不适用本 C# 项目**，遵循上面 PascalCase 约定。）

---

## Files to Change

### Backend
| File | Action | Justification |
|---|---|---|
| `web-app/src/config.js` | CREATE | 集中 WS_GAME_URL/GAME_HTTP_URL/roomCode 校验+规范化/share code 生成 |
| `web-app/src/middleware/loadOwnedSurvey.js` | CREATE | 归属校验中间件 |
| `web-app/src/lib/gameSocket.js` | CREATE | `sendToGameRoom()` WS 骨架辅助 |
| `web-app/src/routes/export.js` | UPDATE | 两 WS 端点改用辅助 + 归属中间件 + config |
| `web-app/src/routes/surveys.js` | UPDATE | 归属中间件 + config（share code/roomCode） |
| `web-app/src/routes/responses.js` | UPDATE | config + 归属中间件 |
| `web-app/src/routes/game-status.js` | UPDATE | config |
| `web-app/src/routes/results.js` | UPDATE | 归属中间件 + config |
| `web-app/src/db.js` | UPDATE | 提取统一 `applyMigrations(db)`，删内联 game_sessions 冗余 |
| `web-app/__tests__/test-helpers.js` | UPDATE | 改调用统一 `applyMigrations`，消除第 3 副本 |

### Frontend
| File | Action | Justification |
|---|---|---|
| `web-app/client/src/gameLaunch.js` | UPDATE | 新增 `buildShareUrl(shareCode)` |
| `web-app/client/src/utils/csvExport.js` | UPDATE | 新增 `downloadBlobObject(blob, filename)` 兼容已有 Blob 签名 |
| `web-app/client/src/components/ResultsTable.jsx` | CREATE | 赛果表展示组件 |
| `web-app/client/src/components/EventLogTable.jsx` | CREATE | 事件日志表展示组件 |
| `web-app/client/src/components/ResultsTab.jsx` | UPDATE | 用新组件 |
| `web-app/client/src/pages/HistoryPage.jsx` | UPDATE | 用新组件 |
| `web-app/client/src/pages/EditorPage.jsx` | UPDATE | 用 csvExport 下载工具 |
| `web-app/client/src/pages/DashboardPage.jsx` | UPDATE | 用 buildShareUrl |
| `web-app/client/src/components/SharePanel.jsx` | UPDATE | 用 buildShareUrl |

### Unity C#
| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Data/EventRuleKeys.cs` | CREATE | 共享 `Key[]` 常量 + `AssignKeys()` 辅助 |
| `Assets/Scripts/Data/AttributeEntryExtensions.cs` | CREATE | `AttributeEntry[].Get()` 扩展方法（首个扩展方法） |
| `Assets/Scripts/UI/LeaderboardFormatter.cs` | CREATE | 名次格式化 + 金银铜牌颜色（Color 版 + hex 版） |
| `Assets/Scripts/Data/DefaultEventRules.cs` | CREATE | 默认规则集单一真相来源（决定 Sunset 归属后） |
| `Assets/Scripts/Data/SurveyConfigManager.cs` | UPDATE | 用共享 keys/AssignKeys |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | 用共享 keys/AssignKeys |
| `Assets/Scripts/Data/CarData.cs` | UPDATE | GetAttribute/HasAttribute 改用扩展方法 |
| `Assets/Scripts/Car/CarIdentity.cs` | UPDATE | 同上 |
| `Assets/Scripts/Data/SurveyResponseMapper.cs` | UPDATE | FindResponse/ApplyLookup 改用扩展方法 |
| `Assets/Scripts/Data/ResultsExporter.cs` | UPDATE | 内联查找改用扩展方法 |
| `Assets/Scripts/Data/SessionData.cs` | UPDATE | ColorIndex 查找改用扩展方法 |
| `Assets/Scripts/UI/LeaderboardPanel.cs` | UPDATE | 用 LeaderboardFormatter |
| `Assets/Scripts/UI/RaceFinishPanel.cs` | UPDATE | 用 LeaderboardFormatter（hex 版） |
| `Assets/Scripts/UI/CarLabelSpawner.cs` | UPDATE | 用 LeaderboardFormatter 金色常量 |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | 用 LeaderboardFormatter 金色常量 |
| `Assets/Scripts/Events/EventSchedule.cs` | UPDATE | 引用 DefaultEventRules |
| `Assets/Scripts/Data/SurveyTemplates.cs` | UPDATE | 引用 DefaultEventRules |

## NOT Building
- 不改任何 HTTP 路由路径、WS 消息类型、响应信封字段名
- 不引入前端测试框架（前端当前零测试；重构靠手动验证）
- 不把 `db.js` 迁移改成正式 migration 框架——只做「提取为一个函数、消除 3 副本」
- 不统一金牌的 `"yellow"` vs `(1,0.84,0)`——保留 RaceFinishPanel 现有富文本行为（提供 hex 版方法即可），除非验证等价
- 不移动 `gameLaunch.js` 到 `utils/`（保留在 src 根，避免连带改 2 处 import；仅就地新增函数）
- 不改 Unity asmdef、命名空间结构
- 不删除 `db.js` 中的 3 段 `ALTER TABLE`（旧库迁移必须保留）
- 不新增 Sunset 到两个模板 / 不从 EventSchedule 删 Sunset —— 见 Task U4（需人工决策，默认保留各自现状，仅提取「共同的前 7 条」）

---

## Step-by-Step Tasks

> 三个 workstream 独立。建议顺序：Backend → Frontend → Unity，或并行分 PR。每个 workstream 内部按序。

### ========== BACKEND ==========

### Task B1: 创建 config.js 集中配置
- **ACTION**: 新建 `web-app/src/config.js`
- **IMPLEMENT**:
  ```js
  import { randomBytes } from 'crypto';
  export const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';
  export const GAME_HTTP_URL = WS_GAME_URL.replace(/^ws/, 'http');
  // roomCode 校验+规范化：返回 { ok, code, error }
  export function normalizeRoomCode(roomCode) {
    if (!roomCode || !roomCode.trim()) return { ok: false, error: 'roomCode is required' };
    return { ok: true, code: roomCode.trim().toUpperCase() };
  }
  export function generateShareCode() {
    return randomBytes(4).toString('hex').toUpperCase(); // 8-char code
  }
  ```
- **MIRROR**: BACKEND_ROUTE_MODULE（UPPER_SNAKE 常量、camelCase 函数）
- **IMPORTS**: `import { randomBytes } from 'crypto';`（放文件顶部）
- **GOTCHA**: `results.js:101` 的 roomCode 校验没有 trim/upper（只 `if (!roomCode)`）——迁移它时确认是否需要保持宽松语义，或统一到 `normalizeRoomCode`（推荐统一，但需确认 results 下游是否大小写敏感）。share code 与 `responses.js` 的 `WHERE share_code = ? COLLATE NOCASE` 呼应，生成端 `toUpperCase()` 不可改。
- **VALIDATE**: `cd web-app && node -e "import('./src/config.js').then(m=>console.log(m.WS_GAME_URL, m.GAME_HTTP_URL, m.normalizeRoomCode(' ab '), m.generateShareCode().length))"`

### Task B2: 各路由改用 config.js
- **ACTION**: UPDATE `export.js:7`、`responses.js:5-6`、`game-status.js:6-7`、`surveys.js:8-10`（generateShareCode）、`results.js:101`（roomCode 校验）
- **IMPLEMENT**: 删除各文件顶部的 `const WS_GAME_URL = ...` / `GAME_HTTP_URL` / 本地 `generateShareCode` / 内联 roomCode 校验，改 `import { WS_GAME_URL, GAME_HTTP_URL, normalizeRoomCode, generateShareCode } from '../config.js';`
- **MIRROR**: BACKEND_ROUTE_MODULE
- **IMPORTS**: 见上（相对路径 `../config.js`，带 `.js` 后缀）
- **GOTCHA**: `surveys.js:2` 已 `import { randomBytes }`——若 generateShareCode 移走，检查 randomBytes 是否还有其他用途，无则删除该 import。roomCode 校验的 400 信封文案 `'roomCode is required'` 必须逐字不变。
- **VALIDATE**: `cd web-app && npm test`（现有 game-launch/results-archive/adversarial-ws 测试覆盖这些路径）

### Task B3: 创建 loadOwnedSurvey 中间件
- **ACTION**: 新建 `web-app/src/middleware/loadOwnedSurvey.js`
- **IMPLEMENT**:
  ```js
  import { getDb } from '../db.js';
  export function loadOwnedSurvey(req, res, next) {
    const db = getDb();
    const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
      .get(req.params.id, req.user.userId);
    if (!survey) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }
    req.survey = survey;
    next();
  }
  ```
- **MIRROR**: BACKEND_MIDDLEWARE（auth.js）
- **IMPORTS**: `import { getDb } from '../db.js';`
- **GOTCHA**: 默认 `SELECT *`。只需 id 的路由（如 PATCH active）也能用（多读几列无害）。**必须挂在 `requireAuth` 之后**（依赖 `req.user.userId`）。
- **VALIDATE**: 见 B4 之后 `npm test`

### Task B4: 11 处 SELECT-then-404 改用中间件
- **ACTION**: UPDATE 归属校验的 11 处 SELECT-then-404：
  - `surveys.js`: `:24`(count)、`:37`(GET /:id)、`:86`(PUT)、`:114`(PATCH active)
  - `export.js`: `:169`、`:185`、`:206`、`:260`(send-to-game)、`:344`(send-config)
  - `responses.js`: `:91`；`results.js`: `:12`、`:41`；`game-status.js`: `:30`（条件性，见 GOTCHA）
- **IMPLEMENT**: 路由签名加中间件 `router.get('/:id', requireAuth, loadOwnedSurvey, (req,res)=>{...})`，body 内删除 SELECT+404 块，改用 `req.survey`（原用 `SELECT id` 存 `existing` 的把后续 `existing` 引用改 `req.survey`）
- **MIRROR**: BACKEND_MIDDLEWARE
- **IMPORTS**: `import { loadOwnedSurvey } from '../middleware/loadOwnedSurvey.js';`
- **GOTCHA**:
  - `game-status.js:30` 的校验是**条件性**的（仅当 `surveyId !== null`）——不适合无条件中间件，**保留原样**或包装成可选中间件。
  - **3 处不迁移**（用 `result.changes===0`）：`surveys.js` link-room PATCH(`:135`)、link-room DELETE(`:153`)、DELETE `/:id`(`:166`)——它们是纯 UPDATE/DELETE 无需先读；保留现状，或若要统一则改为 `loadOwnedSurvey` + 操作两步（多一次查询，权衡后默认保留）。
  - PUT `/:id` 原读 `SELECT id` 存 `existing`，改后把 `existing` 全部替换为 `req.survey`。
- **VALIDATE**: `cd web-app && npm test` —— 全绿；特别关注 auth.test.js（401/404 路径）

### Task B5: 提取 sendToGameRoom() WS 骨架
- **ACTION**: CREATE `web-app/src/lib/gameSocket.js`；UPDATE `export.js:252-419` 两端点
- **IMPLEMENT**: 提取共同的 WS 骨架（建立连接 / 5000ms 超时→504 / open 发 `web_join_room` / message 的 JSON.parse try-catch / `error` 消息→400 / `ws.on('error')`→502 / `responded` 标志），差异点作回调注入：
  ```js
  import { WebSocket } from 'ws';
  import { WS_GAME_URL } from '../config.js';
  /**
   * 连接游戏服务器房间，处理握手与超时。差异点由回调注入。
   * @param res Express 响应
   * @param code 规范化后的房间码
   * @param onRoomJoined (ws) => void  房间加入后发送具体导入消息
   * @param handleAck (msg, res, ws, done) => void  处理 ack 消息，调用 done() 收尾
   */
  export function sendToGameRoom(res, { code, onRoomJoined, handleAck }) {
    const ws = new WebSocket(WS_GAME_URL);
    let responded = false;
    const timeout = setTimeout(() => {
      if (!responded) { responded = true; ws.close();
        res.status(504).json({ success: false, error: 'Game server did not respond in time' }); }
    }, 5000);
    const done = () => { responded = true; clearTimeout(timeout); ws.close(); };
    ws.on('open', () => ws.send(JSON.stringify({ type: 'web_join_room', roomCode: code })));
    ws.on('message', (data) => {
      if (responded) return;
      let msg; try { msg = JSON.parse(data.toString()); } catch { return; }
      if (msg.type === 'error') {
        done();
        return res.status(400).json({ success: false, error: msg.message || 'Room not found' });
      }
      if (msg.type === 'room_joined') { onRoomJoined(ws); return; }
      handleAck(msg, res, ws, done);
    });
    ws.on('error', () => {
      if (!responded) { responded = true; clearTimeout(timeout);
        res.status(502).json({ success: false, error: 'Cannot connect to game server' }); }
    });
  }
  ```
  - `send-to-game`（export.js:252-334）：前置校验（roomCode + `carData.length===0`→400 "No responses to send…"）+ 归属（用 `req.survey`）+ 构建 `exportPayload`(camelCase)，然后 `sendToGameRoom(res, { code, onRoomJoined: ws => ws.send(JSON.stringify({type:'survey_import', configName, exportJson})), handleAck: (msg,res,ws,done)=>{ if(msg.type==='survey_import_ack'){done(); res.json({success:true,data:{carsCount,rulesCount}});} } })`
  - `send-config-to-game`（export.js:336-419）：无 carData 空检查，构建 `configPayload`(PascalCase)，`onRoomJoined: ws => ws.send(JSON.stringify({type:'config_import', configName, configJson}))`，`handleAck` 处理 `config_sync_ack`（依赖 `msg.success`：成功 `{configName}`，否则 400 `msg.error||'Config sync failed'`）
- **MIRROR**: BACKEND_ROUTE_MODULE、export.js 现有两端点
- **IMPORTS**: `import { WebSocket } from 'ws';`（gameSocket.js 内）；export.js 加 `import { sendToGameRoom } from '../lib/gameSocket.js';`
- **GOTCHA**: 逐字差异见探索报告：两端点**唯一不同**是①send-to-game 有 carData 空检查②payload 构建（camelCase vs PascalCase）③发送的消息 type（survey_import/config_import）④等待的 ack（survey_import_ack/config_sync_ack）⑤成功响应体。骨架必须逐字保留 timeout(504)/error 消息(400)/连接错误(502)/JSON.parse try-catch/`responded` 幂等。新建 `lib/` 目录。**`done()` 已封装 responded+clearTimeout+close**，handleAck 里先 `done()` 再 `res.json/status`，避免与 error/timeout 竞态。
- **VALIDATE**: `cd web-app && npm test` —— adversarial-ws.test.js 覆盖这些 WS 路径（504/502/400/成功），必须全绿

### Task B6: 统一 game_sessions schema/迁移（消除 3 副本）
- **ACTION**: UPDATE `db.js` 提取 `applyMigrations(db)`；`test-helpers.js` 改调用它
- **IMPLEMENT**:
  - 在 `db.js` 导出 `export function applyMigrations(db) { /* 3 段 ALTER TABLE try/catch */ }`。
  - **删除** `db.js` 中内联的 `CREATE TABLE IF NOT EXISTS game_sessions (...)`（schema.sql 已建，`IF NOT EXISTS` 使其对全新库冗余、对旧库 no-op）——⚠️ 见 GOTCHA 先验证旧库路径。
  - `db.js` 初始化流程：`db.exec(schema)` 后调 `applyMigrations(db)`。
  - `test-helpers.js:createTestDb()` 删除自己重放的 ALTER + 内联 game_sessions，改 `import { applyMigrations } from '../src/db.js'` 并调用。
- **MIRROR**: BACKEND_ROUTE_MODULE（具名 export function）
- **IMPORTS**: test-helpers 加 `import { applyMigrations } from '../src/db.js';`
- **GOTCHA**: **删 db.js 内联 game_sessions 前必须确认**：schema.sql 的 `CREATE TABLE IF NOT EXISTS game_sessions` 是否真的覆盖旧库？旧库首次加载新 schema.sql 时该 CREATE 会执行建表——是的，冗余可删。**但 ALTER TABLE 三段必须保留**（给已存在的 surveys/templates 表补列）。若不确定，保守做法：仅提取为函数、不删内联 CREATE（去重 test-helpers 副本即已消除漂移主源）。`applyMigrations` 需接受 db 实例参数（避免依赖单例 getDb）。
- **VALIDATE**: `cd web-app && npm test` —— db.test.js 验证 game_sessions 表存在与列完整

### ========== FRONTEND ==========

### Task F1: gameLaunch.js 新增 buildShareUrl
- **ACTION**: UPDATE `web-app/client/src/gameLaunch.js`
- **IMPLEMENT**:
  ```js
  export function buildShareUrl(shareCode) {
    return `${window.location.origin}/survey/#/s/${shareCode}`;
  }
  ```
- **MIRROR**: FRONTEND_UTIL（named export function，带注释）
- **IMPORTS**: 无新增
- **GOTCHA**: 保留在 `src/gameLaunch.js`（**不移到 utils/**，避免改 2 处现有 import）。DashboardPage 第 3 处用的是 `s.share_code`（snake_case，后端 raw 记录）——调用时传 `s.share_code`。SharePanel 用 `shareCode`（camelCase prop）。函数参数名中立即可。
- **VALIDATE**: `cd web-app/client && npm run build`（Vite 构建通过）

### Task F2: 3 处分享链接改用 buildShareUrl
- **ACTION**: UPDATE `SharePanel.jsx:6`、`DashboardPage.jsx:50-54`、`DashboardPage.jsx:137-143`
- **IMPLEMENT**: 替换模板字符串为 `buildShareUrl(shareCode)` / `buildShareUrl(s.share_code)`
- **MIRROR**: FRONTEND_UTIL
- **IMPORTS**: `SharePanel.jsx` 加 `import { buildShareUrl } from '../gameLaunch.js';`；`DashboardPage.jsx:4` 已 import gameLaunch，追加 `buildShareUrl` 到解构
- **GOTCHA**: `DashboardPage.jsx:137` 是只读 `<input value={...}>`，替换后保持 `value={buildShareUrl(s.share_code)}`
- **VALIDATE**: `cd web-app/client && npm run build` + 手动核对渲染的分享 URL 与之前逐字一致

### Task F3: csvExport 新增 Blob 对象下载函数，EditorPage 复用
- **ACTION**: UPDATE `web-app/client/src/utils/csvExport.js` 新增函数；UPDATE `EditorPage.jsx:93-123`
- **IMPLEMENT**:
  - csvExport.js 新增（兼容「已构造 Blob」签名）：
    ```js
    /** Trigger a browser download of an already-constructed Blob. */
    export function downloadBlobObject(blob, filename) {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      a.click();
      URL.revokeObjectURL(url);
    }
    ```
  - EditorPage 删除本地 `downloadBlob(blob, filename)`（`:93-100`），调用点 `:104`/`:109` 改 `downloadBlobObject(...)`。
  - EditorPage `downloadExportJson`（`:112-123`）改为 `downloadBlob(json, filename, 'application/json')`（用 csvExport 的 content+mime 版）。
- **MIRROR**: `csvExport.js:16-25` 现有 downloadBlob
- **IMPORTS**: `EditorPage.jsx` 加 `import { downloadBlob, downloadBlobObject } from '../utils/csvExport.js';`
- **GOTCHA**: **两种签名并存**——`downloadBlob(content, filename, mime)`（新建 Blob）用于有内容字符串的场景；`downloadBlobObject(blob, filename)`用于 EditorPage 已从 `res.blob()` 拿到 Blob 的场景（`:104` xlsx、`:109` csv）。不要合并成一个，否则 EditorPage 需反向拆 Blob。
- **VALIDATE**: `cd web-app/client && npm run build` + 手动触发 3 种下载（xlsx/csv/json）确认文件名与内容不变

### Task F4: 抽取 ResultsTable 与 EventLogTable 展示组件
- **ACTION**: CREATE `components/ResultsTable.jsx`、`components/EventLogTable.jsx`；UPDATE `ResultsTab.jsx`、`HistoryPage.jsx`
- **IMPLEMENT**:
  - `ResultsTable.jsx`：props `{ rankings }`，渲染 Rank/Team/Laps/Checkpoints/Time 表（复制 `ResultsTab.jsx:55-76` 结构），内部用 `(rankings || []).map`
  - `EventLogTable.jsx`：props `{ eventLog }`，渲染 Time/Event/Affected 表（复制 `ResultsTab.jsx:78-100` 结构，含外层 `length > 0 &&` 与 `<h4>Event Log</h4>`）
  - `ResultsTab.jsx` 与 `HistoryPage.jsx` 用 `<ResultsTable rankings={session.rankings} />` / `<EventLogTable eventLog={session.eventLog} />`
- **MIRROR**: FRONTEND_PRESENTATIONAL_COMPONENT（LiveLeaderboard）
- **IMPORTS**: 两页面加 `import ResultsTable from '../components/ResultsTable.jsx';`（HistoryPage 相对路径 `../components/`，ResultsTab 同目录 `./`）
- **GOTCHA**: **字段 PascalCase**（`car.Rank`、`car.TeamName`、`car.LapsCompleted`、`car.CheckpointsPassed`、`car.TotalTime`；`e.Timestamp`、`e.EventName`、`e.AffectedCount`、`e.TotalCars`）——来自 C# 序列化，勿改小写。保留 class `response-table`/`response-row`/`rank-{n}`（`Rank <= 3` 高亮）、`(car.TotalTime||0).toFixed(2)`、`(e.Timestamp||0).toFixed(1)`。HistoryPage 原用 `session.rankings.map`（已判空），改用组件后组件内 `(rankings||[])` 兼容两处。
- **VALIDATE**: `cd web-app/client && npm run build` + 手动对比 ResultsTab 与 HistoryPage 渲染的表格与重构前逐字一致

### ========== UNITY C# ==========

### Task U1: 提取 Digit 按键共享常量 + AssignKeys
- **ACTION**: CREATE `Assets/Scripts/Data/EventRuleKeys.cs`；UPDATE `SurveyConfigManager.cs:101-112`、`RaceManager.cs:80-88`
- **IMPLEMENT**:
  ```csharp
  using UnityEngine;
  using UnityEngine.InputSystem;
  public static class EventRuleKeys
  {
      public static readonly Key[] DigitKeys = {
          Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
          Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
      };
      /// <summary>Convert SavedEventRule[] to EventRule[] assigning Digit1..9 in order.</summary>
      public static EventRule[] AssignKeys(SavedEventRule[] rules)
      {
          int count = Mathf.Min(rules.Length, DigitKeys.Length);
          var eventRules = new EventRule[count];
          for (int i = 0; i < count; i++)
              eventRules[i] = rules[i].ToRule(DigitKeys[i]);
          return eventRules;
      }
  }
  ```
  - `SurveyConfigManager.cs`：替换 keys 数组+循环为 `var eventRules = EventRuleKeys.AssignKeys(ActiveConfig.Rules); schedule.Events = eventRules;`，**保留** `:116-117` 的溢出告警。
  - `RaceManager.cs`：替换为 `EventManager.Schedule.Events = EventRuleKeys.AssignKeys(rules);`
- **MIRROR**: CSHARP_STATIC_UTIL、CSHARP_NAMING（`static readonly` 数组）
- **IMPORTS**: `using UnityEngine.InputSystem;`（用 `Key` 而非 `KeyCode`）
- **GOTCHA**: 是 `UnityEngine.InputSystem.Key` **不是** `KeyCode`。`ToRule(Key)` 是 `SavedEventRule` 实例方法（`SessionData.cs`）。SurveyConfigManager 原有溢出告警 `if (ActiveConfig.Rules.Length > keys.Length)`——因 keys 移入常量，改用 `EventRuleKeys.DigitKeys.Length`。
- **VALIDATE**: Unity 编译无错 + `SurveyConfigManagerTests` 通过（见 Unity 验证命令）

### Task U2: AttributeEntry[].Get() 扩展方法（8 处）
- **ACTION**: CREATE `Assets/Scripts/Data/AttributeEntryExtensions.cs`；UPDATE 8 处查找
- **IMPLEMENT**:
  ```csharp
  using System;
  public static class AttributeEntryExtensions
  {
      /// <summary>Case-insensitive lookup by Key. Returns Value or defaultValue.</summary>
      public static string Get(this AttributeEntry[] entries, string key, string defaultValue = null)
      {
          if (entries == null || string.IsNullOrEmpty(key)) return defaultValue;
          for (int i = 0; i < entries.Length; i++)
              if (string.Equals(entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                  return entries[i].Value;
          return defaultValue;
      }
      public static bool Has(this AttributeEntry[] entries, string key) =>
          entries.Get(key, null) != null;
  }
  ```
  改造 8 处：
  1. `CarData.cs:45-49` GetAttribute → `return Attributes.Get(key, defaultValue);`
  2. `CarData.cs:68-73` HasAttribute → `return Attributes.Has(key);`
  3. `CarIdentity.cs:41-45` GetAttribute → 同 1
  4. `CarIdentity.cs:57-61` HasAttribute → 同 2
  5. `SurveyResponseMapper.cs:50-61` FindResponse → `return responses.Get(questionId, null);`
  6. `SurveyResponseMapper.cs:83-95` ApplyLookup → 保留原前置 `if (lookupEntries == null || lookupEntries.Length == 0) return responseValue;`，其后 `var m = lookupEntries.Get(responseValue, null); return m ?? (defaultValue ?? responseValue);`
  7. `ResultsExporter.cs:56-66` 内联 → `val = car.Attributes.Get(key, val);`
  8. `SessionData.cs:186-190` ColorIndex → `var v = Attributes.Get("colorIndex", null); if (v != null && int.TryParse(v, out int val)) return val; return 0;`
- **MIRROR**: CSHARP_ATTRIBUTE_LOOKUP、CSHARP_STATIC_UTIL
- **IMPORTS**: 无（全局命名空间，扩展方法自动可见）
- **GOTCHA**: 各原实现的默认返回值不同（`defaultValue` / `false` / `null` / `responseValue`）——`Get` 的 defaultValue 参数覆盖前几类；处 6 语义是「找不到返回 responseValue」，**必须保留原前置空数组检查**（否则 defaultValue 非 null 时行为变化）；处 8 需保留 int.TryParse。`Has` 用 `!= null` 语义等价原命中判断。**首个扩展方法**——asmdef 无 C# 版本限制（扩展方法 C# 3.0+）。
- **VALIDATE**: Unity 编译 + `CarDataTests`、`SurveyResponseMapperTests`、`ResultsExporterTests` 通过

### Task U3: LeaderboardFormatter（4 处颜色 + 格式化）
- **ACTION**: CREATE `Assets/Scripts/UI/LeaderboardFormatter.cs`；UPDATE `LeaderboardPanel.cs:67-73`、`RaceFinishPanel.cs:99-104`、`CarLabelSpawner.cs:80-82`、`NetworkSync.cs:328`
- **IMPLEMENT**:
  ```csharp
  using UnityEngine;
  public static class LeaderboardFormatter
  {
      public static readonly Color Gold   = new Color(1f, 0.84f, 0f);
      public static readonly Color Silver = new Color(0.75f, 0.75f, 0.75f);
      public static readonly Color Bronze = new Color(0.8f, 0.5f, 0.2f);
      /// <summary>Rank-based color: 1st gold, 2nd silver, 3rd bronze, else white. (0-based index)</summary>
      public static Color RankColor(int rankZeroBased) => rankZeroBased switch {
          0 => Gold, 1 => Silver, 2 => Bronze, _ => Color.white
      };
      /// <summary>Rich-text color name/hex for rank (RaceFinishPanel style). (0-based index)</summary>
      public static string RankHex(int rankZeroBased) => rankZeroBased switch {
          0 => "yellow", 1 => "#C0C0C0", 2 => "#CD7F32", _ => "white"
      };
  }
  ```
  - `LeaderboardPanel.cs:70-73` → `text.color = LeaderboardFormatter.RankColor(i);`
  - `RaceFinishPanel.cs:102` → `string color = LeaderboardFormatter.RankHex(i);`
  - `CarLabelSpawner.cs:81` → `text.color = LeaderboardFormatter.Gold;`
  - `NetworkSync.cs:328` → `mat.SetColor("_EmissionColor", LeaderboardFormatter.Gold * 0.3f);`
- **MIRROR**: CSHARP_STATIC_UTIL、CSHARP_NAMING（`static readonly Color`）
- **IMPORTS**: `using UnityEngine;`
- **GOTCHA**: **名次前缀格式两处不同**（`"{i+1}. [{lap}] {name}"` vs `"{i+1}. {name}  (Lap {lap})"`）——不强行统一字符串格式，**只提取颜色**（格式串留在各自调用点）。金色 RaceFinishPanel 原用 `"yellow"`（非 hex）——`RankHex` 保留 `"yellow"` 以逐字等价。`rankZeroBased` 用 0-based（现有代码 `i`）。C# `switch` 表达式需 C# 8+（Unity 6.3 用 C# 9，无虞）。
- **VALIDATE**: Unity 编译无错 + 手动/截图核对排行榜与完赛面板颜色不变

### Task U4: DefaultEventRules 单一真相来源（⚠️ 需决策）
- **ACTION**: CREATE `Assets/Scripts/Data/DefaultEventRules.cs`；UPDATE `EventSchedule.cs:13-111`、`SurveyTemplates.cs:37-116`、`SurveyTemplates.cs:395-404`
- **IMPLEMENT**: 提供两个工厂方法（因三副本类型/条数不同）：
  ```csharp
  public static class DefaultEventRules
  {
      /// <summary>The 7 shared base rules as SavedEventRule[] (no TriggerKey).</summary>
      public static SavedEventRule[] BaseSaved() { /* 7 条逐字复制 SurveyTemplates.cs:37-116 */ }
      /// <summary>Full EventRule[] incl. Sunset (8 rules) for EventSchedule default.</summary>
      public static EventRule[] BaseRuntime() { /* 8 条，含 Sunset，TriggerKey=Digit1..8 */ }
  }
  ```
  - `SurveyTemplates.cs` 两处 `Rules = ...` 改 `Rules = DefaultEventRules.BaseSaved()`
  - `EventSchedule.cs` 字段初始化器改引用 `BaseRuntime()`（ScriptableObject 字段初始化器可调静态方法）
- **MIRROR**: CSHARP_STATIC_UTIL
- **IMPORTS**: 无
- **GOTCHA**: ⚠️ **三副本不完全一致，不是纯提取**：
  - EventSchedule = 8 条（含 `Sunset Weather` Digit8，`SpeedDelta=-3f,Duration=20f,WeatherType.Sunset`）+ 强类型 `EventRule` + 每条 `TriggerKey`
  - 两模板 = 7 条（无 Sunset）+ `SavedEventRule` + `(int)` 枚举转换 + 无 TriggerKey
  - 处 2（`:37-116` 多行）与处 3（`:395-404` 单行）**前 7 条数据值逐字一致**——最纯粹的重复。
  - **决策点**：Sunset 是否应进入模板？默认**保守**——`BaseSaved()` = 现有 7 条（不含 Sunset，保持模板行为不变）；`BaseRuntime()` = 8 条（含 Sunset，保持 EventSchedule 不变）。可让 `BaseRuntime` 内部 `EventRuleKeys.AssignKeys(BaseSaved())` 再 append Sunset + 手动补 Digit8，以复用前 7 条数据；或各自独立写以避免类型耦合。
  - `SavedEventRule` 用 `(int)ComparisonOperator.X`，`EventRule` 用强类型枚举——转换靠 `SavedEventRule.ToRule(Key)`（已存在于 SessionData.cs）。
  - **若不确定 Sunset 归属，询问用户**——这是唯一有行为语义分歧的任务。
- **VALIDATE**: Unity 编译 + `EventScheduleTests`、`SurveyTemplatesTests`、`SavedEventRuleTests` 通过（这些测试锁定条数/字段值，是回归防线）

### Task U5: Unity 测试 —— 为新提取单元补测
- **ACTION**: CREATE/UPDATE `Assets/Tests/EditMode/` 下测试
- **IMPLEMENT**:
  - `AttributeEntryExtensionsTests`：`Get_ExistingKeyCaseInsensitive_ReturnsValue`、`Get_MissingKey_ReturnsDefault`、`Get_NullArray_ReturnsDefault`、`Has_*`
  - `LeaderboardFormatterTests`：`RankColor_First_ReturnsGold` 等、`RankHex_*`
  - `EventRuleKeysTests`：`AssignKeys_AssignsDigit1To9InOrder`、`AssignKeys_MoreRulesThanKeys_Truncates`
  - 扩展现有 `EventScheduleTests`/`SurveyTemplatesTests` 断言 DefaultEventRules 条数（8/7）与首条字段
- **MIRROR**: CSHARP_TEST（`Method_Scenario_ExpectedResult`，`Assert.AreEqual`）
- **IMPORTS**: `using NUnit.Framework;`（+ `System` 如需 `Array`）
- **GOTCHA**: 遵循代码库 PascalCase 命名（非 `.claude/rules/test-standards.md` 的 GDScript 蛇形约定）。测试放 `Tests.asmdef` 程序集下（已引用 `EDIRacing.Runtime` + `Unity.InputSystem`）。
- **VALIDATE**: 见下方 Unity 验证命令

---

## Testing Strategy

### Backend Unit Tests（Vitest，扩展现有）
| Test | Input | Expected | Edge? |
|---|---|---|---|
| config.normalizeRoomCode | `' ab '` | `{ok:true, code:'AB'}` | trim+upper |
| config.normalizeRoomCode | `''`/`null` | `{ok:false, error:'roomCode is required'}` | ✓ |
| config.generateShareCode | — | 8 字符 hex 大写 | — |
| loadOwnedSurvey | 他人 survey id | 404 `{success:false,error:'Survey not found'}` | ✓ 归属 |
| loadOwnedSurvey | 自己 survey | `req.survey` 挂载，next() | — |
| sendToGameRoom | 服务器无响应 | 504 `Game server did not respond in time` | ✓ 超时 |
| sendToGameRoom | 连接失败 | 502 `Cannot connect to game server` | ✓ |
| applyMigrations | 全新库 | game_sessions 表 + 15 列齐全 | — |
| applyMigrations | 旧库（缺列） | ALTER 补列成功不抛错 | ✓ 重放幂等 |

### Frontend（无测试框架，手动验证）
| 验证点 | 方法 |
|---|---|
| 分享链接 3 处 URL | 手动对比重构前后字符串逐字一致 |
| 3 种下载（xlsx/csv/json） | 手动触发，核对文件名+内容 |
| ResultsTable / EventLogTable | ResultsTab 与 HistoryPage 渲染逐像素对比 |

### Unity（EditMode）
| Test | Expected |
|---|---|
| AttributeEntryExtensions.Get 大小写不敏感 | 命中返回 Value |
| LeaderboardFormatter.RankColor(0/1/2/3) | Gold/Silver/Bronze/white |
| EventRuleKeys.AssignKeys | Digit1..N 顺序分配、超限截断 |
| EventScheduleTests | 默认 8 条（含 Sunset） |
| SurveyTemplatesTests | 模板 7 条 |

### Edge Cases Checklist
- [x] 空/null 输入（Get null array、normalizeRoomCode 空串）
- [x] 归属越权（他人 survey → 404）
- [x] 迁移幂等（ALTER 重放不抛错）
- [x] rules 超过 9 条（AssignKeys 截断 + 告警保留）
- [x] WS 超时/连接失败（504/502）
- [x] 权限拒绝（loadOwnedSurvey 404 语义）

---

## Validation Commands

### Backend
```bash
cd web-app && npm test          # vitest run — 全部现有 + 新增测试通过
node -e "import('./src/config.js').then(m=>console.log('OK', m.generateShareCode().length===8))"
```
EXPECT: 所有测试通过；config 导出正常

### Frontend
```bash
cd web-app/client && npm run build     # Vite 构建无错
npm run lint                            # oxlint 无新增告警
```
EXPECT: 构建成功、lint 干净；手动核对 3 类下载与两张表渲染不变

### Unity（EditMode 测试）
```bash
# 优先通过 UnitySkills API（若可用）触发 EditMode 测试
curl -s -X POST http://localhost:8090/run-tests -d '{"mode":"EditMode"}' 2>/dev/null || \
# 回退：CLI headless（路径按本机 Unity 安装调整）
/Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . -testPlatform EditMode \
  -testResults /tmp/editmode-results.xml -logFile -
```
EXPECT: 全部 EditMode 测试通过（含新增 AttributeEntryExtensions/LeaderboardFormatter/EventRuleKeys + 现有 EventSchedule/SurveyTemplates 回归）

### Manual Validation
- [ ] 后端：`npm test` 全绿，无删改任何响应信封文案
- [ ] 后端：`git grep "ws://localhost:8080"` 仅剩 `config.js` 一处
- [ ] 后端：`git grep "WHERE id = ? AND user_id = ?"` 仅剩 loadOwnedSurvey + 3 个 changes 变体
- [ ] 后端：`git grep -c "CREATE TABLE IF NOT EXISTS game_sessions"` = 1（仅 schema.sql，若采纳删内联方案）
- [ ] 前端：`git grep "survey/#/s/"` 仅剩 gameLaunch.js 一处
- [ ] 前端：三处下载 + 两张表手动验证
- [ ] Unity：`grep -rn "new Color(1f, 0.84f, 0f)"` 仅剩 LeaderboardFormatter
- [ ] Unity：`grep -rn "Key.Digit1, Key.Digit2"` 仅剩 EventRuleKeys
- [ ] Unity：确认 Sunset 决策已落实、EventSchedule 8 条 / 模板 7 条不变

---

## Acceptance Criteria
- [ ] 全部任务完成
- [ ] 所有验证命令通过（后端 vitest、前端 build/lint、Unity EditMode）
- [ ] 后端 + Unity 新增单测通过
- [ ] 无类型/编译错误
- [ ] 无 lint 新增告警
- [ ] 行为逐字不变（响应信封、WS 消息、渲染表格、颜色、规则条数）

## Completion Checklist
- [ ] 代码遵循发现的模式（中间件/util/组件/扩展方法/static class 模板）
- [ ] 错误处理沿用 `{success:false,error}` 信封
- [ ] 日志沿用 `[Tag]` / `[ClassName]` 前缀
- [ ] 测试遵循 vitest / NUnit `Method_Scenario_ExpectedResult` 约定
- [ ] 无硬编码残留（grep 校验单一真相来源）
- [ ] 无超范围改动（NOT Building 清单遵守）
- [ ] gameLaunch.js 未移动、db.js 3 段 ALTER 保留

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 删 db.js 内联 game_sessions 破坏旧库迁移 | 中 | 高 | 保守方案：仅提取函数不删 CREATE；或先验证旧库路径；ALTER 三段绝不删 |
| DefaultEventRules Sunset 归属决策错误改变游戏行为 | 中 | 中 | 默认保持各自现状（模板 7 / EventSchedule 8）；不确定则问用户；测试锁条数 |
| Blob 下载合并成单签名导致 EditorPage 破坏 | 中 | 中 | 明确保留两个 named export（content+mime / blob+filename）不合并 |
| sendToGameRoom 回调时序与 responded 竞态 | 中 | 中 | done() 封装 responded+clearTimeout+close；adversarial-ws.test.js 兜底 |
| 表格组件 PascalCase 字段读错渲染空白 | 中 | 中 | 严格按 C# 序列化字段名；手动对比渲染 |
| loadOwnedSurvey 误用于 game-status 条件校验 | 低 | 中 | game-status:30 保留原样（条件性），不套无条件中间件 |
| ApplyLookup 空数组语义在扩展方法改造后变化 | 中 | 中 | 保留原前置空检查分支；U2 GOTCHA 已标注 |
| Unity 首个扩展方法编译/可见性问题 | 低 | 低 | 全局命名空间自动可见；C# 3.0+ 支持；编译验证 |
| 前端无测试网，回归靠人工 | 中 | 中 | 逐项手动 checklist；build+lint 兜底静态错误 |

## Notes
- 三 workstream **完全独立**，强烈建议**分 3 个 PR**（后端 / 前端 / Unity），便于审查与回滚。本计划按 workstream 分段，可分别 `/prp-implement`。
- 唯一需要人工决策的点：**Task U4 的 Sunset 规则归属**。其余均为机械的行为保持型重构。
- 探索发现的重复计数已在「关键偏差」表修正（属性查找 8 处非 6、game_sessions 3 副本非 2、Blob 3 处且已有共享工具、WS 端点在 export.js）。
- 全程无外部依赖研究需要——纯内部模式。
