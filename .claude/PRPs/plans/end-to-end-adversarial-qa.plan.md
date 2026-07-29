# Plan: End-to-End + Adversarial QA (Role-Bound Game Links — Phase 7)

## Summary
Final QA barrier for the role-bound-game-links feature. Two goals: (1) **prove the professor→student workflow** end-to-end (Dashboard "Host Game" → hosted room with auto-injected survey data → shareable student link → student picks 3D or 2D and watches live, ≤2 clicks / 0 typed room codes); (2) **prove the security boundary** — a student-link socket and a hand-crafted host URL without a valid token **cannot** create a room or trigger events. This phase writes no new product features; it wires the deferred Unity scene objects, runs the EditMode tests that prior phases wrote-but-couldn't-run, adds an automated adversarial WebSocket integration suite, and produces documented QA evidence.

## User Story
As a **professor running a live classroom session**, I want **the one-click host launch + student watch link to actually work end-to-end and to be un-hijackable**, so that **I can teach without fumbling room codes and without a student being able to seize host control**.

## Problem → Solution
Phases 1–6 landed code but deferred every runtime/integration check ("Unity editor bound to main checkout, not this worktree" — see all six reports). Result: scene wiring is unwired, EditMode tests are written but never executed, `REQUIRE_HOST_TOKEN` enforcement has never been exercised against a live socket, and the full flow has never been walked. → Phase 7 executes all deferred verification on the main checkout (where the Unity editor + UnitySkills API are live), adds the missing automated adversarial coverage, and gates the feature Done with evidence.

## Metadata
- **Complexity**: Large (QA-heavy, cross-surface: Unity scene + EditMode + Node WS server + React client + manual walkthrough)
- **Source PRD**: `.claude/PRPs/prds/role-bound-game-links.prd.md`
- **PRD Phase**: 7 — End-to-end + adversarial QA (depends: 3, 5, 6)
- **Estimated Files**: 3–5 (1 new automated test, 1 QA evidence doc, 1 smoke doc, scene file wiring, PRD status update)

---

## UX Design

Internal QA phase — **no new user-facing UX**. It *verifies* the UX delivered by phases 2–6. The two flows under test:

### Professor flow (must hold in ≤2 clicks, 0 typed codes)
```
Dashboard (survey with responses selected)
   └─ click "主持游戏 / Host Game"      ← click 1 (mints host token, opens /#role=host&token=…&survey=ID)
        └─ Unity auto-CreateRoom(token) → room_created
             └─ auto-inject survey_import (no manual Send-to-Game)
                  └─ EventPanel visible, Join UI hidden
                       └─ student link shown to copy       ← click 2 (copy) — 0 room codes typed
```

