# Plan: Fix Student Auto-Cam Button (No Auto Tracing / Camera Switching)

## Summary
On the student side of the Unity WebGL build, tapping the "Auto Cam" button (and the student's
default auto-broadcast camera) never traces cars or cuts between fixed cameras. The button's click
path works and toggles the camera mode, but neither auto mode can ever pick a target because the
student client's `ScoreManager` is never populated with cars — `LoadAndStartRaceVisualOnly` (student
path) skips the `ScoreManager.RegisterCar` loop that the host path (`LoadAndStartRace`) performs. The
fix registers the visual-only spawned cars into the student's `ScoreManager` so ranking is non-empty
and the auto camera has a leader/top-N to follow.

## User Story
As a **student watching the race in the web browser**,
I want **the "Auto Cam" button to switch the broadcast camera between the top-3 chase and the
all-cams-on-leader view (and for the auto camera to actually track cars)**,
so that **I can follow the race hands-free without needing the professor's keyboard or click-to-follow**.

## Problem → Solution
**Current:** Student `ScoreManager` has zero registered cars → `GetRankedCars()` returns an empty list
→ `SpectatorCamera.UpdateTarget()` early-returns for both `AutoTopCars` and `AutoAllCams`
(`ranked.Count == 0`) → `currentTarget` stays null → the camera holds a frozen frame. Tapping "Auto
Cam" flips the mode and relabels the button, but nothing visibly moves.
**Desired:** Student `ScoreManager` holds the visual cars (whose `TotalCheckpointsPassed` is already
synced by `NetworkSync.HandleStateUpdate`) → `GetRankedCars()` returns a ranked list → the auto
camera traces the leader / cycles the top-N, and the "Auto Cam" button cuts between the two auto modes.

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A (free-form bug report)
- **PRD Phase**: N/A
- **Estimated Files**: 2 (1 source, 1 test)

---

## Root Cause Analysis

### The click path is NOT the problem (ruled out)
- `OnAutoCamButton` (`Assets/Scripts/UI/RaceUI.cs:392`) → `CameraManager.ToggleAutoSwitch()` →
  `SetMode(...)`. This chain is **not** gated by `AllowFreeControl` (that flag only gates the
  professor keyboard keys inside `CameraManager.Update()`), so a student's tap always reaches
  `ToggleAutoSwitch`.
- Active Input Handling = `1` (new Input System) in `ProjectSettings/ProjectSettings.asset:928`, and
  the scene's EventSystem uses `InputSystemUIInputModule`
  (`Assets/Scenes/complete_track_demo.unity:7935`) — matched, so UGUI/touch clicks fire. The sibling
  "Leaderboard" button on the same panel works, confirming the raycaster/EventSystem path is intact.
- Proof the toggle runs: `OnAutoCamButton` relabels the button via `AutoCamButtonLabel(...)`. The
  label DOES change on tap; only the camera fails to move — pinpointing the fault downstream of the
  toggle, in target selection.

### The actual defect: student `ScoreManager` is empty
Trace of the student spectator flow:
1. `StudentJoinBootstrap.Start()` → `RaceUI.LockAsStudent()` → `ApplyRole()` sets
   `CameraManager.AllowFreeControl = false` and `SetMode(AutoAllCams)`
   (`RaceUI.cs:150-154`, `CameraModeForRole(false)` = `AutoAllCams`).
2. Host broadcasts `race_start`; student handles it in `NetworkSync.HandleRaceStart`
   (`Assets/Scripts/Network/NetworkSync.cs:390`) → `RaceManager.LoadAndStartRaceVisualOnly(...)`.
3. **`LoadAndStartRaceVisualOnly` (`Assets/Scripts/Race/RaceManager.cs:235-242`) spawns visual cars
   via `CarSpawner.SpawnVisualCars` but NEVER calls `ScoreManager.RegisterCar`.** The host path
   `LoadAndStartRace` (`RaceManager.cs:88-118`) registers each car (`RaceManager.cs:98`).
