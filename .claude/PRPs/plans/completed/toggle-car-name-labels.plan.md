# Plan: Toggle Car Name Labels (Button + Hotkey)

## Summary
Car name labels already spawn above every car when the race starts and are shown by default.
This adds a professor-facing HUD **button** and a **keyboard hotkey (`N`)** that toggle all car
name labels on/off at runtime, so the professor can declutter the projector view on demand.

## User Story
As a professor running a race on the projector,
I want to toggle every car's name label on or off with a button or a hotkey,
So that I can show names by default but hide them when I want a clean view of the track.

## Problem → Solution
Car name labels are always visible while racing (only distance-culled by `CarLabel`) with no way to
hide them → A single toggle (button in the control bar, or the `N` key) flips all labels between
shown (default) and hidden, for both professor and student clients.

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A
- **PRD Phase**: N/A
- **Estimated Files**: 4 changed + 1 new test = 5

---

## UX Design

### Before
```
┌───────────────────────────────────────────────┐
│           [car]  ">> Team Red <<"              │  ← names always on
│      [car] "Team Blue"    [car] "Team Green"   │
│                                                │
│  ┌──────────────────────────────────────────┐ │
│  │ [Pause] [Save] [Export] [Auto Cam]  status│ │  ← control bar (professor)
│  └──────────────────────────────────────────┘ │
└───────────────────────────────────────────────┘
```

### After
```
┌───────────────────────────────────────────────┐
│           [car]        [car]        [car]      │  ← names hidden after toggle
│                                                │
│  ┌────────────────────────────────────────────┐
│  │ [Pause] [Save] [Export] [Auto Cam] [Names: On]  status│  ← new button
│  └────────────────────────────────────────────┘
└───────────────────────────────────────────────┘
        press N  ──►  labels hide, button reads "Names: Off"
        press N  ──►  labels show, button reads "Names: On"
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Car name labels | Always spawned + shown (distance-culled only) | Shown by default; toggle hides/shows all | Default unchanged = ON |
| `N` key | Unused | Toggles all labels | Works for professor **and** student (spawner Update always runs) |
| Control bar | 4 buttons (Pause/Save/Export/Auto Cam) | 5th "Names: On/Off" button | Professor-only bar; label mirrors Auto Cam pattern |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/UI/CarLabelSpawner.cs` | 1-119 | Owns label list + lifecycle — the toggle state lives here |
| P0 | `Assets/Scripts/UI/CarLabel.cs` | 30-55 | `LateUpdate` self-manages `canvas.enabled`; explains why SetActive is the correct hide mechanism |
| P0 | `Assets/Scripts/UI/RaceControlPanel.cs` | 1-70 | Button field + `Start()` listener + auto-resolve pattern to mirror exactly |
| P0 | `Assets/Scripts/Editor/TrackSetupEditor.cs` | 877-920 | `WireOrCreateControlPanel` — where the 5th button is created and wired |
| P1 | `Assets/Scripts/Camera/CameraManager.cs` | 34, 66-101 | Hotkey field + `Update()` `wasPressedThisFrame` pattern to mirror |
| P1 | `Assets/Scripts/UI/LeaderboardPanel.cs` | 80-82, 264-269, 298-304 | `public Key ... = Key.Tab;` field + `HandleToggleInput` + pure static toggle helper |
| P2 | `Assets/Tests/EditMode/LeaderboardDisplayModeTests.cs` | all | Test file structure to mirror for the new toggle test |

## External Documentation
No external research needed — feature uses established internal patterns (new Input System `Keyboard.current`, UGUI `Button`, MonoBehaviour lifecycle).

---

## Patterns to Mirror

### HOTKEY_FIELD
```csharp
// SOURCE: Assets/Scripts/Camera/CameraManager.cs:34  (and LeaderboardPanel.cs:80-82)
[Header("Hotkeys")]
public Key AutoSwitchKey = Key.C;
```

