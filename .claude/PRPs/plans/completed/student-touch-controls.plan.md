# Plan: Student View On-Screen Buttons (Leaderboard Size + Auto Cam)

## Summary
Students who join the race in the Unity WebGL build currently have **keyboard-only** controls: `Tab` cycles the leaderboard size (Normal → Enlarged → Fullscreen) and the auto-camera modes are professor-gated behind the `C` key. Tablet/mobile spectators have no keyboard, so these controls are unreachable. This adds two always-visible, touch-friendly on-screen buttons to the **student** HUD — "Leaderboard" (cycles the board size) and "Auto Cam" (flips Top-3 chase ↔ All-Cams-on-leader) — built at runtime the same way RaceUI already builds the student hint overlay.

## User Story
As a **student watching the race on a tablet or phone (no keyboard)**,
I want **on-screen buttons to resize the leaderboard and switch the auto-camera mode**,
so that **I can control my spectator view with touch, exactly like a professor can with the keyboard.**

## Problem → Solution
**Current:** Student view exposes leaderboard resize only via `Tab` (`LeaderboardPanel.HandleToggleInput`) and auto-cam switching only via `C` (`CameraManager.Update`, gated off for students by `AllowFreeControl == false`). No pointer/touch path exists → tablet/mobile students are stuck in the default Normal board + AutoTopCars camera.
**Desired:** A small student-only button panel, shown while racing, with two UGUI buttons wired to `LeaderboardPanel.CycleDisplayMode()` (new) and `CameraManager.ToggleAutoSwitch()` (existing, already `public`). Clicks work with mouse and touch via the existing EventSystem/GraphicRaycaster (proven — the student fullscreen leaderboard rows are already clickable).

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A (free-form feature request)
- **PRD Phase**: N/A
- **Estimated Files**: 4 (2 source edits, 2 test files) — no scene/prefab edits

---

## UX Design

### Before
```
Student WebGL view (tablet — no keyboard)
┌──────────────────────────────────────────────┐
│ [Leaderboard: Normal]        (race view)      │
│                                               │
│                                               │
│                                               │
│ Tab: leaderboard size | Click team | Esc ...  │  ← hint tells them to press keys
└──────────────────────────────────────────────┘
   ✗ No keyboard → cannot resize board or switch auto-cam
```

### After
```
Student WebGL view (tablet — no keyboard)
┌──────────────────────────────────────────────┐
│ [Leaderboard: Normal]        (race view)      │
│                                               │
│                                               │
│                            ┌──────────────┐   │
│                            │  Leaderboard │   │  ← tap: Normal→Enlarged→Fullscreen→Normal
│                            ├──────────────┤   │
│                            │ Auto: Top 3  │   │  ← tap: flips Top-3 ↔ All Cams
│                            └──────────────┘   │
│ Tab: leaderboard size | Tap buttons | Esc ... │
└──────────────────────────────────────────────┘
   ✓ Buttons render ABOVE the fullscreen leaderboard so "Leaderboard" stays reachable to cycle back
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Resize leaderboard (student) | `Tab` key only | `Tab` key **or** "Leaderboard" button | Same cycle: Normal → Enlarged → Fullscreen → Normal |
| Switch auto-camera (student) | Unavailable (`C` gated off) | "Auto Cam" button | Flips AutoTopCars ↔ AutoAllCams via existing `ToggleAutoSwitch()` |
| Follow a car (student) | Click team row in fullscreen | Unchanged | Existing behavior preserved |
| Return to auto cam (student) | `Esc` key | `Esc` key (unchanged) + Auto Cam button re-enters an auto mode from FollowCar | Buttons are additive; no keyboard control removed |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/UI/RaceUI.cs` | 138-283 | `ApplyRole`, `OnStateChanged` (student `else` branch), and the runtime overlay builders `BuildCameraHint`/`BuildStudentHint` — the EXACT pattern to mirror for the button panel |
| P0 (critical) | `Assets/Scripts/UI/LeaderboardPanel.cs` | 253-331 | `HandleToggleInput`, `NextMode`, `SetDisplayMode`, private `currentMode` — where `CycleDisplayMode()` is added and how the Tab path is refactored to share it |
| P0 (critical) | `Assets/Scripts/Camera/CameraManager.cs` | 111-131 | `ToggleAutoSwitch()` (public, button target) and `CurrentMode` getter used to label the Auto Cam button |
| P1 (important) | `Assets/Scripts/UI/RaceControlPanel.cs` | 65-90 | Professor `ToggleAutoCam()` — the label text ("Auto: All Cam" / "Auto: Top 3") and status semantics to reuse for the student button |
| P2 (reference) | `Assets/Scripts/UI/StudentJoinBootstrap.cs` | 42-59 | How a client becomes a locked Student (`LockAsStudent()`) — confirms the role gate the panel keys off |
| P2 (reference) | `Assets/Tests/EditMode/LeaderboardDisplayModeTests.cs` | all | Pure-logic EditMode test structure to mirror |
| P2 (reference) | `Assets/Tests/EditMode/CameraRoleDecisionTests.cs` | all | Pure-logic EditMode test structure to mirror |

