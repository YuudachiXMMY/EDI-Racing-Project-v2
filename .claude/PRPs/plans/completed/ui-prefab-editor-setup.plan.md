# Plan: Editor-Configured UI Prefab System (Phase 4 Option B)

## Summary
Replace the procedural `RuntimeSetup.cs` bootstrapper with proper Editor-configured UI Prefabs. Create a Canvas prefab hierarchy containing `RaceUI`, `SetupScreen`, `LeaderboardPanel`, `EventPanel`, and `RaceControlPanel` with all Inspector references pre-wired. Create sub-prefabs for `LeaderboardRow` and `EventRow`. Set up camera system objects in the scene. Once complete, `RuntimeSetup.cs` becomes unnecessary and can be disabled.

## User Story
As a developer, I want the UI to be configured via proper Unity prefabs and scene references, so that I can iterate on layout/styling in the Editor without touching code, and the runtime bootstrap hack can be removed.

## Problem → Solution
All UI is created procedurally at runtime by `RuntimeSetup.cs` (432 lines of layout code) → Proper Canvas prefab with Inspector-wired panel components, enabling visual editing, prefab variants, and standard Unity workflow.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 4 — UI & Camera (Option B upgrade)
- **Estimated Files**: 3 new prefabs + 2 scene modifications + 1 script update

---

## UX Design

### Before
```
┌──────────────────────────────────────────────────────┐
│ RuntimeSetup.cs creates all UI at Awake()            │
│ - No prefabs exist (Assets/Prefabs/UI/ is empty)     │
│ - Layout hardcoded as RectTransform offsets in C#     │
│ - Cannot preview UI in Scene View                    │
│ - Two parallel UI systems coexist (guards prevent    │
│   double-instantiation, but code is confusing)       │
│ - Camera/FixedPoints also created procedurally       │
└──────────────────────────────────────────────────────┘
```

### After
```
┌──────────────────────────────────────────────────────┐
│ Scene contains:                                      │
│                                                      │
│ ├─ RaceCanvas (prefab)                               │
│ │  ├─ RaceUI component [wired to all panels]         │
│ │  ├─ SetupScreen (active in Setup state)            │
│ │  │  ├─ InfoText                                    │
│ │  │  ├─ StartDefaultButton                          │
│ │  │  └─ LoadSessionButton                           │
│ │  ├─ LeaderboardPanel (active in Racing/Paused)     │
│ │  │  ├─ Title                                       │
│ │  │  └─ Content (VerticalLayoutGroup)               │
│ │  │     └─ [RowPrefab instances pooled at runtime]  │
│ │  ├─ EventPanel (Professor only)                    │
│ │  │  ├─ Title                                       │
│ │  │  └─ Content (VerticalLayoutGroup)               │
│ │  │     └─ [EventRowPrefab instances at runtime]    │
│ │  └─ RaceControlPanel (Professor only)              │
│ │     ├─ PauseResumeButton + Label                   │
│ │     ├─ SaveButton                                  │
│ │     ├─ ExportButton                                │
│ │     └─ StatusText                                  │
│ ├─ CameraManager (scene object)                      │
│ │  └─ Wired to Camera.main components                │
│ ├─ CarLabelSpawner (scene object)                    │
│ └─ FixedCam_F1..F9 (scene objects along track)       │
│                                                      │
│ Assets/Prefabs/UI/                                   │
│ ├─ LeaderboardRow.prefab  (Text component)           │
│ └─ EventRow.prefab  (Button + Text)                  │
│                                                      │
│ RuntimeSetup.cs: disabled (component unchecked)      │
└──────────────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| UI Layout | Hardcoded in RuntimeSetup.cs C# | Editable in Scene View / Inspector | Standard Unity workflow |
| Prefab iteration | Must edit code, recompile | Drag and tweak in Inspector | Instant feedback |
| Panel visibility | Two parallel systems with guards | Single `RaceUI` component manages all | Simpler architecture |
| Camera setup | Created procedurally in code | Pre-placed scene objects | Can adjust positions visually |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/UI/RaceUI.cs` | all (70) | Top-level controller — Canvas must match its SerializeField refs |
| P0 (critical) | `Assets/Scripts/UI/SetupScreen.cs` | all (65) | Needs Button + Text refs wired |
| P0 (critical) | `Assets/Scripts/UI/LeaderboardPanel.cs` | all (82) | Needs ContentParent + RowPrefab refs |
| P0 (critical) | `Assets/Scripts/UI/EventPanel.cs` | all (95) | Needs ContentParent + EventRowPrefab refs |
| P0 (critical) | `Assets/Scripts/UI/RaceControlPanel.cs` | all (94) | Needs 3 Button + 2 Text refs wired |
| P0 (critical) | `Assets/Scripts/RuntimeSetup.cs` | all (432) | Reference for layout values, to be disabled |
| P1 (important) | `Assets/Scripts/Camera/CameraManager.cs` | all (88) | Scene object needs FreeCamera + SpectatorCam refs |
| P1 (important) | `Assets/Scripts/UI/CarLabelSpawner.cs` | all (101) | Scene object needs RaceManager ref |
| P1 (important) | `Assets/Scripts/Race/RaceManager.cs` | 44-53 | Auto-start logic checks for SetupScreen presence |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| None needed | — | All techniques are standard Unity UGUI; no external research required |

