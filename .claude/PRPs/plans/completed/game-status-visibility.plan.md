# Plan: Game Status Visibility in Web App

## Summary
Add a REST endpoint to the WebSocket server that exposes room status (existence, student count, race phase), and integrate a live-polling status badge into the web app's `SendToGameModal` so professors see whether their room is open before sending data. This closes the "blind send" gap identified in GAP 3 of the web-unity gap analysis.

## User Story
As a professor using the web survey app,
I want to see whether the Unity game room is running and its current state,
So that I avoid confusion when trying to send data to a room that doesn't exist or is mid-race.

## Problem → Solution
**Current:** `SendToGameModal` blindly connects via WebSocket; shows error only after a 5-second timeout if room doesn't exist. No visibility into room state, student count, or race phase.

**Desired:** Before clicking "Send", the professor sees a live status badge showing room state (e.g., "Room ABCDEF: 3 students, Setup phase") or "Room not found". The send button is disabled when the room doesn't exist.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/plans/web-unity-gap-analysis-and-hosting.plan.md`
- **PRD Phase**: GAP 3
- **Estimated Files**: 8

---

## UX Design

### Before
```
┌─────────────── Send to Game ─────────────────┐
│                                               │
│  Host a room in the Unity game first,         │
│  then enter the room code below.              │
│                                               │
│  Room Code: [________]                        │
│                                               │
│  [Send]  [Cancel]                             │
│                                               │
│  → Clicks Send → 5s wait → "Room not found"  │
└───────────────────────────────────────────────┘
```

### After
```
┌─────────────── Send to Game ─────────────────┐
│                                               │
│  Host a room in the Unity game first,         │
│  then enter the room code below.              │
│                                               │
│  Room Code: [ABCDEF ]                         │
│                                               │
│  ┌─ Room Status ──────────────────────────┐   │
│  │  ● Room ABCDEF — Setup                 │   │
│  │    3 student(s) connected              │   │
│  └────────────────────────────────────────┘   │
│                                               │
│  [Send]  [Cancel]                             │
│                                               │
│  --- OR if not found ---                      │
│                                               │
│  ┌─ Room Status ──────────────────────────┐   │
│  │  ○ Room not found                      │   │
│  └────────────────────────────────────────┘   │
│                                               │
│  [Send (disabled)]  [Cancel]                  │
└───────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Room code input | No feedback until Send clicked | Live status poll after debounce (800ms) | Polls every 5s while modal open |
| Send button | Always enabled | Disabled when room not found or checking | Prevents futile send attempts |
| Error state | Shows after 5s timeout | Instant "Room not found" badge | No more waiting |
| Status visibility | None | Color-coded badge with phase + student count | Setup=blue, Racing=amber, Finished=gray |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Server/server.js` | all | Room data structure, message handling — adding HTTP endpoint here |
| P0 | `web-app/src/routes/export.js` | 116-201 | Existing WebSocket client pattern in Express (send-to-game flow) |
| P0 | `web-app/client/src/components/SendToGameModal.jsx` | all | Component being modified to show status badge |
| P1 | `web-app/client/src/api.js` | all | API client pattern — adding new function |
| P1 | `web-app/src/index.js` | all | Express route registration — adding new route file |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | 133-138 | GameStateMessage type already defined but not sent |
| P1 | `Assets/Scripts/Race/RaceManager.cs` | 41-50 | GameState enum + SetState() — Unity needs to broadcast state changes |
| P2 | `web-app/client/src/index.css` | 170-181 | Modal styling patterns |
| P2 | `Deploy/docker-compose.yml` | all | WS_GAME_URL env var used by web-app |

## External Documentation

No external research needed — feature uses established internal patterns (WebSocket, Express REST, React polling).

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```javascript
// SOURCE: web-app/src/routes/export.js:1-9
// Route files: lowercase, default Router export
import { Router } from 'express';
const router = Router();
// ...
export default router;
```

### ERROR_HANDLING
```javascript
// SOURCE: web-app/src/routes/export.js:88-90
// REST endpoint error pattern: { success: false, error: 'message' }
if (!survey) {
  return res.status(404).json({ success: false, error: 'Survey not found' });
}
```