## External Documentation

No external research needed — feature uses established internal patterns (runtime UGUI construction already present in `RaceUI`, `CameraManager`, `TrackSetupEditor.CreateUIButton`). UGUI `Button.onClick` fires on touch/pointer via the scene's existing EventSystem + GraphicRaycaster; this is already relied on by `LeaderboardPanel.MakeRowClickable` for student fullscreen row clicks, so no new input plumbing is required.

---

## Patterns to Mirror

### RUNTIME_UI_BUILDER (build a student-only overlay in code, no scene wiring)
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:259-283  (BuildStudentHint)
private GameObject studentHint;

private void BuildStudentHint()
{
    var obj = new GameObject("StudentHint");
    obj.transform.SetParent(transform, false);

    var text = obj.AddComponent<Text>();
    text.text = "Tab: leaderboard size ...";
    text.fontSize = 15;
    text.alignment = TextAnchor.LowerLeft;
    text.color = new Color(1f, 1f, 1f, 0.75f);
    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    // ...
    var rt = obj.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0f, 0f);
    rt.anchorMax = new Vector2(0f, 0f);
    rt.pivot = new Vector2(0f, 0f);
    rt.anchoredPosition = new Vector2(14f, 12f);
    rt.sizeDelta = new Vector2(820f, 24f);
    studentHint = obj;
}
```

### ROLE_STATE_ACTIVATION (show/hide the student overlay by role + state)
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:212-219  (OnStateChanged, student else-branch)
else
{
    // Student: the professor camera hint never applies, but the student still needs to know
    // the leaderboard resizes with Tab and that its rows are clickable in fullscreen.
    if (cameraHint != null) cameraHint.SetActive(false);
    if (studentHint == null) BuildStudentHint();
    if (studentHint != null) studentHint.SetActive(isRacing);
}
```

### PUBLIC_TOGGLE_ENTRY (a public method a UI button can call, mirroring the key handler)
```csharp
// SOURCE: Assets/Scripts/Camera/CameraManager.cs:123-131  (ToggleAutoSwitch — already public)
/// <summary>
/// Cycle the Auto Cam modes. ... Safe to call from a UI button or the keyboard hotkey.
/// </summary>
public void ToggleAutoSwitch()
{
    SetMode(CurrentMode == CameraMode.AutoTopCars ? CameraMode.AutoAllCams : CameraMode.AutoTopCars);
}
```

### KEY_HANDLER_DELEGATES_TO_PUBLIC_METHOD (refactor Tab to share one entry with the button)
```csharp
// SOURCE: Assets/Scripts/UI/LeaderboardPanel.cs:264-269  (HandleToggleInput — current)
private void HandleToggleInput()
{
    if (Keyboard.current == null) return;
    if (Keyboard.current[ToggleKey].wasPressedThisFrame)
        SetDisplayMode(NextMode(currentMode));   // ← extract this into a public CycleDisplayMode()
}
```

### BUTTON_LABEL_FROM_MODE (professor's existing label rule to reuse for the student button)
```csharp
// SOURCE: Assets/Scripts/UI/RaceControlPanel.cs:74-90  (ToggleAutoCam)
CameraManager.ToggleAutoSwitch();
if (CameraManager.CurrentMode == CameraManager.CameraMode.AutoAllCams)
{
    if (AutoCamLabel != null) AutoCamLabel.text = "Auto: All Cam";
}
else
{
    if (AutoCamLabel != null) AutoCamLabel.text = "Auto: Top 3";
}
```

