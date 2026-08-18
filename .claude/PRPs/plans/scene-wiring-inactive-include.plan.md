# Plan: SceneWiring 根治 — FindObjectsInactive.Include 防面板重复/丢失

## Summary
Unity 的 `FindFirstObjectByType<T>()` 默认跳过 inactive 对象。EDI Racing 的多个 UI 面板（JoinScreen、EventPanel、RaceControlPanel、LeaderboardPanel、ImportPanel 等）创建后立即 `SetActive(false)`。因此重跑编辑器接线（`EDI Racing > Wire All References` / `Setup Track`）时，这些已存在但处于 inactive 状态的面板被漏找 → RaceUI 引用不再重连、`FindOrCreate` 重复创建组件、`WireOrCreateJoinScreen` 重复生成整个 JoinScreen 面板。本计划把两支编辑器脚本里所有定位场景对象的查找调用统一改为 `FindFirstObjectByType<T>(FindObjectsInactive.Include)`，从根上消除该问题。

## User Story
As a 讲师/开发者维护 EDI Racing 场景,
I want 重新运行接线或 Setup Track 时不会漏掉、复制或丢失处于隐藏状态的 UI 面板,
So that 每次重生成场景后学生仍能看到并加入到 JoinScreen，且场景层级不会被重复对象污染。

## Problem → Solution
**现状**：接线脚本用不带 `FindObjectsInactive.Include` 的 `FindFirstObjectByType`，找不到 inactive 面板 → 漏接线 + 重复创建。
**目标**：所有定位场景单例/面板的查找都带 `FindObjectsInactive.Include`，inactive 面板也能被找到并复用，接线幂等性对"任意激活状态"都成立。

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A（free-form 任务描述）
- **PRD Phase**: N/A
- **Estimated Files**: 2（`SceneWiring.cs`, `TrackSetupEditor.cs`）

---

## UX Design

### Before
```
重跑 Wire All References（JoinScreen 处于 inactive）
        │
        ▼
FindFirstObjectByType<JoinScreen>() → null（跳过 inactive）
        │
        ├─ SceneWiring: Wire(raceUI.JoinScreen, null) → ⚠ "target not found"，引用悬空
        └─ TrackSetupEditor.WireOrCreateJoinScreen → 再造一个 JoinScreen（重复面板）
        │
        ▼
场景里出现 2 个 JoinScreen / RaceUI.JoinScreen = null → 学生端加入界面丢失
```

### After
```
重跑 Wire All References（JoinScreen 处于 inactive）
        │
        ▼
FindFirstObjectByType<JoinScreen>(FindObjectsInactive.Include) → 复用已存在实例
        │
        ├─ SceneWiring: Wire(raceUI.JoinScreen, joinScreen) → 正确重连
        └─ TrackSetupEditor.WireOrCreateJoinScreen → early-return 现有实例，不再重复创建
        │
        ▼
唯一 JoinScreen，引用完好 → 学生端加入界面稳定存在
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| `EDI Racing > Wire All References` 重跑 | inactive 面板漏接线/重复 | 幂等，复用 inactive 面板 | 核心修复点 |
| `EDI Racing > Setup Track` 重跑 | 重复生成 JoinScreen/其它面板 | early-return 现有面板 | create-if-missing 分支 |
| 运行时 `RaceUI.ResolveMissingReferences` | 已正确（含 Include） | 不变 | 已是先例，无需改 |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/UI/RaceUI.cs` | 48-70 | **要照抄的先例** — `ResolveMissingReferences` 已用 `FindObjectsInactive.Include` 修好运行时同款 bug，含解释性注释 |
| P0 (critical) | `Assets/Scripts/Editor/SceneWiring.cs` | 27-52, 314-336, 369-378 | 待改的所有查找调用 + `FindOrCreate` 助手（重复创建元凶） |
| P0 (critical) | `Assets/Scripts/Editor/TrackSetupEditor.cs` | 411, 493, 540-549, 657, 789, 834, 879, 916 | 待改的所有查找调用；916 是 JoinScreen 重复创建点 |
| P1 (important) | `Assets/Scripts/Editor/TrackSetupEditor.cs` | 828, 873, 910, 993 | 确认这些面板 create 后 `SetActive(false)` → 属于 inactive 高危对象 |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| `FindObjectsInactive` 枚举 | Unity 6.x ScriptReference（`Object.FindFirstObjectByType`） | 重载 `FindFirstObjectByType<T>(FindObjectsInactive.Include)` 是 Unity 2022.2+/Unity 6 官方 API；`Include` 使查找覆盖 inactive 对象。项目已在 `RaceUI.cs` 使用，无版本风险。 |

