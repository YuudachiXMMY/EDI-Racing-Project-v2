# Implementation Report: Toggle Car Name Labels (Button + Hotkey)

## Summary
Added a professor-facing HUD button ("Names: On/Off") and an `N` keyboard hotkey that toggle all
car name labels on/off at runtime. Labels remain shown by default. Button and hotkey drive the same
single source of truth (`CarLabelSpawner.LabelsVisible`). Implemented exactly per plan.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Small | Small |
| Confidence | 8/10 | 9/10 (implementation clean; only runtime-validation caveat below) |
| Files Changed | 4 changed + 1 new test = 5 | 3 changed + 1 new test = 4 |

Note: file count came out at 4 (not 5) — the plan double-counted; `CarLabelSpawner.cs`,
`RaceControlPanel.cs`, `TrackSetupEditor.cs` are the 3 edits, plus the 1 new test file.

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | CarLabelSpawner: state + hotkey + API + spawn re-apply | Complete | Added `using UnityEngine.InputSystem;`, `ToggleLabelsKey = Key.N`, `labelsVisible`, `Update()`, `ToggleLabels`/`SetLabelsVisible`/`LabelsVisible`, and the SpawnLabels re-apply block |
| 2 | RaceControlPanel: button + handler | Complete | Added `ToggleNamesButton`/`ToggleNamesLabel`, auto-resolved `CarLabelSpawner` ref, listener, and `ToggleNames()` mirroring `ToggleAutoCam` |
| 3 | TrackSetupEditor: create + wire button, widen panel | Complete | Panel 560→660px (`-330..330`), `ToggleNamesBtn` at x=490..600, `cp.CarLabelSpawner` wired |
| 4 | EditMode unit test | Complete | `CarLabelSpawnerToggleTests` — 4 tests |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis | Pass | Brace balance verified on all 4 files; new symbols coherent across caller/callee; `Tests.asmdef` already references `EDIRacing.Runtime` + `Unity.InputSystem`, so the test compiles without asmdef changes; runtime asmdef already uses InputSystem (RaceManager/CameraManager/LeaderboardPanel), so the new `using` compiles |
| Unit Tests | Written, not executed here | 4 EditMode tests authored; see caveat below — will run in CI (game-ci) |
| Build | Not executed here | See caveat |
| Integration | N/A | No new integration surface |
| Edge Cases | Pass (by inspection) | Empty label list, null labels, no `Keyboard.current`, missing spawner ref — all guarded; `Update()` does not run in EditMode so the test is unaffected |

### Validation caveat (important, honest)
The running Unity Editor (UnitySkills API at `localhost:8090`) has the **main checkout** open, not this
worktree. Compiling or running tests through it would validate main (which lacks these changes), and
copying worktree files into the main checkout would pollute the user's working copy. So Unity
compile/EditMode-run was **not** executed against this diff. Validation here is static + logic-level.
The 4 EditMode tests will execute on the PR via `game-ci/unity-test-runner` (per coding-standards.md CI rules).

## Files Changed

| File | Action | Lines |
|---|---|---|
| `Assets/Scripts/UI/CarLabelSpawner.cs` | UPDATED | +34 |
| `Assets/Scripts/UI/RaceControlPanel.cs` | UPDATED | +17 |
| `Assets/Scripts/Editor/TrackSetupEditor.cs` | UPDATED | +11 / -2 |
| `Assets/Tests/EditMode/CarLabelSpawnerToggleTests.cs` | CREATED | +54 |

## Deviations from Plan
- **File count**: 4 total, not 5 (plan's "4 changed + 1 test" double-counted). No functional deviation.
- **Unity runtime validation deferred to CI** rather than run locally — see caveat above. This was not
  foreseen in the plan (the plan assumed the API targets this checkout).

## Issues Encountered
- **New test `.cs` has no `.meta`**: Unity generates `.meta` files on asset import, but the Editor is on
  the main checkout, so the worktree file's `.meta` isn't generated yet. Unity will generate it on import
  when the branch is opened/merged (and CI generates it on import too). No GUID is referenced by anything,
  so this is cosmetic. Follow-up: let Unity generate the `.meta` on first import of the branch.
- **Existing pre-built scenes**: `WireOrCreateControlPanel` early-returns an existing panel without
  re-wiring, so a scene that already ships a `RaceControlPanel` won't get the new button GameObject from
  the editor path. Mitigated by `RaceControlPanel.Start()` auto-resolving `CarLabelSpawner`; the button
  GameObject itself still needs a full Setup-Track rebuild (or manual/UnitySkills add) in such scenes.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `Assets/Tests/EditMode/CarLabelSpawnerToggleTests.cs` | 4 tests | Default-visible; single toggle hides; double toggle restores; `SetLabelsVisible(false)` hides |

## Next Steps
- [ ] CI (game-ci) runs the EditMode suite on the PR — confirm green.
- [ ] In-editor manual walkthrough (ADVISORY, per test standards): start race, press `N`, click the button, verify 5-button bar with no StatusText overlap.
- [ ] Let Unity generate the new test file's `.meta` on branch import.
- [ ] Code review via `/code-review` or merge PR #13.