4. `NetworkSync.HandleStateUpdate` (`NetworkSync.cs:440-462`) DOES sync race progress onto each
   spawned car's `CarIdentity` (`id.CurrentLap = cs.l; id.TotalCheckpointsPassed = cs.c;`,
   `NetworkSync.cs:457-458`) — so the ranking's PRIMARY key already exists on the student's cars.
5. But `SpectatorCamera.UpdateTarget()` (`Assets/Scripts/Camera/SpectatorCamera.cs:141-178`) ranks via
   `ScoreManager.GetRankedCars()` — which returns EMPTY on the student because step 3 registered
   nothing. Both auto modes bail:
   - `AutoTopCars` (`ChaseTopN`): `ranked.Count == 0` → `return` (`SpectatorCamera.cs:154-155`).
   - `AutoAllCams` (`FixedPointsOnLeader`): same guard → `return`.
   `currentTarget` stays null → `LateUpdate` returns at `SpectatorCamera.cs:119` → frozen camera.
6. Click-to-follow still works because `SpecificCar` mode bypasses `ScoreManager`
   (`SpectatorCamera.cs:146-150`), matching the observed behavior (only the AUTO camera is dead).

### Why registering cars is sufficient
`SpawnVisualCars` (`Assets/Scripts/Race/CarSpawner.cs:156-181`) guarantees every spawned car has a
`CarIdentity` (`CarSpawner.cs:172-174`). `NetworkSync.HandleStateUpdate` mutates those same
`CarIdentity` instances every frame with `TotalCheckpointsPassed`. So once the cars are registered
into `ScoreManager`, `GetRankedCars()` (`ScoreManager.cs:22-28`, orders by `TotalCheckpointsPassed`
desc, then `CheckpointTime` desc) returns a correctly-ordered list and the auto camera acquires a
target. No other layer needs to change.

---

## UX Design

### Before
```
Student (WebGL) — Racing
┌───────────────────────────────────────┐
│  [race view — CAMERA FROZEN, no cars   │
│   traced, no cuts between cams]         │
│                                        │
│                        ┌─────────────┐ │
│                        │ Leaderboard │ │  ← works (cycles board)
│                        ├─────────────┤ │
│                        │ Auto: All   │ │  ← tap: label flips to
│                        │      Cam    │ │    "Auto: Top 3" but the
│                        └─────────────┘ │    camera never moves
└───────────────────────────────────────┘
```

### After
```
Student (WebGL) — Racing
┌───────────────────────────────────────┐
│  [race view — auto camera parks at the │
│   fixed cam near the leader / chases   │
│   the top-3, cutting on the interval]  │
│                        ┌─────────────┐ │
│                        │ Leaderboard │ │
│                        ├─────────────┤ │
│                        │ Auto: Top 3 │ │  ← tap: flips modes AND the
│                        └─────────────┘ │    camera visibly changes shot
└───────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Student default camera (on race start) | AutoAllCams selected but frozen (no target) | AutoAllCams parks at fixed cam near leader, aimed at it | Fix restores the intended default broadcast |
| "Auto Cam" button tap | Label flips; camera unchanged | Label flips; camera switches between top-3 chase and all-cams-on-leader | Button already fired `ToggleAutoSwitch`; only targets were missing |
| Esc (→ `AutoTopCars`, `CameraManager.ModeForEscape(false)`) | Returns to a frozen auto mode | Returns to a working top-3 chase | Same root fix |
| Click-to-follow a team | Works (SpecificCar bypasses ScoreManager) | Unchanged | Already functional |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/Race/RaceManager.cs` | 88-118, 235-242 | The fix site (`LoadAndStartRaceVisualOnly`) + the host reference (`LoadAndStartRace`) to mirror |
| P0 (critical) | `Assets/Scripts/Race/ScoreManager.cs` | 13-45 | `RegisterCar` / `GetRankedCars` / `Clear` contract the fix relies on |
| P0 (critical) | `Assets/Scripts/Camera/SpectatorCamera.cs` | 141-178 | Confirms both auto modes early-return on empty ranking |
| P1 (important) | `Assets/Scripts/Network/NetworkSync.cs` | 390-462 | `HandleRaceStart` (calls the fix site) + `HandleStateUpdate` (syncs `TotalCheckpointsPassed`) |
| P1 (important) | `Assets/Scripts/Race/CarSpawner.cs` | 156-181 | `SpawnVisualCars` guarantees each car has a `CarIdentity`; needs `CarPrefabs`/`Config`/`SpawnPoint` |
| P2 (reference) | `Assets/Scripts/UI/RaceUI.cs` | 321-398 | The student touch-control button build + `OnAutoCamButton` (confirms click path is fine) |
| P2 (reference) | `Assets/Tests/EditMode/ScoreManagerTests.cs` | 1-232 | Test style: EditMode NUnit, `CarIdentity` fixtures via `AddComponent` + `Initialize` |
| P2 (reference) | `Assets/Tests/EditMode/CameraRoleDecisionTests.cs` | 1-38 | Test style for pure camera-role decisions |

