# Plan: HUD 面板数据源 Auto-Wire（排行榜 / 事件流接线）

## Summary
分支 `origin/worktree-raceui-hud-autowire-camera-hint` 上有一个从未合并进 main 的 commit `a103209`，它修复了一个真实的 HUD 可见性回归：`LeaderboardPanel` 和 `EventPanel` 在场景里可见，但因为它们各自的数据源（`ScoreManager` / `EventManager`）引用为 null，排行榜和事件流永远是空的。本计划把这个 29 行、3 文件的干净修复重新落地到 main，并补上面板当前缺失的回归测试。

## User Story
As a 主持比赛的老师（host），
I want 游戏内 HUD 的排行榜和事件面板真正显示实时排名与事件内容，
So that 学生和观众能看到比赛进程，而不是一个可见但永久空白的面板。

## Problem → Solution
**当前状态**：`SceneWiring.WireAll()` 接线了 `RaceUI.Leaderboard` / `RaceUI.Events`（让面板*出现*），但从未接线面板*读取数据*所依赖的 `LeaderboardPanel.ScoreManager` 和 `EventPanel.EventManager`。`WireOrCreate*` 对已存在的面板会提前返回、不重新赋值，所以场景可以带着 null manager 出厂 → 面板可见但 `RefreshLeaderboard()` / `BuildEventRows()` 在 null 时提前返回 → 永久空白。
→
**目标状态**：(1) 两个面板在 `Start()` 里做防御性 auto-resolve（manager 为 null 时用 `FindFirstObjectByType` 兜底）；(2) `SceneWiring.WireAll()` 编辑器菜单也永久接线这两个 manager 引用。合并后 HUD 内容正常显示。

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A（自由文本输入 — 未合并欠账 commit 的归属决策）
- **PRD Phase**: N/A
- **Estimated Files**: 3 生产文件（复用 commit a103209）+ 1 可选测试文件

---

## 决策：做完（重新落地），不要删掉

调研结论（已用只读命令验证，见下）明确指向「做完」：

| 判据 | 结果 |
|---|---|
| commit 是否真实修复 bug？ | 是 — 修复 HUD 面板可见但空白的回归 |
| 是否已在 main 上？ | 否 — 三处改动 grep 均 `NOT PRESENT` |
| 是否能干净应用到 main？ | 是 — `git apply --check` 三个文件全部通过、无冲突 |
| 改动是否自包含、低风险？ | 是 — 29 行纯新增（0 删除），仅新增防御性兜底 |
| 分支其余 diff 是什么？ | 仅分支落后 main 的陈旧噪音（web-app 本地化等已在 main），**不属于** commit a103209 |

> 「删掉」会永久丢失一个已验证的 HUD 修复，且该 bug 会在下次生成/重置场景时复发。「做完」成本极低（可 cherry-pick）。**推荐做完。**

---

## UX Design

### Before
```
┌──────────────── 游戏内 HUD ────────────────┐
│  🏁 LEADERBOARD              📢 EVENTS       │
│  ┌───────────────┐          ┌────────────┐  │
│  │               │          │            │  │
│  │   (空白)      │          │  (空白)    │  │
│  │  面板可见但    │          │ 面板可见但  │  │
│  │  永远没有行    │          │ 没有事件行  │  │
│  └───────────────┘          └────────────┘  │
│   ScoreManager = null        EventManager=null│
└──────────────────────────────────────────────┘
```

### After
```
┌──────────────── 游戏内 HUD ────────────────┐
│  🏁 LEADERBOARD              📢 EVENTS       │
│  ┌───────────────┐          ┌────────────┐  │
│  │ 1. Car_Red  42│          │ [雨天] 触发 │  │
│  │ 2. Car_Blue 38│          │ [加速] 触发 │  │
│  │ 3. Car_Grn  31│          │ [减速]      │  │
│  └───────────────┘          └────────────┘  │
│   实时刷新(0.5s)             事件按钮可点击   │
└──────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| 排行榜面板 | 可见但空白 | 每 0.5s 显示排名行 | `ScoreManager` 兜底解析 |
| 事件面板 | 可见但空白 | 显示事件按钮行 | `EventManager` 兜底解析 |
| 编辑器菜单 `EDI Racing > Wire All References` | 不接 panel manager | 永久接线两个 manager | 场景一次修复到位 |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Editor/SceneWiring.cs` | 115-128, 357-367 | RaceUI 接线块（插入点在 128 行 `SetDirty(raceUI)` 之后）+ `Wire<T>` 幂等辅助方法 |
| P0 | `Assets/Scripts/UI/LeaderboardPanel.cs` | 9-51 | `ScoreManager` 字段声明、`Start()`、`RefreshLeaderboard()` 的 null 提前返回 |
| P0 | `Assets/Scripts/UI/EventPanel.cs` | 8-42 | `EventManager` 字段声明、`Start()`、`BuildEventRows()` 的 null 提前返回 |
| P1 | `Assets/Scripts/Race/ScoreManager.cs` | 9, 18 | `public List<CarIdentity> GetRankedCars()` — 面板读取的 API |
| P1 | `Assets/Scripts/Events/EventManager.cs` | 10 | `EventManager` 类 + `Schedule` 字段 |
| P2 | `Assets/Tests/EditMode/EventManagerTests.cs` | 1-45 | 要镜像的 EditMode 测试范式（[TestFixture]/[SetUp]/GameObject.AddComponent） |
| P2 | `Assets/Tests/EditMode/Tests.asmdef` | all | 测试程序集引用（EDIRacing.Runtime, Unity.InputSystem） |