### API_RESPONSE
```javascript
// SOURCE: web-app/src/index.js:32-33
// Health check pattern: { success: true, data: { ... } }
app.get('/api/health', (req, res) => {
  res.json({ success: true, data: { status: 'ok' } });
});
```

### API_CLIENT
```javascript
// SOURCE: web-app/client/src/api.js:19-33
// Client request wrapper with auth token
async function request(path, options = {}) {
  const token = getToken();
  const headers = { 'Content-Type': 'application/json', ...options.headers };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const res = await fetch(`/api${path}`, { ...options, headers });
  const json = await res.json();
  if (res.status === 401) { clearToken(); window.location.hash = '#/login'; }
  return json;
}
```

### WEBSOCKET_CLIENT_IN_EXPRESS
```javascript
// SOURCE: web-app/src/routes/export.js:156-199
// Pattern: open ephemeral WS connection, join room, send/receive, close
const ws = new WebSocket(WS_GAME_URL);
let responded = false;
const timeout = setTimeout(() => { ... }, 5000);
ws.on('open', () => { ws.send(JSON.stringify({ type: 'web_join_room', roomCode: code })); });
ws.on('message', (data) => { ... });
ws.on('error', () => { ... });
```

### REACT_MODAL
```jsx
// SOURCE: web-app/client/src/components/SendToGameModal.jsx:34-74
// Modal pattern: overlay + content, stopPropagation, status state machine
<div className="modal-overlay" onClick={onClose}>
  <div className="modal-content" onClick={e => e.stopPropagation()}>
    ...
  </div>
</div>
```

### CSS_VARIABLES
```css
/* SOURCE: web-app/client/src/index.css:1-14 */
/* Color scheme: dark theme with named variables */
--accent: #4a9eff;
--success: #40a040;
--warning: #e0a020;
--danger: #e04040;
--text-dim: #888;
```

### UNITY_STATE_TRACKING
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:41-50
// Game state management — already has CurrentState + OnStateChanged event
public GameState CurrentState { get; private set; } = GameState.Setup;
public event Action<GameState> OnStateChanged;
private void SetState(GameState state)
{
    CurrentState = state;
    OnStateChanged?.Invoke(state);
}
```

### SERVER_ROOM_DATA
```javascript
// SOURCE: Server/server.js:7-8
// Room data structure in memory
// Room: { professor: WebSocket, students: Set<WebSocket>, raceStarted: boolean, latestState: string|null }
const rooms = new Map();
const clientRooms = new Map(); // WebSocket -> { roomCode, role }
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Server/server.js` | UPDATE | Add HTTP server alongside WebSocket; add `/api/room-status/:code` endpoint; add `gamePhase` field to room; handle `game_state` message from professor |
| `web-app/src/routes/game-status.js` | CREATE | New Express route that proxies room status query to WS server's HTTP endpoint |
| `web-app/src/index.js` | UPDATE | Register new game-status route |
| `web-app/client/src/api.js` | UPDATE | Add `getRoomStatus(roomCode)` function |
| `web-app/client/src/components/SendToGameModal.jsx` | UPDATE | Add debounced room status polling and status badge display |
| `web-app/client/src/components/RoomStatusBadge.jsx` | CREATE | Presentational component for room status display |
| `web-app/client/src/index.css` | UPDATE | Add styles for room status badge |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Broadcast `game_state` message when RaceManager state changes |

## NOT Building

- Real-time WebSocket push from game server to web app (polling is sufficient for this UX)
- Room status on the Dashboard page (only in SendToGameModal for now)
- Historical room data or session persistence (out of scope — GAP 6)
- WebSocket reconnection logic (out of scope — GAP 7)
- Automatic room code detection/linking (professor must still type room code)

---

## Step-by-Step Tasks

### Task 1: Add HTTP server and room-status endpoint to WebSocket server
- **ACTION**: Modify `Server/server.js` to co-host an HTTP server on the same port (or a separate port) that exposes room status. Add `gamePhase` field to room data structure. Handle `game_state` message from the professor client to update the phase.
- **IMPLEMENT**:
  1. Add `const http = require('http');` and create an HTTP server that handles `/api/room-status/:code` requests
  2. Extend room data structure to include `gamePhase: 'Setup'` (default)
  3. Add handler for `game_state` message type in the professor's relay (`default` switch branch) to update `room.gamePhase`
  4. HTTP endpoint returns JSON: `{ exists: true, roomCode, studentCount, gamePhase, raceStarted }`
  5. Attach WebSocketServer to the HTTP server instead of standalone port
- **MIRROR**: `SERVER_ROOM_DATA`, `API_RESPONSE`
- **IMPORTS**: `const http = require('http');`
- **GOTCHA**: WebSocketServer currently listens on its own port. Refactor to use `http.createServer()` and pass the server to `new WebSocketServer({ server })` instead of `{ port }`. This is a compatible change — the WS upgrade still works on the same port.
- **GOTCHA**: The HTTP endpoint must NOT require authentication — it only exposes room existence + count, no sensitive data.
- **GOTCHA**: Must handle CORS for the HTTP endpoint since web-app may be on a different origin in dev.
- **VALIDATE**: `curl http://localhost:8080/api/room-status/NONEXIST` returns `{ "exists": false }`. After creating a room via WS, querying with the room code returns `{ "exists": true, "roomCode": "ABCDEF", "studentCount": 0, "gamePhase": "Setup", "raceStarted": false }`.

