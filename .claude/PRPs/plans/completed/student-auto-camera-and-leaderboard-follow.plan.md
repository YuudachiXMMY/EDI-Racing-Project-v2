# Plan: Student Auto-Camera Default + Fullscreen-Leaderboard Click-to-Follow

## Summary
Give the WebGL student (spectator) view the auto-switching camera by default instead of the
plain leader-follow, make the leaderboard + its Tab size-toggle usable and discoverable for
students, and let **both** professor and student click a car's name in the **fullscreen**
leaderboard to switch the main camera into a 3rd-person chase that follows that specific car.

## User Story
As a **student watching the race in the web build**, I want the camera to auto-follow the
action by default, to see and resize the leaderboard with Tab, and to click any team's name in
the fullscreen leaderboard so the camera follows that car in 3rd person — so that I can watch
the race like a broadcast and focus on the team I care about.

## Problem → Solution
**Current state**
- Students launch (role=`play`) → `RaceUI.ApplyRole()` puts them in `CameraMode.Spectator`
  (`SpectatorCamera` `ChaseTopN` with `FollowCount == 1` = leader-only follow). No auto cycling.
- The leaderboard is already shown to students while racing, and `LeaderboardPanel.HandleToggleInput`
  (Tab) is **not** role-gated — but students are never told (the on-screen hint is professor-only),
  so the capability is effectively hidden.
- No one can click a car in the leaderboard to focus the camera on it. Camera targets are
  chosen only by rank (leader / top-N) or fixed-camera proximity.

**Desired state**
- Student default camera = the auto-switching chase cam (top-N), matching the professor's
  "Auto Cam" entry mode.
- Students get a minimal on-screen hint covering Tab (leaderboard size) and click-to-follow.
- A click on a row in the **fullscreen** leaderboard resolves that row's team → the spawned car,
  switches the camera into a 3rd-person chase of that car, and shrinks the leaderboard back to
  Normal so the race is visible. Works identically for professor and student.

