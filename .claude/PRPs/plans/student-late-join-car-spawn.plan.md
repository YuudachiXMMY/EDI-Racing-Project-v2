# Plan: Student Late-Join Car Spawn (spectators see no cars)

## Summary
Students who open the join link/QR **after the professor has started the race** load the Unity build and join the room, but no cars ever spawn in their 3D view (and the 2D minimap is empty). The relay caches only the latest *position* frame (`state_update`) and drops the one-time `race_start` roster that carries the car list. A late-joining client therefore receives positions for cars it never created, so its spawn path is never invoked. The fix caches the race roster on the relay and replays it (personalized) to any client that joins after the race began.

## User Story
As a **student joining a live race from the shared link/QR after it has already started**, I want the cars to appear and move in my browser, so that I can watch my team's car race like students who joined before the start.

## Problem → Solution
**Current:** Relay stores `room.latestState`, which starts as the `race_start` JSON but is overwritten by the first `state_update` (~100 ms later). A late joiner is sent only `latestState` (a `state_update`), never the roster. Unity's `HandleStateUpdate` bails at `remoteCars == null`; `SpawnVisualCars` never runs → **no cars.**
**Desired:** Relay caches the last `race_start` roster in a dedicated field and, on any late join while `raceStarted === true`, replays a **personalized** `race_start` (with `yourCarIndex`) *before* the cached `state_update`, so the client spawns visual cars and then immediately positions them.

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A (standalone bug fix)
- **PRD Phase**: N/A
- **Estimated Files**: 2 (1 fix + 1 test) — Unity client needs **no** change

---

## UX Design

### Before
```
Prof starts race ─▶ relay caches state_update (roster lost)
                          │
Student opens link ──▶ join_room ──▶ receives ONLY state_update
                          │
                    Unity: remoteCars == null ─▶ HandleStateUpdate returns
                          │
                    ┌───────────────────────────┐
                    │  Empty track. No cars.     │
                    │  "Race In Progress" but    │
                    │  nothing to watch.         │
                    └───────────────────────────┘
```

### After
```
Prof starts race ─▶ relay caches race_start roster + state_update
                          │
Student opens link ──▶ join_room ──▶ receives race_start (personalized) THEN state_update
                          │
                    Unity: HandleRaceStart ─▶ SpawnVisualCars ─▶ HandleStateUpdate positions them
                          │
                    ┌───────────────────────────┐
                    │  Cars spawned & moving.    │
                    │  Own car highlighted gold. │
                    └───────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Student joins after race start (3D) | Empty track, no cars | Cars spawn + interpolate | Primary fix |
| Student joins before race start (3D) | Works (already) | Unchanged | Guard against regression |
| Web 2D spectator joins after start | Minimap has no car identities | Cars appear on minimap | Same root cause, `web_join_room` |
| Student reconnect (grace period) after start | Cars can be missing if roster was lost | Cars re-spawn | `rejoin_room` student branch |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Server/server.js` | 88–96 | Room shape comment + `rooms`/`sessions` maps — add the new cache field here |
| P0 (critical) | `Server/server.js` | 116–125 | `broadcastToStudents` helper pattern |
| P0 (critical) | `Server/server.js` | 396–475 | `create_room` (room fields init) + `join_room` (late-join replay block) |
| P0 (critical) | `Server/server.js` | 609–680 | `default` relay branch — where `race_start` personalization + `latestState` caching live |
| P1 (important) | `Server/server.js` | 478–553 | `rejoin_room` (student branch) + `web_join_room` — must also replay the roster |
| P1 (important) | `Assets/Scripts/Network/NetworkSync.cs` | 239–369 | Confirms client contract: `HandleRaceStart` spawns, `HandleStateUpdate` returns if `remoteCars == null` — **do not change** |
| P2 (reference) | `web-app/__tests__/adversarial-ws.test.js` | 1–160 | Real-socket integration harness that spawns `Server/server.js`; add the regression test here |
| P2 (reference) | `Assets/Scripts/Race/RaceManager.cs` | 226–235 | `LoadAndStartRaceVisualOnly` → `SpawnVisualCars` (the path that must be reached) |