## External Documentation

No external research needed — the fix uses established internal patterns (mirror the host
`ScoreManager.RegisterCar` loop already present in `LoadAndStartRace`).

---

## Patterns to Mirror

### REGISTER_CARS_INTO_SCOREMANAGER (host path — mirror this on the student path)
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:90-99
spawnedCars = CarSpawner.SpawnCars(carDataList);

foreach (var car in spawnedCars)
{
    var identity = car.GetComponent<CarIdentity>();
    // Seed the lap-timing clock to race start (Time.time, same frame as raceStartTime
    // below) so the first completed lap measures its true duration, not the whole clock.
    identity.LastLapStartTime = Time.time;
    ScoreManager.RegisterCar(identity);
}
```
> NOTE for the student path: DO NOT seed `LastLapStartTime` or start lap timing — the student is
> visual-only; laps/checkpoints arrive pre-computed via `HandleStateUpdate`. Only the
> `ScoreManager.RegisterCar` part is relevant.

### CURRENT_STUDENT_PATH (the fix site — before)
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:235-242
public void LoadAndStartRaceVisualOnly(List<CarData> carDataList)
{
    spawnedCars = CarSpawner.SpawnVisualCars(carDataList);
    raceStarted = true;
    raceFinished = false;
    SetState(GameState.Racing);
    Debug.Log($"[RaceManager] Visual-only race started with {spawnedCars.Count} cars");
}
```

### SCOREMANAGER_CONTRACT
```csharp
// SOURCE: Assets/Scripts/Race/ScoreManager.cs:17-28,42-45
public void RegisterCar(CarIdentity car) { cars.Add(car); }

public List<CarIdentity> GetRankedCars()
    => cars.OrderByDescending(c => c.TotalCheckpointsPassed)
           .ThenByDescending(c => c.CheckpointTime)
           .ToList();

public void Clear() { cars.Clear(); }
```