### HOTKEY_HANDLING
```csharp
// SOURCE: Assets/Scripts/UI/LeaderboardPanel.cs:264-269
private void HandleToggleInput()
{
    if (Keyboard.current == null) return;
    if (Keyboard.current[ToggleKey].wasPressedThisFrame)
        SetDisplayMode(NextMode(currentMode));
}
```

### BUTTON_FIELD_AND_LISTENER
```csharp
// SOURCE: Assets/Scripts/UI/RaceControlPanel.cs:22-23, 47-48, 54-70
public Button AutoCamButton;
public Text AutoCamLabel;
// ... in Start():
if (AutoCamButton != null)
    AutoCamButton.onClick.AddListener(ToggleAutoCam);
// ... handler updates its own label text:
private void ToggleAutoCam()
{
    if (CameraManager == null) return;
    CameraManager.ToggleAutoSwitch();
    if (AutoCamLabel != null) AutoCamLabel.text = "Auto: Top 3";
    ShowStatus("Auto camera: following top 3");
}
```

### DEFENSIVE_AUTO_RESOLVE
```csharp
// SOURCE: Assets/Scripts/UI/RaceControlPanel.cs:36-39
if (RaceManager == null)
    RaceManager = FindFirstObjectByType<RaceManager>(FindObjectsInactive.Include);
if (CameraManager == null)
    CameraManager = FindFirstObjectByType<CameraManager>(FindObjectsInactive.Include);
```

### EDITOR_BUTTON_CREATION
```csharp
// SOURCE: Assets/Scripts/Editor/TrackSetupEditor.cs:907-916
// Auto Cam button (toggles the auto-switching top-3 chase cam; mirrors the 'C' hotkey)
cp.AutoCamButton = CreateUIButton(panel.transform, "AutoCamBtn", "Auto Cam",
    new Vector2(0, 0.5f), new Vector2(0, 0.5f),
    new Vector2(370, -17), new Vector2(480, 17));
cp.AutoCamLabel = cp.AutoCamButton.GetComponentInChildren<Text>();

// Status text
cp.StatusText = CreateLabel(panel.transform, "StatusText", "", 14, TextAnchor.MiddleRight,
    new Vector2(1, 0), new Vector2(1, 1),
    new Vector2(-70, 0), new Vector2(-5, 0));
```

### PURE_STATIC_TOGGLE_HELPER + TEST
```csharp
// SOURCE: Assets/Scripts/UI/LeaderboardPanel.cs:298-304
public static DisplayMode NextMode(DisplayMode mode) => mode switch { ... };
// SOURCE: Assets/Tests/EditMode/LeaderboardDisplayModeTests.cs:12-17
[Test]
public void NextMode_Normal_ReturnsEnlarged() {
    Assert.AreEqual(LeaderboardPanel.DisplayMode.Enlarged,
        LeaderboardPanel.NextMode(LeaderboardPanel.DisplayMode.Normal));
}
```

### LABEL_LIST_OWNERSHIP
```csharp
// SOURCE: Assets/Scripts/UI/CarLabelSpawner.cs:24, 46-108
private readonly List<GameObject> spawnedLabels = new List<GameObject>();
// SpawnLabels() adds one world-space canvas GameObject per car to spawnedLabels.
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/UI/CarLabelSpawner.cs` | UPDATE | Own `LabelsVisible` state + `ToggleLabelsKey` hotkey + `SetLabelsVisible`/`ToggleLabels` API; apply visibility on spawn |
| `Assets/Scripts/UI/RaceControlPanel.cs` | UPDATE | Add `ToggleNamesButton`/`ToggleNamesLabel` + `CarLabelSpawner` ref + `ToggleNames()` handler |
| `Assets/Scripts/Editor/TrackSetupEditor.cs` | UPDATE | Create + wire the 5th button; widen control panel; wire `CarLabelSpawner` into `RaceControlPanel` |
| `Assets/Tests/EditMode/CarLabelSpawnerToggleTests.cs` | CREATE | Unit-test the toggle state flip (Logic evidence — BLOCKING gate) |