## External Documentation
No external research needed — feature uses established internal patterns (`ws` WebSocketServer, existing relay caching convention).

---

## Patterns to Mirror

### ROOM_STATE_FIELD (add cache next to the others)
```javascript
// SOURCE: Server/server.js:407-423 (create_room)
rooms.set(roomCode, {
  professor: ws,
  students: new Set(),
  webapps: new Set(),
  studentTeamNames: new Map(),
  raceStarted: false,
  latestState: null,
  gamePhase: 'Setup',
  raceResults: null,
  latestLeaderboard: null,
  surveyData: null,
  latestConfig: null,
  professorSessionId: msg.sessionId || null,
  surveyId,
  graceTimer: null,
  createdAt: new Date().toISOString(),
});
```

### RACE_START_PERSONALIZATION (reuse for replay — this is the exact `yourCarIndex` logic)
```javascript
// SOURCE: Server/server.js:624-638 (default branch, professor race_start)
// Send personalized race_start to each student with yourCarIndex
const cars = msg.cars || [];
for (const student of room.students) {
  if (student.readyState !== 1) continue;
  const studentInfo = clientRooms.get(student);
  const studentTeam = (studentInfo && studentInfo.teamName) || '';
  let yourIndex = -1;
  if (studentTeam) {
    yourIndex = cars.findIndex(c =>
      c.teamName && c.teamName.toLowerCase() === studentTeam.toLowerCase()
    );
  }
  const personalizedMsg = { ...msg, yourCarIndex: yourIndex };
  student.send(JSON.stringify(personalizedMsg));
}
```

### LATE_JOIN_REPLAY (current block that is incomplete — roster is missing)
```javascript
// SOURCE: Server/server.js:466-473 (join_room)
// Send cached survey to late-joiner (if survey distributed but race not started)
if (room.surveyData && !room.raceStarted) {
  ws.send(room.surveyData);
}
// If race already started, send latest state to late-joiner
if (room.latestState) {
  ws.send(room.latestState);
}
```