### TEST_STRUCTURE (EditMode NUnit + CarIdentity fixture)
```csharp
// SOURCE: Assets/Tests/EditMode/ScoreManagerTests.cs:29-39
private CarIdentity CreateCar(string name, int checkpoints, float time, int lap = 0)
{
    var obj = new GameObject(name);
    carObjects.Add(obj);                 // tracked for TearDown DestroyImmediate
    var identity = obj.AddComponent<CarIdentity>();
    identity.Initialize(new CarData(name, Array.Empty<AttributeEntry>()));
    identity.TotalCheckpointsPassed = checkpoints;
    identity.CheckpointTime = time;
    identity.CurrentLap = lap;
    return identity;
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Register spawned visual cars into `ScoreManager` inside `LoadAndStartRaceVisualOnly` |
| `Assets/Tests/EditMode/RaceManagerVisualSpawnTests.cs` | CREATE | Regression test proving the student path populates `ScoreManager` so ranking is non-empty |

## NOT Building

- **No change to the click/EventSystem path** — it already works (ruled out in Root Cause).
- **No change to `AllowFreeControl` gating** — the button path is not gated by it.
- **No syncing of `CheckpointTime`** across the network. Ranking's PRIMARY key
  (`TotalCheckpointsPassed`) is already synced and is sufficient for correct leader/top-N selection.
  Syncing the `CheckpointTime` tiebreaker (adding a field to `StateUpdateMessage` +
  `HandleStateUpdate`) is a possible future polish for smoother ordering among cars tied on
  checkpoints, but is OUT OF SCOPE for this fix.
- **No change to `SpectatorCamera`** — it is correct; it was starved of data.
- **No new UI, no relabeling, no scene/prefab edits.**

---

## Step-by-Step Tasks

### Task 1: Register visual cars into ScoreManager on the student path
- **ACTION**: Edit `LoadAndStartRaceVisualOnly` in `Assets/Scripts/Race/RaceManager.cs` (lines 235-242).
- **IMPLEMENT**: After `spawnedCars = CarSpawner.SpawnVisualCars(carDataList);`, clear then register
  each spawned car's `CarIdentity` into `ScoreManager` (guard against a null `ScoreManager`). Clear
  first so a re-join / second `race_start` does not accumulate stale/duplicate entries.
  ```csharp
  public void LoadAndStartRaceVisualOnly(List<CarData> carDataList)
  {
      spawnedCars = CarSpawner.SpawnVisualCars(carDataList);

      // Register the visual cars so the student's ScoreManager can rank them. Progress
      // (TotalCheckpointsPassed) is synced onto these same CarIdentity objects by
      // NetworkSync.HandleStateUpdate, so ranking — and therefore the auto broadcast camera's
      // leader / top-N target selection — works on the student client. Without this the student's
      // ScoreManager stays empty, GetRankedCars() returns [], and SpectatorCamera's AutoTopCars /
      // AutoAllCams modes have no target (the auto-cam button appears dead). Clear first so a
      // reconnect / re-sent race_start rebuilds the roster instead of appending duplicates.
      if (ScoreManager != null)
      {
          ScoreManager.Clear();
          foreach (var car in spawnedCars)
          {
              var identity = car.GetComponent<CarIdentity>();
              if (identity != null) ScoreManager.RegisterCar(identity);
          }
      }

      raceStarted = true;
      raceFinished = false;
      SetState(GameState.Racing);
      Debug.Log($"[RaceManager] Visual-only race started with {spawnedCars.Count} cars");
  }
  ```
- **MIRROR**: `REGISTER_CARS_INTO_SCOREMANAGER` (host path `RaceManager.cs:90-99`), minus the
  `LastLapStartTime` seeding (student is visual-only, laps arrive pre-computed).
- **IMPORTS**: None new — `RaceManager.cs` already `using System.Collections.Generic;` and references
  `ScoreManager` / `CarIdentity`.
- **GOTCHA**: Do NOT seed `identity.LastLapStartTime` or hook `LapTracker.OnLapCompleted` here — the
  student never runs lap/checkpoint detection; those values are network-synced. Keep the null guard
  on `ScoreManager` (defensive; the scene wires it via `TrackSetupEditor.cs:511`, but a stripped test
  scene may not).
- **VALIDATE**: `GetRankedCars().Count` equals the number of visual cars after this call (Task 2 test).

### Task 2: Add a regression test for the student-path registration
- **ACTION**: Create `Assets/Tests/EditMode/RaceManagerVisualSpawnTests.cs`.
- **IMPLEMENT**: Build a minimal `RaceManager` + `CarSpawner` + `ScoreManager` + `RaceConfig` in
  EditMode. Give `CarSpawner` a one-entry `CarPrefabs` array pointing at a stub prefab GameObject that
  has a `CarIdentity`, a `Config` (`RaceConfig` with `CarScale = 1`), and leave `SpawnPoint` null
  (falls back to `Vector3.zero`, per `CarSpawner.cs:165`). Call
  `raceManager.LoadAndStartRaceVisualOnly(carDataList)` with 2-3 `CarData` entries and assert:
  1. `scoreManager.GetRankedCars().Count == carDataList.Count` (roster registered — the core regression).
  2. After setting `TotalCheckpointsPassed` on the spawned cars (simulating `HandleStateUpdate`),
     `GetRankedCars()[0]` is the car with the most checkpoints (proves the auto camera would trace
     the true leader).
  3. Calling `LoadAndStartRaceVisualOnly` twice does NOT double the roster (proves the `Clear()`
     guard — re-join safety).
- **MIRROR**: `TEST_STRUCTURE` (`ScoreManagerTests.cs:29-39`) for `CarIdentity` fixtures and the
  `[SetUp]`/`[TearDown]` + tracked-objects `DestroyImmediate` cleanup pattern.
- **IMPORTS**: `using System; using System.Collections.Generic; using NUnit.Framework; using UnityEngine;`
- **GOTCHA**: `CarSpawner.SpawnVisualCars` also calls `AddTrailRenderer` (needs a trail shader; logs a
  warning and continues if none found — harmless in tests). `RaceManager.LoadAndStartRaceVisualOnly`
  calls `SetState(GameState.Racing)` which invokes `OnStateChanged` — no subscribers in the test, so
  it is a safe no-op. Use `Object.DestroyImmediate` in `[TearDown]` on every created GameObject
  (managers, prefab stub, and each spawned car) to avoid leaking objects between tests. The stub
  prefab must carry `CarIdentity` so `SpawnVisualCars`' `GetComponent<CarIdentity>()` path is exercised
  realistically (it will `AddComponent` if missing, but include it to mirror production prefabs).
- **VALIDATE**: The three asserts pass in the EditMode run (Validation Commands below).

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `LoadAndStartRaceVisualOnly_RegistersCarsInScoreManager` | 3 `CarData` entries | `GetRankedCars().Count == 3` | No (core regression) |
| `LoadAndStartRaceVisualOnly_RankingReflectsSyncedCheckpoints` | 3 cars, set `TotalCheckpointsPassed` = 2/9/5 | `GetRankedCars()[0]` = the 9-checkpoint car | No |
| `LoadAndStartRaceVisualOnly_CalledTwice_DoesNotDuplicateRoster` | Call twice with same 2 cars | `GetRankedCars().Count == 2` | Yes (reconnect / re-sent `race_start`) |
| `LoadAndStartRaceVisualOnly_NullScoreManager_DoesNotThrow` | `RaceManager.ScoreManager = null` | No exception; cars still spawned | Yes (defensive null guard) |

### Edge Cases Checklist
- [x] Empty input (0 cars) → `GetRankedCars().Count == 0`, no throw (loop is a no-op).
- [x] Re-join / duplicate `race_start` → `Clear()` prevents roster duplication.
- [x] Null `ScoreManager` → guarded, no throw.
- [ ] Maximum size input — not meaningful (grid size is small, teacher-defined).
- [x] Ranking correctness with synced `TotalCheckpointsPassed` only (no `CheckpointTime`) — leader is
      still correct because checkpoints are the primary key.
- [ ] Concurrent access — N/A (single-threaded Unity main loop).
- [ ] Permission denied — N/A.

---

## Validation Commands

### Static Analysis / Compile
```bash
# Preferred: trigger a compile/refresh via the UnitySkills REST API (Editor must be running)
curl -s http://localhost:8090/health
# then run the project's script-refresh / compile endpoint (see .claude/skills/unity-skills/SKILL.md)
```
EXPECT: No compile errors in `RaceManager.cs` or the new test file.

### Unit Tests (EditMode — BLOCKING per coding standards: this is Logic)
```bash
# Preferred path: run EditMode tests through UnitySkills API (localhost:8090).
# See .claude/skills/unity-skills/SKILL.md for the test-run endpoint.
# CI equivalent: game-ci/unity-test-runner@v4 (EditMode), per .claude/docs/coding-standards.md.
```
EXPECT: `RaceManagerVisualSpawnTests` (all cases) pass; `ScoreManagerTests`,
`CameraRoleDecisionTests`, `StudentTouchControlTests` still green (no regressions).

### Manual / Runtime Validation (WebGL student view)
Per the recalled memory `unity-playmode-verification`: verify runtime camera behavior via the
UnitySkills API (set `runInBackground`, drive a race, use `camera_screenshot` / play-mode) rather
than by static reasoning alone.
- [ ] Launch a host race (professor), then open the student join link (`#room=CODE&role=play`) in a
      second browser tab / the in-editor student simulation.