---

## Patterns to Mirror

### MONOBEHAVIOUR_COMPONENT
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:11-14
public class RaceManager : MonoBehaviour
{
    [Header("References")]
    public CarSpawner CarSpawner;
```

### UI_PANEL_REFS
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:15-21
[Header("References")]
public RaceManager RaceManager;
public CameraManager CameraManager;

[Header("Panels")]
public LeaderboardPanel Leaderboard;
public EventPanel Events;
public RaceControlPanel Controls;
public SetupScreen Setup;
```

### LEADERBOARD_ROW_PREFAB
```csharp
// SOURCE: Assets/Scripts/UI/LeaderboardPanel.cs:18-19
[Tooltip("Prefab for a single leaderboard row (Text component required)")]
public GameObject RowPrefab;
```
**Key**: RowPrefab is a `GameObject` with a `Text` component. Instantiated under `ContentParent` and pooled.

### EVENT_ROW_PREFAB
```csharp
// SOURCE: Assets/Scripts/UI/EventPanel.cs:17-18
[Tooltip("Prefab for a single event row (Button + Text required)")]
public GameObject EventRowPrefab;
```
**Key**: EventRowPrefab needs `GetComponentInChildren<Button>()` and `GetComponentInChildren<Text>()` to both return valid results.

### RUNTIME_SETUP_LAYOUT_VALUES
```csharp
// SOURCE: Assets/Scripts/RuntimeSetup.cs:136-139
screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
screenCanvas.sortingOrder = 50;
scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1920, 1080);
```

### RUNTIME_SETUP_PANEL_ANCHORS
```
// SOURCE: Assets/Scripts/RuntimeSetup.cs
// Leaderboard: anchor (0,1)-(0,1), offset (10,-10)-(280,400)  — top-left, 280x400
// ControlPanel: anchor (0.5,0)-(0.5,0), offset (-200,10)-(200,50) — bottom-center, 400x50
// EventLogPanel: anchor (1,1)-(1,1), offset (-280,-10)-(-10,200) — top-right, 270x190
// Buttons: dark bg (0.2,0.2,0.2,0.9), highlight (0.35,0.35,0.35), pressed (0.15,0.15,0.15)
// Panel bg: (0,0,0,0.6)
```

### RACEMANAGER_AUTO_START_GUARD
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:48-52
// Auto-start when no SetupScreen is present in the scene
if (FindFirstObjectByType<SetupScreen>() == null && DefaultCsvData != null)
{
    LoadAndStartRace(DefaultCsvData.text);
}
```
**Key**: When a `SetupScreen` exists in the scene, RaceManager does NOT auto-start. The prefab Canvas will contain a SetupScreen, so this guard works correctly — race waits for user to click "Start".

### FIXED_CAMERA_PLACEMENT
```csharp
// SOURCE: Assets/Scripts/RuntimeSetup.cs:99-111
// Fixed cameras placed at waypoint intervals: +12 height, +8 right offset, LookAt waypoint
fpObj.transform.position = wp.position + Vector3.up * 12f + Vector3.right * 8f;
fpObj.transform.LookAt(wp.position);
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Prefabs/UI/LeaderboardRow.prefab` | CREATE via MCP | Sub-prefab: single Text row for leaderboard pooling |
| `Assets/Prefabs/UI/EventRow.prefab` | CREATE via MCP | Sub-prefab: Button + Text for event panel rows |
| `Assets/Scenes/complete_track_demo.unity` | UPDATE via MCP | Add Canvas hierarchy, CameraManager, CarLabelSpawner, FixedCameraPoints to scene; wire all Inspector refs; disable RuntimeSetup |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE (no change) | No code change needed — auto-start guard already works |

## NOT Building

- Main `RaceCanvas.prefab` as a standalone prefab asset (create directly in scene instead — simpler for a single-scene project)
- Custom Editor inspector for RaceUI (standard Inspector is sufficient)
- TextMeshPro migration (existing code uses `UnityEngine.UI.Text`, keep consistent)
- New UI scripts (all 7 scripts already exist and are complete)
- UI styling/theming system (direct color/font values on components)
- RuntimeSetup.cs deletion (disable only — keep as fallback reference)

---

## Step-by-Step Tasks

### Task 1: Create LeaderboardRow Prefab
- **ACTION**: Create a minimal prefab at `Assets/Prefabs/UI/LeaderboardRow.prefab` containing a single `Text` component
- **IMPLEMENT**: Use MCP `manage_prefabs` or `manage_gameobject` + `manage_ui`:
  1. Create GameObject "LeaderboardRow"
  2. Add `Text` component: font=`LegacyRuntime.ttf`, fontSize=16, alignment=MiddleLeft, color=white, supportRichText=true
  3. Set RectTransform: height=25, stretch horizontally (anchorMin 0,0 anchorMax 1,1)
  4. Save as prefab to `Assets/Prefabs/UI/LeaderboardRow.prefab`
- **MIRROR**: LEADERBOARD_ROW_PREFAB — `LeaderboardPanel.cs:63` does `rowPool[i].GetComponent<Text>()`, so Text must be on the root GameObject
- **IMPORTS**: N/A (prefab creation, not code)
- **GOTCHA**: Text must be on the ROOT object, not a child — `GetComponent<Text>()` (not `GetComponentInChildren`) is used in `LeaderboardPanel.RefreshLeaderboard()`
- **VALIDATE**: Prefab exists at `Assets/Prefabs/UI/LeaderboardRow.prefab`; has a `Text` component on root

### Task 2: Create EventRow Prefab
- **ACTION**: Create a prefab at `Assets/Prefabs/UI/EventRow.prefab` containing a Button with a child Text
- **IMPLEMENT**: Use MCP tools:
  1. Create GameObject "EventRow" with `Image` (bg color 0.2,0.2,0.2,0.9) + `Button` component
  2. Set Button colors: normal=(0.2,0.2,0.2,0.9), highlighted=(0.35,0.35,0.35,1), pressed=(0.15,0.15,0.15,1)
  3. Create child "Label" with `Text`: fontSize=16, alignment=MiddleCenter, color=white, `LegacyRuntime.ttf`
  4. Set RectTransform: height=35, stretch horizontally
  5. Save as prefab to `Assets/Prefabs/UI/EventRow.prefab`
- **MIRROR**: EVENT_ROW_PREFAB — `EventPanel.cs:52-53` does `GetComponentInChildren<Button>()` and `GetComponentInChildren<Text>()`, so Button on root and Text on child both work
- **IMPORTS**: N/A
- **GOTCHA**: `GetComponentInChildren` searches self first, then children. Button must be on root (or any ancestor of Text). Text can be on root or child — but if both are on root, `GetComponentInChildren<Text>()` returns the Text on the Button's Image, which is wrong. Put Text on a CHILD object.
- **VALIDATE**: Prefab has Button on root, Text on child; `GetComponentInChildren<Button>()` and `GetComponentInChildren<Text>()` both return valid objects

### Task 3: Create Canvas Hierarchy in Scene
- **ACTION**: Build the complete Canvas hierarchy in `complete_track_demo.unity` with all panels and components
- **IMPLEMENT**: Use MCP `manage_ui` and `manage_gameobject` to create:

  **3a. Root Canvas ("RaceCanvas")**
  - Canvas: renderMode=ScreenSpaceOverlay, sortingOrder=50
  - CanvasScaler: ScaleWithScreenSize, referenceResolution=1920x1080
  - GraphicRaycaster
  - Add `RaceUI` component (do NOT wire refs yet — wire in Task 6)

  **3b. SetupScreen panel**
  - Parent: RaceCanvas
  - RectTransform: stretch full screen (anchor 0,0 to 1,1)
  - Image background: (0, 0, 0, 0.8)
  - Add `SetupScreen` component
  - Children:
    - "InfoText" — Text: fontSize=20, alignment=MiddleCenter, "Ready to start race.", positioned center-top area
    - "StartDefaultButton" — Button + Image(0.2,0.2,0.2,0.9), child "Label" Text: "Start with Default Data", fontSize=18
    - "LoadSessionButton" — Button + Image(0.2,0.2,0.2,0.9), child "Label" Text: "Load Session", fontSize=18
  - Wire `SetupScreen` component refs: StartDefaultButton, LoadSessionButton, InfoText

  **3c. LeaderboardPanel**
  - Parent: RaceCanvas
  - RectTransform: anchor top-left (0,1)-(0,1), pivot (0,1), offsetMin=(10,-400), offsetMax=(290,-10) → 280x390 top-left
  - Image background: (0, 0, 0, 0.6)
  - Add `LeaderboardPanel` component
  - Children:
    - "Title" — Text: "Leaderboard", fontSize=20, bold, color=white, height=30, top-anchored
    - "Content" — empty Transform, stretch below title, add VerticalLayoutGroup (childForceExpandWidth=true, spacing=2)
  - Wire: ContentParent → "Content" transform
  - `gameObject.SetActive(false)` — starts hidden

  **3d. EventPanel**
  - Parent: RaceCanvas
  - RectTransform: anchor top-right (1,1)-(1,1), pivot (1,1), 270x390, offset mirrored from leaderboard
  - Image background: (0, 0, 0, 0.6)
  - Add `EventPanel` component
  - Children:
    - "Title" — Text: "Events", fontSize=20, bold, color=white
    - "Content" — empty Transform, VerticalLayoutGroup (spacing=4)
  - Wire: ContentParent → "Content"
  - `gameObject.SetActive(false)` — starts hidden

  **3e. RaceControlPanel**
  - Parent: RaceCanvas
  - RectTransform: anchor bottom-center (0.5,0)-(0.5,0), pivot (0.5,0), 420x55, offset y=10
  - Image background: (0, 0, 0, 0.6)
  - Add `RaceControlPanel` component
  - HorizontalLayoutGroup (spacing=8, padding=5, childForceExpandWidth=false)
  - Children:
    - "PauseResumeButton" — Button(0.2,0.2,0.2,0.9), width=100, height=40
      - "PauseResumeLabel" — child Text: "Pause", fontSize=16, MiddleCenter
    - "SaveButton" — Button, width=80, height=40
      - child Text: "Save"
    - "ExportButton" — Button, width=80, height=40
      - child Text: "Export"
    - "StatusText" — Text: "", fontSize=14, alignment=MiddleRight, color=white, flexWidth
  - Wire all refs: PauseResumeButton, PauseResumeLabel, SaveButton, ExportButton, StatusText
  - `gameObject.SetActive(false)` — starts hidden

- **MIRROR**: RUNTIME_SETUP_LAYOUT_VALUES, RUNTIME_SETUP_PANEL_ANCHORS
- **IMPORTS**: N/A (scene/Inspector setup only)
- **GOTCHA**: Ensure `EventSystem` + `InputSystemUIInputModule` exist in the scene (RuntimeSetup creates one if missing — we need one permanently). Check via MCP before creating.
- **VALIDATE**: Canvas visible in Scene View; all panels exist; components have no missing refs (yellow warning icons)

### Task 4: Set Up Camera System in Scene
- **ACTION**: Create CameraManager, configure camera components, place FixedCameraPoints in the scene
- **IMPLEMENT**: Use MCP tools:

  **4a. Camera components on Main Camera**
  - Find existing Main Camera in scene
  - Add `RaceCameraController` component if not present (MoveSpeed=20, MinSpeed=5, MaxSpeed=100, LookSensitivity=0.15)
  - Add `SpectatorCamera` component if not present (FollowDistance=15, FollowHeight=8, SmoothTime=0.5, LeaderCheckInterval=3), set enabled=false

  **4b. CameraManager scene object**
  - Create empty GameObject "CameraManager"
  - Add `CameraManager` component
  - Wire: FreeCamera → Main Camera's `RaceCameraController`, SpectatorCam → Main Camera's `SpectatorCamera`

  **4c. Fixed camera points**
  - Create 9 GameObjects "FixedCam_F1" through "FixedCam_F9"
  - Each gets a `FixedCameraPoint` component with PointIndex 0-8
  - Position them along the track waypoints: position = waypoint + (0, 12, 0) + (8, 0, 0), LookAt(waypoint)
  - Exact positions depend on waypoint locations — use MCP `execute_code` to read waypoint positions and calculate

- **MIRROR**: FIXED_CAMERA_PLACEMENT
- **IMPORTS**: N/A
- **GOTCHA**: `CameraManager.Start()` auto-discovers FixedCameraPoints via `FindObjectsByType` if `FixedPoints` array is empty, so explicit wiring is optional. But wiring is preferred for clarity.
- **VALIDATE**: CameraManager has non-null FreeCamera and SpectatorCam; 9 FixedCameraPoints visible in Scene hierarchy

### Task 5: Set Up CarLabelSpawner in Scene
- **ACTION**: Create CarLabelSpawner scene object
- **IMPLEMENT**: Use MCP:
  1. Create empty GameObject "CarLabelSpawner"
  2. Add `CarLabelSpawner` component
  3. Wire: RaceManager → scene's RaceManager object
  4. Set: FontSize=24, LabelHeight=4
- **MIRROR**: `Assets/Scripts/UI/CarLabelSpawner.cs:16-19` SerializeField defaults
- **IMPORTS**: N/A
- **GOTCHA**: `CarLabelSpawner` subscribes in `OnEnable`, so it must be active in the scene from the start (it handles state changes internally)
- **VALIDATE**: CarLabelSpawner component in scene with non-null RaceManager ref

### Task 6: Wire All Cross-References
- **ACTION**: Connect all remaining Inspector references across components
- **IMPLEMENT**: Use MCP to set references:

  **6a. RaceUI (on RaceCanvas)**
  - `RaceManager` → scene RaceManager
  - `CameraManager` → scene CameraManager
  - `Leaderboard` → LeaderboardPanel component
  - `Events` → EventPanel component
  - `Controls` → RaceControlPanel component
  - `Setup` → SetupScreen component

  **6b. SetupScreen**
  - `RaceManager` → scene RaceManager

  **6c. LeaderboardPanel**
  - `ScoreManager` → RaceManager's ScoreManager child/component
  - `RowPrefab` → `Assets/Prefabs/UI/LeaderboardRow.prefab`

  **6d. EventPanel**
  - `EventManager` → RaceManager's EventManager reference
  - `EventRowPrefab` → `Assets/Prefabs/UI/EventRow.prefab`

  **6e. RaceControlPanel**
  - `RaceManager` → scene RaceManager

  **6f. SpectatorCamera (on Main Camera)**
  - `ScoreManager` → RaceManager's ScoreManager

- **MIRROR**: UI_PANEL_REFS
- **IMPORTS**: N/A
- **GOTCHA**: Some refs point to ScriptableObjects or components on OTHER GameObjects. Use MCP `manage_components` with `set_field` action or `execute_code` to wire cross-object references. The RaceManager, ScoreManager, EventManager may be on the same or different GameObjects — check scene hierarchy first.
- **VALIDATE**: Enter Play Mode → no `NullReferenceException` in Console; all panels respond to state changes

### Task 7: Ensure EventSystem Exists
- **ACTION**: Add persistent EventSystem to scene (if not already present)
- **IMPLEMENT**: Use MCP:
  1. Check if an EventSystem exists in the scene
  2. If not, create GameObject "EventSystem"
  3. Add `EventSystem` component
  4. Add `InputSystemUIInputModule` component (project uses New Input System)
- **MIRROR**: `RuntimeSetup.SetupEventSystem()` lines 117-123
- **IMPORTS**: N/A
- **GOTCHA**: Without an EventSystem, no UI buttons will respond to clicks. This is easy to miss because RuntimeSetup created one automatically.
- **VALIDATE**: EventSystem exists in scene hierarchy; buttons respond to mouse clicks in Play Mode

### Task 8: Disable RuntimeSetup
- **ACTION**: Disable the `RuntimeSetup` component on the RaceManager GameObject (uncheck in Inspector, do NOT delete)
- **IMPLEMENT**: Use MCP `manage_components` to set `RuntimeSetup.enabled = false`, or `execute_code` to disable it
- **MIRROR**: RuntimeSetup guards (`if (FindFirstObjectByType<RaceUI>() != null) return;`) mean it won't double-create even if left enabled, but disabling is cleaner
- **IMPORTS**: N/A
- **GOTCHA**: Do NOT delete the script file yet — keep as reference. Only disable the component on the GameObject. If something breaks, re-enable as fallback.
- **VALIDATE**: Play Mode works identically to before: SetupScreen appears, "Start with Default Data" works, leaderboard shows, events trigger, camera controls function

### Task 9: Play Mode Integration Test
- **ACTION**: Enter Play Mode and verify the full flow works end-to-end
- **IMPLEMENT**: Use MCP `manage_editor` to enter Play Mode, then verify:
  1. `read_console` — zero errors on start
  2. SetupScreen is visible
  3. Click "Start with Default Data" → race begins
  4. LeaderboardPanel appears with ranked cars
  5. EventPanel appears (Professor role) with event buttons
  6. RaceControlPanel appears with Pause/Save/Export
  7. F1-F9 camera switching works
  8. Escape returns to free camera
  9. Pause freezes cars, Resume continues
  10. `read_console` — zero errors throughout
- **MIRROR**: All patterns
- **IMPORTS**: N/A
- **GOTCHA**: Cannot click UI buttons via MCP directly — use keyboard shortcuts (1-7 for events) to verify event system. Use `execute_code` to call `RaceManager.PauseRace()` directly if needed.
- **VALIDATE**: Zero console errors; all UI panels function correctly; camera modes work

---

## Testing Strategy

### Manual Tests

| Test | Action | Expected | Edge Case? |
|---|---|---|---|
| SetupScreen visible on start | Enter Play Mode | SetupScreen panel active, others hidden | No |
| Start race | Click "Start with Default Data" | Cars spawn, SetupScreen hides, Leaderboard/Events/Controls appear | No |
| Leaderboard updates | Wait 1 second after race start | Rankings appear, top 3 colored | No |
| Event buttons | Click event button | Event triggers, button grays if non-repeatable | No |
| Pause/Resume | Click Pause button | Cars freeze, label changes to "Resume" | No |
| Save session | Click Save button | "Session saved!" status text appears | No |
| Camera Free | WASD + right-click mouse | Camera moves freely | No |
| Camera Fixed | Press F1-F9 | Camera snaps to fixed position | No |
| Camera Spectator | Set Role=Student in Inspector | Auto-follow leader, no event/control panels | No |
| Car labels | Race running | Floating team names above all cars | No |
| Zero cars | Empty CSV | No errors, empty leaderboard | Yes |
| Role switch | Change Role in Inspector, restart | Correct panels shown/hidden | No |

### Edge Cases Checklist
- [ ] No EventSchedule assigned to EventManager
- [ ] No DefaultCsvData assigned to RaceManager
- [ ] Camera at extreme zoom-out
- [ ] Pause then click events (buttons should still work since UI uses unscaledDeltaTime)
- [ ] Load session when no sessions exist

---

## Validation Commands

### Compilation Check
```
# Use MCP read_console after entering Play Mode
# Look for: zero compilation errors
```
EXPECT: No compilation errors

### Play Mode Verification
```
# Enter Play Mode via MCP manage_editor action="play"
# Check console via read_console
# Verify UI flow via execute_code queries
```
EXPECT: Full race flow works without errors

### Manual Validation
- [ ] Open `complete_track_demo.unity` in Unity Editor
- [ ] Verify Canvas visible in Scene View with proper layout
- [ ] Enter Play Mode
- [ ] SetupScreen appears
- [ ] Click "Start with Default Data"
- [ ] All panels appear/hide correctly
- [ ] Camera controls (WASD, F1-F9, Escape) work
- [ ] Events trigger via buttons and keyboard (1-7)
- [ ] Pause/Resume works
- [ ] Save and Export show status text
- [ ] Car labels visible above all cars
- [ ] Switch Role to Student → only leaderboard visible, spectator camera active

---

## Acceptance Criteria
- [ ] Canvas exists in scene with RaceUI, SetupScreen, LeaderboardPanel, EventPanel, RaceControlPanel
- [ ] LeaderboardRow.prefab exists at `Assets/Prefabs/UI/`
- [ ] EventRow.prefab exists at `Assets/Prefabs/UI/`
- [ ] All Inspector references wired (no yellow warnings)
- [ ] CameraManager, CarLabelSpawner, FixedCameraPoints exist as scene objects
- [ ] EventSystem with InputSystemUIInputModule in scene
- [ ] RuntimeSetup component disabled
- [ ] Zero console errors in Play Mode
- [ ] Full race flow works: Setup → Start → Race → Events → Pause → Resume
- [ ] Professor and Student roles show correct panels

## Completion Checklist
- [ ] Layout matches RuntimeSetup values (anchors, offsets, colors)
- [ ] All SerializeField references on all components are populated
- [ ] No code changes required (all work is Editor/prefab/scene configuration)
- [ ] RuntimeSetup kept as disabled fallback (not deleted)
- [ ] Scene saved after all changes

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| MCP cannot set all cross-references | MEDIUM | Incomplete wiring | Use `execute_code` to wire via C# at edit-time; fallback to manual Inspector |
| Prefab creation via MCP limited | LOW | Cannot create prefab assets | Use `execute_code` with PrefabUtility API to create prefab assets |
| Scene modifications not saved | MEDIUM | Changes lost on Unity restart | Explicitly save scene via MCP after each task |
| Layout doesn't match RuntimeSetup exactly | LOW | Visual regression | Use exact same anchor/offset values from RuntimeSetup.cs |

## Notes
- This plan requires NO code changes — all 7 UI/Camera scripts are already complete and have proper `public`/`[Header]` fields ready for Inspector wiring
- The only "code" executed is via MCP `execute_code` for Editor-time operations (creating objects, wiring references, saving prefabs)
- `RuntimeSetup.cs` has guards at every step (`if (FindFirstObjectByType<RaceUI>() != null) return;`), so even if accidentally left enabled, it won't double-create
- `RaceManager.Start()` checks `FindFirstObjectByType<SetupScreen>() == null` — since our Canvas includes a SetupScreen, the auto-start guard will correctly wait for user action
- VerticalLayoutGroup on Content parents enables automatic row layout without hardcoded positions
- Consider this a prerequisite for Phase 5 (Multi-Client Sync) — proper prefab setup makes it easier to spawn the same UI on multiple clients
