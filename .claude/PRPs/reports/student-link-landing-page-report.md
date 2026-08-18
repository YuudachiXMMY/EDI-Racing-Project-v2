# Implementation Report: Student Link + Landing Page (Phase 4)

## Summary
Implemented Phase 4 of `role-bound-game-links`: a shareable **student link** (room code only, **no host token**) surfaced on the Unity professor host screen with a copy-to-clipboard button, and a new public **React landing page** `/survey/#/join/:roomCode` offering a **"进入 3D 游戏"** vs **"2D 观战"** choice. Neither path exposes host UI. All web-app validation (unit tests, lint, build) passes; Unity EditMode run + scene wiring are runtime-QA-pending (Unity editor is bound to the main checkout, not this worktree — same posture as Phase 2/3).

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium — as predicted |
| Confidence | 8/10 | Held — no surprises; only env friction (worktree had no node_modules) |
| Files Changed | 10 (3 new, 7 modified) | 12 tracked (5 new incl. 2 `.meta`, 7 modified) |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | `buildStudentPlayUrl` in gameLaunch.js | Complete | |
| 2 | `JoinLandingPage.jsx` | Complete | |
| 3 | `/join/:roomCode` route in App.jsx | Complete | Public route (no ProtectedRoute) |
| 4 | Landing-page styles in index.css | Complete | Reuses `.btn-primary`, `--bg`/`--accent` |
| 5 | vitest for `buildStudentPlayUrl` | Complete | 3 tests added |
| 6 | jslib `GetPageOrigin` + `CopyToClipboard` | Complete | Comma added after HostAutoInject; brace balance verified |
| 7 | C# wrappers in WebSocketBridge.cs | Complete | Editor fallbacks (`""`, `GUIUtility.systemCopyBuffer`) |
| 8 | `StudentLinkBuilder.cs` (pure helper) | Complete | |
| 9 | SetupScreen host-screen link surface | Complete | Deviation — also hide link UI on reconnect-failed (see below) |
| 10 | `StudentLinkBuilderTests.cs` EditMode | Complete | 4 tests; `.cs.meta` hand-generated |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis | Pass | oxlint clean on JoinLandingPage/App/gameLaunch; C# `using`/asmdef/brace-balance verified by inspection |
| Unit Tests | Pass | web-app vitest: **37/37** (7 in game-launch.test.js incl. 3 new); Unity EditMode 4 tests written, run pending |
| Build | Pass | `vite build` — 56 modules, JoinLandingPage bundled, 0 errors |
| Integration | N/A | No server contract changed; link is client-composed; full professor→student flow is Phase 7 |
| Edge Cases | Pass | Empty origin→"", trailing-slash→no `//`, no-token asserted, dead-room handled by existing 2D Error phase |

### Commands run
- `cd web-app && npx vitest run` → 4 files, 37 tests passed
- `cd web-app/client && npx oxlint src/pages/JoinLandingPage.jsx src/App.jsx src/gameLaunch.js` → clean
- `cd web-app/client && npx vite build` → built in ~0.4s, 0 errors (pre-existing SurveyJS chunk-size warning only)

> Note: the worktree had no `node_modules`; validation ran by symlinking the main checkout's `web-app` and `web-app/client` node_modules (removed before commit — not committed).

## Files Changed

| File | Action | Lines |
|---|---|---|
| `web-app/client/src/gameLaunch.js` | UPDATED | +8 |
| `web-app/client/src/pages/JoinLandingPage.jsx` | CREATED | +29 |
| `web-app/client/src/App.jsx` | UPDATED | +2 |
| `web-app/client/src/index.css` | UPDATED | +10 |
| `web-app/__tests__/game-launch.test.js` | UPDATED | +17 / -1 |
| `Assets/Plugins/WebGL/WebSocketBridge.jslib` | UPDATED | +23 |
| `Assets/Plugins/WebGL/WebSocketBridge.cs` | UPDATED | +26 |
| `Assets/Scripts/UI/StudentLinkBuilder.cs` | CREATED | +17 |
| `Assets/Scripts/UI/StudentLinkBuilder.cs.meta` | CREATED | +2 |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATED | +33 |
| `Assets/Tests/EditMode/StudentLinkBuilderTests.cs` | CREATED | +31 |
| `Assets/Tests/EditMode/StudentLinkBuilderTests.cs.meta` | CREATED | +2 |

## Deviations from Plan
- **Task 9 (minor addition)**: also hide `StudentLinkText`/`CopyLinkButton` in `OnReconnectFailed` (alongside the existing RoomCode/StudentCount hide). **Why**: a failed reconnect means the room may have expired, so the stale student link should not remain visible — consistent with how the sibling UI is already treated. Not a behavior change to the plan's happy path.
- **`.cs.meta` files hand-generated**: the plan expected Unity to import the new files and generate metas. Since Unity is not running against this worktree, I created minimal two-line metas (matching this project's existing `.cs.meta` format) with fresh GUIDs so git/Unity stay consistent. Unity will accept or refresh them on next import.

## Issues Encountered
- **No `node_modules` in the worktree** → resolved by symlinking the main checkout's installed modules for the test/build run, then removing the symlinks before commit (never committed).
- **No standalone C# compiler (`dotnet`/`csc`) available** → `StudentLinkBuilder` logic verified by hand-tracing against the 4 EditMode assertions; full Unity EditMode run is runtime-QA-pending.
- **Unity editor bound to the main checkout, not the worktree** → Unity compile + EditMode + scene wiring deferred to runtime QA, matching the Phase 2/3 shipping posture.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `web-app/__tests__/game-launch.test.js` (extended) | 3 new | `buildStudentPlayUrl`: room+role hash, no query string, no token key |
| `Assets/Tests/EditMode/StudentLinkBuilderTests.cs` | 4 | `BuildJoinLink`: normal compose, trailing-slash origin, empty origin, empty room |

## Runtime-QA-Pending (not blockers, per Phase 2/3 precedent)
- [ ] Unity EditMode run of `StudentLinkBuilderTests` (4) via UnitySkills/Test Runner against a checkout with these changes.
- [ ] Scene wiring: attach `StudentLinkText` + `CopyLinkButton` to the `SetupScreen` in `complete_track_demo.unity` (prefer UnitySkills REST API `http://localhost:8090`).
- [ ] WebGL/HTTPS build: confirm the host screen shows "学生链接: …/survey/#/join/CODE" on room creation and the copy button writes to the clipboard.

## Next Steps
- [ ] Runtime QA above (Unity EditMode + scene wiring + WebGL copy)
- [ ] Phase 5 (Student Unity auto-join + role lock) and Phase 6 (2D wiring) — both depend on this landing page
- [ ] Code review via `/code-review`; PR draft #43 already open on this branch (extends the plan commit)
