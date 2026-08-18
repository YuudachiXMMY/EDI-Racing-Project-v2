# Implementation Report: 上线前生产阻塞修复（归档端点密钥 + 影子路由 + 赛果浮层）

## Summary
落地三项上线阻塞修复：**S1** 归档端点消除硬编码公开默认密钥并 fail-closed；**A1** 消除响应/结果路由的双前缀影子 URL；**#2** 把 `RaceFinishPanel` 接入出货场景 `complete_track_demo.unity`（用户确认为真实缺口，走补线分支 7B）。

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium |
| Confidence | 8/10 | 达成（S1/A1 端到端验证；#2 结构验证，视觉待编辑器确认）|
| Files Changed | web-app 2 源 + 1~2 测试；Unity 1 场景 | web-app 3 源 + 1 测试；Unity 1 场景 = 5 文件 |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | S1 归档端点 fail-closed + 单一密钥真源 | ✅ Complete | 加常量时间比较；缺 header 不崩溃 |
| 2 | S1 启动告警归档端点状态 | ✅ Complete | 默认密钥启动即出现 DISABLED 告警 |
| 3 | A1 消除影子 URL（路由前缀烘焙 + 单次挂载）| ✅ Complete | responses/results 各挂一次 /api |
| 4 | S1 纯函数测试矩阵 | ✅ Complete | 5 条（含 non-string 边界）全绿 |
| 5 | A1 supertest URL 断言 | ⏭️ Skipped | 计划标为可选；改用 Task 6 curl 矩阵实证，避免引入 supertest 依赖 |
| 6 | A1/S1 手动 curl 验证矩阵 | ✅ Complete | 见下方集成结果 |
| 7 | #2 决策门 | ✅ Resolved | 用户选择"补线进场景（有缺口）" |
| 7B | #2 补线到出货场景 | ✅ Complete | 结构验证通过；视觉验证 ADVISORY，需编辑器 |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis | ✅ Pass | `node --check` 三源文件语法 OK（纯 JS 无 tsc）|
| Unit Tests | ✅ Pass | 54/54 通过（新增 archive 5 条）|
| Build | N/A | web-app 无构建步骤（Node 直跑）|
| Integration | ✅ Pass | adversarial-ws 集成测试通过（真实 Server + 归档 fire-and-forget）；curl 矩阵全符合 |
| Edge Cases | ✅ Pass | 缺 header→403 不崩溃；默认/空/非字符串密钥→禁用；影子 URL→404 |

### 集成 curl 矩阵结果（本地起服实测）
- 默认密钥：`POST /api/sessions/archive` → **503**（fail-closed）；启动日志含 archive DISABLED 告警。
- 强密钥：错误 header → **403**；缺 header → **403**（无崩溃）；正确 header → **200** 正常写入 `{id:1}`。
- 影子 URL 全 **404**：`/api/surveys/sessions/archive`、`/api/5/results`、`/api/surveys/s/CODE`、`/api/5/responses`。
- 规范 URL 存活：`/api/s/x`→业务 JSON、`/api/sessions`→401。

### #2 场景结构验证
- RaceFinishPanel GUID 场景引用 0→1；三新锚点就位；RectTransform 父节点=Canvas 868943149；接线指向真实 RaceManager 607591796 / ScoreManager 607591801（与场景既有 UI 引用一致）；`serializedVersion: 6`、`m_Layer: 0` 与既有 UI 对齐。

## Files Changed

| File | Action | 说明 |
|---|---|---|
| `web-app/src/routes/results.js` | UPDATED | S1 fail-closed + 常量时间比较 + `archiveSecretUsable` 导出；A1 `/surveys/:id/results` |
| `web-app/src/routes/responses.js` | UPDATED | A1 `/surveys/:id/responses` |
| `web-app/src/index.js` | UPDATED | A1 各 router 单次挂载 /api；S1 归档禁用启动告警 |
| `web-app/__tests__/results-archive.test.js` | CREATED | S1 纯函数 5 测试 |
| `Assets/Scenes/complete_track_demo.unity` | UPDATED | #2/7B FinishPanel GameObject + 组件 + 接线 |

## Deviations from Plan
- **Task 5（supertest URL 测试）跳过**：计划已标"可选"。为不给 web-app 引入新依赖，改由 Task 6 的本地起服 curl 矩阵实证影子 URL 404 与归档 fail-closed，覆盖同等断言。
- **#2 走 7B（补线）**：由用户决策门选定，非偏离。

## Issues Encountered
- **Fresh worktree 缺依赖**：worktree 无 `node_modules`；`web-app` 与 `Server` 各跑一次 `npm install`（`Server` 依赖是 adversarial-ws 集成测试 spawn 真实服务器所需）。安装后 54/54 全绿。
- **UnitySkills API 离线（HTTP 000）**：按 technical-preferences 回退到直接编辑场景 YAML。且 Unity 编辑器运行于主项目而非本 worktree，故采用手改 worktree 场景 YAML 的隔离路径——合并后主项目 Unity 拉取即生效。

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `web-app/__tests__/results-archive.test.js` | 5 | `archiveSecretUsable`：默认/未设/空/非字符串→false，强密钥→true |

## 遗留 / 需人工
- **#2 视觉验证（ADVISORY）**：赛果浮层的实际渲染须在 Unity 编辑器打开 `complete_track_demo.unity`、播放一局至结束确认，并按 coding-standards 存截图到 `production/qa/evidence/` + lead sign-off。无头构建无法验证渲染。
- **生产部署提醒**：归档功能现要求设置强 `INTERNAL_SECRET`；否则端点 503 禁用（预期的 fail-closed）。

## Next Steps
- [ ] Unity 编辑器视觉确认 #2（截图 + sign-off）
- [ ] `/code-review` 复审改动
- [ ] PR #52 转正式（当前为草稿）