### Student flow (no host controls anywhere)
```
Open student link  /survey/#/join/:roomCode
   ├─ "进入 3D 游戏"  → /#room=CODE&role=play → Unity auto-JoinRoom, IsHost=false, no EventPanel/Setup/Host
   └─ "2D 观战"       → /live/:roomCode        → LiveRacePage spectator (leaderboard/minimap/event feed)
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| n/a | n/a | n/a | Internal verification — no interaction change introduced by this phase |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Server/server.js` | 12–14, 50–86, 332–366 | `REQUIRE_HOST_TOKEN` gate + `verifyHostToken` + `create_room` handler — the security boundary under test |
| P0 | `Server/server.js` | 524–539, 487–522 | `survey_import` / `config_import` / `config_export` role checks (host-only message rejections to assert) |
| P0 | `Server/server.js` | 541–610 | `default` relay branch: professor→students broadcast vs student→professor-only relay (the "student can't inject events" property) |
| P0 | `web-app/src/hostToken.js` | all | `mintHostToken` / `verifyHostToken` — the test harness mints valid + crafts invalid tokens with this |
| P0 | `web-app/__tests__/host-token.test.js` | all | vitest structure + deterministic-clock convention to MIRROR for the new suite |
| P1 | `web-app/client/src/gameLaunch.js` | all | URL builders — confirms student link carries no token (already unit-tested in `game-launch.test.js`) |
| P1 | `web-app/client/src/pages/JoinLandingPage.jsx` | all | Landing page 3D/2D choice under test (route `/join/:roomCode`) |
| P1 | `web-app/client/src/pages/DashboardPage.jsx` | ~host-game handler | "Host Game" button, gated on `response_count > 0` — the click-1 entry |
| P1 | `Assets/Scripts/UI/HostLaunchBootstrap.cs` | all | Scene object to wire; auto-`CreateRoom`, role lock, resume guard |
| P1 | `Assets/Scripts/UI/StudentJoinBootstrap.cs` | all | Scene object to wire; auto-`JoinRoom` + `LockAsStudent` |
| P1 | `Assets/Scripts/UI/HostAutoInjectDecision.cs` | all | Phase 3 auto-inject decision wired into host launch |
| P2 | `Deploy/docker-compose.prod.yml` | 16–36 | Prod already sets `REQUIRE_HOST_TOKEN=true` + required strong `INTERNAL_SECRET` — verify, don't re-add |
| P2 | `.claude/PRPs/reports/host-token-model-report.md` | all | Phase 1 security model + the cross-process smoke test precedent this suite formalizes |
| P2 | `.claude/PRPs/reports/professor-host-launch-report.md` | 72–77 | Phase 2 "Remaining Work" — the exact scene-wiring + runtime-QA items folded into this phase |
| P2 | `.claude/PRPs/reports/student-unity-auto-join-role-lock-report.md` | 63–68 | Phase 5 "Runtime-QA-Pending" + adversarial checklist folded into this phase |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| `ws` client API | https://github.com/websockets/ws | `new WebSocket(url)`; `.on('open'|'message'|'error')`; `.send(JSON.stringify(...))` — used to drive adversarial sockets |
| Node `child_process.spawn` | Node docs | Spawn `Server/server.js` with a custom env (`REQUIRE_HOST_TOKEN=true`, known `INTERNAL_SECRET`, fixed test `PORT`); parse stdout "listening on port" to gate readiness |
| UnitySkills REST | `.claude/skills/unity-skills/SKILL.md` (`http://localhost:8090`) | Scene wiring + EditMode `test_run` MUST go through this API first (technical-preferences); `test_run` may require Bypass panel mode (it can enter Play mode) |

No further external research needed — feature uses established internal patterns (HMAC token, WS relay, vitest, UnitySkills).

---

## Patterns to Mirror

### VITEST_DETERMINISTIC_CLOCK
```javascript
// SOURCE: web-app/__tests__/host-token.test.js:10-19
// Never read the real clock (coding-standards: no time-dependent assertions).
const T0 = 1_750_000_000_000;
const TTL = 300_000;
it('round-trips a valid token and recovers the surveyId', () => {
  const { token, expiresAt } = mintHostToken(42, T0);
  expect(expiresAt).toBe(T0 + TTL);
  expect(verifyHostToken(token, T0)).toEqual({ valid: true, surveyId: 42 });
});
```

### TOKEN_MINT_FOR_HARNESS
```javascript
// SOURCE: web-app/src/hostToken.js (mintHostToken) + host-token-model-report.md:33
// The web-app mints (ESM); the Server verifies (CJS) with the SAME INTERNAL_SECRET.
// A valid token for the live-server test is minted with the same secret the spawned
// Server process is given via env — no real clock needed if you pass `now`.
const { token } = mintHostToken(surveyId /*, now optional */);
// Craft an EXPIRED token by minting with a `now` far in the past, or a TAMPERED one by
// flipping a payload/sig char (see host-token.test.js:27-40).
```