- [ ] Confirm the student's default camera (AutoAllCams) parks at a fixed cam near the leader and
      re-aims as the leader moves (was frozen before).
- [ ] Tap "Auto Cam": camera switches to the top-3 chase (label → "Auto: Top 3") and visibly trails
      cars; tap again → back to all-cams-on-leader (label → "Auto: All Cam").
- [ ] Confirm click-to-follow (tap a team row in fullscreen leaderboard) still works, and Esc returns
      to a WORKING top-3 chase.

---

## Acceptance Criteria
- [ ] `LoadAndStartRaceVisualOnly` registers every spawned visual car into `ScoreManager`.
- [ ] Student auto camera (AutoAllCams default) traces the leader on race start.
- [ ] Student "Auto Cam" button visibly switches between top-3 chase and all-cams-on-leader.
- [ ] Regression test file added and passing; no existing tests regress.
- [ ] No compile/type errors.
- [ ] Click-to-follow and Esc behavior unchanged.

## Completion Checklist
- [ ] Code mirrors the host `ScoreManager.RegisterCar` pattern (minus lap-timing seed).
- [ ] Null-guard on `ScoreManager` matches the codebase's defensive style.
- [ ] `Clear()` before register prevents duplicate rosters on reconnect.
- [ ] Test follows `ScoreManagerTests` EditMode + `DestroyImmediate` teardown pattern.
- [ ] No hardcoded values beyond boundary test data.
- [ ] Doc comment on `LoadAndStartRaceVisualOnly` explains WHY registration is needed (camera target).
- [ ] No scope creep (no `CheckpointTime` sync, no UI/scene edits).
- [ ] Self-contained — implementable from this plan without further searching.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Ranking tiebreaker imperfect because `CheckpointTime` isn't synced | Medium | Low | Primary key `TotalCheckpointsPassed` is synced → leader is correct; tiebreaker only matters between cars on the same checkpoint. Documented as out-of-scope follow-up. |
| Duplicate registration on reconnect / re-sent `race_start` | Medium | Medium | `ScoreManager.Clear()` before the register loop. |
| EditMode test needs prefab/Config fixtures and may be brittle | Low | Low | Use a minimal stub prefab with `CarIdentity`, `RaceConfig` with `CarScale=1`, null `SpawnPoint` (falls back to origin). Trail shader warning is harmless. |
| The frozen camera has a *second*, independent cause (e.g. cars not moving) | Low | Medium | Runtime WebGL validation step confirms tracing end-to-end; `HandleStateUpdate` already proves positions + checkpoints are synced onto the cars. |

## Notes
- The bug is a data-wiring omission, not a camera or UI logic error: the student path
  (`LoadAndStartRaceVisualOnly`) diverged from the host path (`LoadAndStartRace`) and dropped the
  `ScoreManager.RegisterCar` loop, while `SpectatorCamera` and the touch-button click path were both
  already correct.
- The professor is unaffected because the host path registers cars normally, so the professor's
  Auto Cam ('C' key) always had a populated `ScoreManager`.
- Confidence this is THE root cause is high: every symptom (frozen auto cam, dead-looking button that
  still relabels, working click-to-follow, working Leaderboard button) is explained by an empty
  student `ScoreManager` combined with `SpecificCar` bypassing it.
