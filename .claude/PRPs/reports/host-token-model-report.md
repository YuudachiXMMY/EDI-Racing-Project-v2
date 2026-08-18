# Implementation Report: Host Token Model (PRD Phase 1)

## Summary
Added an HMAC-signed, short-lived **host token** so that becoming the game's `professor` (the only role that can create a room / trigger events) requires a credential minted by the authenticated web-app. The web-app mints the token (`POST /api/game/host-token`, behind `requireAuth`); the WebSocket relay verifies it locally on `create_room` using the shared `INTERNAL_SECRET` — no cross-service round-trip. Enforcement is gated by `REQUIRE_HOST_TOKEN` (default **off**) so the current in-game Host flow keeps working until Phase 2 wires the token through the Dashboard launch.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium |
| Confidence | 8/10 | Implemented single-pass, all validations green |
| Files Changed | 8 (2 create, 6 update) | 7 (2 create, 5 update) — `.env.example` files blocked by permissions |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | Create `web-app/src/hostToken.js` (ESM mint/verify) | ✅ Complete | |
| 2 | `POST /api/game/host-token` behind `requireAuth` | ✅ Complete | |
| 3 | CJS `verifyHostToken` + gate `create_room` on WS server | ✅ Complete | Hoisted `INTERNAL_SECRET` to module scope (removed dup in `destroyRoom`) |
| 4 | Add `hostToken` to Unity `CreateRoomMessage` | ✅ Complete | Unity recompiled, 0 errors |
| 5 | `CreateRoom(string hostToken = null)` plumbing | ✅ Complete | Optional param — existing `SetupScreen.HostRoom()` unchanged |
| 6 | Env + compose docs | ⚠️ Partial | `docker-compose.yml` done; `Server/.env.example` + `web-app/.env.example` **blocked by permission rule on `.env*`** — see Deviations |
| 7 | vitest tests `host-token.test.js` | ✅ Complete | 8 tests |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis | ✅ Pass | `node --check` on `hostToken.js`, `game-status.js`, `server.js` |
| Unit Tests | ✅ Pass | 25/25 (`vitest run`): 8 new + 17 existing, no regressions |
| Build | ✅ Pass | Unity `asset_refresh` → recompile, `debug_get_errors` count:0; `docker compose config` OK, `REQUIRE_HOST_TOKEN` renders |
| Integration | ✅ Pass | Cross-process (web-app ESM mint → Server CJS verify): no-token / bad / expired → `error`; valid → `room_created`. Backward-compat: flag off + no token → `room_created` |
| Edge Cases | ✅ Pass | expired, tampered payload, tampered signature, malformed input, wrong version — all covered by unit + integration |

## Files Changed

| File | Action | Lines |
|---|---|---|
| `web-app/src/hostToken.js` | CREATED | +95 |
| `web-app/__tests__/host-token.test.js` | CREATED | +73 |
| `Server/server.js` | UPDATED | +65 / -1 |
| `web-app/src/routes/game-status.js` | UPDATED | +11 |
| `Assets/Scripts/Network/NetworkManager.cs` | UPDATED | +2 / -2 |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATED | +1 |
| `Deploy/docker-compose.yml` | UPDATED | +3 |

## Deviations from Plan
- **`Server/.env.example` and `web-app/.env.example` not updated** — the harness denies read/write on `.env*` paths (permission rule). The two env vars (`REQUIRE_HOST_TOKEN`, `HOST_TOKEN_TTL_MS`) are documented in-code (comments in `server.js`, `hostToken.js`) and in `docker-compose.yml`. **Follow-up**: a human with local access should add them to both `.env.example` files.
- **`INTERNAL_SECRET` hoisted** from inside `destroyRoom` to module scope in `server.js` (plan anticipated this) so the archive call and token verification share one declaration.

## Issues Encountered
- **Server deps missing** — `Server/node_modules` had no `ws`; ran `npm install` in `Server/` to enable the integration smoke test. No source impact.
- **UnitySkills `debug_force_recompile` forbidden** in `auto` mode; used `asset_refresh` instead to trigger recompilation, then confirmed 0 errors via `debug_get_errors`.
- **GateGuard fact-forcing gate** fired on every create/edit; satisfied each with the required facts.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `web-app/__tests__/host-token.test.js` | 8 | round-trip, null surveyId, tampered payload, tampered signature, expired, boundary (exp-1ms), malformed inputs, unsupported version |

## Security Notes
- Gating `create_room` transitively enforces "only the professor triggers events": `professor` is the sole role whose messages relay outward (`server.js` default branch), and role is granted only on a successful (now token-gated) `create_room`. No new relay path was added.
- Token is stateless HMAC (survives WS-server restarts, unlike the in-memory user session map). TTL default 5 min via `HOST_TOKEN_TTL_MS`.

## Next Steps
- [ ] Human: add `REQUIRE_HOST_TOKEN` / `HOST_TOKEN_TTL_MS` to `Server/.env.example` and `web-app/.env.example`.
- [ ] `/code-review` on the changeset.
- [ ] Proceed to **PRD Phase 2** (professor host launch): Dashboard "Host Game" button calling `POST /api/game/host-token`, read token from launch URL into `CreateRoom(...)`, then flip `REQUIRE_HOST_TOKEN=true`.
- [ ] Confirm PRD open question: token TTL vs the 60s professor grace period (rejoin uses `sessionId`, not the token, so TTL only needs to cover initial connect — verified in code).
