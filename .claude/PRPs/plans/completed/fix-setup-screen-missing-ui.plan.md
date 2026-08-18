# Plan: Fix SetupScreen Missing UI Elements

## Summary
SetupScreen 组件在 `complete_track_demo.unity` 场景中有 12 个字段为空引用（`{fileID: 0}`），导致 Host Room 按钮等核心 UI 不可见。需要修改 `TrackSetupEditor.cs`，使其在检测到已有 SetupScreen 时也能补全缺失的子 UI 对象并绑定引用。

## User Story
As a professor,
I want to see the Host Room button and all network/survey UI on the setup screen,
So that I can host a room, manage surveys, and run races with students.

## Problem → Solution
SetupScreen 存在但 12 个 UI 子对象未创建/绑定 → Editor 工具补全缺失 UI 并自动绑定引用

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A
- **PRD Phase**: N/A
- **Estimated Files**: 1 (TrackSetupEditor.cs)

---

## Missing Items Inventory

| # | Field | Type | Exists in Scene? | Impact |
|---|---|---|---|---|
| 1 | HostButton | Button | NO | **Critical** — cannot host room |
| 2 | RoomCodeText | Text | NO | **Critical** — cannot show room code |
| 3 | StudentCountText | Text | NO | High — cannot show student count |
| 4 | PushConfigButton | Button | NO | High — cannot push config to web |
| 5 | WebResponseCountText | Text | NO | Medium — cannot show web responses |
| 6 | NewSurveyButton | Button | NO | Medium — cannot create survey |
| 7 | LoadConfigButton | Button | NO | Medium — cannot load config |
| 8 | TemplateButton | Button | NO | Medium — cannot load template |
| 9 | StartWithSurveyButton | Button | NO | Medium — cannot start with survey |
| 10 | ActiveConfigText | Text | NO | Low — cannot show active config name |
| 11 | BuilderPanel | SurveyBuilderPanel | NO | Medium — no survey editor |
| 12 | ConfigPanel | ConfigManagerPanel | NO | Medium — no config manager |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Editor/TrackSetupEditor.cs` | 663-711 | WireOrCreateSetupScreen — current logic with early return |
| P0 | `Assets/Scripts/Editor/TrackSetupEditor.cs` | 926-1001 | CreateUIPanel/CreateLabel/CreateUIButton helpers |
| P0 | `Assets/Scripts/UI/SetupScreen.cs` | 1-125 | All public fields and Start() visibility logic |
| P1 | `Assets/Scenes/complete_track_demo.unity` | 3648-3674 | Current SetupScreen serialized state |

---

## Patterns to Mirror

### UI_BUTTON_CREATION
```csharp
// SOURCE: TrackSetupEditor.cs:697-699
setup.HostButton = CreateUIButton(panel.transform, "HostBtn", "Host Room",
    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
    new Vector2(-150, -30), new Vector2(-10, 5));
```

### UI_LABEL_CREATION
```csharp
// SOURCE: TrackSetupEditor.cs:702-704
setup.RoomCodeText = CreateLabel(panel.transform, "RoomCodeText", "", 16, TextAnchor.MiddleCenter,
    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
    new Vector2(0, -30), new Vector2(150, 5));
```

### FIND_OR_CREATE_PATTERN
```csharp
// SOURCE: TrackSetupEditor.cs:665-666 (current — BROKEN for existing screens)
var existing = Object.FindFirstObjectByType<SetupScreen>();
if (existing != null) return existing;  // skips wiring missing fields
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Editor/TrackSetupEditor.cs` | UPDATE | Fix WireOrCreateSetupScreen to patch missing UI on existing SetupScreen |

## NOT Building
- SurveyBuilderPanel / ConfigManagerPanel components (these are separate systems; we only create placeholder GameObjects if the components don't exist in scene)
- Any runtime behavior changes
- Any new scripts

---

## Step-by-Step Tasks

### Task 1: Refactor WireOrCreateSetupScreen to patch existing SetupScreen

- **ACTION**: Change the early-return logic so that when an existing SetupScreen is found, it still checks each field and creates missing UI children.
- **IMPLEMENT**: Extract the UI creation into a helper method `PatchSetupScreenUI(SetupScreen setup, Transform panel, NetworkManager nm)` that checks each field for null and creates the missing UI object if needed. The existing `WireOrCreateSetupScreen` calls this helper for both new and existing screens.
- **MIRROR**: `UI_BUTTON_CREATION`, `UI_LABEL_CREATION` patterns above
- **GOTCHA**: The existing SetupScreen's transform IS the panel. When creating from scratch, `panel` is a new GameObject; for existing, use `setup.transform`.
- **GOTCHA**: After patching, must call `EditorUtility.SetDirty(setup)` and mark the scene dirty so Unity serializes the new references.
- **VALIDATE**: Open Unity Editor → EDI Racing → Setup Track → run the tool → verify SetupScreen now has all fields populated in Inspector

#### Implementation Detail

Replace lines 663-711 with:

```csharp
private SetupScreen WireOrCreateSetupScreen(Transform canvasRoot, RaceManager rm, NetworkManager nm)
{
    var existing = Object.FindFirstObjectByType<SetupScreen>();
    if (existing != null)
    {
        PatchSetupScreenUI(existing, existing.transform, nm);
        return existing;
    }

    var panel = CreateUIPanel(canvasRoot, "SetupScreen",
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
        new Vector2(-200, -120), new Vector2(200, 120));

    var setup = panel.AddComponent<SetupScreen>();
    setup.RaceManager = rm;
    setup.NetworkManager = nm;

    // Title
    CreateLabel(panel.transform, "Title", "EDI Racing Setup", 24, TextAnchor.UpperCenter,
        new Vector2(0, 1), new Vector2(1, 1),
        new Vector2(10, -50), new Vector2(-10, -10));

    PatchSetupScreenUI(setup, panel.transform, nm);
    return setup;
}

