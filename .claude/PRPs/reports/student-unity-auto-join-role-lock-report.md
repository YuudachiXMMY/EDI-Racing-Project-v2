# Implementation Report: Student Unity Auto-Join + Role Lock (Phase 5)

## Summary
Implemented Phase 5 of `role-bound-game-links`: the Unity WebGL client now reads the student 3D link hash (`/#room=<CODE>&role=play`, no host token — shipped in Phase 4), auto-`JoinRoom`s as an anonymous spectator, and **hard-locks** the client to the Student role — hiding the `EventPanel` / race controls / Host Setup screen so no host controls exist and `IsHost` can never flip to Professor. Mirrors Phase 2's `HostLaunchBootstrap`: a new `StudentJoinBootstrap` + pure `StudentJoinDecision`, reusing `HostLaunchParams.ParseHash`, `NetworkManager.JoinRoom` (already sets `IsHost=false`), and `RaceUI.ApplyRole`. **No web-app or server changes** (the URL/landing page and the server `join_room` role gating already exist). Two hardening fixes beyond the happy path: a `roleLocked` one-way guard in `RaceUI`, and a role-gated host-resume fallback in `HostLaunchBootstrap` so a student link opened in a previously-host browser is not hijacked into resuming the old host room.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium — as predicted |
| Confidence | 9/10 | Held — no blocking surprises |
| Files Changed | 6 (3 new .cs + 3 meta, 2 updated .cs, 1 scene wiring) | 8 tracked (2 new .cs + 3 meta, 2 updated .cs, 1 new test .cs) — scene wiring deferred to runtime QA |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | `StudentJoinDecision.cs` (pure) | Complete | `ShouldAutoJoin(role,room)` → `role=="play" && !blank(room)` |
| 2 | `StudentJoinBootstrap.cs` (MonoBehaviour) | Complete | parse hash → `JoinRoom(room,"")` → `LockAsStudent()`; hash intentionally retained |
| 3 | `RaceUI` role-lock hardening | Complete | +`roleLocked` guard, +`LockAsStudent()`; **also** gated `OnStateChanged` Setup visibility on role (deviation, see below) |
| 4 | `HostLaunchBootstrap` resume guard | Complete | fallback gated with `role != "play"` |
| 5 | `StudentJoinDecisionTests.cs` (EditMode) | Complete (written); EditMode run deferred | 7 tests |
| 6 | Scene wiring | **Deferred** (runtime QA) | Unity editor bound to main checkout, not this worktree — same posture as Phase 2/3/4 |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis (brace/paren balance, symbol resolution) | Pass | All 5 changed .cs balanced; every referenced symbol (`ParseHash`, `GetPageUrl`, `JoinRoom`, `LockAsStudent`, `ShouldAutoJoin`) resolves to a real signature |
| Unity Compile | **Not run** (no standalone `dotnet`/`csc`; UnitySkills editor bound to main checkout, not worktree) | Verified by inspection/hand-trace; matches Phase 4 posture |
| Unit Tests (Unity EditMode) | **Not run** (blocked) | 7 tests written; run via Editor Test Runner / Bypass in runtime QA |
| Web-app regression | N/A (Pass by exclusion) | Zero web-app/server files changed — nothing to regress |
| Edge Cases | Pass (by construction) | 7 decision cases covered by tests; reload/dead-room/stale-host reasoned through |

## Files Changed

| File | Action | Lines |
|---|---|---|
| `Assets/Scripts/UI/StudentJoinDecision.cs` | CREATED | +17 |
| `Assets/Scripts/UI/StudentJoinDecision.cs.meta` | CREATED | +2 |
| `Assets/Scripts/UI/StudentJoinBootstrap.cs` | CREATED | +50 |
| `Assets/Scripts/UI/StudentJoinBootstrap.cs.meta` | CREATED | +2 |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATED | +27 / -3 |
| `Assets/Scripts/UI/HostLaunchBootstrap.cs` | UPDATED | +4 / -1 |
| `Assets/Tests/EditMode/StudentJoinDecisionTests.cs` | CREATED | +64 |
| `Assets/Tests/EditMode/StudentJoinDecisionTests.cs.meta` | CREATED | +2 |

## Deviations from Plan

1. **`OnStateChanged` Setup visibility gated on role (RaceUI)** — not in the plan's Task 3 sketch. **Why**: `OnStateChanged` unconditionally ran `Setup.SetActive(isSetup)`, so a locked student sitting in `GameState.Setup` would have the Host Setup screen **re-shown** immediately after `ApplyRole()` hid it — defeating the lock. Added `&& isProfessor`. Required for the hard-lock to actually hold; professor behavior unchanged.
2. **Scene wiring (Task 6) deferred to runtime QA** — as flagged in the plan's own Task 6 GOTCHA; the Unity editor is bound to the main checkout, so wiring + the EditMode run happen there (Phase 7).

## Issues Encountered
- **No standalone C# compiler and UnitySkills editor bound to main checkout** → could not run a true Unity compile/EditMode pass against the worktree. Mitigated with brace/paren balance checks + symbol-resolution grep + hand-tracing every referenced API signature. Same constraint and mitigation as Phase 4.
- **`.cs.meta` hand-generated** (fresh `openssl`-random GUIDs, 2-line project format) since Unity is not importing this worktree.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `Assets/Tests/EditMode/StudentJoinDecisionTests.cs` | 7 | `ShouldAutoJoin`: play+room→true; empty/whitespace/null room→false; host role→false; empty role→false; `student` alias→false (locks the `play`-only contract) |

## Runtime-QA-Pending (not blockers, per Phase 2/3/4 precedent)
- [ ] Run `StudentJoinDecisionTests` (7) via Editor Test Runner / Bypass on a checkout with these changes.
- [ ] Scene wiring: attach `StudentJoinBootstrap` to the launch GameObject in `complete_track_demo.unity` alongside `HostLaunchBootstrap`; assign `NetworkManager` + `RaceUI`.
- [ ] Runtime flow: open the "进入 3D 游戏" link → auto-joins, JoinScreen dismisses, no EventPanel/Controls/Setup; reload → auto-rejoins.
- [ ] Adversarial (Phase 7): `event_triggered`/`create_room` from the student socket rejected (Phase 1 token backstop); `edi-was-host="1"` + student link → joins as student, not host.

## Next Steps
- [ ] Code review via `/code-review`
- [ ] Create PR via `/prp-pr` (extends the existing Phase 5 branch / PR #45)
- [ ] Phase 6 (student 2D wiring) — independent sibling off the same landing page