### WS_RELAY_ROLE_BRANCH (the property under test)
```javascript
// SOURCE: Server/server.js:603-608
// A student's non-handshake message relays ONLY to the professor — never broadcast to
// other students or web viewers. This is why a student CANNOT inject an event into the room.
} else if (info.role === 'student') {
  // Student → Professor relay
  if (room.professor && room.professor.readyState === 1) {
    room.professor.send(raw);
  }
}
```

### HOST_ONLY_ROLE_GATE (rejection under test)
```javascript
// SOURCE: Server/server.js:524-529 (survey_import); mirror for config_import/config_export
const webInfo = clientRooms.get(ws);
if (!webInfo || webInfo.role !== 'webapp') {
  sendJSON(ws, { type: 'error', message: 'Not authorized' });
  return;
}
```

### CREATE_ROOM_TOKEN_GATE (rejection under test)
```javascript
// SOURCE: Server/server.js:333-340
if (REQUIRE_HOST_TOKEN) {
  const result = verifyHostToken(msg.hostToken);
  if (!result.valid) {
    sendJSON(ws, { type: 'error', message: 'Host authorization required' });
    return;   // never granted 'professor' role → transitively can never trigger events
  }
}
```

### EDITMODE_TEST_FILE (already-written, needs a run)
```
// SOURCE: Assets/Tests/EditMode/ — pure-decision EditMode tests written by prior phases,
// never executed (Unity editor was bound to main checkout, not the worktrees):
//   HostLaunchParamsTests.cs       (Phase 2, 8 tests)
//   HostAutoInjectDecisionTests.cs (Phase 3)
//   StudentLinkBuilderTests.cs     (Phase 4)
//   StudentJoinDecisionTests.cs    (Phase 5, 7 tests)
// Run all via UnitySkills test_run on the main checkout (this phase's home).
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/__tests__/adversarial-ws.test.js` | CREATE | Automated adversarial coverage: live Server + real `ws` sockets prove token gate + student-relay boundary |
| `Assets/Scenes/complete_track_demo.unity` | UPDATE (via UnitySkills) | Wire `HostLaunchBootstrap` + `StudentJoinBootstrap` onto the launch GameObject; assign `NetworkManager` / `RaceUI` refs (deferred from phases 2 & 5) |
| `production/qa/evidence/phase7-role-bound-links-walkthrough.md` | CREATE | Manual E2E walkthrough + screenshots (professor + student flows) — the ≤2-click / 0-code evidence |
| `production/qa/smoke-2026-07-29.md` | CREATE | Smoke-check record gating QA hand-off (per coding-standards test-evidence table) |
| `.claude/PRPs/prds/role-bound-game-links.prd.md` | UPDATE | Flip Phase 7 status `pending → in-progress`, then `complete`; link this plan |
| `web-app/package.json` | (verify only) | Confirm `ws` is a dependency (it is: `^8.21.1`) so the new test needs no install |

## NOT Building
- **No new product features** — zero changes to `Server/server.js`, `hostToken.js`, `gameLaunch.js`, or gameplay `.cs` logic. If QA finds a real defect, fix it as a scoped follow-up, not silently inside this plan.
- **No headless Unity Play-mode E2E automation** — the full 3D runtime flow is verified by a documented manual walkthrough + screenshots (coding-standards: full gameplay sessions are playtested, not automated).
- **No student-driving / car-control tests** — join stays visual-only (PRD "What We're NOT Building").
- **No load/soak/perf testing** — out of scope for this correctness barrier.
- **No new deploy config** — `docker-compose.prod.yml` already enables enforcement; this phase verifies it, does not author it.

---

## Step-by-Step Tasks