### Task 2: Broadcast game_state from Unity when state changes
- **ACTION**: Modify `NetworkSync.cs` to send a `game_state` message to the server whenever the RaceManager's state changes.
- **IMPLEMENT**:
  1. In `NetworkSync`, subscribe to `RaceManager.OnStateChanged` event
  2. When state changes, if NetworkManager is host and connected, send `GameStateMessage` with state name
  3. `GameStateMessage` type already exists in `NetworkMessages.cs` (line 133-138) — just use it
- **MIRROR**: `UNITY_STATE_TRACKING`
- **IMPORTS**: None additional needed
- **GOTCHA**: `GameStateMessage` has a `state` field (string). Map `GameState.Setup` → `"Setup"`, etc.
- **GOTCHA**: Only send if `NetworkManager.IsHost` is true — students should not broadcast state.
- **VALIDATE**: Start Unity in editor, host a room, start a race → check server logs for `game_state` message handling.

### Task 3: Handle game_state message in server
- **ACTION**: In `Server/server.js`, handle the `game_state` message type from professors to update `room.gamePhase`.
- **IMPLEMENT**:
  1. In the `default` switch case, add a check for `msg.type === 'game_state'` within the professor relay block
  2. When received, set `room.gamePhase = msg.state`
  3. Still relay to students (existing behavior for professor→student relay)
- **MIRROR**: `SERVER_ROOM_DATA`
- **IMPORTS**: None
- **GOTCHA**: This goes inside the existing `if (info.role === 'professor')` block in the default case, alongside `race_start` and `state_update` handlers.
- **VALIDATE**: After Task 2 runs in Unity, verify server logs show `game_state` messages and the room's `gamePhase` updates.

### Task 4: Create game-status proxy route in web-app Express server
- **ACTION**: Create `web-app/src/routes/game-status.js` with a GET endpoint that queries the WS server's HTTP API for room status.
- **IMPLEMENT**:
  ```javascript
  import { Router } from 'express';
  
  const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';
  // Derive HTTP URL from WS URL
  const GAME_HTTP_URL = WS_GAME_URL.replace(/^ws/, 'http');
  
  const router = Router();
  
  // GET /api/game/room-status/:code
  router.get('/room-status/:code', async (req, res) => {
    const code = req.params.code.toUpperCase();
    try {
      const response = await fetch(`${GAME_HTTP_URL}/api/room-status/${code}`);
      const data = await response.json();
      res.json({ success: true, data });
    } catch {
      res.json({ success: true, data: { exists: false, error: 'Game server unreachable' } });
    }
  });
  
  export default router;
  ```
