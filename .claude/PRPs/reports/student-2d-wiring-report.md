# Implementation Report: Student 2D Wiring (Phase 6)

## Summary
Completed the 2D-spectator leg of the student landing page. Centralised the `/live/:roomCode`
route into a testable, uppercase-normalising `buildSpectatorPath` helper (symmetric with the 3D
`buildStudentPlayUrl`), routed the landing "2D 观战" `<Link>` through it, and added a "← 返回"
round-trip back-link on `LiveRacePage` → `#/join/:roomCode` so a 2D viewer can switch to 3D. The
2D spectator view and its WS path were reused unchanged and confirmed to expose no host controls.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Small (~5 files, ~50 lines) | Small — 5 files, ~40 lines |
| Confidence | 9/10 | Matched — single-pass, zero rework |
| Files Changed | 5 (0 new, 5 modified) | 5 (0 new, 5 modified) |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | Add `buildSpectatorPath` to gameLaunch.js | ✅ Complete | Router path, uppercase-normalised, no token |
| 2 | Route landing 2D choice through helper | ✅ Complete | Import + `<Link to={buildSpectatorPath(roomCode)}>` |
| 3 | Back-link on the 2D spectator page | ✅ Complete | `<Link className="live-back" to={`/join/${roomCode}`}>` |
| 4 | Unit-test `buildSpectatorPath` | ✅ Complete | 3 tests (normal, casing, router-path/no-token) |
| 5 | Add `.live-back` style | ✅ Complete | Reuses `--accent`/`--border` tokens |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis (oxlint) | ✅ Pass | Exit 0; zero new warnings in changed files (pre-existing warnings in unrelated files only) |
| Unit Tests (vitest) | ✅ Pass | 40/40 passed; `game-launch.test.js` 7 → 10 tests |
| Build (vite) | ✅ Pass | Built in 587ms; only pre-existing chunk-size advisory |
| Integration | N/A | Pure-frontend routing; 2D WS spectator path reused unchanged |
| Edge Cases | ✅ Pass | Casing, no-token, router-path asserted in unit tests |

## Files Changed

| File | Action | Lines |
|---|---|---|
| `web-app/client/src/gameLaunch.js` | UPDATED | +9 |
| `web-app/client/src/pages/JoinLandingPage.jsx` | UPDATED | +1 / -1 |
| `web-app/client/src/pages/LiveRacePage.jsx` | UPDATED | +2 / -1 |
| `web-app/client/src/index.css` | UPDATED | +3 |
| `web-app/__tests__/game-launch.test.js` | UPDATED | +17 / -1 |

## Deviations from Plan
None — implemented exactly as planned.

## Issues Encountered
- The worktree had no `node_modules` (gitignored, not copied into worktrees). Ran `npm install` in
  both `web-app` and `web-app/client` before lint/test/build. Resolved; no code impact.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `web-app/__tests__/game-launch.test.js` | +3 | `buildSpectatorPath`: normal route, uppercase normalisation, router-path/no-token invariants |

## Remaining Manual QA (unchanged from plan)
- [ ] Professor host launch → shared student link → "2D 观战" → live spectator dashboard for the room.
- [ ] "← 返回" round-trips to the choice page; "进入 3D 游戏" from there opens the game root.
- [ ] 2D URL is `/survey/#/live/<UPPERCASE-CODE>` regardless of the casing in the join link.
- [ ] No host controls reachable from the 2D page (verified statically; confirm in a live session).

## Next Steps
- [ ] Code review via `/code-review`
- [ ] Create PR via `/prp-pr` (branch `worktree-phase6-student-2d-wiring-plan`, draft PR #44 already open — push updates it)
