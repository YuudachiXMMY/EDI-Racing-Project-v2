# Implementation Report: Professor Host Launch (Phase 2)

## Summary
Implemented the Dashboard "主持游戏 / Host Game" one-click launch: an authenticated professor mints a host token (existing `POST /api/game/host-token`), the client opens the Unity build at the game root with `role/token/survey` in the URL **hash**, and Unity's new `HostLaunchBootstrap` reads the hash, auto-`CreateRoom(token)`s, locks the UI to Professor (hiding the student Join UI), and persists `sessionId` so a reload resumes the room instead of creating a duplicate.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Large | Large |
| Confidence | 8/10 | Held — no blocking surprises |
| Files Changed | 9 (2 new / 7 modified) | 12 (5 new / 7 modified) — added `HostLaunchParams.cs`, `ClearUrlHash` bridge, and 2 test files |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | Client `requestHostToken` wrapper | Complete | `api.js` |
| 2 | `gameLaunch.js` URL helper | Complete | hash-based; `VITE_GAME_URL` override |
| 3 | Dashboard "Host Game" button + handler | Complete | gated on `response_count > 0` |
| 4 | jslib URL + localStorage bridge | Complete | +`ClearUrlHash` (deviation, see below) |
| 5 | C# DllImport wrappers + editor fallback | Complete | `WebSocketBridge` statics, `PlayerPrefs` fallback |
| 6 | sessionId persistence + resume | Complete | `HasPersistedHostSession` / `ResumeHostSession` |
| 7 | `HostLaunchBootstrap` + `HostLaunchParams` | Complete | parser split into a testable static class |
| 8 | RaceUI role convergence | Complete | optional `NetworkManager` field → `OnRoomCreated` |
| 9 | Tests | Complete (written); EditMode run blocked | see Validation |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis (client lint) | Pass | oxlint — no new warnings in changed files |
| Client Build | Pass | `vite build` clean |
| Unit Tests (web-app vitest) | Pass | 34/34 (incl. new `game-launch` 4, `host-token` 13) |
| Unity Compile | Pass | `asset_refresh` → **zero** console errors across all 3 asmdefs |
| Unity EditMode tests | **Not run** (blocked) | `test_run` forbidden in `auto` panel mode (may enter Play mode → needs Bypass). Test file **compiles**; run via Editor Test Runner or Bypass mode. |
| Integration / Runtime | **Deferred** | Scene wiring + live flow = Phase 7 QA (see Remaining Work) |

## Files Changed

| File | Action | Lines |
|---|---|---|
| `web-app/client/src/gameLaunch.js` | CREATED | +10 |
| `web-app/client/src/api.js` | UPDATED | +9 |
| `web-app/client/src/pages/DashboardPage.jsx` | UPDATED | +22 |
| `Assets/Plugins/WebGL/WebSocketBridge.jslib` | UPDATED | +27 |
| `Assets/Plugins/WebGL/WebSocketBridge.cs` | UPDATED | +51 |
| `Assets/Scripts/Network/NetworkManager.cs` | UPDATED | +52 |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATED | +10 |
| `Assets/Scripts/UI/HostLaunchBootstrap.cs` | CREATED | +50 |
| `Assets/Scripts/UI/HostLaunchParams.cs` | CREATED | +32 |
| `Assets/Tests/EditMode/HostLaunchParamsTests.cs` | CREATED | +80 |
| `web-app/__tests__/game-launch.test.js` | CREATED | +28 |

## Deviations from Plan

1. **Split `HostLaunchParams` out of `HostLaunchBootstrap`** — the plan sketched an inline parser; extracted to a `public static` class so the EditMode test can call it without a MonoBehaviour.
2. **Added `WebSocketBridge_ClearUrlHash` (jslib + C# wrapper)** — not in the plan. Needed for correctness: without stripping the hash after a host launch, reloading the tab would re-run `CreateRoom` with a now-stale token (duplicate room / rejected under enforcement). After launch we `ClearUrlHash()`, so a reload falls through to the persisted-session **resume** path.
3. **`ResumeHostSession()` on NetworkManager** — the plan said "reconnect path resumes"; but the reconnect coroutine keys off *in-memory* `lastRoomCode`, which is lost on reload. Added an explicit resume that restores room+sessionId from storage and sends `rejoin_room`, mirroring `CreateRoom`'s pendingAction pattern.

## Issues Encountered
- **Explore agent gave a wrong path** for `WebSocketBridge.cs` (`Assets/Scripts/Network/` vs actual `Assets/Plugins/WebGL/`) — corrected by `find`.
- **`test_run` blocked** in the UnitySkills `auto` panel mode (risk-gated because it may enter Play mode). Compilation validation still succeeded via `asset_refresh` + `console_get_logs`.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `web-app/__tests__/game-launch.test.js` | 4 | `buildHostLaunchUrl` — hash format, coercion, encoding, no-query-string |
| `Assets/Tests/EditMode/HostLaunchParamsTests.cs` | 8 | `HostLaunchParams.ParseHash` — normal, empty, null, no-hash, `=`-in-value, leading `?`, percent-decode, valueless-skip |

## Remaining Work (before Phase 2 → complete)
- [ ] **Scene wiring** (manual / UnitySkills, needs in-editor verification): attach `HostLaunchBootstrap` to a scene GameObject alongside `NetworkManager`/`RaceUI`; assign both refs; assign `RaceUI.NetworkManager`. The feature is inert until this is done.
- [ ] **Run EditMode tests** via Editor Test Runner or Bypass mode (`HostLaunchParamsTests`).
- [ ] **Runtime QA** (folds into Phase 7): Dashboard click → hosted room, Join UI hidden; reload → resume (no orphan room); with `REQUIRE_HOST_TOKEN=true` + strong secret, tampered/absent token rejected.
- [ ] Note: EventPanel-on-launch needs `GameState==Racing` → realized by Phase 3 (auto-inject + auto-start).

## Next Steps
- [ ] Wire the scene + run EditMode tests in the Editor
- [ ] `/code-review` the changeset
- [ ] Proceed to Phase 3 (auto-inject) — parallel sibling that completes the "survey done → race live" flow