---

## Patterns to Mirror

### INACTIVE_INCLUDE_LOOKUP（核心模式）
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:56-69
if (RaceManager == null)
    RaceManager = FindFirstObjectByType<RaceManager>(FindObjectsInactive.Include);
...
if (JoinScreen == null)
    JoinScreen = FindFirstObjectByType<JoinScreen>(FindObjectsInactive.Include);
```
> 关键：`FindFirstObjectByType<T>(FindObjectsInactive.Include)`。`SceneWiring.cs` 用的是 `Object.FindFirstObjectByType<T>()`（静态限定 `Object.`），保留该限定，仅在泛型后补参数：`Object.FindFirstObjectByType<T>(FindObjectsInactive.Include)`。

### EXPLANATORY_COMMENT（注释风格）
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:48-53
// Defensive auto-wire: ... The panels start inactive, so FindObjectsInactive.Include
// is required — without it the leaderboard/events/controls would never re-appear at Racing.
```
> 在被改的查找块上方补一句同风格注释，说明"面板可能 inactive，必须 Include 才能复用而非重复创建"。

### IDEMPOTENT_CREATE_GUARD（会被本修复强化的幂等分支）
```csharp
// SOURCE: Assets/Scripts/Editor/TrackSetupEditor.cs:914-917
private JoinScreen WireOrCreateJoinScreen(Transform canvasRoot, NetworkManager nm)
{
    var existing = Object.FindFirstObjectByType<JoinScreen>();  // ← 改这里
    if (existing != null) return existing;
    ...
```

### DUPLICATE_CREATE_HELPER（SceneWiring 重复创建元凶）
```csharp
// SOURCE: Assets/Scripts/Editor/SceneWiring.cs:369-378
private static T FindOrCreate<T>(GameObject host, string label) where T : Component
{
    var existing = Object.FindFirstObjectByType<T>();  // ← 改这里，否则 inactive 目标被重复 AddComponent
    if (existing != null) return existing;
    var component = host.AddComponent<T>();
    ...
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Editor/SceneWiring.cs` | UPDATE | 全部 `Object.FindFirstObjectByType<T>()` → 加 `FindObjectsInactive.Include`，含 `FindOrCreate` 助手 |
| `Assets/Scripts/Editor/TrackSetupEditor.cs` | UPDATE | 全部 `Object.FindFirstObjectByType<T>()` → 加 `FindObjectsInactive.Include`，重点是 create-if-missing 分支 |

## NOT Building
- 不改 `RaceUI.cs`（运行时逻辑已含 Include，正确）。
- 不改任何非 Editor 的运行时脚本 —— 本 bug 仅出现在编辑器接线/建场景流程。
- 不引入新的 `[MenuItem]`、新工具或新测试框架。
- 不改动面板的 `SetActive(false)` 行为（inactive 是设计意图，不能改）。
- 不重构 `Wire` / `FindOrCreate` 的方法签名，只改内部查找调用。

---

## Step-by-Step Tasks

### Task 1: SceneWiring.cs — 核心单例与面板查找加 Include
- **ACTION**: 把 `SceneWiring.cs` 第 27、34-52 行的所有 `Object.FindFirstObjectByType<T>()` 改为 `Object.FindFirstObjectByType<T>(FindObjectsInactive.Include)`。
- **IMPLEMENT**: 逐个类型：RaceManager、NetworkManager、NetworkSync、ScoreManager、CarSpawner、LapTracker、EventManager、SessionManager、WeatherEffect、CameraManager、WaypointPath、RaceUI、SetupScreen、JoinScreen、LeaderboardPanel、EventPanel、RaceControlPanel。
- **MIRROR**: INACTIVE_INCLUDE_LOOKUP（保留 `Object.` 静态限定）。
- **IMPORTS**: 无需新增 —— `FindObjectsInactive` 在 `UnityEngine` 命名空间，文件首行 `using UnityEngine;` 已存在。
- **GOTCHA**: 保留 `Object.` 前缀（这是 `UnityEngine.Object` 的静态方法，`SceneWiring` 是 static 类无实例上下文）。不要误删。
- **VALIDATE**: `grep -n "FindFirstObjectByType<" Assets/Scripts/Editor/SceneWiring.cs | grep -v "FindObjectsInactive"` 应无输出。