### RUNTIME_BUTTON_CONSTRUCTION (how the codebase builds a clickable UGUI Button in code)
```csharp
// SOURCE: Assets/Scripts/Editor/TrackSetupEditor.cs (CreateUIButton pattern) — a GameObject with
// Image (targetGraphic) + Button + a child Text. Mirror this at runtime (not via the Editor helper,
// which lives in an Editor-only assembly and cannot be called from a runtime MonoBehaviour):
var btnGO = new GameObject("LeaderboardBtn", typeof(RectTransform), typeof(CanvasRenderer),
                           typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
var img = btnGO.GetComponent<UnityEngine.UI.Image>();
img.color = new Color(0f, 0f, 0f, 0.55f);            // semi-opaque touch target
var btn = btnGO.GetComponent<UnityEngine.UI.Button>();
btn.targetGraphic = img;
// child Text label built like BuildStudentHint's Text (LegacyRuntime.ttf, centered)
btn.onClick.AddListener(OnLeaderboardButton);
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/UI/LeaderboardPanel.cs` | UPDATE | Add `public void CycleDisplayMode()`; refactor `HandleToggleInput` to call it so button + Tab share one entry |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | Build the student touch-control panel at runtime (`BuildStudentTouchControls`), show it in the student `else` branch of `OnStateChanged`, add pure `AutoCamButtonLabel` helper + the two click handlers |
| `Assets/Tests/EditMode/StudentTouchControlTests.cs` | CREATE | Pure-logic tests for the Auto Cam button label mapping (`AutoCamButtonLabel`) |
| `Assets/Tests/EditMode/LeaderboardDisplayModeTests.cs` | UPDATE | Add a test asserting `CycleDisplayMode` advances via `NextMode` order (guards the button/Tab shared path stays in cycle) |

> Test files require a matching `.cs.meta`; Unity generates it on next Editor focus. Follow the sibling test files' meta convention if committing the meta explicitly.

## NOT Building

- **No new scene or prefab edits** (`.unity` / `.prefab`). The panel is built at runtime in `RaceUI`, exactly like `BuildStudentHint`/`BuildCameraHint`. This deliberately avoids the "scene wiring lags merged scripts" failure mode — a merged `.cs` with unwired serialized refs would ship a dead button.
- **No professor-side change.** The professor already has `RaceControlPanel` (Pause/Save/Export/Auto Cam/Names). This feature is student-only.
- **No touch-device detection / conditional visibility.** Buttons show for all students while racing; on a desktop student they are harmless (mouse-clickable) and cost nothing. (If later requested, gate with `Application.isMobilePlatform` or `Input.touchSupported` — out of scope now.)
- **No leaderboard "on/off" hide toggle.** The request is to *switch/cycle* the leaderboard (its three size modes), matching the existing `Tab` behavior — not to fully hide it.
- **No new camera modes.** The Auto Cam button reuses the existing `ToggleAutoSwitch()` (Top-3 ↔ All-Cams). It does not add fixed-cam (F1-F9) access for students.
- **No repositioning/removal of the existing student hint text.**

---

## Step-by-Step Tasks

