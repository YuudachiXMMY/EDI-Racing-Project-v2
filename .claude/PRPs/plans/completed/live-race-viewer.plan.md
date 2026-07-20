# Plan: Live Race Viewer (Web App Real-Time 2D Minimap & Leaderboard)

## Summary
Add a standalone, public-access "Live Race" page to the web app that connects directly via WebSocket to the game server, receiving real-time `state_update`, `leaderboard`, and `event_triggered` messages. The page renders a live leaderboard, event feed, and a 2D top-down position minimap using HTML5 Canvas — enabling professors to project race state in a classroom without needing the Unity client.

## User Story
As a professor,
I want to view the live race state (leaderboard, car positions, events) in a web browser,
So that I can project it on a classroom screen without running the Unity game client.

## Problem -> Solution
**Current state:** Only students who join via Unity WebGL can watch the race. The web app manages surveys but has zero game visualization. The server already supports `web_join_room` for webapp clients and relays `race_results` to them, but does NOT relay `state_update`, `leaderboard`, or other real-time messages.

**Desired state:** A new page at `/#/live/:roomCode` connects via WebSocket, receives all race messages, and renders a live leaderboard + 2D position minimap + event feed. Accessible without login (public, like the student survey page), shareable via URL.

## Metadata
- **Complexity**: Large
- **Source PRD**: `.claude/PRPs/plans/web-unity-gap-analysis-and-hosting.plan.md`
- **PRD Phase**: GAP 2 — Real-Time Race Viewer
- **Estimated Files**: 10

---

## UX Design

### Before
```
Web App has no live game visualization.
Professor must run Unity WebGL to see race state.

┌──────────────────────────────────────┐
│  EditorPage                          │
│  [Questions] [Mappings] [Rules]      │
│  [Responses] [Results]               │
│                                      │
│  No live race view available.        │
│  "Send to Game" modal shows room     │
│  status badge only (Setup/Racing).   │
└──────────────────────────────────────┘
```

