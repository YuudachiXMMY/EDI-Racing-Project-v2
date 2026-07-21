# Plan: Unity Survey Dead Code Cleanup (PRD Phase 6)

## Summary
移除 Unity 端已被 Web App 完全替代的旧 survey UI/收集代码（SurveyBuilderPanel、StudentSurveyPanel、SurveyCollector 及辅助类），清理所有引用文件。保留 SurveyConfigManager 和数据模型层。

## User Story
As a developer maintaining the EDI Racing project,
I want to remove the obsolete Unity survey UI code,
So that the codebase is clean and only the Web App handles survey workflows.

## Problem -> Solution
Unity 中 ~1800 行 survey UI/收集代码已被 Web App 完全替代 -> 删除 9 个 dead code 文件，清理 6 个引用文件

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-survey-web-app.prd.md`
- **PRD Phase**: Phase 6 - Unity 端清理
- **Estimated Files**: 9 deleted + 6 modified = 15 files

---

## UX Design

### Before
Unity 中存在两套 survey 系统：旧的 in-Unity survey builder/student panel/collector + 新的 Web App 导入路径。代码冗余、维护混乱。

### After
Unity 仅保留 Web App 数据导入路径（JSON Import + Config Sync），所有 survey 创建/答题在 Web App 完成。

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| SetupScreen | 有 New Survey/Load Config/Templates/Distribute Survey 按钮 | 仅保留 Import JSON + Config Sync 按钮 | 减少 UI 复杂度 |
| NetworkSync | 处理 4 种 survey 消息类型 | 不处理 survey 消息 | Web App 处理 |
| RuntimeSetup | 自动创建 SurveyBuilderPanel | 不创建 survey builder | Web App 替代 |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/UI/SetupScreen.cs` | all | 最复杂的清理目标，需保留 import/sync 功能 |
| P0 | `Assets/Scripts/Network/NetworkSync.cs` | 17-19, 257-271 | Survey 字段和消息处理 |
| P0 | `Assets/Scripts/Editor/SceneWiring.cs` | 44-78, 140-222 | Survey 组件创建和布线 |
| P1 | `Assets/Scripts/RuntimeSetup.cs` | 147, 374-462 | Survey builder 自动创建 |
| P1 | `Assets/Scripts/Editor/TrackSetupEditor.cs` | 481-494 | Survey 对象创建 |
| P1 | `Assets/Scripts/UI/RaceUI.cs` | 24, 84-86 | StudentSurvey 字段 |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | 205-244 | 4 个 survey 消息类 |
| P2 | `Assets/Scripts/Data/SurveyConfigManager.cs` | all | 确认保留内容 |

---

## Patterns to Mirror

### DELETION_PATTERN
// This is a pure deletion task. No new code patterns needed.
// Follow Unity convention: delete .cs file + .meta file pairs.

### FIELD_REMOVAL
// SOURCE: SetupScreen.cs — existing field organization
// Remove field declarations, their listeners in Start(), and any methods that only serve those fields.
// Keep the [Header] grouping clean — remove entire [Header] blocks if all fields under it are removed.

---

## Files to Change

### Files to DELETE (9 files + 9 .meta files)

| File | Action | Lines | Justification |
|---|---|---|---|
| `Assets/Scripts/UI/SurveyBuilderPanel.cs` | DELETE | 502 | Survey builder UI - Web App replacement |
| `Assets/Scripts/UI/StudentSurveyPanel.cs` | DELETE | 495 | Student survey panel - Web App replacement |
| `Assets/Scripts/Network/SurveyCollector.cs` | DELETE | 163 | Survey distribution - Web App replacement |
| `Assets/Scripts/UI/ConfigManagerPanel.cs` | DELETE | 167 | Config panel - only used by builder |
| `Assets/Scripts/UI/QuestionEditorRow.cs` | DELETE | 192 | Helper - only used by SurveyBuilderPanel |
| `Assets/Scripts/UI/MappingEditorRow.cs` | DELETE | ~150 | Helper - only used by SurveyBuilderPanel |
| `Assets/Scripts/UI/RuleEditorRow.cs` | DELETE | ~120 | Helper - only used by SurveyBuilderPanel |
| `Assets/Scripts/UI/TabButton.cs` | DELETE | ~50 | Helper - only used by SurveyBuilderPanel |
| `Assets/Scripts/UI/BuilderUIFactory.cs` | DELETE | ~200 | UI factory - all consumers being deleted |