## NOT Building
- No per-car label toggling (all-or-nothing only).
- No persistence of the toggle state across race restarts (defaults back to ON each Racing start — matches existing "spawn fresh each race" behavior).
- No new label styling, font, or distance-culling changes — `CarLabel.LateUpdate` distance culling stays intact.
- No student-side UI button (students have no control bar); students still get the `N` hotkey.
- No networking/replication of the toggle (local view preference only, like camera modes).

---

## Step-by-Step Tasks

### Task 1: Add toggle state, hotkey, and API to CarLabelSpawner
- **ACTION**: Edit `Assets/Scripts/UI/CarLabelSpawner.cs`.
- **IMPLEMENT**:
  - Add `using UnityEngine.InputSystem;` at top (currently only `System.Collections.Generic`, `UnityEngine`, `UnityEngine.UI`).
  - Add serialized field under a new header:
    ```csharp
    [Header("Toggle")]
    [Tooltip("Key that toggles all car name labels on/off. Labels default to visible.")]
    public Key ToggleLabelsKey = Key.N;
    ```
  - Add private state: `private bool labelsVisible = true;`
  - Add `private void Update()` that mirrors `LeaderboardPanel.HandleToggleInput`:
    ```csharp
    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[ToggleLabelsKey].wasPressedThisFrame)
            ToggleLabels();
    }
    ```
  - Add public API:
    ```csharp
    /// <summary>Flip all car name labels between shown and hidden.</summary>
    public void ToggleLabels() => SetLabelsVisible(!labelsVisible);

    /// <summary>Show or hide every spawned car name label. Default is visible.</summary>
    public void SetLabelsVisible(bool visible)
    {
        labelsVisible = visible;
        foreach (var label in spawnedLabels)
            if (label != null) label.SetActive(visible);
    }

    /// <summary>Whether car name labels are currently shown.</summary>
    public bool LabelsVisible => labelsVisible;
    ```
  - In `SpawnLabels()`, after the loop populates `spawnedLabels` (after line 106, near the Debug.Log), apply the current toggle so a mid-toggle race restart respects the state:
    ```csharp
    // Respect the current toggle when labels are (re)spawned each race.
    if (!labelsVisible)
        foreach (var label in spawnedLabels)
            if (label != null) label.SetActive(false);
    ```
- **MIRROR**: HOTKEY_FIELD, HOTKEY_HANDLING, LABEL_LIST_OWNERSHIP.
- **IMPORTS**: `using UnityEngine.InputSystem;`
- **GOTCHA**: Do **not** toggle `canvas.enabled` directly — `CarLabel.LateUpdate` (CarLabel.cs:42-43) rewrites `canvas.enabled` every frame from distance culling and would immediately undo it. `GameObject.SetActive(false)` disables the `CarLabel` component so its `LateUpdate` stops running, and the label stays hidden until re-enabled; on re-enable, distance culling resumes correctly.
- **VALIDATE**: `foreach` guards on `!= null` (labels are `Destroy`d on Setup state). Confirm default `labelsVisible = true` so behavior is unchanged until toggled.