### Task 1: Add `CycleDisplayMode()` to LeaderboardPanel and share it with the Tab handler
- **ACTION**: In `Assets/Scripts/UI/LeaderboardPanel.cs`, add a public method that advances the current display mode, and route the existing keyboard handler through it.
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Advance the leaderboard size one step (Normal → Enlarged → Fullscreen → Normal). Public so a
  /// touch button (student HUD) drives the same cycle as the <see cref="ToggleKey"/> hotkey.
  /// </summary>
  public void CycleDisplayMode() => SetDisplayMode(NextMode(currentMode));
  ```
  Then change `HandleToggleInput` (lines 264-269) so the Tab branch calls `CycleDisplayMode()` instead of `SetDisplayMode(NextMode(currentMode))`.
- **MIRROR**: KEY_HANDLER_DELEGATES_TO_PUBLIC_METHOD (LeaderboardPanel.cs:264-269).
- **IMPORTS**: none new.
- **GOTCHA**: `currentMode` is a private field (line 106). Keep `CycleDisplayMode` inside `LeaderboardPanel` so it can read it — do NOT expose `currentMode` publicly (nothing else needs it). `SetDisplayMode` already refreshes + fires `OnFullscreenChanged`, so the button gets identical side-effects to Tab for free.
- **VALIDATE**: `HandleToggleInput` now has one statement calling `CycleDisplayMode()`; `CycleDisplayMode` compiles against the existing private `currentMode` and public `NextMode`/`SetDisplayMode`.

### Task 2: Add the pure Auto Cam label helper to RaceUI
- **ACTION**: In `Assets/Scripts/UI/RaceUI.cs`, add a pure static method mapping a camera mode to the button caption, mirroring the professor panel's inline rule so it is unit-testable.
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Caption for the student Auto Cam button given the camera's current mode: "Auto: All Cam" in
  /// AutoAllCams, otherwise "Auto: Top 3". Pure so the label rule is unit-testable and matches the
  /// professor RaceControlPanel wording. (Shown after a toggle, so the label reflects the new mode.)
  /// </summary>
  public static string AutoCamButtonLabel(CameraManager.CameraMode mode)
      => mode == CameraManager.CameraMode.AutoAllCams ? "Auto: All Cam" : "Auto: Top 3";
  ```
- **MIRROR**: BUTTON_LABEL_FROM_MODE (RaceControlPanel.cs:74-90) and the existing pure statics `CameraModeForRole`/`ShouldShowEventPanel` in RaceUI.
- **IMPORTS**: none new (`CameraManager` is in the same assembly, already referenced).
- **GOTCHA**: Keep it `public static` and side-effect-free — this is the only BLOCKING logic test target for this feature (layout/click side-effects need a live scene and are not unit-tested).
- **VALIDATE**: Method compiles; returns the two expected strings.