### Task 2: SceneWiring.cs — FindOrCreate 助手加 Include（防重复 AddComponent）
- **ACTION**: 改第 371 行 `var existing = Object.FindFirstObjectByType<T>();` → 加 `FindObjectsInactive.Include`。
- **IMPLEMENT**: `var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);`
- **MIRROR**: DUPLICATE_CREATE_HELPER。
- **IMPORTS**: 无。
- **GOTCHA**: 这是最隐蔽的重复源 —— SurveyConfigManager / HostLaunchBootstrap / StudentJoinBootstrap 若挂在 inactive GameObject 上，缺 Include 会被重复 `AddComponent`，且旧的 StudentJoinBootstrap 丢失会重开安全漏洞（见 SceneWiring.cs:292-295 注释）。
- **VALIDATE**: Task 1 的 grep 已覆盖此行。

### Task 3: SceneWiring.cs — 相机与标签查找加 Include
- **ACTION**: 改第 314（RaceCameraController）、315（SpectatorCamera）、336（CarLabelSpawner）行。
- **IMPLEMENT**: 同模式追加 `FindObjectsInactive.Include`。
- **MIRROR**: INACTIVE_INCLUDE_LOOKUP。
- **GOTCHA**: 相机对象一般 active，但统一加 Include 无副作用（Include 是 active 的超集），保持全文件一致。
- **VALIDATE**: 同 Task 1 grep。

### Task 4: TrackSetupEditor.cs — 全部查找调用加 Include（重点 create-if-missing）
- **ACTION**: 改第 411、493、540、549、657、789、834、879、916 行的 `Object.FindFirstObjectByType<T>()`。
- **IMPLEMENT**: 逐行追加 `FindObjectsInactive.Include`。第 916 行（`WireOrCreateJoinScreen`）、789（Leaderboard）、834（EventPanel）、879（RaceControlPanel）、657（SetupScreen）是防重复创建的关键；411/493/540/549（WaypointPath/Camera/EventSystem/Canvas）通常 active，为一致性同样加。
- **MIRROR**: IDEMPOTENT_CREATE_GUARD。
- **IMPORTS**: 无（`using UnityEngine;` 已在文件头）。
- **GOTCHA**: 第 540 行是布尔判断 `if (Object.FindFirstObjectByType<EventSystem>() == null)` —— 参数加在泛型括号后、`== null` 前：`if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)`。
- **VALIDATE**: `grep -n "FindFirstObjectByType<" Assets/Scripts/Editor/TrackSetupEditor.cs | grep -v "FindObjectsInactive"` 应无输出。

### Task 5: 补充解释性注释
- **ACTION**: 在 `SceneWiring.cs` 的 UI 面板查找块（第 47-52 行上方）与 `TrackSetupEditor.WireOrCreateJoinScreen` 处各补一行注释。
- **IMPLEMENT**: 例如 `// UI panels start inactive (SetActive(false)); FindObjectsInactive.Include is required so re-running wiring reuses them instead of duplicating/losing them.`
- **MIRROR**: EXPLANATORY_COMMENT。
- **GOTCHA**: 注释要说明"为什么"（inactive 复用），而非"做了什么"，与 RaceUI.cs:48-53 风格一致。
- **VALIDATE**: 人工阅读，确认注释准确。

---

## Testing Strategy

### 说明
本改动为 Unity 编辑器脚本（`Assets/Scripts/Editor/`），依赖 Unity `UnityEngine.Object` 场景 API，**不能** headless 单元测试（见 coding-standards.md「What NOT to Automate」— 平台/编辑器渲染相关）。验证以编辑器内手动幂等测试为主。

