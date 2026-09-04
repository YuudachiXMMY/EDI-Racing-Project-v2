# Implementation Report: Student 2D View — Live Leaderboard + Clickable Car Status + Elliptical Minimap

## Summary
Implemented the full plan across two disjoint file groups run in parallel: the **Unity/upstream** slice (live speed `s` + one-time `track_geometry` broadcast) and the **Server + Web-client** slice (relay/replay + the actual React feature — clickable leaderboard, `CarDetailPanel`, upgraded elliptical `TrackMinimap` with real colors, precise start marker, and selected-car highlight). The web layer reads the two new upstream fields defensively with client-side fallbacks, so it is fully functional today and upgrades automatically once the Unity WebGL is rebuilt.

## Assessment vs Reality
| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Large | Large |
| Confidence | 8/10 (web slice) | Web slice landed single-pass, all gates green |
| Files Changed | ~15 | 16 (13 UPDATE + 3 CREATE) |
| Lines | ~ | +496 / -54 |

## Tasks Completed
| # | Task | Status | Notes |
|---|---|---|---|
| 1 | Unity `CarController.CurrentSpeed` | Complete | `agent.velocity.magnitude`; `agent` field verified |
| 2 | Unity `CarNetState.s` + populate | Complete | Compact key convention kept; auto-relays |
| 3 | Unity `TrackGeometryMessage` + broadcast + race-start call | Complete | Vector3[] flattened to `wpx/wpz`; host-gated |
| 4 | Unity EditMode round-trip tests | Complete | 5 tests (plan asked ≥2) |
| 5 | Server relay + cache + replay `track_geometry` | Complete | Whitelist + `latestTrackGeometry` + `web_join_room` replay |
| 6 | Server relay integration test | Complete | Live relay + late-join replay + negative case (3 new) |
| 7 | Web color/function constant mirrors | Complete | `CAR_COLORS`/`CAR_COLOR_NAMES`/`FUNCTION_LABELS` |
| 8 | Web pure `carStatus` util + tests | Complete | 13 unit tests, DOM-free (Node vitest) |
| 9 | Web hook handles `track_geometry` (+ speed fallback) | Complete — deviated | Speed fallback wired (plan marked optional) |
| 10 | Web lift selection + render `CarDetailPanel` | Complete | Toggle-on-repeat selection |
| 11 | Web clickable leaderboard rows | Complete | Select by `entry.name` |
| 12 | Web upgrade `TrackMinimap` | Complete | Real colors, fixed transform, ellipse, start, ring, click hit-test, DPR, pz→y flip |
| 13 | Web `CarDetailPanel` component | Complete | Graceful fallbacks (no functions / — rank / n/a / approx) |
| 14 | Web styles | Complete — deviated | Event feed moved to row2/col2 to seat the panel per UX mockup |
| 15 | (Optional) jsdom/RTL infra | Skipped (as planned) | Coverage via DOM-free util; approval-gated |

## Validation Results
| Level | Status | Notes |
|---|---|---|
| Static Analysis (lint) | Pass | `oxlint` exit 0; only pre-existing warnings in untouched files |
| Unit Tests (web) | Pass | **162 passed / 162** (18 files); new `carStatus.test.js` (13) + `adversarial-ws` (26, +3) — independently re-run by the parent session |
| Build (Vite) | Pass | `✓ built in 402ms`, 90 modules, no errors |
| Integration (WS relay) | Pass | Real-server `adversarial-ws` suite green incl. `track_geometry` live + replay |
| Unity EditMode | **Not verified here** | UnitySkills API points at the main checkout, not this worktree; 5 tests written + manual review (member names, brace balance) done. **Requires Unity rebuild against this worktree to confirm.** |

## Files Changed
| File | Action | Lines |
|---|---|---|
| `Assets/Scripts/Car/CarController.cs` | UPDATE | +8 |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | +17 |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | +69 |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | +3 |
| `Assets/Tests/EditMode/NetworkMessagesTests.cs` | UPDATE | +82 |
| `Server/server.js` | UPDATE | +10 |
| `web-app/__tests__/adversarial-ws.test.js` | UPDATE | +67 |
| `web-app/client/src/components/LiveLeaderboard.jsx` | UPDATE | +8 |
| `web-app/client/src/components/TrackMinimap.jsx` | UPDATE | +202/-54 |
| `web-app/client/src/constants.js` | UPDATE | +6 |
| `web-app/client/src/hooks/useRaceWebSocket.js` | UPDATE | +34 |
| `web-app/client/src/index.css` | UPDATE | +30 |
| `web-app/client/src/pages/LiveRacePage.jsx` | UPDATE | +14 |
| `web-app/client/src/lib/carStatus.js` | CREATE | new |
| `web-app/__tests__/carStatus.test.js` | CREATE | new (13 tests) |
| `web-app/client/src/components/CarDetailPanel.jsx` | CREATE | new |

## Deviations from Plan
1. **Speed fallback wired in the hook (Task 9 was "optional").** `state_update` now derives per-car speed from the prior frame via `deriveSpeed` when authoritative `s` is absent, tagging `sApprox`; `CarDetailPanel` labels it "(approx)". WHY: makes speed live before a Unity rebuild (the plan's graceful-degradation intent) and exercises the tested `deriveSpeed`. `prevPositions` reset on `race_start` to avoid a teleport spike.
2. **Event feed moved to `.live-grid` row2/col2**, seating `CarDetailPanel` at row2/col1. WHY: matches the plan's "After" UX mockup (status panel beside the feed).
3. **Added `buildTransform` export** (not in the plan's named export list) as the supporting helper for `normalize`/`TrackMinimap`'s fixed transform. `normalize`/`fitEllipse` are exactly as specified.

## Issues Encountered
- **Unity validation gap**: the UnitySkills API (`:8090`) is bound to the main checkout, so compiling/running EditMode tests through it would validate the wrong tree. Resolved by not running it (avoids a misleading pass) and doing a careful manual review instead. End-to-end verification of `s` + `track_geometry` requires a Unity WebGL rebuild + redeploy (per plan Risks).

## Tests Written
| Test File | Tests | Coverage |
|---|---|---|
| `web-app/__tests__/carStatus.test.js` | 13 | attr parse (incl. missing/whitespace), teamName join, >15-car null rank, speed derivation, pz→y flip, ellipse |
| `web-app/__tests__/adversarial-ws.test.js` (added) | 3 | `track_geometry` live relay, late-join replay, no-cache negative |
| `Assets/Tests/EditMode/NetworkMessagesTests.cs` (added) | 5 | `CarNetState.s` + `TrackGeometryMessage` JsonUtility round-trips (unrun here) |

## Next Steps
- [ ] Unity: rebuild WebGL + redeploy to activate authoritative `s` + precise `track_geometry` (until then the web view uses labeled client-side approximations)
- [ ] Optional: `/code-review` on the diff
- [ ] Manual browser walkthrough against a live race (click row/dot, verify highlight + panel + stable ellipse/start)