### Task 1: Wire the deferred Unity scene objects (via UnitySkills API)
- **ACTION**: On the main checkout, attach the bootstrap components deferred by phases 2 & 5 to the launch GameObject in `complete_track_demo.unity`.
- **IMPLEMENT**: Via `http://localhost:8090` (UnitySkills — API is UP): open `Assets/Scenes/complete_track_demo.unity`; on the GameObject that already holds `NetworkManager`/`RaceUI` (or the designated launch/bootstrap object), add `HostLaunchBootstrap` and `StudentJoinBootstrap`; assign their `NetworkManager` + `RaceUI` references; assign `RaceUI.NetworkManager` (Phase 2 convergence field). Both bootstraps read the same URL hash and each no-ops when its role isn't present, so co-existing on one object is safe.
- **MIRROR**: `EDITMODE_TEST_FILE` context + professor-host-launch-report.md:72–77 (exact wiring checklist) and student-unity-auto-join-role-lock-report.md:64–66.
- **IMPORTS**: n/a (Unity Inspector wiring through the API).
- **GOTCHA**: technical-preferences mandates UnitySkills API **first**, direct `.unity` YAML edit only as fallback. `HostLaunchBootstrap` has a role-gated resume guard (`role != "play"`) so a student link opened in a former-host browser is NOT hijacked — confirm both bootstraps are present so that guard has its counterpart. After launch the hash is cleared (`WebSocketBridge_ClearUrlHash`) so reload resumes via persisted `sessionId`, not a stale token.
- **VALIDATE**: Save scene; `debug_get_errors` → 0; enter Play mode once and confirm no NullReference from unassigned refs on the bootstrap objects.

### Task 2: Run all deferred EditMode tests
- **ACTION**: Execute the pure-decision EditMode tests written by phases 2–5 but never run.
- **IMPLEMENT**: Via UnitySkills `test_run` (EditMode) on the `Tests` asmdef, or the Editor Test Runner if the API blocks it. Target at minimum: `HostLaunchParamsTests` (8), `StudentJoinDecisionTests` (7), `HostAutoInjectDecisionTests`, `StudentLinkBuilderTests`. Running the whole EditMode suite is preferred (catches cross-file regressions).
- **MIRROR**: `EDITMODE_TEST_FILE`.
- **IMPORTS**: n/a.
- **GOTCHA**: `test_run` is risk-gated in UnitySkills `auto` panel mode (it can enter Play mode) — prior phases hit exactly this. If blocked, switch the panel to Bypass or run via the in-Editor Test Runner and capture the result. Do NOT mark the phase complete on "tests compile" alone — they must actually pass (prior reports only got as far as compile).
- **VALIDATE**: All targeted EditMode tests report pass; capture the pass count into the QA evidence doc.