private void PatchSetupScreenUI(SetupScreen setup, Transform panel, NetworkManager nm)
{
    if (setup.NetworkManager == null)
        setup.NetworkManager = nm;

    if (setup.InfoText == null)
        setup.InfoText = CreateLabel(panel, "InfoText", "Ready to start race.", 16, TextAnchor.MiddleCenter,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f),
            new Vector2(10, -10), new Vector2(-10, 10));

    if (setup.StartDefaultButton == null)
        setup.StartDefaultButton = CreateUIButton(panel, "StartDefaultBtn", "Start Race (Default CSV)",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-150, 60), new Vector2(150, 95));

    if (setup.LoadSessionButton == null)
        setup.LoadSessionButton = CreateUIButton(panel, "LoadSessionBtn", "Load Session",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-150, 15), new Vector2(150, 50));

    // --- Network UI ---
    if (setup.HostButton == null)
        setup.HostButton = CreateUIButton(panel, "HostBtn", "Host Room",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-150, -30), new Vector2(-10, 5));

    if (setup.RoomCodeText == null)
        setup.RoomCodeText = CreateLabel(panel, "RoomCodeText", "", 16, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, -30), new Vector2(150, 5));

    if (setup.StudentCountText == null)
        setup.StudentCountText = CreateLabel(panel, "StudentCountText", "", 14, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-150, -60), new Vector2(150, -35));

    // --- Survey Builder UI ---
    if (setup.NewSurveyButton == null)
        setup.NewSurveyButton = CreateUIButton(panel, "NewSurveyBtn", "New Survey",
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(10, -100), new Vector2(110, -70));

    if (setup.LoadConfigButton == null)
        setup.LoadConfigButton = CreateUIButton(panel, "LoadConfigBtn", "Load Config",
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(120, -100), new Vector2(220, -70));

    if (setup.TemplateButton == null)
        setup.TemplateButton = CreateUIButton(panel, "TemplateBtn", "Templates",
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(230, -100), new Vector2(330, -70));

    if (setup.StartWithSurveyButton == null)
        setup.StartWithSurveyButton = CreateUIButton(panel, "StartWithSurveyBtn", "Start (Survey Config)",
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(10, -140), new Vector2(200, -110));

    if (setup.ActiveConfigText == null)
        setup.ActiveConfigText = CreateLabel(panel, "ActiveConfigText", "No active config", 14, TextAnchor.MiddleLeft,
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(10, -170), new Vector2(-10, -145));

    // --- Config Sync ---
    if (setup.PushConfigButton == null)
        setup.PushConfigButton = CreateUIButton(panel, "PushConfigBtn", "Push to Web",
            new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-160, -140), new Vector2(-10, -110));

    if (setup.WebResponseCountText == null)
        setup.WebResponseCountText = CreateLabel(panel, "WebResponseCountText", "", 14, TextAnchor.MiddleRight,
            new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-200, -170), new Vector2(-10, -145));

    EditorUtility.SetDirty(setup);
}
```

### Task 2: Verify in Unity Editor

- **ACTION**: Run EDI Racing → Setup Track in Unity Editor
- **VALIDATE**: 
  - Inspector shows all 12 previously-null fields now populated
  - Play Mode shows "Host Room" button
  - Clicking "Host Room" triggers connection attempt (InfoText shows "Connecting...")

---

## Testing Strategy

### Manual Validation
- [ ] Open Unity Editor → EDI Racing → Setup Track → click setup button
- [ ] Check SetupScreen Inspector — all 12 fields should be non-null
- [ ] Enter Play Mode — "Host Room" button visible
- [ ] Click "Host Room" — InfoText changes to "Connecting..."
- [ ] Survey buttons visible when SurveyConfigManager is assigned
- [ ] Run tool a second time — no duplicate UI objects created (idempotent)

### Edge Cases Checklist
- [ ] Running the tool twice doesn't create duplicate buttons (null check guards)
- [ ] Existing wired fields (StartDefaultButton etc.) are not overwritten
- [ ] Scene is marked dirty after patching (changes are saveable)

---

## Acceptance Criteria
- [ ] All 12 missing fields are created and bound
- [ ] Host Room button visible and functional in Play Mode
- [ ] Editor tool is idempotent (safe to run multiple times)
- [ ] No existing functionality broken

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Button layout overlaps with existing UI | Medium | Low | Positions match TrackSetupEditor's existing layout constants |
| SetupScreen panel too small for all buttons | Medium | Medium | May need to expand panel size; verify in Editor |

## Notes
- BuilderPanel and ConfigPanel require their respective MonoBehaviour components (SurveyBuilderPanel, ConfigManagerPanel) which are separate systems not present in the scene. The plan creates the UI buttons that reference them, but these panels remain null until those systems are added to the scene independently.
- The panel might need resizing from the current 400x240 to accommodate the new survey/config rows. This can be adjusted after visual inspection.