## External Documentation
No external research needed — feature uses established internal patterns（Unity `FindFirstObjectByType`、UGUI 面板、既有 SceneWiring `Wire<T>` 幂等模式）。

> 唯一版本注记：`FindFirstObjectByType<T>(FindObjectsInactive.Include)` 是 Unity 6 API（替代旧版 `FindObjectOfType`），项目已 pin Unity 6.3 LTS，commit a103209 已正确使用此签名。

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/UI/LeaderboardPanel.cs:12
// public 字段 PascalCase；类无 namespace（全局命名空间）；文件名 = 类名
[Header("References")]
public ScoreManager ScoreManager;
```

### DEFENSIVE_AUTOWIRE（本次要落地的核心模式）
```csharp
// SOURCE: commit a103209 — Assets/Scripts/UI/LeaderboardPanel.cs Start()
// manager 为 null 时兜底解析；FindObjectsInactive.Include 覆盖未激活对象
if (ScoreManager == null)
    ScoreManager = FindFirstObjectByType<ScoreManager>(FindObjectsInactive.Include);
```

### NULL_GUARD（面板消费端 — 解释为什么空 manager = 空面板）
```csharp
// SOURCE: Assets/Scripts/UI/LeaderboardPanel.cs:50-51 (RefreshLeaderboard)
if (ScoreManager == null) return;               // ← 空 manager 导致永久空白
List<CarIdentity> ranked = ScoreManager.GetRankedCars();
// SOURCE: Assets/Scripts/UI/EventPanel.cs:41 (BuildEventRows)
if (EventManager == null || EventManager.Schedule == null) return;
```

### EDITOR_WIRE_PATTERN（幂等接线辅助）
```csharp
// SOURCE: Assets/Scripts/Editor/SceneWiring.cs:357-367
private static void Wire<T>(ref T field, T value, string label) where T : Object
{
    if (field != null) return;          // 已接线则跳过（幂等）
    if (value == null) { Warn($"{label} — target not found in scene"); return; }
    field = value;
    wiredCount++;
}
// 用法（SOURCE: SceneWiring.cs:122-123）：
Wire(ref raceUI.Leaderboard, leaderboard, "RaceUI.Leaderboard");
// SetDirty 收尾（SOURCE: SceneWiring.cs:127）：
EditorUtility.SetDirty(raceUI);
```

### TEST_STRUCTURE
```csharp
// SOURCE: Assets/Tests/EditMode/EventManagerTests.cs:6-19
[TestFixture]
public class EventManagerTests
{
    private GameObject managerObj;
    private EventManager eventManager;