### Task 2: Add the toggle button + handler to RaceControlPanel
- **ACTION**: Edit `Assets/Scripts/UI/RaceControlPanel.cs`.
- **IMPLEMENT**:
  - Add UI element fields under `[Header("UI Elements")]` (after `AutoCamLabel`):
    ```csharp
    public Button ToggleNamesButton;
    public Text ToggleNamesLabel;
    ```
  - Add reference field under `[Header("References")]` (after `CameraManager`):
    ```csharp
    [Tooltip("Drives the Names toggle button. Auto-resolved if unset.")]
    public CarLabelSpawner CarLabelSpawner;
    ```
  - In `Start()`, add defensive auto-resolve mirroring the CameraManager one:
    ```csharp
    if (CarLabelSpawner == null)
        CarLabelSpawner = FindFirstObjectByType<CarLabelSpawner>(FindObjectsInactive.Include);
    ```
  - In `Start()`, add the listener (after the AutoCamButton wiring):
    ```csharp
    if (ToggleNamesButton != null)
        ToggleNamesButton.onClick.AddListener(ToggleNames);
    ```
  - Add the handler mirroring `ToggleAutoCam`:
    ```csharp
    private void ToggleNames()
    {
        if (CarLabelSpawner == null) return;
        CarLabelSpawner.ToggleLabels();
        bool on = CarLabelSpawner.LabelsVisible;
        if (ToggleNamesLabel != null) ToggleNamesLabel.text = on ? "Names: On" : "Names: Off";
        ShowStatus(on ? "Car names shown" : "Car names hidden");
    }
    ```
- **MIRROR**: BUTTON_FIELD_AND_LISTENER, DEFENSIVE_AUTO_RESOLVE.
- **IMPORTS**: none new (`UnityEngine`, `UnityEngine.UI` already present).
- **GOTCHA**: `RaceControlPanel` is the professor-only bar; that's intentional. The `N` hotkey (Task 1) covers students, who have no control bar.
- **VALIDATE**: Handler early-returns on null `CarLabelSpawner`; button label reflects real `LabelsVisible` state, not a local guess.

### Task 3: Create and wire the button in the editor setup, widen the panel
- **ACTION**: Edit `Assets/Scripts/Editor/TrackSetupEditor.cs`, method `WireOrCreateControlPanel` (lines 877-920).
- **IMPLEMENT**:
  - Widen the panel to make room for a 5th button. Change the `CreateUIPanel` offsets from `(-280, 10)/(280, 60)` (560px) to `(-330, 10)/(330, 60)` (660px):
    ```csharp
    var panel = CreateUIPanel(canvasRoot, "RaceControlPanel",
        new Vector2(0.5f, 0), new Vector2(0.5f, 0),
        new Vector2(-330, 10), new Vector2(330, 60));
    ```
  - After the Auto Cam button block (line 911), add the Names toggle button (starts where Auto Cam ends at x=480):
    ```csharp
    // Toggle Names button (shows/hides all car name labels; mirrors the 'N' hotkey)
    cp.ToggleNamesButton = CreateUIButton(panel.transform, "ToggleNamesBtn", "Names: On",
        new Vector2(0, 0.5f), new Vector2(0, 0.5f),
        new Vector2(490, -17), new Vector2(600, 17));
    cp.ToggleNamesLabel = cp.ToggleNamesButton.GetComponentInChildren<Text>();
    ```
  - Wire the `CarLabelSpawner` reference onto the panel. Add after `cp.CameraManager = cm;` (line 889):
    ```csharp
    cp.CarLabelSpawner = Object.FindFirstObjectByType<CarLabelSpawner>(FindObjectsInactive.Include);
    ```
- **MIRROR**: EDITOR_BUTTON_CREATION.
- **IMPORTS**: none new (`CarLabelSpawner` is a global-namespace runtime type; `Object.FindFirstObjectByType` already used throughout this file, e.g. line 879).
- **GOTCHA**: `CreateRaceManager` (lines 535-537) creates the `CarLabelSpawner` GameObject **before** the UI panels are wired (line 577 calls `WireOrCreateControlPanel`), so `FindFirstObjectByType<CarLabelSpawner>` here will find it. But `WireOrCreateControlPanel` early-returns an `existing` panel (lines 879-880) without re-wiring — for existing scenes rely on the `RaceControlPanel.Start()` auto-resolve from Task 2 instead. Both paths are covered.
- **VALIDATE**: Open the scene setup / re-run Setup Track; confirm the control bar shows 5 buttons and StatusText is not overlapped (StatusText stays right-anchored `(-70,0)/(-5,0)`, now inside the 660px panel with the last button ending at x=600).