### INTEGRATION_TEST_STRUCTURE (real spawned server + socket helpers)
```javascript
// SOURCE: web-app/__tests__/adversarial-ws.test.js:34-133
const PORT = 18080;
const WS_URL = `ws://127.0.0.1:${PORT}`;
// makeClient(): wraps a ws with an inbox + `.next(pred)` awaiter and `.send(obj)`.
// beforeAll spawns `node Server/server.js` with env {PORT, INTERNAL_SECRET, REQUIRE_HOST_TOKEN}
// and resolves once stdout matches /listening on port/i.
// createRoom(host): host.send({type:'create_room', hostToken}) then await 'room_created'.
// joinAsStudent(roomCode, teamName): s.send({type:'join_room', roomCode, teamName}).
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Server/server.js` | UPDATE | Add `latestRaceStart` cache; populate on `race_start`; replay personalized roster to late joiners in `join_room`, `rejoin_room` (student), `web_join_room` |
| `web-app/__tests__/adversarial-ws.test.js` | UPDATE | Add regression test: student joining after `race_start` receives a `race_start` (with roster + `yourCarIndex`) before `state_update` |

## NOT Building
- **No Unity/C# changes.** `NetworkSync.HandleRaceStart` / `HandleStateUpdate` already do the right thing once the roster arrives; the defect is 100% server-side caching.
- **No nginx / `/ws` proxy / `/api/game/enter` gateway changes.** The professor host uses the identical WS path and works, proving connectivity and routing are fine.
- **No change to broadcast cadence or the live (join-before-start) path.**
- **No change to the 2D React client** (`useRaceWebSocket.js`) — once the relay replays `race_start`, its existing `case 'race_start'` populates `cars` and the minimap renders. (Confirm during validation; only touch if the replay alone doesn't light up the 2D minimap.)
- **No persistence** of the roster beyond room lifetime.

---

## Step-by-Step Tasks

### Task 1: Add the roster cache field to the room
- **ACTION**: In `create_room` (Server/server.js:407–423), add `latestRaceStart: null,` alongside `latestState`. Update the room-shape doc comment at line 88 to mention it.
- **IMPLEMENT**: `latestRaceStart` holds the raw `race_start` JSON string (the host's original message with the `cars` roster), independent of `latestState`.
- **MIRROR**: ROOM_STATE_FIELD.
- **IMPORTS**: none.
- **GOTCHA**: This must be a **separate** field. Do not reuse `latestState` — it is deliberately overwritten by every `state_update` for the "resume at current positions" behavior.
- **VALIDATE**: Grep shows the field initialized in `create_room`; asserted indirectly via Task 5.

### Task 2: Populate the cache when the professor starts the race
- **ACTION**: In the `default` branch professor `race_start` handler (Server/server.js:619–645), set `room.latestRaceStart = raw;` right where `room.latestState = raw;` is set (line 622).
- **IMPLEMENT**: `raw` is already `data.toString()` (line 615). Cache the roster string before the personalized per-student loop.
- **MIRROR**: RACE_START_PERSONALIZATION (same block).
- **IMPORTS**: none.
- **GOTCHA**: Cache the **raw host message** (no `yourCarIndex`) — personalization is per-recipient and must be computed at send time, not stored.
- **VALIDATE**: After a `race_start`, `room.latestRaceStart` is non-null (asserted indirectly via Task 5).

### Task 3: Replay the roster to late-joining students (`join_room`)
- **ACTION**: In `join_room` (Server/server.js:466–473), before the `if (room.latestState)` replay, add: when `room.raceStarted && room.latestRaceStart`, compute this student's `yourCarIndex` from `teamName` and send a personalized `race_start` first.
- **IMPLEMENT**:
  ```javascript
  // Late joiner after race start: replay the roster (personalized) BEFORE the latest
  // positions, so the client spawns visual cars and then snaps them to current state.
  sendRaceStartTo(ws, room, teamName);   // helper defined in Task 4
  if (room.latestState) {
    ws.send(room.latestState);
  }
  ```
- **MIRROR**: RACE_START_PERSONALIZATION for the `yourCarIndex` computation; LATE_JOIN_REPLAY for placement.
- **IMPORTS**: none. `teamName` is already in scope (Server/server.js:444).
- **GOTCHA**: **Order matters** — `race_start` MUST be sent before `state_update`, else `HandleStateUpdate` runs while `remoteCars == null` and drops the frame, so cars only appear on the *next* update. Sending roster first spawns them immediately.
- **GOTCHA**: Do not gate on `!room.raceStarted` like the `surveyData` line above — that block is for the pre-race survey; this is the post-start path (the helper self-gates on `room.raceStarted`).
- **VALIDATE**: Task 5 asserts a late student receives `race_start` then `state_update`.

### Task 4: Extract `sendRaceStartTo` helper; wire `rejoin_room` (student) and `web_join_room`
- **ACTION**:
  1. Add a top-level helper (after `broadcastToStudents`, Server/server.js:116–125).
  2. Call it in `join_room` (Task 3), in the `rejoin_room` student branch before `if (room.latestState)` (Server/server.js:526), and in `web_join_room` before `if (webRoom.latestState)` (Server/server.js:548) with empty team.
- **IMPLEMENT**:
  ```javascript
  // Replay the cached race roster to a late-joining client so it spawns visual cars, then
  // (caller) sends latestState to snap them to current positions. Personalized per team;
  // yourCarIndex is -1 for anonymous students and web viewers. No-op before race start.
  function sendRaceStartTo(ws, room, teamName) {
    if (!room.raceStarted || !room.latestRaceStart || ws.readyState !== 1) return;
    try {
      const startMsg = JSON.parse(room.latestRaceStart);
      const cars = startMsg.cars || [];
      let yourIndex = -1;
      if (teamName) {
        yourIndex = cars.findIndex(c =>
          c.teamName && c.teamName.toLowerCase() === teamName.toLowerCase()
        );
      }
      ws.send(JSON.stringify({ ...startMsg, yourCarIndex: yourIndex }));
    } catch { /* malformed cache — caller still sends latestState */ }
  }
  ```
  - `join_room`: `sendRaceStartTo(ws, room, teamName);`
  - `rejoin_room` (student branch): `sendRaceStartTo(ws, room, teamName);` (teamName resolved at line 514)
  - `web_join_room`: `sendRaceStartTo(ws, webRoom, '');`
- **MIRROR**: existing helper style (`broadcastToStudents`); RACE_START_PERSONALIZATION.
- **IMPORTS**: none.
- **GOTCHA**: `rejoin_room` already sends `reconnect_state` first; keep that ordering: `reconnect_state` → roster → `latestState`.
- **VALIDATE**: Task 5 covers `join_room`; add a `web_join_room` assertion if practical.

### Task 5: Regression test — late student gets the roster
- **ACTION**: Add integration tests to `web-app/__tests__/adversarial-ws.test.js` using the existing harness.
- **IMPLEMENT**:
  - Host `createRoom`, then `host.send({ type: 'race_start', cars: [{ teamName: 'Red', colorIndex: 2 }, { teamName: 'Blue', colorIndex: 3 }] })`.
  - New student `join_room` with `teamName: 'Blue'`; assert it receives a `race_start` with `cars.length === 2` and `yourCarIndex === 1`.
  - Ordering case: host sends `race_start` then a `state_update`; a joining student's first race-related message is `race_start` (not `state_update`).
  - Unknown/empty team → `yourCarIndex === -1`, full roster still present.
- **MIRROR**: INTEGRATION_TEST_STRUCTURE (`makeClient`, `.next(pred)`, `createRoom`, `joinAsStudent`).
- **IMPORTS**: reuse existing test imports; no new deps.
- **GOTCHA**: The relay's `default` branch only relays professor→students when the sender's role is `professor` (gate at line 617); `createRoom` establishes that role.
- **GOTCHA**: Enforcement (`REQUIRE_HOST_TOKEN`) is ON in this suite — mint a host token via the existing `mintHostToken` for `create_room`.
- **VALIDATE**: `cd web-app && npx vitest run __tests__/adversarial-ws.test.js` — new cases pass, existing pass.

### Task 6: Cache-lifetime hygiene
- **ACTION**: Confirm no in-room "reset race" relay message exists that would require clearing `latestRaceStart`. A new `create_room`/re-host already starts null (Task 1); a repeated `race_start` refreshes it (Task 2).
- **IMPLEMENT**: `grep -n "race_reset\|reset" Server/server.js` — if no relay-handled reset, no extra clearing needed.
- **MIRROR**: n/a.
- **VALIDATE**: grep returns no relay-handled reset path.

---

## Testing Strategy

### Unit / Integration Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Late student gets roster | host `race_start` (2 cars) → student `join_room` team `Blue` | Student receives `race_start` with `cars.length==2`, `yourCarIndex==1` | No |
| Roster before state | host `race_start` → host `state_update` → student joins | `race_start` arrives before any `state_update` on the student socket | Yes (ordering) |
| Unknown team | student joins team `Zebra` after start | `race_start` with `yourCarIndex==-1`, full roster | Yes |
| Anonymous student | student joins `teamName: ''` after start | `race_start` with `yourCarIndex==-1` | Yes |
| Join before start unchanged | student joins, then host `race_start` | Student still gets exactly one personalized `race_start` (no duplicate) | Yes (regression) |
| Web viewer late join | host `race_start` → `web_join_room` | Web socket receives `race_start` (roster) then `latestState` | Yes |

### Edge Cases Checklist
- [ ] Student joins before race start (existing path — no double roster)
- [ ] Student joins after start (new path — cars spawn)
- [ ] Malformed cached roster (JSON.parse throws → caught, falls through to state)
- [ ] Reconnect after start (`rejoin_room` student) re-spawns cars
- [ ] Web 2D spectator late join populates minimap
- [ ] Race finished then a straggler joins (roster still replays; final positions from `latestState`)

---

## Validation Commands

### Static Analysis
```bash
node --check Server/server.js
cd web-app && npx oxlint ../Server 2>/dev/null || true
```
EXPECT: `node --check` passes (no syntax errors).

### Unit / Integration Tests
```bash
cd web-app && npx vitest run __tests__/adversarial-ws.test.js
```
EXPECT: All cases pass, including new late-join cases.

### Full Web-App Suite
```bash
cd web-app && npx vitest run
```
EXPECT: No regressions.

### Manual Validation (deployed, definitive)
- [ ] Professor: Host Game → Start race.
- [ ] After cars are moving, open the student link (or scan QR) on a second device/incognito → "Enter 3D Game".
- [ ] Cars spawn and interpolate on the student view; own team's car (if the join carried a team) glows gold.
- [ ] Open the same link → "2D Spectate" after start → minimap shows cars.
- [ ] Reload the student tab mid-race → cars re-spawn (hash re-triggers `StudentJoinBootstrap`, `rejoin_room`/`join_room` replays roster).
- [ ] Relay logs show the student `join_room` and no errors.

### Optional: local end-to-end without Unity
```bash
# Terminal 1: relay
INTERNAL_SECRET=dev REQUIRE_HOST_TOKEN=false node Server/server.js
# Terminal 2: scripted ws host (create_room + race_start + state_update) then a ws student
# (join_room) — assert it receives race_start. (Covered by the Task 5 test.)
```

---

## Acceptance Criteria
- [ ] A student joining after race start receives a `race_start` roster (personalized `yourCarIndex`) before positions.
- [ ] Cars spawn and move on the late-joining student's 3D view (deployed manual check).
- [ ] 2D spectator minimap populates for late joiners.
- [ ] Students who join before start still work (no duplicate roster, no regression).
- [ ] `adversarial-ws.test.js` new cases pass; full suite green.
- [ ] `node --check Server/server.js` passes.

## Completion Checklist
- [ ] `latestRaceStart` cached separately from `latestState`
- [ ] Personalized replay reuses the exact `yourCarIndex` team-match logic
- [ ] `join_room`, `rejoin_room` (student), `web_join_room` all replay the roster
- [ ] Roster sent BEFORE `latestState` on every late-join path
- [ ] `try/catch` guards the cached-JSON parse (matches relay's defensive style)
- [ ] No Unity/nginx/gateway changes
- [ ] Regression test added following the spawned-server harness
- [ ] Room-shape doc comment (line 88) updated

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Real cause is join-*before*-start (a second defect) | Low | High | Task 0 diagnostic (see Notes): check relay logs for whether failing students `join_room` before/after `race_start`. The late-join gap is a real defect regardless; confirm it's THE one the professor hit |
| Ordering race (state before roster on socket) | Low | Med | Send roster synchronously before `latestState` on the same `ws` in the same tick — TCP preserves order |
| Duplicate `race_start` for join-before-start clients | Low | Low | Replay is gated on `room.raceStarted`; a pre-start joiner takes the `surveyData` path, not the roster path |
| Personalization mismatch (team casing) | Low | Low | Reuse the existing case-insensitive `findIndex` used by the live path |
| 2D client needs more than roster | Low | Low | `useRaceWebSocket` already handles `race_start`; verify in manual step, patch only if needed |

## Notes
- **Diagnostic first (recommended Task 0):** Confirm on the deployed box whether the failing students join before or after `race_start` (relay logs print `[Room CODE] Student '…' joined` and the `race_start` timing). If students fail even when joining *before* start, there is a second defect (e.g., the Unity student build's track scene not loaded, or `LoadAndStartRaceVisualOnly` erroring) — investigate `Assets/Scripts/Race/RaceManager.cs:226` and the WebGL build's browser console. The relay fix is necessary either way.
- The relay is a plain Node `ws` server (`Server/server.js`), deployed via the pinned root-owned compose on IthacaServer (see memory `ediracing-deploy-pinned-compose`) — shipping this fix requires rebuilding/redeploying the relay/game container, not just the web-app.
- Client contract is already correct: `NetworkSync.HandleRaceStart` (Assets/Scripts/Network/NetworkSync.cs:297) spawns via `RaceManager.LoadAndStartRaceVisualOnly` → `CarSpawner.SpawnVisualCars`; `HandleStateUpdate` (line 347) is a no-op until `remoteCars` exists. This is why caching+replaying the roster is sufficient.