    [SetUp]
    public void SetUp()
    {
        managerObj = new GameObject("EventManager");
        eventManager = managerObj.AddComponent<EventManager>();
        // ...
    }
    // [TearDown] 用 Object.DestroyImmediate 清理 GameObject
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/UI/LeaderboardPanel.cs` | UPDATE | `Start()` 开头加 `ScoreManager` 兜底解析（commit a103209 第 30-35 行） |
| `Assets/Scripts/UI/EventPanel.cs` | UPDATE | `Start()` 开头加 `EventManager` 兜底解析（commit a103209 第 23-28 行） |
| `Assets/Scripts/Editor/SceneWiring.cs` | UPDATE | `WireAll()` RaceUI 块后增加接线 `leaderboard.ScoreManager` / `eventPanel.EventManager`（commit a103209 第 127 行后） |
| `Assets/Tests/EditMode/HudPanelAutoWireTests.cs` | CREATE（可选/ADVISORY） | 补面板当前缺失的回归测试（codegraph 报告两面板「no covering tests」） |

## NOT Building

- **不** 引入分支 `worktree-raceui-hud-autowire-camera-hint` 上的其他 diff — 那些是分支落后 main 的陈旧噪音（web-app 本地化、`.codegraph/`、session-logs 等已在 main 或不相关），**只取 commit a103209**。
- **不** 改动 `complete_track_demo.unity` 场景文件 — commit a103209 的 `--stat` 只含 3 个 `.cs`，场景 diff 属于陈旧噪音，运行时 `Start()` 兜底 + 编辑器菜单已足够。
- **不** 重构 `RefreshLeaderboard()` / `BuildEventRows()` 的渲染逻辑或对象池。
- **不** 修改 `ScoreManager` / `EventManager` 的公共 API。
- **不** 触碰相机提示（camera-hint）相关代码 — 那部分已随 PR #66 合并进 main。

---

## Step-by-Step Tasks

### 落地路径选择
> **最快路径**：`git cherry-pick a103209`（已验证干净应用，无冲突）即可完成 Task 1-3。下面的手动步骤是等价说明，供无法 cherry-pick 时逐行重建。**推荐先尝试 cherry-pick。**

### Task 1: LeaderboardPanel 防御性 auto-wire
- **ACTION**: 在 `Assets/Scripts/UI/LeaderboardPanel.cs` 的 `Start()` 方法体最前面（第 33 行 `// Pre-instantiate row pool` 之前）插入兜底解析。
- **IMPLEMENT**:
  ```csharp
  // Defensive auto-wire: SceneWiring/TrackSetupEditor do not re-assign this on an
  // already-existing panel, so a scene can ship with ScoreManager unset — which leaves the
  // leaderboard visible but permanently empty (RefreshLeaderboard early-returns on null).
  if (ScoreManager == null)
      ScoreManager = FindFirstObjectByType<ScoreManager>(FindObjectsInactive.Include);
  ```
- **MIRROR**: DEFENSIVE_AUTOWIRE
- **IMPORTS**: 无新增（`FindFirstObjectByType` 是 `MonoBehaviour`/`Object` 继承方法；`FindObjectsInactive` 在 `UnityEngine` 命名空间，文件已 `using UnityEngine;`）
- **GOTCHA**: 必须放在 `Start()` **最前面**，在任何依赖 `ScoreManager` 的逻辑之前；`RefreshLeaderboard()` 由 `Update()` 每 0.5s 调用，只要 `Start()` 结束前解析即可。
- **VALIDATE**: `grep -n "FindFirstObjectByType<ScoreManager>" Assets/Scripts/UI/LeaderboardPanel.cs` 返回一行。

### Task 2: EventPanel 防御性 auto-wire
- **ACTION**: 在 `Assets/Scripts/UI/EventPanel.cs` 的 `Start()` 里，`BuildEventRows();` 调用**之前**插入兜底解析。
- **IMPLEMENT**:
  ```csharp
  // Defensive auto-wire: SceneWiring/TrackSetupEditor do not re-assign this on an
  // already-existing panel, so a scene can ship with EventManager unset — which leaves the
  // panel visible but empty (BuildEventRows early-returns on null).
  if (EventManager == null)
      EventManager = FindFirstObjectByType<EventManager>(FindObjectsInactive.Include);
  ```
- **MIRROR**: DEFENSIVE_AUTOWIRE
- **IMPORTS**: 无新增
- **GOTCHA**: 必须在 `BuildEventRows()` 之前，因为该方法在 `EventManager == null` 时提前返回。注意 `OnEnable()` 也用 `EventManager` 订阅事件；`OnEnable` 在 `Start` 之前触发，此处不改 `OnEnable`（若 manager 在 OnEnable 时仍 null，订阅会跳过 — 与 commit a103209 原始行为一致，属可接受，不扩大 scope）。
- **VALIDATE**: `grep -n "FindFirstObjectByType<EventManager>" Assets/Scripts/UI/EventPanel.cs` 返回一行。

### Task 3: SceneWiring 编辑器菜单永久接线
- **ACTION**: 在 `Assets/Scripts/Editor/SceneWiring.cs` 的 `WireAll()` 中，RaceUI 接线块结束（第 127 行 `EditorUtility.SetDirty(raceUI);` 与第 128 行 `}` 之后）插入 panel manager 接线块。`leaderboard`、`eventPanel`、`scoreManager`、`eventManager` 局部变量均已在方法顶部解析（第 36/39/50/51 行）。
- **IMPLEMENT**:
  ```csharp
  // ═══════════════════════════════════════════════════════
  // WIRE: HUD panel data sources
  // The panels are visible via RaceUI, but stay empty unless their own manager
  // reference is set — WireAll previously wired RaceUI.Leaderboard/Events but not
  // the ScoreManager/EventManager the panels read from.
  // ═══════════════════════════════════════════════════════
  if (leaderboard != null)
  {
      Wire(ref leaderboard.ScoreManager, scoreManager, "LeaderboardPanel.ScoreManager");
      EditorUtility.SetDirty(leaderboard);
  }
  if (eventPanel != null)
  {
      Wire(ref eventPanel.EventManager, eventManager, "EventPanel.EventManager");
      EditorUtility.SetDirty(eventPanel);
  }
  ```
- **MIRROR**: EDITOR_WIRE_PATTERN
- **IMPORTS**: 无新增（`Wire<T>`、`EditorUtility` 已在文件内使用）
- **GOTCHA**: `Wire<T>` 幂等 — 若面板 manager 已在 Inspector 里手动接线则跳过，不会覆盖。插入位置必须在 `WireAll()` 内、`leaderboard`/`eventPanel` 声明之后。
- **VALIDATE**: `grep -n "leaderboard.ScoreManager\|eventPanel.EventManager" Assets/Scripts/Editor/SceneWiring.cs` 返回两行。

### Task 4（可选 / ADVISORY）: 回归测试
- **ACTION**: 创建 `Assets/Tests/EditMode/HudPanelAutoWireTests.cs`，验证「当 manager 存在于场景时，面板能解析到它」。
- **IMPLEMENT**: 用 EditMode 测试构造一个含 `ScoreManager` 的 GameObject 与一个 `LeaderboardPanel`（`ScoreManager` 字段留空），手动调用一个可测试的解析入口后断言字段非 null。
  - **注意**：`Start()` 为 private 且 EditMode 下不自动触发。两种可行做法（择一，保持最小改动）：
    1. 测试里用 `FindFirstObjectByType<ScoreManager>(FindObjectsInactive.Include)` 复现同一解析逻辑并断言它能找到对象（验证 API 契约层面的兜底可行）；
    2. 或将兜底解析抽成 internal 方法 `ResolveScoreManager()` 供测试直接调用（需 `Tests.EditMode` asmdef 已引用 `EDIRacing.Runtime` ✓，并对 Runtime 程序集加 `[assembly: InternalsVisibleTo("Tests.EditMode")]`）。**默认用做法 1，避免改生产可见性。**
- **MIRROR**: TEST_STRUCTURE
- **IMPORTS**: `using NUnit.Framework; using UnityEngine;`
- **GOTCHA**: 每个测试 `[TearDown]` 用 `Object.DestroyImmediate` 清理创建的 GameObject，避免测试间场景对象泄漏污染 `FindFirstObjectByType`（违反 coding-standards 的 Isolation 规则）。
- **VALIDATE**: 测试在 EditMode 运行通过（见下方验证命令）。
- **SCOPE 注记**: 面板属 UI/Visual 类型（ADVISORY 门槛，非 BLOCKING）。若时间紧，可跳过 Task 4，用 Manual Validation 中的场景走查替代 — 但补测试能消除 codegraph 标记的「no covering tests」缺口，推荐做。

---

## Testing Strategy

### Unit Tests（Task 4）
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `ScoreManager 存在时可被解析` | 场景含 1 个 ScoreManager，面板字段为 null | `FindFirstObjectByType<ScoreManager>` 返回非 null | 否 |
| `EventManager 存在时可被解析` | 场景含 1 个 EventManager | 返回非 null | 否 |
| `未激活对象也能解析` | ScoreManager 挂在 inactive GameObject | `FindObjectsInactive.Include` 仍返回非 null | 是 |
| `无 manager 时安全返回 null` | 场景无 ScoreManager | 返回 null，面板不抛异常（RefreshLeaderboard 提前返回） | 是 |

### Edge Cases Checklist
- [x] 空场景无 manager → 面板不崩溃（既有 null 守卫已覆盖）
- [x] manager 挂在未激活对象 → `FindObjectsInactive.Include` 覆盖
- [x] manager 已手动接线 → `Start()` 的 `if (== null)` 与 `Wire<T>` 幂等均不覆盖
- [ ] 场景中存在多个 ScoreManager（超出本项目单例假设，不处理 — `FindFirstObjectByType` 返回首个）

---

## Validation Commands

### 落地前只读校验（确认仍为欠账）
```bash
grep -n "FindFirstObjectByType<ScoreManager>" Assets/Scripts/UI/LeaderboardPanel.cs || echo "仍缺失 → 需落地"
git format-patch -1 a103209 --stdout -- Assets/Scripts/UI/LeaderboardPanel.cs Assets/Scripts/UI/EventPanel.cs Assets/Scripts/Editor/SceneWiring.cs | git apply --check
```
EXPECT: 修复缺失、patch 可干净应用

### 落地后静态校验
```bash
grep -n "FindFirstObjectByType<ScoreManager>" Assets/Scripts/UI/LeaderboardPanel.cs
grep -n "FindFirstObjectByType<EventManager>" Assets/Scripts/UI/EventPanel.cs
grep -n "leaderboard.ScoreManager\|eventPanel.EventManager" Assets/Scripts/Editor/SceneWiring.cs
```
EXPECT: 三处均命中（1/1/2 行）

### 编译 + EditMode 测试（CI 等价）
```bash
# 本地/CI：game-ci/unity-test-runner@v4（.github/workflows/test.yml）
# 或 Unity 编辑器菜单：Window > General > Test Runner > EditMode > Run All
```
EXPECT: 编译零错误；EditMode 全部通过（含新增 HudPanelAutoWireTests）

### Manual Validation（Visual/UI — ADVISORY）
- [ ] 打开 `Assets/Scenes/complete_track_demo.unity`
- [ ] 菜单 `EDI Racing > Wire All References`，Console 出现 `[WireAll] LeaderboardPanel.ScoreManager` 与 `EventPanel.EventManager` 接线日志、无 Warning
- [ ] Play：排行榜面板显示排名行（0.5s 刷新），事件面板显示事件按钮行
- [ ] 从 Inspector 手动清空面板的 manager 字段再 Play：`Start()` 兜底重新解析，面板仍有内容（验证防御性 auto-wire）

---

## Acceptance Criteria
- [ ] commit a103209 的 3 处代码改动已落地到 main（cherry-pick 或等价手改）
- [ ] 三条静态 grep 校验全部命中
- [ ] Unity 编译零错误
- [ ] EditMode 测试通过（若做 Task 4）
- [ ] 场景走查：排行榜与事件面板显示内容（非空白）
- [ ] 分支 `worktree-raceui-hud-autowire-camera-hint` 落地后可删除（欠账清零）

## Completion Checklist
- [ ] 代码遵循 DEFENSIVE_AUTOWIRE / EDITOR_WIRE_PATTERN 模式
- [ ] 注释密度与 commit a103209 原文一致（每处兜底带 3 行 why 注释）
- [ ] 命名遵循 PascalCase public 字段、无 namespace 全局类
- [ ] 测试遵循 EventManagerTests 的 [TestFixture]/[SetUp]/[TearDown] 范式
- [ ] 无硬编码值
- [ ] 未引入分支陈旧噪音（只取 a103209）
- [ ] 自包含 — 实现期无需再搜代码库

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 分支陈旧噪音被误一起合入 | 中 | 高 | 用 `git cherry-pick a103209` 或 `git format-patch -1 a103209` 只取该 commit，**禁止** merge 整个分支 |
| `Start()` 兜底找错 manager（多实例） | 低 | 中 | 项目单例假设成立；`FindFirstObjectByType` 返回首个即可 |
| EditMode 测试因场景对象泄漏而不稳定 | 中 | 低 | `[TearDown]` 严格 `DestroyImmediate`，遵守 Isolation |
| 落地后旧场景 .unity 里 manager 仍 null | 低 | 低 | 运行时 `Start()` 兜底覆盖；再跑一次 `Wire All References` 永久写入场景 |

## Notes
- 该 commit 作者与本项目 git user 同一人（jadyn.hwu@gmail.com），Co-Authored-By 记录了 Claude Opus 4.8。
- PR #66 已从同一分支合并（相机提示 + 面板引用恢复），a103209 是 PR #66 合并**之后**追加的后续修复，因此漏合 —— 属典型「follow-up commit 未跟进」欠账。
- 落地建议走独立 worktree/分支 + draft PR（本仓库惯例，见近期 PR #66/#67/#68），PR 描述引用 `Story: in-game HUD visibility regression`。
- 落地并合并后，`git branch -D worktree-raceui-hud-autowire-camera-hint` + 删除远端分支，清零欠账。