### Files to MODIFY (6 files)

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Remove survey fields + message handling |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Remove builder/collector refs, keep import/sync |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | Remove StudentSurvey field |
| `Assets/Scripts/RuntimeSetup.cs` | UPDATE | Remove BuildSurveyBuilder method |
| `Assets/Scripts/Editor/SceneWiring.cs` | UPDATE | Remove survey component wiring |
| `Assets/Scripts/Editor/TrackSetupEditor.cs` | UPDATE | Remove survey component creation |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Remove 4 survey message classes |

## NOT Building
- New replacement UI
- Migration scripts or compatibility shims
- Additional tests
- Changes to web-app/ directory

---

## Step-by-Step Tasks

### Task 1: Delete 9 survey UI/helper files
- **ACTION**: Delete files and their .meta counterparts
- **IMPLEMENT**: `rm` the 9 .cs files and 9 .meta files listed above
- **MIRROR**: Standard Unity file deletion pattern
- **IMPORTS**: N/A
- **GOTCHA**: Must delete .meta files too or Unity will show warnings
- **VALIDATE**: `ls` confirms files are gone; `grep` returns zero matches for class names

### Task 2: Clean up NetworkSync.cs
- **ACTION**: Remove survey fields and message case blocks
- **IMPLEMENT**:
  - Remove `[Header("Survey (Optional)")]` + `SurveyCollector` + `StudentSurveyPanel` fields (lines 17-19)
  - Remove 4 case blocks: `survey_questions`, `survey_response`, `survey_closed`, `survey_ack` (lines 257-271)
- **MIRROR**: Keep existing switch/case structure clean
- **IMPORTS**: No import changes needed
- **GOTCHA**: Keep the `break;` for surrounding cases intact
- **VALIDATE**: No references to SurveyCollector or StudentSurveyPanel remain

### Task 3: Clean up SetupScreen.cs
- **ACTION**: Remove survey builder/collector fields, buttons, and methods
- **IMPLEMENT**:
  - Remove fields: `BuilderPanel`, `ConfigPanel`, `NewSurveyButton`, `LoadConfigButton`, `TemplateButton`, `StartWithSurveyButton`, `ActiveConfigText`
  - Remove fields: `SurveyCollector`, `DistributeSurveyButton`, `StartWithResponsesButton`, `ResponseCountText`
  - Remove doc comment referencing SurveyBuilderPanel (line 7)
  - Remove button listeners for removed buttons in `Start()`
  - Remove survey UI visibility code
  - Remove methods: `OpenNewSurvey`, `OpenLoadConfig`, `OpenTemplates`, `StartWithSurveyConfig`
  - Remove methods: `OnDistributeSurvey`, `OnSurveyResponseReceived`, `OnStartWithResponses`
  - Remove `canDistribute` logic in `OnRoomCreated` and `OnNetworkMessage`
  - **KEEP**: `SurveyConfigManager` field, `RefreshActiveConfigDisplay`, Import JSON, Config sync
- **MIRROR**: Keep existing field/method organization
- **IMPORTS**: No import changes
- **GOTCHA**: `RefreshActiveConfigDisplay` must stay — called by config_import handler. `SurveyConfigManager` must stay — used for ApplyRulesToSchedule and ActiveConfig.
- **VALIDATE**: Import JSON and Config sync paths still work; no references to deleted types

### Task 4: Clean up RaceUI.cs
- **ACTION**: Remove StudentSurvey field and usage
- **IMPLEMENT**:
  - Remove `public StudentSurveyPanel StudentSurvey;` (line 24)
  - Remove `if (isRacing && StudentSurvey != null) StudentSurvey.gameObject.SetActive(false);` (lines 84-86)
- **VALIDATE**: Compiles cleanly

### Task 5: Clean up RuntimeSetup.cs
- **ACTION**: Remove survey builder creation
- **IMPLEMENT**:
  - Remove `BuildSurveyBuilder(canvasObj.transform);` (line 147)
  - Remove `BuildSurveyBuilder` method (lines 376-412)
  - Remove `BuildSetupSurveyButtons` method (lines 414-462)
- **VALIDATE**: Compiles, UI setup still works without survey builder