- **MIRROR**: `NAMING_CONVENTION`, `ERROR_HANDLING`, `API_RESPONSE`
- **IMPORTS**: `import { Router } from 'express';`
- **GOTCHA**: The WS_GAME_URL env var uses `ws://` protocol. Derive HTTP URL by replacing `ws://` with `http://`. Both protocols use the same port on the refactored server.
- **GOTCHA**: This endpoint does NOT require auth (`requireAuth`) — room status is not sensitive (just existence + count). This lets us potentially use it from public contexts later.
- **GOTCHA**: Use native `fetch` (available in Node 18+). Check web-app package.json — it uses ES modules, so Node 18+ is assumed.
- **VALIDATE**: `curl http://localhost:3001/api/game/room-status/ABCDEF` returns proxied result from game server.

### Task 5: Register game-status route in Express app
- **ACTION**: Import and mount the new route in `web-app/src/index.js`.
- **IMPLEMENT**:
  1. Add import: `import gameStatusRoutes from './routes/game-status.js';`
  2. Add mount: `app.use('/api/game', gameStatusRoutes);` (after existing route mounts)
- **MIRROR**: `NAMING_CONVENTION` (follows existing `app.use('/api/...', routes)` pattern)
- **IMPORTS**: `import gameStatusRoutes from './routes/game-status.js';`
- **GOTCHA**: Mount at `/api/game` so the full path is `/api/game/room-status/:code`. This namespaces game-related endpoints separately from survey endpoints.
- **VALIDATE**: Server starts without errors. Route is accessible at `/api/game/room-status/:code`.

### Task 6: Add API client function for room status
- **ACTION**: Add `getRoomStatus(roomCode)` to `web-app/client/src/api.js`.
- **IMPLEMENT**:
  ```javascript
  export async function getRoomStatus(roomCode) {
    return request(`/game/room-status/${roomCode.toUpperCase()}`);
  }
  ```
- **MIRROR**: `API_CLIENT`
- **IMPORTS**: None (uses existing `request` wrapper)
- **GOTCHA**: Uses the authenticated `request` wrapper for consistency, even though the backend doesn't require auth. This is fine — the auth header is simply ignored.
- **VALIDATE**: Import in browser console or test component, call with a known room code.

### Task 7: Create RoomStatusBadge component
- **ACTION**: Create `web-app/client/src/components/RoomStatusBadge.jsx` as a presentational component.
- **IMPLEMENT**:
  ```jsx
  export default function RoomStatusBadge({ status }) {
    if (!status) return null;
    
    if (!status.exists) {
      return (
        <div className="room-status room-status-error">
          <span className="status-dot"></span>
          {status.error || 'Room not found'}
        </div>
      );
    }
    
    const phaseClass = {
      Setup: 'room-status-setup',
      Racing: 'room-status-racing',
      Paused: 'room-status-racing',
      Finished: 'room-status-finished',
    }[status.gamePhase] || 'room-status-setup';
    
    return (
      <div className={`room-status ${phaseClass}`}>
        <span className="status-dot"></span>
        <div>
          <strong>Room {status.roomCode}</strong> — {status.gamePhase}
          <br />
          <span className="status-detail">{status.studentCount} student(s) connected</span>
        </div>
      </div>
    );
  }
  ```
- **MIRROR**: `REACT_MODAL` (simple presentational component style)
- **IMPORTS**: None (pure component)
- **GOTCHA**: Handle all four `GameState` values: Setup, Racing, Paused, Finished.
- **VALIDATE**: Renders correctly for each status variant when passed mock props.

### Task 8: Integrate room status polling into SendToGameModal
- **ACTION**: Update `SendToGameModal.jsx` to poll room status when a room code is entered, display the status badge, and disable Send when room doesn't exist.
- **IMPLEMENT**:
  1. Import `getRoomStatus` from `api.js` and `RoomStatusBadge`
  2. Add state: `roomStatus` (null | object), `checking` (boolean)
  3. Use `useEffect` with debounce: when `roomCode` has 4+ chars, call `getRoomStatus` after 800ms. Clear on unmount.
  4. Set up polling interval (5 seconds) while modal is open and room code is valid.
  5. Show `<RoomStatusBadge status={roomStatus} />` between the input and the actions.
  6. Disable Send button when `roomStatus` exists and `roomStatus.exists === false`, or when `checking`.
  7. Keep existing send logic unchanged.