### Task 4: Add unit test for the toggle state flip
- **ACTION**: Create `Assets/Tests/EditMode/CarLabelSpawnerToggleTests.cs`.
- **IMPLEMENT**: EditMode test that constructs a `CarLabelSpawner` on a throwaway GameObject (no RaceManager needed — `spawnedLabels` is empty, so `SetLabelsVisible` just flips state) and asserts the flip. Arrange/Act/Assert, per test-standards.md:
  ```csharp
  using NUnit.Framework;
  using UnityEngine;

  /// <summary>
  /// Covers the on/off flip of car name labels exposed by <see cref="CarLabelSpawner.ToggleLabels"/>
  /// and <see cref="CarLabelSpawner.SetLabelsVisible"/>. Labels default to visible; a toggle flips the
  /// flag. Pinned so a refactor can't silently change the default or the toggle direction.
  /// </summary>
  [TestFixture]
  public class CarLabelSpawnerToggleTests
  {
      private CarLabelSpawner spawner;

      [SetUp]
      public void SetUp()
      {
          var go = new GameObject("CarLabelSpawner_Test");
          spawner = go.AddComponent<CarLabelSpawner>();
      }

      [TearDown]
      public void TearDown()
      {
          if (spawner != null) Object.DestroyImmediate(spawner.gameObject);
      }

      [Test]
      public void LabelsVisible_DefaultsToTrue()
      {
          Assert.IsTrue(spawner.LabelsVisible);
      }

      [Test]
      public void ToggleLabels_FromDefault_HidesLabels()
      {
          spawner.ToggleLabels();
          Assert.IsFalse(spawner.LabelsVisible);
      }

      [Test]
      public void ToggleLabels_Twice_ReturnsToVisible()
      {
          spawner.ToggleLabels();
          spawner.ToggleLabels();
          Assert.IsTrue(spawner.LabelsVisible);
      }

      [Test]
      public void SetLabelsVisible_False_HidesLabels()
      {
          spawner.SetLabelsVisible(false);
          Assert.IsFalse(spawner.LabelsVisible);
      }
  }
  ```
- **MIRROR**: PURE_STATIC_TOGGLE_HELPER + TEST (LeaderboardDisplayModeTests structure).
- **IMPORTS**: `using NUnit.Framework;`, `using UnityEngine;`
- **GOTCHA**: Must live in the EditMode test assembly (`Assets/Tests/EditMode/`) — the folder already has an `.asmdef` covering these files. `DestroyImmediate` (not `Destroy`) in EditMode teardown. No filesystem/network state → deterministic.
- **VALIDATE**: Runs green under Unity Test Framework EditMode.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `LabelsVisible_DefaultsToTrue` | fresh spawner | `true` | default guarantee |
| `ToggleLabels_FromDefault_HidesLabels` | one toggle | `false` | — |
| `ToggleLabels_Twice_ReturnsToVisible` | two toggles | `true` | round-trip |
| `SetLabelsVisible_False_HidesLabels` | set false | `false` | direct setter |

### Edge Cases Checklist
- [x] Toggle before race starts (empty `spawnedLabels`) → flag flips, no NRE (foreach over empty list)
- [x] Toggle mid-race → all live labels `SetActive`d
- [x] Race restart while hidden → `SpawnLabels` re-applies `labelsVisible` (Task 1 final block)
- [x] Null labels in list (destroyed on Setup) → guarded by `!= null`
- [x] No `Keyboard.current` (headless) → `Update` early-returns
- [x] Missing `CarLabelSpawner` ref on panel → handler early-returns, button inert (not crashing)

---

## Validation Commands

### Static Analysis
```bash
# Unity compiles on focus / via CLI. If using the UnitySkills API, trigger a recompile:
curl -s -X POST http://localhost:8090/api/compile 2>/dev/null || echo "Use Editor focus to recompile"
```
EXPECT: Zero compile errors in the Console.