### Task 6: Clean up SceneWiring.cs
- **ACTION**: Remove all survey component find/create/wire logic
- **IMPLEMENT**:
  - Remove `FindOrCreate<SurveyCollector>(...)` (lines 45-46)
  - Remove `builderPanel` find (line 55)
  - Remove `configPanel` find (line 56)
  - Remove `StudentSurveyPanel` find/create block (lines 58-80)
  - Remove `NetworkSync.SurveyCollector` and `NetworkSync.StudentSurveyPanel` wiring (lines 140-141)
  - Remove entire `WIRE: SurveyCollector` section (lines 146-153)
  - Remove entire `WIRE: StudentSurveyPanel` section (lines 156-162)
  - Remove `Wire(ref raceUI.StudentSurvey, ...)` (line 176)
  - Remove `Wire(ref setupScreen.BuilderPanel, ...)` (line 188)
  - Remove `Wire(ref setupScreen.ConfigPanel, ...)` (line 189)
  - Remove `Wire(ref setupScreen.SurveyCollector, ...)` (line 190)
  - Remove survey distribution UI creation (lines 192-222)
- **GOTCHA**: Keep `configManager` (`SurveyConfigManager`) — it's still wired to RaceManager and SetupScreen
- **VALIDATE**: Compiles, remaining wiring is correct

### Task 7: Clean up TrackSetupEditor.cs
- **ACTION**: Simplify survey object creation
- **IMPLEMENT**:
  - Keep `SurveyConfigManager` creation on RaceManager
  - Remove `SurveyCollector` and `StudentSurveyPanel` creation (lines 484, 486)
  - Remove their wiring lines (lines 488-492)
  - Keep `raceManager.SurveyConfigManager = surveyConfigManager` (line 493)
- **VALIDATE**: Compiles

### Task 8: Clean up NetworkMessages.cs
- **ACTION**: Remove 4 survey message classes
- **IMPLEMENT**: Delete `SurveyQuestionsMessage`, `SurveyResponseMessage`, `SurveyClosedMessage`, `SurveyAckMessage` (lines 205-244)
- **VALIDATE**: Compiles, no remaining references

### Task 9: Update PRD status
- **ACTION**: Update Phase 6 status in PRD
- **IMPLEMENT**: Change `pending` to `complete` in `.claude/PRPs/prds/edi-survey-web-app.prd.md` line 213
- **VALIDATE**: PRD shows no remaining pending phases

---

## Testing Strategy

### Verification Commands

```bash
# 1. Verify no references to deleted classes
grep -r "SurveyBuilderPanel\|StudentSurveyPanel\|SurveyCollector\|ConfigManagerPanel\|QuestionEditorRow\|MappingEditorRow\|RuleEditorRow\|TabButton\|BuilderUIFactory" Assets/Scripts/ --include="*.cs"
# EXPECT: Zero matches

# 2. Verify SurveyConfigManager still referenced
grep -rn "SurveyConfigManager" Assets/Scripts/ --include="*.cs"
# EXPECT: References in RaceManager.cs, SetupScreen.cs, SceneWiring.cs, TrackSetupEditor.cs

# 3. Verify import/sync functionality preserved
grep -rn "ImportJson\|survey_import\|config_import\|ConfigExport" Assets/Scripts/ --include="*.cs"
# EXPECT: References in SetupScreen.cs
```

### Edge Cases Checklist
- [x] Scene file may have serialized refs to deleted components -> Missing Script warnings, auto-clean on save
- [x] .meta files deleted alongside .cs files -> no orphan metas
- [x] SurveyConfigManager retained -> session loading still works
- [x] Web App import path preserved -> JSON import still works

---

## Acceptance Criteria
- [ ] 9 .cs files + 9 .meta files deleted (~1800+ lines removed)
- [ ] 6 .cs files cleaned of dead survey references
- [ ] NetworkMessages.cs: 4 survey message classes removed
- [ ] `grep` for deleted class names returns zero matches in .cs files
- [ ] All 5 existing EditMode tests pass
- [ ] Web App JSON import path preserved in SetupScreen
- [ ] Config sync path preserved in SetupScreen
- [ ] SurveyConfigManager + ApplyRulesToSchedule preserved
- [ ] PRD Phase 6 status updated to `complete`

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scene file serialized refs | HIGH | LOW | Missing Script warnings self-heal on save |
| Missed reference in .cs | LOW | MEDIUM | Grep verification + Unity compiler |
| SurveyConfigManager methods orphaned | LOW | LOW | Methods still support config import & session loading |

## Notes
- Total deletion: ~1800 lines of dead code across 9 files
- `BuilderUIFactory.cs` added beyond original GAP 1 scope — verified all consumers are being deleted
- `ConfigManagerPanel.cs` added — only served the old in-Unity survey builder workflow
- `CreateImportUI.cs` does NOT call BuilderUIFactory despite a pattern reference comment — kept
- 4 survey network message types removed — Web App handles all survey network communication
