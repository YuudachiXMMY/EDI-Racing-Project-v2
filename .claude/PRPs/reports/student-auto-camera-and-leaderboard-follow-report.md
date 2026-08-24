# Implementation Report: Student Auto-Camera Default + Fullscreen-Leaderboard Click-to-Follow

## Summary
Implemented the WebGL student-view camera/leaderboard feature per plan: students now default to the
auto-switching broadcast camera (`AutoTopCars`), have a discoverability hint for the Tab-resized
leaderboard, and — for both professor and student — clicking a car's name in the **fullscreen**
leaderboard follows that car in a 3rd-person chase and shrinks the board. All Unity C#; no web-app,
scene, or prefab edits.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium |
| Confidence | 8/10 | Confirmed — top runtime risk (student `SpawnedCars`) verified populated |
| Files Changed | 7 (+2 meta) | 7 source (4 mod, 3 new) + 3 new meta |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | SpectatorCamera `SpecificCar` follow mode | Complete | `SetFollowTarget`; SpecificCar branch above ScoreManager guard |
| 2 | CameraManager `FollowCar` + `AllowFreeControl` gate | Complete | Esc→role default; students can't fly with F/C |
| 3 | `CarLookup.FindByTeamName` pure resolver | Complete | New file `Assets/Scripts/UI/CarLookup.cs` |
| 4 | Clickable fullscreen leaderboard rows → `OnCarSelected` | Complete | Per-row `Button`, method-param index (no closure bug), Fullscreen-gated |
| 5 | RaceUI student default auto-cam + click-to-follow wiring | Complete | Pure `CameraModeForRole`; `HandleCarSelected` resolves + drives camera |
| 6 | Student on-screen hint (Tab + click-to-follow + Esc) | Complete | Runtime `StudentHint`, ASCII, mirrors `BuildCameraHint` |
| 7 | `CarLookupTests` (EditMode) | Complete | 9 tests (6 planned + 3 review-added) |
| 8 | `CameraRoleDecisionTests` (EditMode) | Complete | 4 tests (2 planned + 2 for `ModeForEscape`) |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis (compile) | Not run headlessly | UnitySkills API (localhost:8090) down; batchmode reimport of a fresh worktree impractical. Substituted: brace/paren balance check (all files balanced) + full-file re-reads + reference/wiring greps. Run in-Editor or CI to confirm. |
| Unit Tests | Not run headlessly | 13 EditMode tests authored (pure/`GameObject`-only, deterministic). Run via `Unity -batchmode -runTests -testPlatform EditMode`. |
| Adversarial Review | Pass (after fixes) | 9-agent workflow, 4 lenses + verify; 5 findings all confirmed and **all fixed** (see below). |
| Build | Not run | WebGL build verify command specified in plan. |
| Edge Cases | Reasoned + covered | Null/blank team, no-match, null list entry, no-`CarIdentity`, duplicate, Normal/Enlarged click ignored, despawned target. |

## Files Changed

| File | Action | Approx Lines |
|---|---|---|
| `Assets/Scripts/Camera/SpectatorCamera.cs` | UPDATE | +25 |
| `Assets/Scripts/Camera/CameraManager.cs` | UPDATE | +45 (incl. Awake move + `ModeForEscape`) |
| `Assets/Scripts/UI/LeaderboardPanel.cs` | UPDATE | +35 |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | +45 |
| `Assets/Scripts/UI/CarLookup.cs` (+ .meta) | CREATE | +26 |
| `Assets/Tests/EditMode/CarLookupTests.cs` (+ .meta) | CREATE | +130 |
| `Assets/Tests/EditMode/CameraRoleDecisionTests.cs` (+ .meta) | CREATE | +40 |

## Deviations from Plan
- **`CameraManager` self-init moved from `Start()` to `Awake()`** (not in the plan). Required by review
  Finding 1: the plan's `RaceUI.ApplyRole()` sets the student's camera in `Start()`, which races the
  pre-existing `CameraManager.Start()` default `SetMode(Free)` under Unity's undefined cross-GameObject
  `Start()` order. `Awake()` deterministically precedes every `Start()`, so the consumer's mode wins.
- **Added pure `CameraManager.ModeForEscape(bool)`** (review Finding 5) so the Esc-return rule is
  unit-tested rather than only inlined in `Update()`.
- **Row click wiring uses a `MakeRowClickable(row, index)` helper** with `index` as a method parameter
  (plan captured the loop var into a local). Same effect, avoids the closure-over-loop-variable pitfall
  by construction.

## Issues Encountered
- **Headless Unity compilation/tests unavailable** — UnitySkills API down; a batchmode run needs an
  exclusive project lock + license + full reimport of a cacheless worktree. Mitigated with rigorous
  static review, an adversarial multi-agent review, and balance/wiring checks. Left for in-Editor/CI.
- **Adversarial review — 5 confirmed findings, all fixed:**
  1. (HIGH) `CameraManager` Start-order race → moved self-init to `Awake()`.
  2. (LOW) `CarLookupTests.MakeCar` set object name == team name → made distinct + added a match-key test.
  3. (LOW) Case-sensitivity untested → added `FindByTeamName_CaseMismatch_ReturnsNull`.
  4. (LOW) `identity != null` branch untested → added `FindByTeamName_EntryWithoutCarIdentity_IsSkipped`.
  5. (LOW) Esc rule untested → extracted `ModeForEscape` + 2 tests.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `CarLookupTests.cs` | 9 | Match, unknown, null list, blank name, null entry, duplicate→first, match-key (TeamName not object name), case-sensitivity, no-`CarIdentity` skip |
| `CameraRoleDecisionTests.cs` | 4 | `CameraModeForRole` professor→Free / student→AutoTopCars; `ModeForEscape` free→Free / gated→AutoTopCars |

## Runtime Facts Verified During Implementation
- `RaceManager.SpawnedCars` (public `List<GameObject>`) is populated on the **student** client via
  `CarSpawner.SpawnVisualCars` (`RaceManager.cs:191`), host via `SpawnCars` (`:90`); each car has a
  `CarIdentity.TeamName` == `LeaderboardEntry.name`, so click-to-follow resolves for both roles.
- `EventSystem` + `GraphicRaycaster` already exist in the scene (`TrackSetupEditor`), so UGUI row
  clicks work with no new scene wiring.

## Next Steps
- [ ] Run EditMode tests in the Editor / CI (`game-ci/unity-test-runner`) — the one gap not runnable here.
- [ ] Play-mode manual pass (student auto-cam, Tab resize, click-to-follow, Esc→auto) per the plan checklist.
- [ ] Review via `/code-review` if desired; PR is already open (#12).