### 手动幂等测试（BLOCKING — Integration/UI 类）
| 测试 | 步骤 | 期望 |
|---|---|---|
| 重复 Wire 不复制 | 打开 `complete_track_demo.unity` → 运行 `EDI Racing > Wire All References` 两次 | JoinScreen/EventPanel/RaceControlPanel/LeaderboardPanel 各仅 1 个；Console 无 "target not found" 警告 |
| inactive 复用 | 在层级里手动把 JoinScreen `SetActive(false)` → 再跑 Wire All | 不新建 JoinScreen；`RaceUI.JoinScreen` 引用仍指向原实例 |
| Setup Track 重跑 | 对已建好的场景重跑 `EDI Racing > Setup Track` | 无重复面板；无重复 StudentJoinBootstrap/HostLaunchBootstrap |
| 学生端回归 | 重生成场景后按学生链接（role=play）加入 | JoinScreen 正常显示，可输入房间号加入 3D 赛道 |

### Edge Cases Checklist
- [x] 面板处于 inactive → 被复用而非重建（核心）
- [x] `FindOrCreate` 目标 inactive → 不重复 AddComponent
- [x] 空场景（无 RaceManager）→ 仍按原逻辑 early-return 报错（Task 未改该守卫）
- [x] 面板处于 active → 行为不变（Include 是超集）
- [ ] N/A — 无并发/网络/权限维度

---

## Validation Commands

### Static Analysis（语法/编译）
```bash
# 关键：改后 Unity 需重新编译无报错。若装了 dotnet 可对 Assembly 做粗检；
# 权威验证是 Unity 编辑器 Console 无 CS 编译错误。
grep -rn "FindFirstObjectByType<" Assets/Scripts/Editor/ | grep -v "FindObjectsInactive"
```
EXPECT: **无输出**（所有编辑器查找调用都已带 Include）

### 编译验证（Unity）
```
# 在 Unity 编辑器中：Assets 面板等待重新编译，查看 Console
```
EXPECT: 无 CS 编译错误；`FindObjectsInactive` 解析成功

### 全量测试套件
```bash
# 现有 EditMode 测试（不覆盖本编辑器脚本，跑通确认无回归）
# Unity Test Runner (EditMode) 或 CI: game-ci/unity-test-runner@v4
```
EXPECT: 无回归（既有测试全绿）

### Manual Validation
- [ ] `EDI Racing > Wire All References` 连跑 2 次，层级里每种面板仅 1 个
- [ ] Console 无 "target not found" / 无 "[WireAll] Created ..." 对已存在面板的误报
- [ ] 手动把 JoinScreen inactive 后重跑，不产生第 2 个 JoinScreen
- [ ] 学生链接加入流程回归通过

---

## Acceptance Criteria
- [ ] `SceneWiring.cs` 与 `TrackSetupEditor.cs` 中所有 `FindFirstObjectByType<T>()` 均带 `FindObjectsInactive.Include`
- [ ] `grep ... | grep -v FindObjectsInactive` 对两文件均无输出
- [ ] Unity 编译无错误
- [ ] 重复 Wire / Setup 不产生重复面板（手动验证通过）
- [ ] inactive JoinScreen 被复用而非重建
- [ ] 无既有测试回归

## Completion Checklist
- [ ] 遵循 RaceUI.cs 的 `FindObjectsInactive.Include` 先例
- [ ] 保留 `Object.` 静态限定与原方法结构
- [ ] 补充「为什么」风格注释
- [ ] 无硬编码新值
- [ ] 无越界改动（不碰运行时脚本、不改面板激活语义）
- [ ] 自包含 —— 实施期无需再查代码库

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 遗漏某个 find 调用 | Low | Medium | 用 grep 校验命令兜底，两文件均须无输出 |
| 误删 `Object.` 前缀致编译错 | Low | Low | Unity 立即报 CS 编译错，改前后对照 |
| Include 找到"错误"的 inactive 同类型对象 | Very Low | Low | 场景内各面板类型均为单例，无同类型多实例；`FindFirstObjectByType` 语义不变 |

## Notes
- 这是 PR #68（`fix(unity): wire JoinScreen.NetworkManager`）的**根治版**：#68 补的是单个引用，本计划消除"重生成场景即丢失/重复"的结构性根因。
- `RaceUI.cs:48-53` 的注释已把此类 bug 的原理讲清楚，实施时直接引用其措辞即可保持一致。
- 编辑器脚本无法自动化测试属项目既定约束（coding-standards.md），故以幂等手动测试为 BLOCKING 证据。
