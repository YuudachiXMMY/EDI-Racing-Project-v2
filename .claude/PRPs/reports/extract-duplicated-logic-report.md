# Implementation Report: Extract Duplicated Logic — Backend + Frontend

> **全部实施完成**：Backend (B1–B6) + Frontend (F1–F4) 已合入 PR #54；**Unity workstream (U1–U5) 已实施并推送（PR #55）**。三个 workstream 全部完成，计划已归档。
>
> **Unity (U1–U5) 摘要**：`EventRuleKeys`（Digit1-9 共享 + AssignKeys，2 处内联消除）、`AttributeEntryExtensions.Get/Has`（8 处查找 → 首个扩展方法）、`LeaderboardFormatter`（金银铜色 + RankHex，4 处）、`DefaultEventRules`（默认规则单一真相；**U4 决策：模板加入 Sunset → 8 条**，EventSchedule 行为逐字不变）。验证经活跃 UnitySkills API（Unity 6000.3.19f1）：编译 0 错误 0 警告；EditMode **429/430 通过**（唯一失败为 UnitySkills 包内部 ShaderGraph 测试，与本改动无关）。8 新文件 + 13 修改，源文件净减（EventSchedule −100、SurveyTemplates −92）。
>
> **Frontend 摘要**：`buildShareUrl`（3 处硬编码收敛）、`downloadBlobObject`（EditorPage 去本地副本）、`ResultsTable`/`EventLogTable` 展示组件（ResultsTab+HistoryPage 复用，PascalCase 字段保留）。无偏离；Vite build 通过、oxlint 无新增告警；净 ~−120 行；2 新建组件 + 7 修改。前端无测试框架，靠 build+lint+人工核对。

## Summary
按 `.claude/PRPs/plans/extract-duplicated-logic.plan.md` 实施后端去重：集中化配置（`config.js`）、归属校验中间件（`loadOwnedSurvey`）、WS 端点骨架提取（`sendToGameRoom`）、迁移函数统一（`applyMigrations`，消除 `game_sessions` 第 3 副本）。纯行为保持型重构，净减 146 行。

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | XL（整体） | Backend 部分 = Medium/Large |
| Confidence | 8/10 | 后端单遍成功，无返工 |
| Files Changed（后端） | ~10 | 13（3 新建源 + 3 新建测试 + 7 修改） |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| B1 | config.js 集中配置 | ✅ Complete | 逐字实现 4 个导出 |
| B2 | 各路由改用 config.js | ✅ Complete | **偏离**：`results.js:101` archive roomCode 保留原样（保行为） |
| B3 | loadOwnedSurvey 中间件 | ✅ Complete | 挂 requireAuth 之后，404 信封逐字 |
| B4 | 11 处 SELECT-then-404 改中间件 | ✅ Complete | 3 处 changes===0 变体保留；game-status 条件校验保留 |
| B5 | sendToGameRoom() WS 骨架 | ✅ Complete | 两端点逐字差异全保留；done() 幂等封装 |
| B6 | applyMigrations + 消除 3 副本 | ✅ Complete | **保守**：保留 db.js 内联 CREATE，仅去重 test-helpers |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis | ✅ Pass | ESM 无类型系统；import 路径校验 |
| Unit Tests | ✅ Pass | 71 passed (9 files)；新增 17 例 |
| Build | N/A | Node 无 build 步骤 |
| Integration | ✅ Pass | adversarial-ws.test.js（WS 504/502/400/成功）全绿 |
| Edge Cases | ✅ Pass | 空/null roomCode、归属越权 404、迁移重放幂等 |

## Files Changed

| File | Action | Lines |
|---|---|---|
| `web-app/src/config.js` | CREATED | +~30 |
| `web-app/src/middleware/loadOwnedSurvey.js` | CREATED | +~23 |
| `web-app/src/lib/gameSocket.js` | CREATED | +~68 |
| `web-app/src/routes/export.js` | UPDATED | −186 净大幅瘦身 |
| `web-app/src/routes/surveys.js` | UPDATED | −39 |
| `web-app/src/routes/responses.js` | UPDATED | −13 |
| `web-app/src/routes/results.js` | UPDATED | −17 |
| `web-app/src/routes/game-status.js` | UPDATED | −4 |
| `web-app/src/db.js` | UPDATED | applyMigrations 提取 |
| `web-app/__tests__/test-helpers.js` | UPDATED | −24 去副本 |

净：+115 / −261 = **−146 行**（不含新增测试）。

## Deviations from Plan

1. **`results.js` archive roomCode 未迁移到 normalizeRoomCode**（WHAT）。WHY：该端点原样存 `room_code`（大小写敏感落库），且 `if (!roomCode)` 接受纯空格串而 normalizeRoomCode 拒绝——迁移会改变可观察行为。计划 B1-GOTCHA 要求确认下游大小写敏感性，此为结论：保守保留最安全。
2. **B6 保留 db.js 内联 CREATE**（WHAT）。WHY：`IF NOT EXISTS` 对旧库零风险 no-op/补建；真正漂移源（test-helpers 第 3 副本）已消除。符合计划 GOTCHA 保守选项。
3. **3 个端点归属 404 与 body 400 顺序翻转**（WHAT）。WHY：中间件必先于 handler；仅「他人 survey + 非法 body」极端边缘场景状态码顺序变，信封文案不变，无测试覆盖该顺序。

## Issues Encountered
- worktree 缺依赖 → `web-app/` 与 `Server/` 分别 `npm install`（含 better-sqlite3 原生编译）。
- loadOwnedSurvey 内部 `getDb()` 不可注入 → 测试用 `vi.mock('../src/db.js')` 注入内存库。

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `__tests__/config.test.js` | 11 | normalizeRoomCode/generateShareCode/URL 默认值 |
| `__tests__/loadOwnedSurvey.test.js` | 3 | 他人/不存在→404；自己→挂 req.survey+next |
| `__tests__/migrations.test.js` | 3 | 全新库 14 列；重放幂等；旧库补列 |

## Remaining Workstreams（未实施）

| Workstream | Tasks | 阻塞/依赖 |
|---|---|---|
| Frontend | F1–F4 | 无阻塞；可 build/lint 验证（无测试网） |
| Unity C# | U1–U5 | **U4 Sunset 归属需用户决策**；EditMode 验证需 Unity 编辑器/UnitySkills API 在线 |

## Next Steps
- [ ] Backend：`/code-review` 或直接 review PR #54，合入后端
- [ ] Frontend workstream：独立 `/prp-implement`（可本会话或新会话）
- [ ] Unity workstream：需先决定 Task U4 的 Sunset 规则归属，再实施 + EditMode 验证