### Task 3: Write the automated adversarial WebSocket integration suite
- **ACTION**: Create `web-app/__tests__/adversarial-ws.test.js` — spawn the real `Server/server.js` with enforcement ON and drive it with real `ws` sockets to prove the security boundary.
- **IMPLEMENT**:
  - In `beforeAll`: `spawn('node', [<abs path to Server/server.js>], { env: { ...process.env, PORT: '18080', REQUIRE_HOST_TOKEN: 'true', INTERNAL_SECRET: 'phase7-test-secret-abc123', API_URL: 'http://127.0.0.1:1' } })`; resolve a ready-promise when stdout matches `/listening on port/`. In `afterAll`: `child.kill()` and close all sockets.
  - Helper `connect()` → returns a `ws` client with a `next(predicate, timeoutMs=1500)` awaiter and a `collect(ms)` window collector for negative assertions.
  - Import `mintHostToken` from `../src/hostToken.js`; the spawned Server shares `INTERNAL_SECRET`, so a token minted here verifies there.
  - **Scenarios (assertions):**
    1. `create_room` with **no** `hostToken` → receives `{type:'error', message:'Host authorization required'}`; never `room_created`.
    2. `create_room` with a **tampered** token (flip a sig char) → `error`.
    3. `create_room` with an **expired** token (mint with `now = T0 - 10*TTL`) → `error`.
    4. `create_room` with a **valid** token → `room_created` (positive control; proves the gate isn't blanket-denying).
    5. **Student-injected event**: valid host creates room → 2 students `join_room` + 1 `web_join_room`; student-B sends `{type:'event_triggered', ...}`. Assert within a `collect(300)` window that student-A and the web viewer receive **nothing**, and the professor **does** receive the relayed frame (student→professor-only relay). This is the "a student cannot trigger a game event even via a crafted URL" property.
    6. **Host-only messages from a student**: student sends `survey_import`, `config_import`, `config_export` → each returns `Not authorized` (or `config_sync_ack {success:false}`); no relay to professor/webapp.
    7. **Crafted host URL == raw socket**: a fresh socket (no join) sending `create_room` without a token → `error` (URL role is not merely cosmetic).
- **MIRROR**: `VITEST_DETERMINISTIC_CLOCK`, `TOKEN_MINT_FOR_HARNESS`, `WS_RELAY_ROLE_BRANCH`, `HOST_ONLY_ROLE_GATE`, `CREATE_ROOM_TOKEN_GATE`. Test-file layout mirrors `host-token.test.js` (`describe`/`it`, `expect`).
- **IMPORTS**: `import { describe, it, expect, beforeAll, afterAll } from 'vitest';` `import { WebSocket } from 'ws';` `import { spawn } from 'node:child_process';` `import { mintHostToken } from '../src/hostToken.js';` `import { once } from 'node:events';` `import path from 'node:path';`
- **GOTCHA**: Negative WS assertions ("student-A must NOT receive X") are inherently timing-based — use a bounded `collect(ms)` window, not an unbounded wait; document this as the one accepted deviation from "no time-dependent assertions" (a fixed window is deterministic in outcome even if wall-clock-bound). Use an uncommon fixed test port (e.g. `18080`) to avoid clashing with a dev server on 8080, and assert on the Server's logged port line. `child_process` cwd must resolve `Server/server.js` relative to `web-app/` — use an absolute path (`path.resolve(__dirname, '../../Server/server.js')`) or set `cwd`. Ensure `Server/node_modules/ws` exists (host-token report notes it can be missing) — `beforeAll` can guard by watching spawn stderr for `MODULE_NOT_FOUND` and failing with a clear message + hint to run `npm install` in `Server/`.
- **VALIDATE**: `cd web-app && npm test` → new suite green, all existing 34 tests still pass (no regression). All 7 scenarios assert as specified.

### Task 4: Enable + verify enforcement for the E2E walkthrough
- **ACTION**: Bring up the stack with `REQUIRE_HOST_TOKEN=true` and a strong secret, matching production.
- **IMPLEMENT**: Use `Deploy/docker-compose.prod.yml` (already `REQUIRE_HOST_TOKEN=true`, `INTERNAL_SECRET` required) OR run web-app + Server locally with `REQUIRE_HOST_TOKEN=true INTERNAL_SECRET=$(openssl rand -hex 32)` exported to BOTH processes (they must share the secret). Confirm the Server boot guard: with enforcement on + default/empty secret it must `process.exit(1)` (`checkSecretConfig` fatal) — a quick negative check that the guard works.
- **MIRROR**: `Server/server.js:19-37, 618-629` (boot guard) + `docker-compose.prod.yml:16-36`.
- **IMPORTS**: n/a (ops).
- **GOTCHA**: web-app and Server must receive the **same** `INTERNAL_SECRET` or every host launch is rejected. Do NOT commit a real secret; use an env var / `.env` (which is gitignored and permission-blocked from edits — set it in the shell).
- **VALIDATE**: Server logs `listening on port`; a boot with `REQUIRE_HOST_TOKEN=true` + default secret exits non-zero with the fatal message.

### Task 5: Full professor→student E2E walkthrough with evidence
- **ACTION**: Manually walk both flows against the enforced stack and capture screenshots.
- **IMPLEMENT**: Create `production/qa/evidence/phase7-role-bound-links-walkthrough.md`. Steps + evidence:
  - **Professor**: log in → Dashboard → select a survey **with responses** → click "主持游戏 / Host Game" (screenshot: button + response gate). Unity opens hosted; assert room auto-created, survey data auto-injected (EventPanel populated), Join UI hidden, student link displayed (screenshot). Count clicks: launch (1) + copy link (2), **0 typed room codes** → metric met.
  - **Student 3D**: open the student link `/survey/#/join/:roomCode` (screenshot landing) → "进入 3D 游戏" → auto-joins visual-only, **no** EventPanel/Setup/Host button (screenshot).
  - **Student 2D**: from the same landing → "2D 观战" → `/live/:roomCode` spectator shows live leaderboard/minimap/event feed (screenshot).
  - **Reload resilience**: reload the professor tab → resumes the same room (no orphan), via persisted `sessionId` not a fresh token.
- **MIRROR**: PRD User Flow (lines 101–106) + Success Metrics table (lines 38–44).
- **IMPORTS**: Chrome DevTools MCP / Playwright MCP for driving + screenshots is acceptable; browser QA is the medium.
- **GOTCHA**: The student link must NOT contain `token=` anywhere (inspect the copied link) — this is a leak check, not just a functional one. Confirm the 3D student's `IsHost` is false and there is no UI path to flip it.
- **VALIDATE**: Every PRD success metric row has a corresponding evidence artifact; all four metrics meet target.

### Task 6: Adversarial walkthrough (crafted URL, live stack)
- **ACTION**: Reproduce the automated adversarial results against the live browser stack for defense-in-depth evidence.
- **IMPLEMENT**: In the walkthrough doc, add: (a) open a **crafted host URL** `/#role=host&token=BOGUS&survey=1` in a fresh browser → Unity attempts `CreateRoom("BOGUS")` → Server rejects (`Host authorization required` in Server log; no room, no EventPanel). (b) From the 3D student tab, via DevTools console, send a raw `event_triggered` over the game's WebSocket → confirm no other student/web viewer receives it (only relays to professor, which ignores it as inbound). Capture Server logs + screenshots.
- **MIRROR**: student-unity-auto-join-role-lock-report.md:67 (adversarial checklist) + `CREATE_ROOM_TOKEN_GATE`.
- **IMPORTS**: DevTools console for the raw-frame injection.
- **GOTCHA**: This is the PRD's headline security claim ("a student on the student link cannot trigger a game event even by manipulating the URL", line 27). If ANY unauthorized action succeeds, that is a **blocking** defect — stop and file it, do not mark Phase 7 complete.
- **VALIDATE**: Both crafted-URL and raw-frame attempts are rejected/inert; evidence captured.

### Task 7: Smoke check + PRD status update
- **ACTION**: Record the smoke gate and close out the PRD phase.
- **IMPLEMENT**: Create `production/qa/smoke-2026-07-29.md` summarizing: EditMode pass count, adversarial suite result, E2E metric outcomes, PASS/FAIL. Update `.claude/PRPs/prds/role-bound-game-links.prd.md` Phase 7 row: `pending → in-progress` at start, `→ complete` at end, and set the PRP Plan cell to this file. Also mark the still-`in-progress` Phase 2 & 3 rows resolved (their only remaining work — scene wiring + runtime QA — is discharged here); note that in the PRD.
- **MIRROR**: coding-standards Test-Evidence-by-Story-Type table (smoke doc location `production/qa/smoke-[date].md`).
- **IMPORTS**: n/a.
- **GOTCHA**: Only flip Phase 7 → complete if EditMode tests pass, the adversarial suite is green, AND no blocking security defect surfaced. A partial pass = leave `in-progress` with the blocker noted (Parallel Task Protocol: surface blockers, produce a partial report).
- **VALIDATE**: PRD renders with Phase 7 complete + plan link; smoke doc committed.

---

## Testing Strategy

### Unit / Integration Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| create_room, no token | `{type:'create_room'}` | `error: Host authorization required` | — |
| create_room, tampered token | valid token w/ flipped sig char | `error` | Yes |
| create_room, expired token | mint w/ `now=T0-10*TTL` | `error` | Yes (boundary) |
| create_room, valid token | `mintHostToken(sid)` | `room_created` | No (positive control) |
| student sends event_triggered | 2 students + 1 webapp joined | only professor receives; others get nothing in 300ms window | Yes (the core boundary) |
| student sends survey_import | joined student socket | `error: Not authorized` | Yes |
| student sends config_import/export | joined student socket | `config_sync_ack {success:false}` / `Not authorized` | Yes |
| raw socket create_room (crafted URL) | no prior join, no token | `error` | Yes |
| EditMode: HostLaunchParams / StudentJoinDecision / HostAutoInjectDecision / StudentLinkBuilder | (pure fns) | all pass | — |

### Edge Cases Checklist
- [ ] Empty / missing token on create_room
- [ ] Tampered payload AND tampered signature (two distinct failures)
- [ ] Expired token (mint in the past)
- [ ] Valid token positive control (gate is not blanket-deny)
- [ ] Student → other-students / web-viewer isolation (negative, bounded window)
- [ ] Host-only messages (`survey_import`/`config_import`/`config_export`) from a student
- [ ] Crafted host URL with a bogus token (live browser + raw socket)
- [ ] Student link contains no `token=` substring (leak check)
- [ ] Boot guard: enforcement on + default secret → `process.exit(1)`
- [ ] Professor reload resumes room (no orphan / no duplicate create)

---

## Validation Commands

### Static Analysis
```bash
cd web-app && npx oxlint __tests__/adversarial-ws.test.js   # lint the new suite (client uses oxlint per Phase 2 report)
node --check web-app/__tests__/adversarial-ws.test.js       # syntax
```
EXPECT: Zero errors/new warnings

### New + Full web-app Test Suite
```bash
cd web-app && npm test                                      # vitest run — new adversarial suite + all existing
```
EXPECT: New suite green; existing 34 tests still pass (no regressions)

### Unity EditMode Tests (via UnitySkills, main checkout)
```
POST http://localhost:8090  → test_run (EditMode, Tests asmdef)   # or Editor Test Runner if API-blocked
```
EXPECT: HostLaunchParams (8) + StudentJoinDecision (7) + HostAutoInjectDecision + StudentLinkBuilder all pass; whole EditMode suite green

### Unity Compile / Scene Health
```
POST http://localhost:8090  → asset_refresh ; debug_get_errors
```
EXPECT: 0 errors after scene wiring; no NullReference in Play mode

### Enforcement Boot Guard (negative)
```bash
REQUIRE_HOST_TOKEN=true INTERNAL_SECRET=edi-internal-default node Server/server.js
```
EXPECT: Exits non-zero with `[Auth] FATAL: … Refusing to start.`

### Live Stack (E2E walkthrough)
```bash
docker compose -f Deploy/docker-compose.prod.yml up --build   # INTERNAL_SECRET must be exported
# OR locally: export a shared strong INTERNAL_SECRET + REQUIRE_HOST_TOKEN=true to web-app AND Server
```
EXPECT: Professor host launch works; student link works; all metrics met

### Manual Validation
- [ ] Professor: Dashboard → Host Game → hosted room + auto-injected data + student link, in ≤2 clicks / 0 typed codes
- [ ] Student 3D: link → landing → 进入 3D 游戏 → visual-only, no host controls
- [ ] Student 2D: link → landing → 2D 观战 → live spectator
- [ ] Crafted host URL `#role=host&token=BOGUS` → rejected, no room
- [ ] Raw `event_triggered` from student socket → not received by other students/viewers
- [ ] Copied student link contains no `token=`
- [ ] Professor reload → same room resumed

---

## Acceptance Criteria
- [ ] Scene wired (`HostLaunchBootstrap` + `StudentJoinBootstrap` on `complete_track_demo.unity`, refs assigned), 0 compile errors
- [ ] All deferred EditMode tests actually run and pass
- [ ] `adversarial-ws.test.js` created, all 7 scenarios green, no web-app regressions
- [ ] Enforcement verified live (valid token hosts; no/bad/expired token rejected; boot guard fatal on misconfig)
- [ ] Full professor→student E2E walkthrough passes with screenshots; all 4 PRD success metrics met
- [ ] Crafted-URL + raw-frame adversarial attempts rejected (headline security claim proven)
- [ ] QA evidence + smoke doc written; PRD Phase 7 → complete

## Completion Checklist
- [ ] New test follows `host-token.test.js` structure + deterministic-clock convention
- [ ] Negative WS assertions use a bounded window (documented as accepted)
- [ ] Server driven as a real spawned process with the shared `INTERNAL_SECRET` (faithful, not mocked)
- [ ] No product code changed (any defect fix is a scoped, called-out follow-up)
- [ ] UnitySkills API used for scene/test ops (direct YAML edit only as fallback)
- [ ] Evidence stored under `production/qa/evidence/` + `production/qa/smoke-*.md`
- [ ] PRD status + plan link updated
- [ ] Self-contained — no codebase search needed to execute

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| UnitySkills `test_run` blocked in `auto` mode (Play-mode risk gate) | H | Med | Switch to Bypass panel or in-Editor Test Runner; prior phases confirmed this is the workaround |
| `Server/node_modules/ws` missing → spawned Server crashes | M | Med | `beforeAll` guards on `MODULE_NOT_FOUND` in stderr; `cd Server && npm install` once (host-token report precedent) |
| Negative WS assertion flaky (timing) | M | Med | Bounded `collect(300ms)` window; assert professor-DID-receive as the paired positive to anchor timing |
| web-app + Server given different `INTERNAL_SECRET` → every launch rejected | M | High | Export one shared secret to both; Task 4 explicitly checks this |
| Scene wiring introduces a NullReference (unassigned ref) | M | Med | `debug_get_errors` + one Play-mode smoke before the walkthrough |
| A real defect surfaces (e.g., auto-inject race, hash not cleared) | L | High | Treat as blocking; file scoped bug, leave Phase 7 `in-progress` with partial report — do not silently patch inside this plan |
| Test port 18080 clashes with a running service | L | Low | Choose a high uncommon port; assert on the Server's logged port line |

## Notes
- **This is the barrier phase** — it discharges the "scene wiring + runtime QA pending" debt that phases 2, 3, 4, 5, 6 each explicitly deferred because their worktrees weren't bound to the Unity editor. Phase 7 runs on `main`, where UnitySkills (`http://localhost:8090`) is UP.
- **Security model recap** (why the tests are sufficient): role `professor` is the ONLY role whose messages broadcast outward; that role is granted ONLY on a successful `create_room`; `create_room` is token-gated under `REQUIRE_HOST_TOKEN`. So gating create_room transitively gates event triggering — no separate `event_triggered` token check is needed, and the adversarial suite proves both the gate and the student-relay isolation.
- **Enforcement is prod-ready**: `docker-compose.prod.yml` already sets `REQUIRE_HOST_TOKEN=true` + a required strong `INTERNAL_SECRET`; the dev `docker-compose.yml` defaults it off. Phase 7 verifies, it does not author, deploy config.
- **Open PRD question** (student driving) stays deferred — join remains visual-only; no test asserts driving.
- If executed as a background job on a worktree, note the Unity-editor-binding caveat: scene wiring + EditMode `test_run` require the checkout the editor is actually watching (historically `main`). Coordinate so those two tasks run against that checkout.