- **MIRROR**: `REACT_MODAL`, `API_CLIENT`
- **IMPORTS**: `import { getRoomStatus } from '../api.js'; import RoomStatusBadge from './RoomStatusBadge.jsx';`
- **GOTCHA**: Must clean up both the debounce timeout and the polling interval on unmount and when room code changes.
- **GOTCHA**: Don't poll when room code is less than 4 characters (room codes are 6 chars). Reset `roomStatus` to null when code is too short.
- **GOTCHA**: When `status === 'sending'`, pause the polling to avoid interference.
- **VALIDATE**: Open modal, type a room code → see status badge appear after 800ms. Badge updates every 5s. Invalid code shows "Room not found". Send button disabled when room not found.

### Task 9: Add CSS styles for room status badge
- **ACTION**: Add styles to `web-app/client/src/index.css` for the room status badge.
- **IMPLEMENT**: Add after the existing modal styles (line ~181):
  ```css
  /* --- Room Status Badge --- */
  .room-status { display: flex; align-items: center; gap: 10px; padding: 10px 12px; border-radius: 6px; font-size: 13px; margin-bottom: 12px; }
  .room-status .status-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
  .room-status .status-detail { font-size: 12px; color: var(--text-dim); }
  .room-status-setup { background: rgba(74,158,255,.12); }
  .room-status-setup .status-dot { background: var(--accent); }
  .room-status-racing { background: rgba(224,160,32,.12); }
  .room-status-racing .status-dot { background: var(--warning); }
  .room-status-finished { background: rgba(136,136,136,.12); }
  .room-status-finished .status-dot { background: var(--text-dim); }
  .room-status-error { background: rgba(224,64,64,.12); color: var(--danger); }
  .room-status-error .status-dot { background: var(--danger); }
  ```
- **MIRROR**: `CSS_VARIABLES`
- **IMPORTS**: N/A
- **GOTCHA**: Use the existing CSS variable names (`--accent`, `--warning`, `--danger`, `--text-dim`) for consistency with the dark theme.
- **VALIDATE**: Badge renders with correct colors for each status phase.

### Task 10: Add nginx proxy rule for game status endpoint
- **ACTION**: Verify the existing nginx config already proxies `/api` to the web-app. Since the new route is at `/api/game/room-status/:code`, it will be caught by the existing `/api` location block.
- **IMPLEMENT**: No change needed — the existing `location /api` block in `Deploy/nginx/nginx.conf:84-89` already proxies all `/api/*` requests to `web-app:3001`.
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: Confirm the existing proxy rule handles the new path. It does: `location /api { proxy_pass http://web-app:3001; }` catches `/api/game/room-status/ABCDEF`.
- **VALIDATE**: After deploying, `curl http://host:3900/api/game/room-status/ABCDEF` is correctly proxied.

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Room status endpoint — existing room | GET `/api/room-status/ABCDEF` (valid room) | `{ exists: true, roomCode: "ABCDEF", studentCount: 0, gamePhase: "Setup", raceStarted: false }` | No |
| Room status endpoint — nonexistent room | GET `/api/room-status/ZZZZZ` | `{ exists: false }` | No |
| Room status endpoint — case insensitive | GET `/api/room-status/abcdef` | Same as uppercase request | Yes |
| Room status after students join | GET after 3 students join | `studentCount: 3` | No |
| Room status after race starts | GET after professor starts race | `gamePhase: "Racing", raceStarted: true` | No |
| Room status after room closes | GET after professor disconnects | `{ exists: false }` | Yes |
| Web-app proxy — game server unreachable | GET when WS server is down | `{ success: true, data: { exists: false, error: "Game server unreachable" } }` | Yes |
| RoomStatusBadge — null status | `status={null}` | Renders nothing | Yes |
| RoomStatusBadge — room found | `status={ exists: true, gamePhase: "Setup", ... }` | Shows setup badge with blue dot | No |
| RoomStatusBadge — room not found | `status={ exists: false }` | Shows error badge with red dot | No |