### After
```
New standalone Live Race page accessible at /#/live/ABCDEF

┌──────────────────────────────────────────────────┐
│  Live Race — Room ABCDEF          [Setup/Racing]  │
│  ┌──── Leaderboard ─────┐ ┌──── Minimap ──────┐ │
│  │  1. Team Alpha    L3  │ │  ·  ·   ·         │ │
│  │  2. Team Beta     L3  │ │    ·      ·  ·    │ │
│  │  3. Team Gamma    L2  │ │  ·    ·       ·   │ │
│  │  4. Team Delta    L2  │ │     ·   ·  ·      │ │
│  │  ...                  │ │  ·       ·        │ │
│  └───────────────────────┘ └───────────────────┘ │
│  ┌──── Event Feed ──────────────────────────────┐ │
│  │  12.3s  Tire Blowout — 3/10 cars affected    │ │
│  │   8.1s  Speed Boost — 5/10 cars affected     │ │
│  └──────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────┘

SendToGameModal adds a "Watch Live" link:
  🔗 Watch Live: /#/live/ABCDEF
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Live race viewing | Not possible in web app | New page at `/#/live/:roomCode` | Public access, no auth |
| SendToGameModal | Shows room status badge | Also shows "Watch Live" link | Opens in new tab |
| EditorPage header | "Send to Game" button only | "Send to Game" button + optional "Watch" | Quick access |
| Server relay | Only relays `race_results` to webapps | Relays all game messages to webapps | Server change |
| Server cache | Only caches `latestState` | Also caches `latestLeaderboard` | New cache field |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Server/server.js` | 1-362 | WebSocket server — modify relay logic and room structure |
| P0 (critical) | `Assets/Scripts/Network/NetworkMessages.cs` | 150-192 | state_update & leaderboard message schemas |
| P0 (critical) | `web-app/client/src/components/SendToGameModal.jsx` | 1-160 | Existing WebSocket integration pattern (REST proxy) |
| P0 (critical) | `web-app/client/src/App.jsx` | 1-24 | Router config — add new route |
| P1 (important) | `Assets/Scripts/Network/NetworkSync.cs` | 112-156 | How state_update and leaderboard are broadcast |
| P1 (important) | `web-app/client/src/components/RoomStatusBadge.jsx` | 1-39 | Component pattern to mirror |
| P1 (important) | `web-app/client/src/components/ResultsTab.jsx` | 1-158 | Table/display pattern to mirror |
| P1 (important) | `web-app/client/src/index.css` | 1-213 | Styling patterns (CSS variables, class naming) |
| P2 (reference) | `web-app/client/src/api.js` | 1-147 | API client pattern |
| P2 (reference) | `web-app/client/vite.config.js` | 1-16 | Dev proxy config |
| P2 (reference) | `Deploy/nginx/nginx.conf` | 28-37 | Production WebSocket proxy at /ws |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| WebSocket API | MDN Web Docs | Use native `new WebSocket()` — no library needed, already works in all browsers |
| HTML5 Canvas | MDN Web Docs | Use `CanvasRenderingContext2D` for 2D position rendering |
| React useRef + Canvas | React docs | Use `useRef` for canvas element, `useEffect` for draw loop |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```javascript
// SOURCE: web-app/client/src/components/*.jsx
// Files: PascalCase.jsx for components, camelCase.js for utilities/hooks
// Components: export default function ComponentName({ props }) { ... }
// CSS classes: kebab-case (e.g., room-status-racing, result-session-header)
// State: const [value, setValue] = useState(initial)
// Constants: UPPER_SNAKE_CASE at module top (e.g., POLL_INTERVAL, DEBOUNCE_DELAY)
```

### ERROR_HANDLING
```javascript
// SOURCE: web-app/client/src/components/SendToGameModal.jsx:38-55
// Pattern: try/catch with silent failure for non-critical operations
try {
  const roomRes = await getRoomResults(trimmed);
  if (roomRes.success && roomRes.data && roomRes.data.type === 'race_results') {
    // process data...
  }
} catch {
  // Non-critical: professor can retry later
}
```

### LOGGING_PATTERN
```javascript
// SOURCE: Server/server.js:55,88,187
// Server: console.log(`[Room ${roomCode}] Description`);
// Client: No console.log in production code
console.log(`[Room ${roomCode}] Web-app client joined`);
```

### API_RESPONSE_PATTERN
```javascript
// SOURCE: web-app/src/routes/game-status.js:9-18
// All API responses wrapped in { success: true, data: ... }
// Errors: { success: true, data: { exists: false, error: 'message' } }
router.get('/room-status/:code', async (req, res) => {
  try {
    const response = await fetch(`${GAME_HTTP_URL}/api/room-status/${code}`);
    const data = await response.json();
    res.json({ success: true, data });
  } catch {
    res.json({ success: true, data: { exists: false, error: 'Game server unreachable' } });
  }
});
```

### COMPONENT_PATTERN
```jsx
// SOURCE: web-app/client/src/components/RoomStatusBadge.jsx:1-39
// Pattern: functional component, no hooks for simple display
// Props destructured in function signature
// Early returns for loading/empty states
// CSS class composition with template literals
export default function RoomStatusBadge({ status, checking }) {
  if (checking) {
    return <div className="room-status room-status-setup">...</div>;
  }
  if (!status) return null;
  // ...render content
}
```

### CSS_PATTERN
```css
/* SOURCE: web-app/client/src/index.css:170-210 */
/* Pattern: CSS variables from :root, kebab-case class names */
/* Component sections marked with comments */
/* Consistent spacing: padding 10-16px, border-radius 4-8px */
/* Dark theme: var(--bg-card), var(--text), var(--accent) */
.results-tab { padding: 0; }
.result-session { border: 1px solid var(--border); border-radius: 6px; margin-bottom: 12px; overflow: hidden; }
```

### ROUTING_PATTERN
```jsx
// SOURCE: web-app/client/src/App.jsx:1-24
// HashRouter with Routes/Route
// Public pages: no wrapper (like StudentSurveyPage)
// Protected pages: wrapped in <ProtectedRoute>
<Route path="/s/:shareCode" element={<StudentSurveyPage />} />
```

### WEBSOCKET_SERVER_RELAY
```javascript
// SOURCE: Server/server.js:309-349
// Professor messages are broadcast to students via broadcastToStudents()
// Specific types (race_results) also relayed to webapps
// State cached on room object (latestState, raceResults, surveyData)
if (msg.type === 'race_results') {
  for (const webapp of room.webapps) {
    if (webapp.readyState === 1) webapp.send(raw);
  }
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Server/server.js` | UPDATE | Relay game messages to webapps; cache latest leaderboard |
| `web-app/client/src/hooks/useRaceWebSocket.js` | CREATE | WebSocket connection hook for live viewer |
| `web-app/client/src/pages/LiveRacePage.jsx` | CREATE | Main live race viewer page |
| `web-app/client/src/components/LiveLeaderboard.jsx` | CREATE | Real-time leaderboard display |
| `web-app/client/src/components/LiveEventFeed.jsx` | CREATE | Event log feed |
| `web-app/client/src/components/TrackMinimap.jsx` | CREATE | 2D canvas position minimap |
| `web-app/client/src/App.jsx` | UPDATE | Add /live/:roomCode route |
| `web-app/client/src/index.css` | UPDATE | Add live viewer styles |
| `web-app/client/vite.config.js` | UPDATE | Add WebSocket proxy for dev |
| `web-app/client/src/components/SendToGameModal.jsx` | UPDATE | Add "Watch Live" link |

## NOT Building

- **Full 3D rendering** — no WebGL or three.js; 2D canvas dots only
- **Track shape overlay** — waypoint data is Unity runtime (scene transforms), not exportable without Unity-side changes. The minimap shows dots without a track silhouette.
- **Chat or interaction** — viewer is read-only
- **Authentication for live page** — intentionally public so anyone with the room code URL can watch
- **Persistent replay** — live only, no recording/playback
- **Mobile-optimized layout** — basic responsive grid, no mobile-first design pass
- **Server-side WebSocket rate limiting for webapps** — Unity already sends at fixed intervals (10Hz state, 2Hz leaderboard)
- **Multi-room dashboard** — one room per page

---

## Step-by-Step Tasks

### Task 1: Update Server to Relay Game Messages to Webapps
- **ACTION**: Modify `Server/server.js` to relay `state_update`, `leaderboard`, `game_state`, `event_triggered`, `race_start`, `race_end` messages to all `room.webapps` clients, and cache `latestLeaderboard`.
- **IMPLEMENT**:
  1. Add `latestLeaderboard: null` to room object initialization (line 169-180)
  2. In the default message handler (line 309-349), after the existing `race_results` webapp relay block, add relay for all game messages to webapps:
     ```javascript
     // Cache leaderboard for late-joiners
     if (msg.type === 'leaderboard') {
       room.latestLeaderboard = raw;
     }
     
     // Relay all professor game messages to web-app viewers
     const WEBAPP_RELAY_TYPES = ['state_update', 'leaderboard', 'game_state', 
       'event_triggered', 'race_start', 'race_end', 'race_results'];
     if (WEBAPP_RELAY_TYPES.includes(msg.type)) {
       for (const webapp of room.webapps) {
         if (webapp.readyState === 1) webapp.send(raw);
       }
     }
     ```
  3. In `web_join_room` handler (line 278-289), send cached state to late-joining webapp:
     ```javascript
     // Send cached state to late-joining web viewer
     if (webRoom.latestState) ws.send(webRoom.latestState);
     if (webRoom.latestLeaderboard) ws.send(webRoom.latestLeaderboard);
     ```
  4. Remove the duplicate `race_results` webapp relay (lines 339-342) since it's now covered by the unified relay block.
- **MIRROR**: WEBSOCKET_SERVER_RELAY, LOGGING_PATTERN
- **IMPORTS**: None needed (Node.js built-ins)
- **GOTCHA**: The existing `race_results` relay at lines 339-342 must be REMOVED to avoid double-sending. The new unified relay block handles it. Also, `state_update` at 10Hz to webapps could be high bandwidth — acceptable for LAN classrooms, but note this for future optimization.
- **VALIDATE**: 
  1. Start server, create room, join as webapp via wscat
  2. Send a fake `state_update` message as professor → verify webapp receives it
  3. Join webapp AFTER race started → verify it receives `latestState` and `latestLeaderboard`

### Task 2: Create WebSocket Hook (`useRaceWebSocket`)
- **ACTION**: Create a custom React hook that manages WebSocket lifecycle, message parsing, and state for the live viewer.
- **IMPLEMENT**:
  ```javascript
  // web-app/client/src/hooks/useRaceWebSocket.js
  import { useState, useEffect, useRef, useCallback } from 'react';
  
  const RECONNECT_DELAY = 3000;
  const MAX_RECONNECT = 5;
  
  export default function useRaceWebSocket(roomCode) {
    const [connected, setConnected] = useState(false);
    const [gamePhase, setGamePhase] = useState('Connecting');
    const [cars, setCars] = useState([]);       // from race_start
    const [positions, setPositions] = useState([]);  // from state_update
    const [leaderboard, setLeaderboard] = useState([]); // from leaderboard
    const [events, setEvents] = useState([]);    // from event_triggered
    const [raceTime, setRaceTime] = useState(0);
    const wsRef = useRef(null);
    const reconnectCount = useRef(0);
    const reconnectTimer = useRef(null);
    
    const connect = useCallback(() => {
      // Derive WS URL from page location
      const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
      const wsUrl = `${protocol}//${location.host}/ws`;
      const ws = new WebSocket(wsUrl);
      wsRef.current = ws;
      
      ws.onopen = () => {
        setConnected(true);
        reconnectCount.current = 0;
        ws.send(JSON.stringify({ type: 'web_join_room', roomCode: roomCode.toUpperCase() }));
      };
      
      ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        switch (msg.type) {
          case 'room_joined':
            setGamePhase('Setup');
            break;
          case 'error':
            setGamePhase('Error');
            break;
          case 'race_start':
            setGamePhase('Racing');
            setCars(msg.cars || []);
            break;
          case 'state_update':
            setPositions(msg.cars || []);
            setRaceTime(msg.t || 0);
            break;
          case 'leaderboard':
            setLeaderboard(msg.rankings || []);
            break;
          case 'game_state':
            setGamePhase(msg.state || 'Setup');
            break;
          case 'event_triggered':
            setEvents(prev => [{ ...msg, timestamp: Date.now() }, ...prev].slice(0, 50));
            break;
          case 'race_end':
          case 'race_results':
            setGamePhase('Finished');
            break;
          case 'room_closed':
            setGamePhase('Closed');
            break;
        }
      };
      
      ws.onclose = () => {
        setConnected(false);
        wsRef.current = null;
        if (reconnectCount.current < MAX_RECONNECT) {
          reconnectCount.current++;
          reconnectTimer.current = setTimeout(connect, RECONNECT_DELAY);
        }
      };
      
      ws.onerror = () => ws.close();
    }, [roomCode]);
    
    useEffect(() => {
      if (!roomCode) return;
      connect();
      return () => {
        reconnectCount.current = MAX_RECONNECT; // prevent reconnect on unmount
        clearTimeout(reconnectTimer.current);
        if (wsRef.current) wsRef.current.close();
      };
    }, [roomCode, connect]);
    
    return { connected, gamePhase, cars, positions, leaderboard, events, raceTime };
  }
  ```
- **MIRROR**: NAMING_CONVENTION (camelCase file, UPPER_SNAKE constants), COMPONENT_PATTERN (hooks pattern from React docs)
- **IMPORTS**: `{ useState, useEffect, useRef, useCallback }` from `react`
- **GOTCHA**: 
  1. In dev mode, Vite proxy must forward `/ws` to the WS server (Task 8 handles this)
  2. WebSocket URL must be derived from `location.host`, not hardcoded
  3. Events list should be capped (`.slice(0, 50)`) to prevent memory growth
  4. Must set `reconnectCount` to max on unmount to prevent reconnect after component tears down
- **VALIDATE**: Import hook in a test component, verify connection establishes and messages parse correctly

### Task 3: Create LiveLeaderboard Component
- **ACTION**: Create a real-time leaderboard table component that updates every 0.5s as `leaderboard` messages arrive.
- **IMPLEMENT**:
  ```jsx
  // web-app/client/src/components/LiveLeaderboard.jsx
  export default function LiveLeaderboard({ rankings }) {
    if (!rankings || rankings.length === 0) {
      return <div className="live-leaderboard empty">Waiting for race data...</div>;
    }
    return (
      <div className="live-leaderboard">
        <h3>Leaderboard</h3>
        <table className="response-table">
          <thead>
            <tr><th>#</th><th>Team</th><th>Lap</th><th>CP</th></tr>
          </thead>
          <tbody>
            {rankings.map((entry, i) => (
              <tr key={i} className="response-row">
                <td className={entry.rank <= 3 ? `rank-${entry.rank}` : ''}>{entry.rank}</td>
                <td>{entry.name}</td>
                <td>{entry.lap}</td>
                <td>{entry.cp}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }
  ```
- **MIRROR**: COMPONENT_PATTERN (functional, props destructured), CSS_PATTERN (reuse `response-table`, `rank-*` classes from ResultsTab)
- **IMPORTS**: None (pure presentational component)
- **GOTCHA**: Leaderboard `rankings` uses `name`/`lap`/`cp` fields (not `TeamName`/`LapsCompleted`/`CheckpointsPassed` — those are the stored DB format). The WebSocket message format from Unity uses the short field names.
- **VALIDATE**: Render with mock data `[{ rank: 1, name: 'Team A', lap: 3, cp: 15 }]`

### Task 4: Create LiveEventFeed Component
- **ACTION**: Create a scrolling event feed that shows race events as they happen.
- **IMPLEMENT**:
  ```jsx
  // web-app/client/src/components/LiveEventFeed.jsx
  export default function LiveEventFeed({ events }) {
    if (!events || events.length === 0) {
      return <div className="live-events empty">No events yet</div>;
    }
    return (
      <div className="live-events">
        <h3>Event Feed</h3>
        <div className="event-list">
          {events.map((evt, i) => (
            <div key={i} className="event-item">
              <span className="event-name">{evt.name}</span>
              <span className="event-detail">{evt.affected}/{evt.total} cars</span>
            </div>
          ))}
        </div>
      </div>
    );
  }
  ```
- **MIRROR**: COMPONENT_PATTERN, CSS_PATTERN
- **IMPORTS**: None
- **GOTCHA**: Events accumulate in reverse order (newest first) — handled by the hook's `setEvents` prepend logic.
- **VALIDATE**: Render with mock event data

### Task 5: Create TrackMinimap Component
- **ACTION**: Create a 2D HTML5 Canvas component that renders car positions as colored dots using `px`/`pz` coordinates (top-down view).
- **IMPLEMENT**:
  ```jsx
  // web-app/client/src/components/TrackMinimap.jsx
  import { useRef, useEffect } from 'react';
  
  const COLORS = [
    '#e04040', '#4a9eff', '#40a040', '#e0a020', '#a040e0',
    '#e07020', '#20c0c0', '#c060a0', '#80b040', '#4060e0',
    '#c0c040', '#6080b0', '#b04040', '#40b0a0', '#a0a0a0',
  ];
  const PADDING = 20;
  const DOT_RADIUS = 6;
  
  export default function TrackMinimap({ positions, cars }) {
    const canvasRef = useRef(null);
    
    useEffect(() => {
      const canvas = canvasRef.current;
      if (!canvas || !positions || positions.length === 0) return;
      
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = canvas.height;
      
      // Compute bounding box from positions
      let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
      for (const p of positions) {
        if (p.px < minX) minX = p.px;
        if (p.px > maxX) maxX = p.px;
        if (p.pz < minZ) minZ = p.pz;
        if (p.pz > maxZ) maxZ = p.pz;
      }
      
      // Add margin and handle degenerate case
      const rangeX = Math.max(maxX - minX, 1);
      const rangeZ = Math.max(maxZ - minZ, 1);
      const scale = Math.min((w - PADDING * 2) / rangeX, (h - PADDING * 2) / rangeZ);
      const offsetX = (w - rangeX * scale) / 2;
      const offsetZ = (h - rangeZ * scale) / 2;
      
      // Clear
      ctx.fillStyle = '#14141f';
      ctx.fillRect(0, 0, w, h);
      
      // Draw grid
      ctx.strokeStyle = '#2a2a3a';
      ctx.lineWidth = 0.5;
      // ...minimal grid lines...
      
      // Draw car dots
      for (const p of positions) {
        const x = offsetX + (p.px - minX) * scale;
        const y = offsetZ + (p.pz - minZ) * scale;
        const color = COLORS[p.i % COLORS.length];
        
        // Dot
        ctx.beginPath();
        ctx.arc(x, y, DOT_RADIUS, 0, Math.PI * 2);
        ctx.fillStyle = color;
        ctx.fill();
        
        // Label (team name from cars array if available)
        const carName = cars && cars[p.i] ? cars[p.i].teamName : `#${p.i + 1}`;
        ctx.fillStyle = '#e0e0e0';
        ctx.font = '10px system-ui';
        ctx.textAlign = 'center';
        ctx.fillText(carName, x, y - DOT_RADIUS - 3);
      }
    }, [positions, cars]);
    
    return (
      <div className="track-minimap">
        <h3>Track View</h3>
        <canvas ref={canvasRef} width={400} height={300} className="minimap-canvas" />
      </div>
    );
  }
  ```
- **MIRROR**: CSS_PATTERN (colors from CSS variables mapped to JS constants)
- **IMPORTS**: `{ useRef, useEffect }` from `react`
- **GOTCHA**:
  1. Unity coordinates: `px` = X (left/right), `pz` = Z (forward/back), `py` = Y (up/down, ignored in 2D)
  2. The bounding box auto-scales, so cars at the start clustered together may look odd — spread out once racing begins
  3. Canvas must be explicitly sized (width/height attributes, not just CSS)
  4. At 10Hz updates (100ms), canvas redraws are fast enough for smooth animation
  5. If `positions` is empty (before race starts), show nothing — no crash
- **VALIDATE**: Render with mock positions `[{i:0, px:10, py:0, pz:20, ry:90, l:1, c:5}]`

### Task 6: Create LiveRacePage
- **ACTION**: Create the main page component that composes the hook and child components into a full-page live viewer.
- **IMPLEMENT**:
  ```jsx
  // web-app/client/src/pages/LiveRacePage.jsx
  import { useParams } from 'react-router-dom';
  import useRaceWebSocket from '../hooks/useRaceWebSocket.js';
  import LiveLeaderboard from '../components/LiveLeaderboard.jsx';
  import LiveEventFeed from '../components/LiveEventFeed.jsx';
  import TrackMinimap from '../components/TrackMinimap.jsx';
  
  const PHASE_LABELS = {
    Connecting: 'Connecting...',
    Setup: 'Waiting for Race',
    Racing: 'Race In Progress',
    Paused: 'Paused',
    Finished: 'Race Finished',
    Closed: 'Room Closed',
    Error: 'Room Not Found',
  };
  
  export default function LiveRacePage() {
    const { roomCode } = useParams();
    const { connected, gamePhase, cars, positions, leaderboard, events, raceTime } = useRaceWebSocket(roomCode);
    
    const phaseClass = {
      Setup: 'room-status-setup',
      Racing: 'room-status-racing',
      Paused: 'room-status-racing',
      Finished: 'room-status-finished',
      Error: 'room-status-error',
      Closed: 'room-status-error',
    }[gamePhase] || 'room-status-setup';
    
    return (
      <div className="live-race-page">
        <header className="live-header">
          <h1>Live Race</h1>
          <div className="live-room-info">
            <span className="live-room-code">Room {roomCode?.toUpperCase()}</span>
            <div className={`room-status ${phaseClass}`}>
              <span className="status-dot"></span>
              {PHASE_LABELS[gamePhase] || gamePhase}
            </div>
          </div>
          {gamePhase === 'Racing' && (
            <span className="live-timer">{raceTime.toFixed(1)}s</span>
          )}
          <span className={`live-connection ${connected ? 'connected' : 'disconnected'}`}>
            {connected ? 'Connected' : 'Disconnected'}
          </span>
        </header>
        
        {(gamePhase === 'Error' || gamePhase === 'Closed') && (
          <div className="live-message">
            <p>{gamePhase === 'Error' ? 'Room not found. Check the room code and try again.' : 'The room has been closed by the host.'}</p>
          </div>
        )}
        
        {gamePhase === 'Setup' && (
          <div className="live-message">
            <p>Connected to room. Waiting for the host to start the race...</p>
          </div>
        )}
        
        {(gamePhase === 'Racing' || gamePhase === 'Paused' || gamePhase === 'Finished') && (
          <div className="live-grid">
            <LiveLeaderboard rankings={leaderboard} />
            <TrackMinimap positions={positions} cars={cars} />
            <LiveEventFeed events={events} />
          </div>
        )}
      </div>
    );
  }
  ```
- **MIRROR**: ROUTING_PATTERN (useParams), COMPONENT_PATTERN, RoomStatusBadge phase class mapping
- **IMPORTS**: `useParams` from `react-router-dom`, custom hook and child components
- **GOTCHA**: The `roomCode` from URL params may be lowercase — always `.toUpperCase()` before sending to server. The page should work gracefully in all phases (no crash if positions/leaderboard are empty arrays).
- **VALIDATE**: Navigate to `/#/live/TESTCODE` — should show connecting state, then error if no room exists

### Task 7: Add Route in App.jsx
- **ACTION**: Add the `/live/:roomCode` route to the hash router. This is a public route (no auth wrapper).
- **IMPLEMENT**:
  1. Import: `import LiveRacePage from './pages/LiveRacePage.jsx';`
  2. Add route before the wildcard route:
     ```jsx
     <Route path="/live/:roomCode" element={<LiveRacePage />} />
     ```
- **MIRROR**: ROUTING_PATTERN — matches `StudentSurveyPage` pattern (public, no `ProtectedRoute` wrapper)
- **IMPORTS**: `LiveRacePage` from `./pages/LiveRacePage.jsx`
- **GOTCHA**: Must be placed BEFORE the `path="*"` catch-all route or it won't match
- **VALIDATE**: Navigate to `/#/live/ABCDEF` — LiveRacePage renders

### Task 8: Add WebSocket Proxy in Vite Dev Config
- **ACTION**: Add a WebSocket proxy entry in `vite.config.js` so that dev mode connects to the local WS server.
- **IMPLEMENT**:
  ```javascript
  // Add to proxy config in vite.config.js
  '/ws': {
    target: 'ws://localhost:8080',
    ws: true,
  }
  ```
- **MIRROR**: Existing `/api` proxy pattern in same file
- **IMPORTS**: None
- **GOTCHA**: The `ws: true` flag is required for Vite to handle WebSocket upgrade. The target port (8080) matches the server's default `PORT`. In production, nginx already handles this (Deploy/nginx/nginx.conf:28-37).
- **VALIDATE**: Run `npm run dev` in client, connect to `ws://localhost:5173/ws` via wscat — should proxy to 8080

### Task 9: Add Live Viewer Styles
- **ACTION**: Append CSS for the live race viewer to `web-app/client/src/index.css`.
- **IMPLEMENT**:
  ```css
  /* --- Live Race Viewer --- */
  .live-race-page { display: flex; flex-direction: column; height: 100vh; background: var(--bg); }
  .live-header { display: flex; align-items: center; gap: 16px; padding: 12px 20px; border-bottom: 1px solid var(--border); }
  .live-header h1 { font-size: 20px; margin: 0; }
  .live-room-info { display: flex; align-items: center; gap: 10px; }
  .live-room-code { font-size: 16px; font-weight: 600; letter-spacing: 1px; color: var(--accent); }
  .live-timer { font-size: 18px; font-weight: 600; font-variant-numeric: tabular-nums; color: var(--warning); }
  .live-connection { font-size: 12px; margin-left: auto; padding: 4px 8px; border-radius: 4px; }
  .live-connection.connected { background: rgba(64,160,64,.15); color: var(--success); }
  .live-connection.disconnected { background: rgba(224,64,64,.15); color: var(--danger); }
  
  .live-message { display: flex; align-items: center; justify-content: center; flex: 1; color: var(--text-dim); font-size: 16px; }
  
  .live-grid { display: grid; grid-template-columns: 1fr 1fr; grid-template-rows: 1fr auto; gap: 16px; padding: 16px; flex: 1; overflow: auto; }
  .live-leaderboard { grid-row: 1; }
  .live-leaderboard h3, .live-events h3, .track-minimap h3 { font-size: 14px; color: var(--text-dim); margin-bottom: 8px; text-transform: uppercase; letter-spacing: 1px; }
  .live-events { grid-column: 1 / -1; max-height: 180px; overflow-y: auto; }
  .event-list { display: flex; flex-direction: column; gap: 4px; }
  .event-item { display: flex; align-items: center; gap: 12px; padding: 6px 10px; background: var(--bg-card); border-radius: 4px; font-size: 13px; }
  .event-name { font-weight: 500; color: var(--warning); }
  .event-detail { color: var(--text-dim); font-size: 12px; }
  
  .track-minimap { grid-row: 1; }
  .minimap-canvas { width: 100%; height: auto; border: 1px solid var(--border); border-radius: 6px; background: var(--bg); }
  ```
- **MIRROR**: CSS_PATTERN (variables, naming, spacing)
- **IMPORTS**: N/A (CSS)
- **GOTCHA**: Canvas CSS `width: 100%` stretches the canvas display — the `width`/`height` attributes on the element set the drawing resolution. These are different. Set element attributes to a reasonable resolution (e.g., 400x300) and let CSS scale the display.
- **VALIDATE**: Visual inspection — consistent with existing dark theme

### Task 10: Add "Watch Live" Link in SendToGameModal
- **ACTION**: When a room is found and in Racing/Setup state, show a "Watch Live" link that opens the live viewer.
- **IMPLEMENT**: After the `RoomStatusBadge` in `SendToGameModal.jsx`, add:
  ```jsx
  {roomStatus && roomStatus.exists && (
    <a
      href={`#/live/${roomCode.trim().toUpperCase()}`}
      target="_blank"
      rel="noopener"
      className="live-link"
    >
      Watch Live Race
    </a>
  )}
  ```
  And add minimal CSS:
  ```css
  .live-link { display: inline-block; font-size: 13px; color: var(--accent); margin-bottom: 12px; text-decoration: none; }
  .live-link:hover { text-decoration: underline; }
  ```
- **MIRROR**: COMPONENT_PATTERN, CSS_PATTERN
- **IMPORTS**: None
- **GOTCHA**: Using `target="_blank"` so professor's editor workflow is not interrupted. The hash-based route (`#/live/...`) works correctly with `<a href>` — no need for React Router's `Link`.
- **VALIDATE**: Open SendToGameModal, enter valid room code → "Watch Live Race" link appears

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| useRaceWebSocket connects | Valid room code | `connected: true`, `gamePhase: 'Setup'` | No |
| useRaceWebSocket handles state_update | `{ type: 'state_update', t: 5.2, cars: [...] }` | `positions` updated, `raceTime: 5.2` | No |
| useRaceWebSocket handles room_closed | `{ type: 'room_closed' }` | `gamePhase: 'Closed'` | No |
| useRaceWebSocket reconnects | WS close event | Reconnect attempt after 3s | No |
| useRaceWebSocket caps events | 60 event_triggered messages | `events.length <= 50` | Yes |
| LiveLeaderboard empty state | `rankings: []` | Shows "Waiting for race data..." | Yes |
| LiveLeaderboard renders | 5 rankings | 5 rows with rank/name/lap/cp | No |
| TrackMinimap no positions | `positions: []` | Canvas not drawn (no crash) | Yes |
| TrackMinimap single car | 1 position | Single dot rendered, no division by zero | Yes |
| Server relays to webapps | Professor sends state_update | All webapps in room receive it | No |
| Server caches leaderboard | Professor sends leaderboard | Late-joining webapp gets cached leaderboard | No |
| Server late-join state | Webapp joins mid-race | Receives latestState + latestLeaderboard | No |

### Edge Cases Checklist
- [x] Empty leaderboard (before race starts)
- [x] No positions (before race starts)
- [x] Single car position (degenerate bounding box)
- [x] Room not found (error state)
- [x] Room closed mid-viewing
- [x] WebSocket disconnect + auto-reconnect
- [x] Late-joining viewer (receives cached state)
- [x] 50-car race (leaderboard capped at 15 by Unity, positions handle all)
- [x] Multiple webapp viewers on same room
- [x] URL with lowercase room code (toUpperCase normalization)

---

## Validation Commands

### Static Analysis
```bash
cd web-app/client && npx oxlint src/hooks/useRaceWebSocket.js src/pages/LiveRacePage.jsx src/components/LiveLeaderboard.jsx src/components/LiveEventFeed.jsx src/components/TrackMinimap.jsx
```
EXPECT: Zero lint errors

### Dev Server
```bash
cd web-app && npm run dev &
cd web-app/client && npm run dev &
cd Server && node server.js &
```
EXPECT: All three processes start without errors

### Manual Validation
- [ ] Navigate to `/#/live/INVALID` — shows "Room Not Found" error state
- [ ] Start WS server + create room via Unity or wscat → navigate to `/#/live/{CODE}` — shows "Waiting for Race"
- [ ] Start race → leaderboard populates, minimap shows car dots, events appear
- [ ] Open second browser tab to same URL → both receive updates simultaneously
- [ ] Close the room → live page shows "Room Closed"
- [ ] Disconnect server → live page shows "Disconnected", auto-reconnects when server restarts
- [ ] Open SendToGameModal → enter room code → "Watch Live Race" link appears and opens new tab
- [ ] Verify Vite dev proxy works: `ws://localhost:5173/ws` connects successfully
- [ ] Verify production nginx: Docker build serves live page at `/survey/#/live/{CODE}`

### Browser Validation
```bash
cd web-app/client && npm run build
```
EXPECT: Build succeeds with no errors, `dist/` contains updated bundle

---

## Acceptance Criteria
- [ ] All 10 tasks completed
- [ ] Server relays game messages (state_update, leaderboard, etc.) to webapp clients
- [ ] Server caches latestLeaderboard for late-joining viewers
- [ ] Live page at `/#/live/:roomCode` renders without authentication
- [ ] Leaderboard updates in real-time (every 0.5s)
- [ ] Minimap shows car positions as colored dots (updated every 0.1s)
- [ ] Event feed shows race events as they happen
- [ ] WebSocket auto-reconnects on disconnect (up to 5 attempts)
- [ ] "Watch Live Race" link appears in SendToGameModal
- [ ] No lint errors, build succeeds
- [ ] Works in development (Vite proxy) and production (nginx proxy)

## Completion Checklist
- [ ] Code follows discovered patterns (functional components, CSS variables, no console.log in client)
- [ ] Error handling matches codebase style (try/catch with graceful fallback)
- [ ] Logging follows codebase conventions (`[Room CODE] ...` on server)
- [ ] CSS uses existing variables (`--bg`, `--accent`, `--border`, etc.)
- [ ] Component structure mirrors existing files (RoomStatusBadge, ResultsTab)
- [ ] No hardcoded URLs (derived from `location.host`)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 10Hz state_update bandwidth to webapps | Medium | Low | Acceptable for LAN; can add throttling later if needed |
| Canvas rendering performance with 50 cars | Low | Low | 50 dots + labels at 10Hz is trivial for modern browsers |
| WebSocket proxy in dev mode doesn't upgrade | Medium | Medium | Vite `ws: true` flag handles this; test early |
| Multiple webapps per room increases server memory | Low | Low | WebSocket connections are lightweight; room data is shared |
| Room closed between URL copy and page load | Low | Low | Error state shown gracefully |

## Notes
- The minimap renders car positions without a track silhouette. Adding track shape would require exporting WaypointPath data from Unity (which is scene transform data, not easily serializable). This is a future enhancement (not in scope).
- The `web_join_room` message type and `room.webapps` Set already exist in the server — this plan leverages existing infrastructure rather than creating new connection types.
- The leaderboard message from Unity caps at 15 entries (`NetworkSync.cs:143`). For large races (50 cars), only the top 15 are shown in the leaderboard. All 50 are shown on the minimap via state_update positions.
- The live page is intentionally public (no auth) — same philosophy as the student survey page (`/s/:shareCode`). The room code acts as a lightweight access control.