### Task 3: Build the student touch-control panel at runtime in RaceUI
- **ACTION**: Add a `BuildStudentTouchControls()` builder + two click handlers, mirroring `BuildStudentHint`. Create a small vertical panel (bottom-right) holding two buttons: "Leaderboard" and an Auto Cam button whose label starts at `AutoCamButtonLabel(CameraManager.CurrentMode)`.
- **IMPLEMENT**:
  - Fields near `studentHint` (line 257):
    ```csharp
    private GameObject studentTouchControls;
    private Text autoCamButtonLabel;
    ```
  - Builder (mirror BuildStudentHint's Text/RectTransform setup; anchor bottom-right so it does not sit over the bottom-left hint):
    ```csharp
    private void BuildStudentTouchControls()
    {
        var panel = new GameObject("StudentTouchControls", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(1f, 0f);
        prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot = new Vector2(1f, 0f);
        prt.anchoredPosition = new Vector2(-14f, 14f);
        prt.sizeDelta = new Vector2(150f, 96f);

        var vlg = panel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.spacing = 8f; vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;

        CreateTouchButton(panel.transform, "LeaderboardBtn", "Leaderboard", OnLeaderboardButton, out _);
        CreateTouchButton(panel.transform, "AutoCamBtn",
            AutoCamButtonLabel(CameraManager != null ? CameraManager.CurrentMode
                                                     : CameraManager.CameraMode.AutoTopCars),
            OnAutoCamButton, out autoCamButtonLabel);

        studentTouchControls = panel;
    }

    // Runtime UGUI button: Image (target graphic + touch area) + Button + centered child Text.
    private void CreateTouchButton(Transform parent, string name, string label,
                                   UnityEngine.Events.UnityAction onClick, out Text labelText)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        var btn = go.GetComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var rt = txtGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        labelText = txtGO.AddComponent<Text>();
        labelText.text = label;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 16;
        labelText.color = Color.white;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnLeaderboardButton()
    {
        if (Leaderboard != null) Leaderboard.CycleDisplayMode();
    }

    private void OnAutoCamButton()
    {
        if (CameraManager == null) return;
        CameraManager.ToggleAutoSwitch();
        if (autoCamButtonLabel != null)
            autoCamButtonLabel.text = AutoCamButtonLabel(CameraManager.CurrentMode);
    }
    ```
- **MIRROR**: RUNTIME_UI_BUILDER + RUNTIME_BUTTON_CONSTRUCTION + PUBLIC_TOGGLE_ENTRY.
- **IMPORTS**: `using UnityEngine.UI;` is already at the top of RaceUI.cs (line 2). Fully-qualified `UnityEngine.UI.*` is used above to be explicit; either style is fine.
- **GOTCHA**:
  1. **Render order**: In Fullscreen the leaderboard covers the whole screen (bg alpha 0.95) and would swallow taps on the panel. Call `studentTouchControls.transform.SetAsLastSibling()` right after building **and** whenever it is (re)shown, so the buttons draw above the fullscreen board and "Leaderboard" stays reachable to cycle back. (RaceUI, Leaderboard, and the hint are siblings under the same Canvas; last sibling wins for overlapping UGUI.)
  2. Do **not** call the Editor helper `TrackSetupEditor.CreateUIButton` — it lives in an Editor assembly and won't compile into the runtime/WebGL build.
  3. The panel is built lazily and reused; never rebuild per state change (mirror the `studentHint == null` guard).
- **VALIDATE**: In Editor Play mode as a student, both buttons appear bottom-right during Racing; clicking Leaderboard cycles the board through all three sizes including back from Fullscreen; clicking Auto Cam flips the camera and updates its own caption.

### Task 4: Show/hide the panel by role + state (student, racing)
- **ACTION**: In `RaceUI.OnStateChanged`, student `else` branch (lines 212-219), lazily build and toggle the panel exactly like `studentHint`.
- **IMPLEMENT**: after the `studentHint` lines, add:
  ```csharp
  if (studentTouchControls == null) BuildStudentTouchControls();
  if (studentTouchControls != null)
  {
      studentTouchControls.SetActive(isRacing);
      if (isRacing) studentTouchControls.transform.SetAsLastSibling(); // stay above fullscreen board
  }
  ```
  Also ensure the professor branch hides it (defensive, in case role flips before lock): in the `if (isProfessor)` branch add `if (studentTouchControls != null) studentTouchControls.SetActive(false);`.
- **MIRROR**: ROLE_STATE_ACTIVATION (RaceUI.cs:212-219).
- **IMPORTS**: none.
- **GOTCHA**: `ApplyRole()` runs before the first `OnStateChanged` and camera refs are resolved in `ResolveMissingReferences()` (line 59) — so `CameraManager`/`Leaderboard` are non-null by the time the panel builds. Still null-guard every handler (they already do) to match the codebase's defensive style.
- **VALIDATE**: Panel is hidden in Setup/Finished, hidden for the professor, visible only for a student while Racing/Paused.

### Task 5: Unit tests for the pure label mapping + cycle order
- **ACTION**: Create `Assets/Tests/EditMode/StudentTouchControlTests.cs` covering `RaceUI.AutoCamButtonLabel`; extend `LeaderboardDisplayModeTests.cs` to assert the cycle the button rides.
- **IMPLEMENT**:
  ```csharp
  using NUnit.Framework;

  /// <summary>
  /// Pins the student Auto Cam button caption rule (RaceUI.AutoCamButtonLabel): "Auto: All Cam" only
  /// in AutoAllCams, "Auto: Top 3" for every other mode. Pure, so a refactor can't silently mislabel
  /// the touch button relative to the actual camera mode.
  /// </summary>
  [TestFixture]
  public class StudentTouchControlTests
  {
      [Test]
      public void AutoCamButtonLabel_AllCams_ReturnsAllCam()
      {
          Assert.AreEqual("Auto: All Cam",
              RaceUI.AutoCamButtonLabel(CameraManager.CameraMode.AutoAllCams));
      }

      [Test]
      public void AutoCamButtonLabel_TopCars_ReturnsTop3()
      {
          Assert.AreEqual("Auto: Top 3",
              RaceUI.AutoCamButtonLabel(CameraManager.CameraMode.AutoTopCars));
      }

      [Test]
      public void AutoCamButtonLabel_FollowCar_DefaultsToTop3()
      {
          // A student in click-to-follow: label shows the auto mode the button will enter next.
          Assert.AreEqual("Auto: Top 3",
              RaceUI.AutoCamButtonLabel(CameraManager.CameraMode.FollowCar));
      }
  }
  ```
  In `LeaderboardDisplayModeTests.cs`, add one test documenting that the button/Tab shared path advances by `NextMode` (the pure guard; `CycleDisplayMode` itself needs a live RectTransform so it is not unit-tested directly):
  ```csharp
  [Test]
  public void NextMode_DrivesButtonAndTabCycle_Enlarged()
  {
      // CycleDisplayMode() == SetDisplayMode(NextMode(currentMode)); the order is what this pins.
      Assert.AreEqual(LeaderboardPanel.DisplayMode.Enlarged,
          LeaderboardPanel.NextMode(LeaderboardPanel.DisplayMode.Normal));
  }
  ```
- **MIRROR**: `CameraRoleDecisionTests.cs` / `LeaderboardDisplayModeTests.cs` structure (no scene, pure static asserts, one behavior per test, `test_[scenario]_[expected]`-style names).
- **IMPORTS**: `using NUnit.Framework;` only.
- **GOTCHA**: The EditMode test assembly must reference the runtime assembly — it already does (existing tests call `RaceUI.CameraModeForRole` and `LeaderboardPanel.NextMode`), so no `.asmdef` change is needed. A new `.cs.meta` is generated by Unity on Editor focus.
- **VALIDATE**: All new tests pass under the Unity Test Runner EditMode suite.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `AutoCamButtonLabel_AllCams_ReturnsAllCam` | `AutoAllCams` | `"Auto: All Cam"` | No |
| `AutoCamButtonLabel_TopCars_ReturnsTop3` | `AutoTopCars` | `"Auto: Top 3"` | No |
| `AutoCamButtonLabel_FollowCar_DefaultsToTop3` | `FollowCar` | `"Auto: Top 3"` | Yes (non-auto mode) |
| `NextMode_DrivesButtonAndTabCycle_Enlarged` | `Normal` | `Enlarged` | No |

### Edge Cases Checklist
- [x] Fullscreen leaderboard covers the button → mitigated by `SetAsLastSibling()` (Task 3/4 GOTCHA).
- [x] Student in FollowCar taps Auto Cam → `ToggleAutoSwitch()` returns them to AutoTopCars (label "Auto: Top 3"); consistent with `Esc`.
- [x] Null `Leaderboard`/`CameraManager` refs → every handler null-guards (no-op).
- [x] Role flips before lock → professor branch force-hides the panel.
- [ ] Concurrent access — N/A (single-threaded Unity main thread).
- [ ] Network failure — N/A (buttons are local view controls, no socket traffic).
- [ ] Permission denied — N/A (student view-only controls; no host authority invoked).

---

## Validation Commands

### Static Analysis
```bash
# No standalone C# type-checker; compilation IS the type check. Trigger a domain reload / build
# via the UnitySkills API (preferred per technical-preferences.md) or open the Editor.
curl -s http://localhost:8090/health   # confirm UnitySkills API is up before Editor automation
```
EXPECT: Editor compiles `LeaderboardPanel.cs`, `RaceUI.cs`, and the new test file with zero errors.

### Unit Tests (EditMode)
```bash
# Preferred: run the EditMode suite through the UnitySkills API (see .claude/skills/unity-skills).
# CLI fallback (adjust Unity path/version 6.3 LTS):
/Applications/Unity/Hub/Editor/6000.3.*/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath "$PWD" -testPlatform EditMode \
  -testResults "$PWD/editmode-results.xml" -logFile -
```
EXPECT: `StudentTouchControlTests` (3) + `LeaderboardDisplayModeTests` (5) + `CameraRoleDecisionTests` (5) all pass; no regressions across the EditMode suite.

### Full Test Suite
```bash
# Same runner without a name filter runs every EditMode fixture under Assets/Tests/EditMode.
```
EXPECT: No regressions.

### Browser / WebGL Validation (per project — WebGL is the student target)
```bash
# Build WebGL (BuildScript.cs) or run in Editor Play mode with a simulated student role.
# In Editor, exercise the student path without a URL by calling RaceUI.LockAsStudent() at runtime,
# or drive play-mode verification via UnitySkills (runInBackground pause + camera_screenshot).
```
EXPECT: As a student during Racing, two buttons render bottom-right; "Leaderboard" cycles Normal→Enlarged→Fullscreen→Normal (and remains tappable in Fullscreen); "Auto Cam" flips the camera and relabels; professor view is unchanged.

### Manual Validation
- [ ] Join as a student (URL `#room=CODE&role=play`) on a touch device / browser touch emulation.
- [ ] Confirm both buttons are visible only while racing, only for the student.
- [ ] Tap "Leaderboard" 3× → returns to Normal; tap once from Fullscreen → shrinks back (button stayed on top).
- [ ] Tap "Auto Cam" → camera visibly changes (Top-3 chase ↔ all-cams-on-leader) and caption updates.
- [ ] Professor view still shows only `RaceControlPanel`; no student buttons leak in.
- [ ] Keyboard `Tab`/`Esc` still work for a desktop student (buttons are additive).

---

## Acceptance Criteria
- [ ] Student HUD shows a "Leaderboard" button that cycles the board size (matches `Tab`).
- [ ] Student HUD shows an "Auto Cam" button that flips AutoTopCars ↔ AutoAllCams and updates its caption.
- [ ] Buttons appear only for students, only while Racing/Paused; hidden in Setup/Finished and for the professor.
- [ ] Buttons remain tappable when the leaderboard is Fullscreen.
- [ ] Clicks work via touch and mouse (existing EventSystem/GraphicRaycaster).
- [ ] No `.unity`/`.prefab` edits required; feature works in the WebGL build with no scene wiring.
- [ ] All validation commands pass; new EditMode tests green; no regressions.

## Completion Checklist
- [ ] Code follows the runtime-overlay pattern already in RaceUI (`BuildStudentHint`/`BuildCameraHint`).
- [ ] Null-guarding matches the codebase's defensive style (every handler no-ops on null refs).
- [ ] Auto Cam label wording matches `RaceControlPanel` ("Auto: All Cam" / "Auto: Top 3").
- [ ] Tests follow the pure-logic EditMode pattern; `HandleToggleInput` and the button share `CycleDisplayMode()` (no duplicated cycle logic).
- [ ] No hardcoded gameplay values introduced (UI layout constants only, consistent with existing runtime-built overlays).
- [ ] Commit references this feature; Conventional Commits `feat(ui): ...`.
- [ ] Self-contained — no open questions for implementation.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Fullscreen leaderboard eats button taps | Medium | High (Leaderboard button unusable in Fullscreen) | `SetAsLastSibling()` on show (Task 3/4); manual Fullscreen tap-back check |
| Buttons overlap the bottom-left student hint on small/portrait screens | Low | Low | Panel anchored bottom-**right**; hint stays bottom-left |
| Touch not registering in WebGL | Low | High | Reuses the exact EventSystem/GraphicRaycaster path already proven by `MakeRowClickable` student row clicks |
| Editor-only helper accidentally used at runtime | Low | High (build break) | Task 3 GOTCHA: build the button in-file, never call `TrackSetupEditor.CreateUIButton` |
| Merged `.cs` without scene wiring ships a dead button | Low | Medium | Runtime-built panel needs no serialized refs — the whole reason for Approach A (see NOT Building) |

## Notes
- **Why runtime-built (Approach A) over a serialized `StudentControlPanel` + `TrackSetupEditor` wiring (Approach B):** RaceUI already constructs student overlays in code with zero scene dependencies, so Approach A adds the buttons with no `.unity`/`.prefab` changes, no serialization/auto-wire risk, and no scene regeneration. It directly sidesteps the project's known "scene wiring lags merged scripts" pitfall. Approach B would duplicate the `RaceControlPanel` machinery (a new MonoBehaviour + serialized `Button` refs + editor `CreateUIButton` calls + panel activation), i.e. more code and more failure surface, for no benefit here.
- **Scope of "switch the leaderboard":** interpreted as cycling the existing three size modes (the `Tab` behavior students already have on desktop), not a hide/show toggle — this is the control tablet/mobile students are currently missing.
- **`ToggleAutoSwitch()` never turns auto-cam off** (by design — `Esc`/F1-F9 do that, and students have neither), so the Auto Cam button always lands the student in a valid broadcast camera. This is the desired behavior for a passive spectator.
- Follow-up idea (out of scope): gate the panel behind `Input.touchSupported` / `Application.isMobilePlatform` if desktop students find the buttons redundant.