### Edge Cases Checklist
- [x] Empty room code — don't poll, show nothing
- [x] Short room code (<4 chars) — don't poll, show nothing
- [x] Room disappears while modal open — next poll shows "not found"
- [x] Game server unreachable — graceful fallback, don't block sending
- [x] Race finishes while modal open — badge updates to "Finished"
- [x] Concurrent access — HTTP endpoint is read-only, no race conditions
- [x] Modal unmount during fetch — cleanup prevents state update on unmounted component

---

## Validation Commands

### Static Analysis
```bash
# Type check web-app (no TypeScript in this project — skip)
```
EXPECT: N/A (JavaScript project)

### Server Tests
```bash
# Start WS server and verify HTTP endpoint
cd Server && node server.js &
sleep 1
curl -s http://localhost:8080/api/room-status/NONEXIST | python3 -m json.tool
kill %1
```
EXPECT: `{ "exists": false }`

### Web App Dev Server
```bash
cd web-app && npm run dev
```
EXPECT: Server starts on port 3001, new route accessible

### Full Stack (Docker)
```bash
cd Deploy && docker compose up -d --build
curl -s http://localhost:3900/api/game/room-status/TESTCODE | python3 -m json.tool
```
EXPECT: Proxied response from game server through nginx → web-app → WS server HTTP

### Manual Validation
- [ ] Open Unity in editor, host a room → note room code
- [ ] Open web app, go to a survey, click "Send to Game"
- [ ] Type the room code → status badge appears showing "Setup" phase
- [ ] Have a student join in Unity → badge updates student count on next poll
- [ ] Start race in Unity → badge updates to "Racing" phase
- [ ] Type a nonexistent room code → badge shows "Room not found", Send button disabled
- [ ] Close Unity room → badge updates to "Room not found" on next poll

---

## Acceptance Criteria
- [ ] WebSocket server exposes HTTP endpoint at `/api/room-status/:code`
- [ ] Endpoint returns room existence, student count, game phase, and race status
- [ ] Web app proxies room status through Express to avoid CORS issues
- [ ] SendToGameModal shows live room status badge with 800ms debounce and 5s polling
- [ ] Send button is disabled when room doesn't exist
- [ ] Status badge is color-coded by game phase (Setup=blue, Racing=amber, Finished=gray, Error=red)
- [ ] Unity broadcasts `game_state` message when race phase changes
- [ ] Server updates `gamePhase` field on room when receiving `game_state` from professor
- [ ] Graceful degradation when game server is unreachable
- [ ] All existing send-to-game functionality continues to work unchanged

## Completion Checklist
- [ ] Code follows discovered patterns (Express routes, React components, CSS variables)
- [ ] Error handling matches codebase style (`{ success, data, error }` envelope)
- [ ] No hardcoded URLs (uses `WS_GAME_URL` env var)
- [ ] Cleanup on unmount (timeouts, intervals, fetch cancellation)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| HTTP server refactor breaks existing WS connections | Low | High | `WebSocketServer({ server })` is the standard pattern; test WS still works after change |
| Polling creates excessive load | Low | Low | 5s interval with debounce; only active when modal is open |
| Node.js version doesn't support native `fetch` | Low | Medium | Check Node version in Dockerfile; fallback to `http.get` if needed |
| Race condition: room deleted between status check and send | Low | Low | Existing send-to-game error handling already covers this case |

## Notes
- The `GameStateMessage` type already exists in `NetworkMessages.cs:133-138` but is never sent. This plan activates it.
- The `room.raceStarted` field in `server.js` partially tracks state but is boolean-only. The new `gamePhase` field provides richer state (Setup/Racing/Paused/Finished).
- Future enhancement (not in scope): Replace polling with a persistent WebSocket connection from the web app for real-time updates. This would be part of GAP 2 (Real-Time Race Viewer).
- The HTTP endpoint on the WS server is intentionally lightweight — no database, no auth. It reads from the in-memory `rooms` Map.