### Unit Tests
```bash
# Run EditMode tests (Unity Test Framework). In-editor: Window > General > Test Runner > EditMode > Run All.
# CLI (game-ci style):
# Unity -runTests -testPlatform EditMode -projectPath . -testResults results.xml -batchmode -quit
```
EXPECT: `CarLabelSpawnerToggleTests` (4 tests) pass; no regressions in existing EditMode suite.

### Manual Validation (in Editor Play mode or WebGL build)
- [ ] Start a race → car name labels are visible by default (unchanged).
- [ ] Press `N` → all labels disappear; press `N` again → they reappear.
- [ ] Click the "Names: On" button (professor bar) → labels hide, button reads "Names: Off", status shows "Car names hidden".
- [ ] Click again → labels show, button reads "Names: On".
- [ ] Hide labels, then reset to Setup and start a new race → confirm the SpawnLabels re-apply behaves as documented (labels respect the last toggle state).
- [ ] Control bar shows 5 buttons with no overlap on StatusText.

---

## Acceptance Criteria
- [ ] Car name labels still show by default when a race starts.
- [ ] Pressing `N` toggles all labels off/on.
- [ ] A "Names: On/Off" button in the professor control bar toggles all labels and reflects state in its label.
- [ ] Button + hotkey drive the **same** state (both read/write `CarLabelSpawner.LabelsVisible`).
- [ ] `CarLabel` distance-culling still works after labels are re-shown.
- [ ] EditMode tests pass; no compile errors.

## Completion Checklist
- [ ] Code follows discovered patterns (hotkey field, `wasPressedThisFrame`, button+label, auto-resolve).
- [ ] Error handling matches codebase style (null guards, early returns, `FindFirstObjectByType` fallback).
- [ ] `[Tooltip]`/`[Header]` attributes on new serialized fields (matches CarLabelSpawner/RaceControlPanel style).
- [ ] Test follows test-standards.md (arrange/act/assert, deterministic, self-cleanup).
- [ ] No hardcoded values beyond the mirrored button rect offsets and default `Key.N`.
- [ ] No unnecessary scope additions (see NOT Building).

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Control bar buttons overlap StatusText after adding 5th | Medium | Low | Panel widened to 660px; button ends x=600, StatusText right-anchored |
| Existing scenes have a pre-built RaceControlPanel that editor setup won't re-wire (early-return at line 879) | Medium | Medium | `RaceControlPanel.Start()` auto-resolves `CarLabelSpawner`; existing scene still needs the button GameObject added manually or via re-running full setup — note for implementer |
| `.prefab`/`.unity` serialized refs for the new button not set on existing scene | Medium | Medium | Prefer UnitySkills API to add the button to the live scene; or re-run Setup Track which rebuilds the panel when none exists |
| `CarLabel.LateUpdate` fights the toggle | Low | High | Use `SetActive`, not `canvas.enabled` — disabling the GameObject stops `LateUpdate` (documented in Task 1 GOTCHA) |

## Notes
- **Why CarLabelSpawner owns the state**: it already owns the label list and lifecycle (spawn on Racing, clear on Setup). Putting the flag + hotkey there keeps a single source of truth that both the button and the key drive, and makes the state unit-testable without any UI.
- **Default behavior preserved**: `labelsVisible = true` and labels spawn shown, so nothing changes until the user toggles — matching the request "default will show the name."
- **Per project CLAUDE.md**: prefer the UnitySkills API (`http://localhost:8090`) for any live-scene GameObject/prefab wiring over hand-editing `.unity`/`.prefab` YAML. The `.cs` edits above are direct-file edits (API doesn't author arbitrary C# logic), which is the sanctioned fallback.
- **Test evidence gate**: this is a Logic story (state toggle) → automated EditMode test is BLOCKING per coding-standards.md; the button/hotkey feel is Visual/UI → manual walkthrough is ADVISORY.