## Metadata
- **Complexity**: Medium (C#-only, Unity runtime UI + camera; ~6 files, ~250–350 lines incl. tests)
- **Source PRD**: N/A (free-form request)
- **PRD Phase**: N/A
- **Estimated Files**: 6 source/test + 2 `.meta` for new test files

---

## UX Design

### Before
```
Student (role=play) web build, racing:
┌───────────────────────────────────────────────┐
│ [Leaderboard - top-left HUD]     (Tab works    │
│  1. [2] Red Rockets                but nobody   │
│  2. [2] Blue Bolts                 tells you)   │
│                                                │
│        Camera: locked chase on the LEADER only │
│        (no cycling, no way to pick a car)      │
└───────────────────────────────────────────────┘
```

### After
```
Student (role=play) web build, racing:
┌───────────────────────────────────────────────┐
│ [Leaderboard]   Camera auto-cycles top cars    │
│                                                │
│  Press Tab → Enlarged → Fullscreen             │
│                                                │
│  Fullscreen leaderboard (rows are clickable):  │
│   1. [2] Red Rockets   ← click → 3rd-person    │
│   2. [2] Blue Bolts        chase that car,     │
│   3. [1] Green Machine     board shrinks away  │
│                                                │
│  Hint (bottom-left): "Tab: leaderboard size   │
│   |  Click a name in fullscreen to follow"     │
└───────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Student initial camera | `Spectator` (leader follow, no cycle) | `AutoTopCars` (cycles top-N chase) | `RaceUI.ApplyRole()` student branch |
| Student Tab | Works but undiscoverable | Works + on-screen hint | Add student-facing hint text |
| Fullscreen leaderboard row | Static text | Clickable → camera follows that car | Both roles |
| Row click in Normal/Enlarged | n/a | No-op (ignored) | Only Fullscreen rows are actionable |
| After selecting a car | n/a | Board → Normal, cam → 3rd-person chase of that car | Return to auto: Esc (both roles) |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Camera/CameraManager.cs` | 1–143 | Owns `CameraMode` enum + `SetMode`; where the student default and a new `FollowCar` mode live |
| P0 | `Assets/Scripts/Camera/SpectatorCamera.cs` | 1–199 | Chase/target logic; add a `SpecificCar` follow mode + explicit target |
| P0 | `Assets/Scripts/UI/LeaderboardPanel.cs` | 1–500 | Row pooling, Tab toggle, `DisplayMode`, `OnFullscreenChanged` event pattern; add row clicks + `OnCarSelected` |
| P0 | `Assets/Scripts/UI/RaceUI.cs` | 1–223 | `ApplyRole()` (student camera default), `OnFullscreenChanged` wiring, `BuildCameraHint`; add `OnCarSelected` handler + student hint |
| P1 | `Assets/Scripts/Car/CarIdentity.cs` | 1–74 | `TeamName` is the identity key used to resolve a clicked row → car transform |
| P1 | `Assets/Scripts/Race/CarSpawner.cs` | 31–181 | `SpawnCars`/`SpawnVisualCars` both set `car.name = TeamName` and add `CarIdentity` (host & student) |
| P1 | `Assets/Scripts/Race/ScoreManager.cs` | 22–28 | `GetRankedCars()` order used to render host rows (index → `CarIdentity`) |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | 189–196 | `LeaderboardEntry { rank, name, lap, cp }` — student row source; `name` == `TeamName` |
| P2 | `Assets/Scripts/UI/RaceControlPanel.cs` | 29–70 | Reference pattern for `Button.onClick.AddListener` + defensive `FindFirstObjectByType` auto-wire |
| P2 | `Assets/Scripts/UI/CarLabelSpawner.cs` | 46–108 | Confirms `RaceManager.SpawnedCars` (public `List<GameObject>`) is populated for both roles + `GetComponent<CarIdentity>()` usage |
| P2 | `Assets/Scripts/Editor/TrackSetupEditor.cs` | 487–585 | Scene wiring: `EventSystem` + `GraphicRaycaster` already created (clicks work); `SpectatorCam.ScoreManager` wired; how panels/refs are assigned |
| P2 | `Assets/Tests/EditMode/EventPanelVisibilityTests.cs`, `LeaderboardDisplayModeTests.cs` | all | Pure-decision test style to mirror |

## External Documentation
No external research needed — feature uses established internal patterns (Unity UGUI `Button`,
`SpectatorCamera` chase math, and the project's pure-static-decision + `System.Action` event idioms).

---

## Patterns to Mirror

### PURE_STATIC_DECISION (role/visibility rules extracted as testable statics)
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:157-158
public static bool ShouldShowEventPanel(bool isProfessor, bool isRacing, bool leaderboardFullscreen)
    => isProfessor && isRacing && !leaderboardFullscreen;
```
```csharp
// SOURCE: Assets/Scripts/UI/StudentJoinDecision.cs:13-16
public static bool ShouldAutoJoin(string role, string room)
{
    return role == "play" && !string.IsNullOrWhiteSpace(room);
}
```

### EVENT_UP_TO_RACEUI (panel raises a typed event; RaceUI owns cross-object reaction)
```csharp
// SOURCE: Assets/Scripts/UI/LeaderboardPanel.cs:40 + 290
public event System.Action<bool> OnFullscreenChanged;
// ...
OnFullscreenChanged?.Invoke(currentMode == DisplayMode.Fullscreen);
```
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:43-44, 80-81, 91-97
if (Leaderboard != null)
    Leaderboard.OnFullscreenChanged += HandleLeaderboardFullscreenChanged;
// ... unsubscribe in OnDestroy ...
private void HandleLeaderboardFullscreenChanged(bool fullscreen) { /* re-run rule */ }
```

### DEFENSIVE_AUTOWIRE (resolve unique scene singletons by type when serialized ref is lost)
```csharp
// SOURCE: Assets/Scripts/UI/LeaderboardPanel.cs:126-130
if (ScoreManager == null)
    ScoreManager = FindFirstObjectByType<ScoreManager>(FindObjectsInactive.Include);
if (NetworkSync == null)
    NetworkSync = FindFirstObjectByType<NetworkSync>(FindObjectsInactive.Include);
```

### BUTTON_WIRE (UGUI button click subscription)
```csharp
// SOURCE: Assets/Scripts/UI/RaceControlPanel.cs:47-48
if (AutoCamButton != null)
    AutoCamButton.onClick.AddListener(ToggleAutoCam);
```

### FOLLOW_MODE_SWITCH (SpectatorCamera behaviour + reset)
```csharp
// SOURCE: Assets/Scripts/Camera/SpectatorCamera.cs:58-63
public void SetFollowMode(FollowMode mode, int followCount)
{
    Mode = mode;
    FollowCount = followCount;
    ResetCycle();
}
```

### CAMERA_MODE_APPLY (CameraManager maps a mode to SpectatorCamera config)
```csharp
// SOURCE: Assets/Scripts/Camera/CameraManager.cs:114-118
else if (mode == CameraMode.Spectator)
{
    SpectatorCam.SetFollowMode(SpectatorCamera.FollowMode.ChaseTopN, 1);
}
SpectatorCam.enabled = useSpectatorCam;
```

### CAR_BY_COMPONENT (resolve a car GameObject → CarIdentity)
```csharp
// SOURCE: Assets/Scripts/UI/CarLabelSpawner.cs:50-57
var cars = RaceManager.SpawnedCars;   // public List<GameObject>, populated for host AND student
foreach (var car in cars) {
    if (car == null) continue;
    var identity = car.GetComponent<CarIdentity>();
    if (identity == null) continue;
    // identity.TeamName == the leaderboard row's name
}
```

### TEST_STRUCTURE (pure NUnit EditMode test)
```csharp
// SOURCE: Assets/Tests/EditMode/LeaderboardDisplayModeTests.cs:12-17
[Test]
public void NextMode_Normal_ReturnsEnlarged()
{
    Assert.AreEqual(LeaderboardPanel.DisplayMode.Enlarged,
        LeaderboardPanel.NextMode(LeaderboardPanel.DisplayMode.Normal));
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Camera/SpectatorCamera.cs` | UPDATE | Add `FollowMode.SpecificCar` + explicit `SetFollowTarget(Transform)`; target it in `UpdateTarget` |
| `Assets/Scripts/Camera/CameraManager.cs` | UPDATE | Add `CameraMode.FollowCar` + `FollowCar(Transform)`; add `AllowFreeControl` gate; Esc→role default |
| `Assets/Scripts/UI/LeaderboardPanel.cs` | UPDATE | Make pooled rows clickable, track per-row team name, raise `OnCarSelected(string)` only in Fullscreen |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | Student default → auto cam via new pure `CameraModeForRole`; subscribe `OnCarSelected`; resolve car & drive camera + shrink board; student hint |
| `Assets/Scripts/UI/CarLookup.cs` | CREATE | Pure static `FindByTeamName(IReadOnlyList<GameObject>, string) → GameObject` — unit-testable row→car resolution |
| `Assets/Tests/EditMode/CameraRoleDecisionTests.cs` | CREATE | Cover `RaceUI.CameraModeForRole` (professor=Free, student=AutoTopCars) |
| `Assets/Tests/EditMode/CarLookupTests.cs` | CREATE | Cover `CarLookup.FindByTeamName` (match / no-match / null / duplicate-first) |

## NOT Building
- **No web-app / JavaScript changes.** Student role is already established (`buildStudentPlayUrl`
  role=`play` → `StudentJoinBootstrap` → `RaceUI.LockAsStudent`). This work is entirely Unity C#.
- **No new networked message.** Click-to-follow is a local camera action; the clicked team name
  is already present in `LeaderboardEntry.name` (student) / `CarIdentity.TeamName` (host). No
  server round-trip, no `NetworkMessages` change.
- **No click-to-follow in Normal/Enlarged modes.** Only fullscreen rows are actionable (per request).
- **No 3D world-space click-to-select** (clicking the car mesh in the scene). Out of scope — the
  selector is the leaderboard row only.
- **No change to professor F1–F9 / C / free-cam behaviour**, and no removal of the existing
  `Spectator` mode (kept for compatibility; students simply no longer default to it).
- **No prefab/scene `.unity` edits required.** All new UI (row buttons, student hint) is built at
  runtime, matching `BuildCameraHint` and the leaderboard's runtime column construction. `EventSystem`
  + `GraphicRaycaster` already exist in the scene.

---

## Step-by-Step Tasks

### Task 1: Add a `SpecificCar` follow mode to SpectatorCamera
- **ACTION**: Extend `SpectatorCamera` so it can chase one explicitly chosen car (not rank-derived).
- **IMPLEMENT**:
  - Add `SpecificCar` to the enum: `public enum FollowMode { ChaseTopN, FixedPointsOnLeader, SpecificCar }`.
  - Add a private field `private Transform specificTarget;`.
  - Add `public void SetFollowTarget(Transform target) { Mode = FollowMode.SpecificCar; specificTarget = target; ResetCycle(); }`.
  - In `UpdateTarget()`, before the `FixedPointsOnLeader`/`ChaseTopN` branches, handle the new mode
    without touching `ScoreManager` (student `ScoreManager` is empty):
    ```csharp
    if (Mode == FollowMode.SpecificCar)
    {
        currentTarget = specificTarget;   // may be null if the car despawned; LateUpdate no-ops
        return;
    }
    ```
  - In `LateUpdate()`, `SpecificCar` must NOT count as a "cycling" mode and must re-pick each frame
    while `currentTarget == null` (so it reacquires if the target arrives late). The existing
    `cycling` bool already excludes it (`ChaseTopN && FollowCount>1` OR `FixedPointsOnLeader`), and
    the chase branch (lines 114–120) already renders a 3rd-person trail-and-look — no change needed there.
- **MIRROR**: FOLLOW_MODE_SWITCH, CAMERA_MODE_APPLY.
- **IMPORTS**: none new.
- **GOTCHA**: `UpdateTarget` early-returns `if (ScoreManager == null) return;` at line 125 — put the
  `SpecificCar` branch **above** that guard, because a student client's `SpectatorCamera.ScoreManager`
  may be unset/empty and `SpecificCar` doesn't need it.
- **VALIDATE**: EditMode compiles; play-mode: call `SetFollowTarget(car.transform)` and confirm the
  main camera trails behind and looks at that car.

### Task 2: Add a `FollowCar` camera mode + free-control gate to CameraManager
- **ACTION**: Let `CameraManager` drive `SpectatorCamera` into `SpecificCar`, and stop students from
  hijacking the camera with the professor F/C/free-cam keys.
- **IMPLEMENT**:
  - Extend enum: `public enum CameraMode { Free, Fixed, Spectator, AutoTopCars, AutoAllCams, FollowCar }`.
  - Add `[Tooltip("Professor spatial keys (F1-F9, free-cam Esc). RaceUI disables this for students.")] public bool AllowFreeControl = true;`.
  - Add:
    ```csharp
    /// <summary>Follow one specific car in 3rd person (leaderboard click-to-follow, both roles).</summary>
    public void FollowCar(Transform target)
    {
        if (target == null) return;
        SetMode(CameraMode.FollowCar);
        if (SpectatorCam != null) SpectatorCam.SetFollowTarget(target);
    }
    ```
  - In `SetMode`, include `FollowCar` in `useSpectatorCam` (no target-derivation arm needed —
    `FollowCar()` sets the target immediately after `SetMode` returns):
    ```csharp
    bool useSpectatorCam = mode == CameraMode.Spectator
                           || mode == CameraMode.AutoTopCars
                           || mode == CameraMode.AutoAllCams
                           || mode == CameraMode.FollowCar;
    ```
  - Gate `Update()` so students don't move the camera with F/C keys, but Esc still returns to a
    sensible default for everyone:
    ```csharp
    private void Update()
    {
        if (CurrentMode == CameraMode.Spectator) return;
        if (Keyboard.current == null) return;

        // Esc: always available — return to the role-appropriate default.
        if (Keyboard.current[Key.Escape].wasPressedThisFrame)
        {
            SetMode(AllowFreeControl ? CameraMode.Free : CameraMode.AutoTopCars);
            return;
        }

        if (!AllowFreeControl) return;   // students: no C / F1-F9 free control

        if (Keyboard.current[AutoSwitchKey].wasPressedThisFrame) { ToggleAutoSwitch(); return; }
        for (int i = 0; i < 9; i++) { /* F1-F9 unchanged */ }
    }
    ```
- **MIRROR**: CAMERA_MODE_APPLY.
- **IMPORTS**: none new (`UnityEngine.InputSystem` already imported).
- **GOTCHA**: `ToggleAutoSwitch()` flips only between the two Auto modes — calling it from `FollowCar`
  state would jump to `AutoAllCams` unexpectedly. Do NOT route FollowCar through `ToggleAutoSwitch`;
  use the dedicated `FollowCar(Transform)` entry only. Entering `FollowCar` disables `FreeCamera`
  automatically (`isFree` at line 101 is false for any non-`Free` mode).
- **VALIDATE**: Play-mode: `AllowFreeControl=false` → pressing C/F1 does nothing, Esc → AutoTopCars;
  `AllowFreeControl=true` → unchanged professor behaviour.

### Task 3: Create the pure `CarLookup` row→car resolver
- **ACTION**: Add a static that maps a team name to its spawned car GameObject.
- **IMPLEMENT**: New file `Assets/Scripts/UI/CarLookup.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// Resolves a leaderboard row's team name to its spawned car GameObject. The row's identity key
  /// is CarIdentity.TeamName (host rows) / LeaderboardEntry.name (student rows) — both equal the
  /// name CarSpawner assigns, so a single case-sensitive TeamName match covers both roles. Kept a
  /// pure static (no MonoBehaviour state) so the match rule is unit-testable. Returns null on any
  /// miss / blank input; never throws.
  /// </summary>
  public static class CarLookup
  {
      public static GameObject FindByTeamName(IReadOnlyList<GameObject> cars, string teamName)
      {
          if (cars == null || string.IsNullOrEmpty(teamName)) return null;
          for (int i = 0; i < cars.Count; i++)
          {
              var go = cars[i];
              if (go == null) continue;
              var identity = go.GetComponent<CarIdentity>();
              if (identity != null && identity.TeamName == teamName) return go;
          }
          return null;
      }
  }
  ```
- **MIRROR**: CAR_BY_COMPONENT (same `GetComponent<CarIdentity>()` + `.TeamName` compare).
- **IMPORTS**: `System.Collections.Generic`, `UnityEngine`.
- **GOTCHA**: Match must be case-sensitive and exact — the network name is passed verbatim and
  `web_join_room` upper-cases *room codes*, not team names; do not `ToLower()` here or host vs
  student rows could diverge. Returns the **first** match if two teams share a name (pre-existing
  data assumption; leaderboard already shows duplicate names as-is).
- **VALIDATE**: `CarLookupTests` (Task 7) pass.

### Task 4: Make fullscreen leaderboard rows clickable + raise `OnCarSelected`
- **ACTION**: Turn pooled rows into buttons that report the clicked team name, but only while Fullscreen.
- **IMPLEMENT** in `LeaderboardPanel.cs`:
  - Add event near `OnFullscreenChanged` (line 40):
    ```csharp
    /// <summary>Raised when a row is clicked in Fullscreen mode; arg is the row's team name.
    /// RaceUI resolves it to a car and drives the camera. Never fired in Normal/Enlarged.</summary>
    public event System.Action<string> OnCarSelected;
    ```
  - Add a parallel per-row store: `private readonly List<string> rowTeamNames = new List<string>();`.
    Size it alongside the pool in `Start()` (add `rowTeamNames.Add("")` per pooled row), so index i
    of `rowPool` maps to index i of `rowTeamNames`.
  - When building the pool in `Start()` (lines 139–144), after `rowPool.Add(row)`, attach a click
    handler. The row prefab's `Text` is a `Graphic` and can be the button's raycast target:
    ```csharp
    var text = row.GetComponent<Text>();
    if (text != null) text.raycastTarget = true;
    var btn = row.GetComponent<Button>();
    if (btn == null) btn = row.AddComponent<Button>();
    btn.transition = Selectable.Transition.None;   // keep the shipped look; no color swap
    if (text != null) btn.targetGraphic = text;
    int captured = i;                              // capture the pool index, not the loop var
    btn.onClick.AddListener(() => HandleRowClicked(captured));
    ```
  - Record the team name whenever a row is styled, at the two refresh call sites:
    - `RefreshFromScoreManager` (line 406): `rowTeamNames[i] = car.TeamName;`
    - `RenderNetworkEntries` (line 435): `rowTeamNames[i] = entry.name;`
    - Inactive rows (the `else` branches, lines 410 & 439): `rowTeamNames[i] = "";`.
  - Add the handler:
    ```csharp
    private void HandleRowClicked(int index)
    {
        if (currentMode != DisplayMode.Fullscreen) return;      // only fullscreen rows act
        if (index < 0 || index >= rowTeamNames.Count) return;
        string team = rowTeamNames[index];
        if (!string.IsNullOrEmpty(team)) OnCarSelected?.Invoke(team);
    }
    ```
- **MIRROR**: EVENT_UP_TO_RACEUI, BUTTON_WIRE.
- **IMPORTS**: `UnityEngine.UI` (already imported for `Text`/`Image`/layout groups) — `Button`/`Selectable` live here.
- **GOTCHA**: Capture the loop index into a local (`int captured = i;`) before the lambda — closing
  over the loop variable `i` would make every row report the last index. `Button.transition = None`
  avoids the default tint that would fight the rank-color styling in `StyleRow`. Rows are re-parented
  between columns across modes; the `Button` rides on the same GameObject so it survives re-parenting.
- **VALIDATE**: Play-mode: in Fullscreen, clicking a row fires `OnCarSelected` with the right name;
  clicking in Normal/Enlarged does nothing.

### Task 5: Wire student default auto-cam + click-to-follow in RaceUI
- **ACTION**: Default students to the auto cam, disable their free-control, and react to `OnCarSelected`.
- **IMPLEMENT** in `RaceUI.cs`:
  - Add a pure decision static (mirrors `ShouldShowEventPanel`):
    ```csharp
    /// <summary>Camera mode a role starts in: Professor free-flies; Student gets the auto-switching
    /// top-N chase (broadcast feel). Pure so the mapping is unit-testable.</summary>
    public static CameraManager.CameraMode CameraModeForRole(bool isProfessor)
        => isProfessor ? CameraManager.CameraMode.Free : CameraManager.CameraMode.AutoTopCars;
    ```
  - In `ApplyRole()` (lines 141–148) replace the camera block:
    ```csharp
    if (CameraManager != null)
    {
        CameraManager.AllowFreeControl = isProfessor;      // students: no C / F1-F9 free control
        CameraManager.SetMode(CameraModeForRole(isProfessor));
    }
    ```
  - In `Start()` (after line 44) subscribe, and in `OnDestroy()` (after line 81) unsubscribe:
    ```csharp
    if (Leaderboard != null) Leaderboard.OnCarSelected += HandleCarSelected;
    // OnDestroy:
    if (Leaderboard != null) Leaderboard.OnCarSelected -= HandleCarSelected;
    ```
  - Add the handler:
    ```csharp
    // A fullscreen-leaderboard row was clicked: follow that car in 3rd person and shrink the board
    // so the race is visible again. Works for professor and student alike.
    private void HandleCarSelected(string teamName)
    {
        if (CameraManager == null || RaceManager == null) return;
        var car = CarLookup.FindByTeamName(RaceManager.SpawnedCars, teamName);
        if (car == null) return;
        CameraManager.FollowCar(car.transform);
        if (Leaderboard != null) Leaderboard.SetDisplayMode(LeaderboardPanel.DisplayMode.Normal);
    }
    ```
- **MIRROR**: PURE_STATIC_DECISION, EVENT_UP_TO_RACEUI, CAR_BY_COMPONENT.
- **IMPORTS**: none new (`RaceManager.SpawnedCars` is `public List<GameObject>`; `CarLookup` is global).
- **GOTCHA**: `SetDisplayMode(Normal)` fires `OnFullscreenChanged(false)`, which
  `HandleLeaderboardFullscreenChanged` already handles (restores the EventPanel for professors) —
  intended, not a double-effect. Verify `RaceManager.SpawnedCars` is populated on the **student**
  client (visual cars from `SpawnVisualCars`, positioned by `NetworkSync`); `CarLabelSpawner` relies
  on this for both roles, so it should be — confirm during play-mode validation (Task 8 manual).
- **VALIDATE**: `CameraRoleDecisionTests` pass; play-mode click-to-follow works for both roles.

### Task 6: Student-facing on-screen hint (Tab + click-to-follow)
- **ACTION**: Tell students the leaderboard can be resized and rows are clickable — the current hint
  is professor-only.
- **IMPLEMENT** in `RaceUI.cs`, `OnStateChanged` (lines 172–189): in the `else` (student) branch,
  build/show a minimal student hint instead of only hiding `cameraHint`. Reuse the `BuildCameraHint`
  approach (runtime `Text` under this transform, ASCII, bottom-left) with student text:
  `"Tab: leaderboard size (normal / enlarged / fullscreen)  |  Click a team name in fullscreen to follow that car  |  Esc: auto camera"`.
  Keep it as a second lazily-built object (e.g. `studentHint`) so professor/student hints don't collide,
  and gate its `SetActive` on `isRacing`.
- **MIRROR**: `BuildCameraHint` (RaceUI.cs:197-222) verbatim structure (font, anchors, alpha).
- **IMPORTS**: none new.
- **GOTCHA**: The built-in `LegacyRuntime.ttf` has no CJK glyphs — keep the hint ASCII (matches the
  existing professor hint's own comment at RaceUI.cs:192-194).
- **VALIDATE**: Play-mode student view shows the hint while racing; professor view unchanged.

### Task 7: EditMode test — `CarLookup.FindByTeamName`
- **ACTION**: Cover the row→car resolver.
- **IMPLEMENT**: New `Assets/Tests/EditMode/CarLookupTests.cs`. Build tiny `GameObject`s with
  `CarIdentity` (set `.TeamName`) via `new GameObject().AddComponent<CarIdentity>()`, assert:
  - match returns the right GameObject;
  - unknown name returns null;
  - null list / null-or-empty name returns null;
  - a null entry in the list is skipped (doesn't throw);
  - duplicate names return the first.
  Tear down created objects with `Object.DestroyImmediate` in `[TearDown]`.
- **MIRROR**: TEST_STRUCTURE + `CarIdentityTests.cs` (existing, for `CarIdentity` construction style).
- **IMPORTS**: `NUnit.Framework`, `UnityEngine`, `System.Collections.Generic`.
- **GOTCHA**: `CarLookup` calls `GetComponent<CarIdentity>()`, so tests need real `GameObject`s
  (not POCOs). This is an EditMode test that instantiates objects — allowed (mirrors how other
  component-touching EditMode tests build throwaway GameObjects); still deterministic and I/O-free.
- **VALIDATE**: Runs green in the EditMode suite.

### Task 8: EditMode test — `RaceUI.CameraModeForRole`
- **ACTION**: Pin the role→camera-mode mapping.
- **IMPLEMENT**: New `Assets/Tests/EditMode/CameraRoleDecisionTests.cs`:
  - `CameraModeForRole_Professor_ReturnsFree` → `Assert.AreEqual(CameraManager.CameraMode.Free, RaceUI.CameraModeForRole(true))`.
  - `CameraModeForRole_Student_ReturnsAutoTopCars` → `Assert.AreEqual(CameraManager.CameraMode.AutoTopCars, RaceUI.CameraModeForRole(false))`.
- **MIRROR**: TEST_STRUCTURE, `EventPanelVisibilityTests.cs`.
- **IMPORTS**: `NUnit.Framework`.
- **GOTCHA**: Pure static — no scene, no GameObject needed.
- **VALIDATE**: Runs green.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `CameraModeForRole_Professor_ReturnsFree` | `true` | `CameraMode.Free` | no |
| `CameraModeForRole_Student_ReturnsAutoTopCars` | `false` | `CameraMode.AutoTopCars` | no |
| `FindByTeamName_Match_ReturnsCar` | cars incl. "Red", "Blue" | that GameObject | no |
| `FindByTeamName_Unknown_ReturnsNull` | "Ghost" | `null` | yes |
| `FindByTeamName_NullList_ReturnsNull` | `null`, "Red" | `null` | yes |
| `FindByTeamName_BlankName_ReturnsNull` | cars, `""` | `null` | yes |
| `FindByTeamName_NullEntryInList_Skips` | `[null, Red]`, "Red" | Red GameObject (no throw) | yes |
| `FindByTeamName_DuplicateNames_ReturnsFirst` | two "Red" | first | yes |
| (regression, existing) `NextMode_*` | — | unchanged | — |

### Edge Cases Checklist
- [x] Empty/blank team name → `FindByTeamName` returns null → no camera change
- [x] Team name with no matching spawned car (stale/despawned) → no-op
- [x] Row clicked in Normal/Enlarged → ignored (`HandleRowClicked` guards on Fullscreen)
- [x] Clicked car despawns while followed → `SpectatorCamera.currentTarget` null → chase no-ops (no crash)
- [x] Student `ScoreManager` empty → `SpecificCar` mode bypasses ranking (no dependency on it)
- [x] Duplicate team names → first match (documented assumption)
- [x] Professor keys unchanged when `AllowFreeControl = true`
- [ ] Concurrent access — N/A (single-threaded Unity main loop)
- [ ] Network failure — N/A (no new network traffic)

---

## Validation Commands

### Static Analysis / Compile
```bash
# Trigger a domain reload + compile via the UnitySkills API (preferred per technical-preferences.md).
# If unavailable, open the project in the Editor and confirm no console compile errors.
curl -s -X POST http://localhost:8090/refresh_assets   # or the equivalent UnitySkills compile call
```
EXPECT: Zero C# compile errors; no new warnings on the changed files.

### Unit Tests (EditMode)
```bash
# CI parity (headless): game-ci/unity-test-runner in EditMode, or locally:
Unity -batchmode -runTests -testPlatform EditMode -projectPath . \
  -testResults Deploy/editmode-results.xml -quit
```
EXPECT: All EditMode tests pass, including the two new fixtures (`CarLookupTests`, `CameraRoleDecisionTests`)
and the unchanged `LeaderboardDisplayModeTests` / `EventPanelVisibilityTests`.

### WebGL Build Verify (feature ships in the web build)
```bash
Unity -batchmode -executeMethod BuildScript.VerifyBuildArtifacts -projectPath . -quit
```
EXPECT: Build artifacts verified (no template/URL regressions).

### Manual / Play-mode Validation (per unity-playmode-verification memory)
- [ ] **Student, professor-hosted room, Racing**: camera auto-cycles the top cars on load (not a static
      leader lock).
- [ ] **Student**: press Tab → Normal → Enlarged → Fullscreen; leaderboard resizes.
- [ ] **Student**: in Fullscreen, click a team name → camera switches to a 3rd-person chase of that
      car and the board shrinks to Normal.
- [ ] **Student**: press Esc → returns to auto (top-N) camera.
- [ ] **Professor**: in Fullscreen, click a team name → same 3rd-person follow + shrink; then C / F1-F9 /
      Esc still behave as before.
- [ ] **Professor**: `AllowFreeControl` stays true — C toggles auto modes, F1-F9 fixed cams, Esc free cam.
- [ ] Follow a car, then let the race reset → no exceptions in the console.

---

## Acceptance Criteria
- [ ] Student launch (role=`play`) defaults to `AutoTopCars`, not `Spectator`.
- [ ] Student sees the leaderboard while racing and can Tab-cycle its three sizes.
- [ ] Student has an on-screen hint for Tab + click-to-follow + Esc.
- [ ] Clicking a team name in the **fullscreen** leaderboard follows that car in 3rd person for
      **both** professor and student, and shrinks the board.
- [ ] Clicks in Normal/Enlarged do nothing.
- [ ] Professor camera controls (C / F1-F9 / Esc / Auto Cam button) are unchanged.
- [ ] New + existing EditMode tests pass; WebGL build verifies.

## Completion Checklist
- [ ] Code follows discovered patterns (pure-static decisions, `System.Action` events up to RaceUI, defensive auto-wire)
- [ ] Error handling matches codebase style (null-guarded, silent no-op on missing refs — see `RaceControlPanel`)
- [ ] No new logging noise (feature is UI/camera; matches existing quiet handlers)
- [ ] Tests follow `[System][Feature]Tests.cs` + Arrange/Act/Assert naming
- [ ] No hardcoded gameplay values (team-name match is data-driven from spawned cars)
- [ ] Doc comments on new public APIs (`SetFollowTarget`, `FollowCar`, `CameraModeForRole`, `CarLookup`, `OnCarSelected`) per coding-standards.md
- [ ] No web-app changes; no scene/prefab edits required
- [ ] Self-contained — no further codebase search needed to implement

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Student `RaceManager.SpawnedCars` empty/unpopulated → click resolves nothing | Low | Med | `CarLabelSpawner` already reads it for both roles; verify in play-mode (Task 8 manual). If empty, fall back to `FindObjectsByType<CarIdentity>` inside `HandleCarSelected`. |
| Row `Button` tint fights rank-color styling | Low | Low | `Button.transition = Selectable.Transition.None` (Task 4) keeps the shipped text look |
| Students defaulting to an Auto mode exposes professor keys to students | Med | Med | `AllowFreeControl=false` gate in `CameraManager.Update` (Task 2) blocks C/F1-F9; only Esc (→auto) remains |
| "Auto camera" intended as `AutoAllCams` not `AutoTopCars` | Low | Low | Assumption documented; one-line change in `CameraModeForRole` if the professor prefers all-cams |
| Closure-over-loop-variable bug wires every row to the last index | Med (classic) | High | Explicit `int captured = i;` before the lambda (Task 4 GOTCHA) |

## Notes
- **Assumption — "auto camera" = `AutoTopCars`**: the professor's Auto Cam button *enters* on
  `AutoTopCars` (top-3 chase), so students default to the same for a consistent "broadcast" feel.
  Trivially switchable to `AutoAllCams` in `RaceUI.CameraModeForRole`.
- **Return-to-auto UX**: Esc returns to the role default (`AutoTopCars` for students, `Free` for
  professors). This is the single "get me out of following one car" control for students and is
  surfaced in their hint.
- **Why route through RaceUI, not the leaderboard directly**: `LeaderboardPanel` is deliberately kept
  unaware of other systems (see its `OnFullscreenChanged` doc). It raises `OnCarSelected(teamName)`;
  RaceUI — which already holds `CameraManager` + `RaceManager` — resolves the car and drives the
  camera, mirroring the existing `OnFullscreenChanged → HandleLeaderboardFullscreenChanged` wiring.
- **Identity key**: `CarIdentity.TeamName` == `LeaderboardEntry.name` == `CarSpawner` `car.name` for
  both host and student, so one case-sensitive match resolves rows for either role with no new
  network field.
