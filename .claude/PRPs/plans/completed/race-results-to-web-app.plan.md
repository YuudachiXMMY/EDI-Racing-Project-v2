# Plan: Race Results → Web App

## Summary
When a race finishes in Unity, send structured results (rankings, lap times, event log) via WebSocket to the server, persist them in SQLite, and display them in a new "Results" tab in the web app's survey editor. This closes the professor's feedback loop — they can review race outcomes without being at the game screen.

## User Story
As a professor,
I want to see race results in the web app after a race finishes,
So that I can review outcomes, compare with survey responses, and share data with students without needing the Unity game open.

## Problem → Solution
**Current:** Race results exist only inside Unity. The professor must press X to download a CSV file from the browser. The web app has no idea the race finished or what happened.

**Desired:** After a race finishes, results automatically flow Unity → WebSocket server → SQLite database → Web app UI. The professor can view results in a "Results" tab per survey, and also download them as CSV or JSON.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/plans/web-unity-gap-analysis-and-hosting.plan.md`
- **PRD Phase**: GAP 1: Race Results → Web App
- **Estimated Files**: 10

---

## UX Design

### Before
```
┌─────────────────────────────────────────────────────────────────┐
│  Race finishes in Unity                                          │
│                                                                  │
│  Professor presses X → browser downloads results.csv             │
│  Web app shows: [Questions] [Mappings] [Rules] [Responses]       │
│  No "Results" tab. No race data. CSV file is local only.         │
└─────────────────────────────────────────────────────────────────┘
```

### After
```
┌─────────────────────────────────────────────────────────────────┐
│  Race finishes in Unity                                          │
│                                                                  │
│  Unity auto-sends race_results via WebSocket → server stores     │
│  Web app shows: [Questions] [Mappings] [Rules] [Responses] [Results]
│                                                                  │
│  Results tab:                                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │ Race Session — 2026-07-20 14:30  │  Total: 45.2s  │ 5 cars  │ │
│  │                                                             │ │
│  │  Rank  Team        Laps  Checkpoints  Time   Attributes...  │ │
│  │  1     Alpha       3     21           42.1s  speed=8 ...    │ │
│  │  2     Beta        3     19           43.5s  speed=6 ...    │ │
│  │  3     Gamma       2     15           45.2s  speed=5 ...    │ │
│  │                                                             │ │
│  │  Event Log:                                                 │ │
│  │  12.3s  Snow Storm   (3/5 affected)                         │ │
│  │  28.1s  Night Mode   (5/5 affected)                         │ │
│  │                                                             │ │
│  │  [Download CSV]  [Download JSON]                             │ │
│  └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Race finish | Manual X key → CSV download | Auto-send via WS + manual X still works | Non-breaking addition |
| Web app tabs | 4 tabs (Q/M/R/Resp) | 5 tabs (Q/M/R/Resp/Results) | New tab at end |
| Results viewing | Open CSV in Excel | View in web app table + download CSV/JSON | Better UX |
| Multiple sessions | Only latest CSV downloaded | All sessions stored in DB, scrollable | Persistent history |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Data/SessionData.cs` | 114-158 | `RaceResults`, `CarResult`, `EventLogEntry` — the source data structures |
| P0 | `Assets/Scripts/Race/ScoreManager.cs` | 43-68 | `CollectResults()` — how results are assembled |
| P0 | `Server/server.js` | 205-238 | Default message relay pattern (professor→students) |
| P0 | `web-app/src/routes/export.js` | 117-201 | `send-to-game` — the exact WS relay pattern to mirror in reverse |
| P0 | `web-app/client/src/pages/EditorPage.jsx` | all | Tab structure, state management, component rendering |
| P1 | `Assets/Scripts/Network/NetworkSync.cs` | 157-175 | How host sends messages via `NetworkManager.Send` |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | 184-188 | Existing `RaceEndMessage` (currently empty body) |
| P1 | `web-app/client/src/components/ResponsesTab.jsx` | all | Table pattern to mirror for ResultsTab |
| P1 | `web-app/client/src/api.js` | all | API helper pattern |
| P2 | `web-app/src/schema.sql` | all | DB schema pattern |
| P2 | `web-app/src/routes/game-status.js` | all | Simple route pattern |
| P2 | `web-app/src/db.js` | all | `getDb()` pattern |

## External Documentation
No external research needed — feature uses established internal patterns (WebSocket relay, Express routes, SQLite, React components).

---

## Patterns to Mirror

### NAMING_CONVENTION
```js
// SOURCE: web-app/client/src/pages/EditorPage.jsx:11
const TABS = ['Questions', 'Mappings', 'Rules', 'Responses'];
// Tab components: PascalCase + Tab suffix → ResultsTab
// Route files: kebab-case → results.js
// API functions: camelCase → getResults(), getRaceResults()
```

### ERROR_HANDLING
```js
// SOURCE: web-app/src/routes/export.js:88-90
const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
  .get(req.params.id, req.user.userId);
if (!survey) {
  return res.status(404).json({ success: false, error: 'Survey not found' });
}
```

### API_RESPONSE_FORMAT
```js
// SOURCE: web-app/client/src/api.js:19-33
// All API functions return { success: boolean, data?: T, error?: string }
// The request() helper handles auth headers and 401 redirects
async function request(path, options = {}) { /* ... */ }
```

### WEBSOCKET_RELAY_PATTERN
```js
// SOURCE: Server/server.js:213-229
// Professor→Students: parse type, update room state, broadcastToStudents
if (info.role === 'professor') {
  if (msg.type === 'race_end') {
    room.gamePhase = 'Finished';
  }
  broadcastToStudents(info.roomCode, raw);
}
```

### UNITY_JSON_SERIALIZATION
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:123-124
// Unity uses JsonUtility.ToJson for all messages
NetworkManager.Send(JsonUtility.ToJson(msg));
```

### DB_ACCESS_PATTERN
```js
// SOURCE: web-app/src/db.js:12
// Synchronous better-sqlite3, called via getDb()
const db = getDb();
const results = db.prepare('SELECT * FROM race_results WHERE survey_id = ?').all(surveyId);
```

### COMPONENT_PATTERN
```jsx
// SOURCE: web-app/client/src/components/ResponsesTab.jsx:4-7
// Tab components: receive props, manage own loading state
export default function ResponsesTab({ surveyId }) {
  const [responses, setResponses] = useState([]);
  const [loading, setLoading] = useState(true);
  // ...
}
```

### CSS_PATTERN
```css
/* SOURCE: web-app/client/src/index.css:161-163 */
/* BEM-like class naming, dark theme with CSS variables */
.response-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.response-table th { text-align: left; padding: 8px 10px; border-bottom: 2px solid var(--border); }
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add `RaceResultsMessage` with full results payload |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Send `race_results` message when race finishes |
| `Server/server.js` | UPDATE | Handle `race_results` message type — relay to webapp clients AND store on room object |
| `web-app/src/schema.sql` | UPDATE | Add `race_results` table |
| `web-app/src/routes/results.js` | CREATE | REST endpoints: POST to store, GET to retrieve results |
| `web-app/src/index.js` | UPDATE | Mount results route |
| `web-app/client/src/api.js` | UPDATE | Add `getRaceResults()` function |
| `web-app/client/src/components/ResultsTab.jsx` | CREATE | Results display component with table + event log |
| `web-app/client/src/pages/EditorPage.jsx` | UPDATE | Add "Results" tab (index 4) |
| `web-app/client/src/index.css` | UPDATE | Add styles for results tab |

## NOT Building

- Real-time live results streaming (this is post-race, not during-race)
- Race replay functionality
- Results comparison across multiple surveys
- Student-facing results view (professor-only for now)
- Results editing/deletion UI
- Results notification/push to web app (professor polls/refreshes)

---

## Step-by-Step Tasks

### Task 1: Add RaceResultsMessage to NetworkMessages.cs

- **ACTION**: Add a new serializable message type for race results
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Professor → Server: sends race results after race completion.
  /// resultsJson is double-serialized RaceResults (JsonUtility limitation).
  /// </summary>
  [Serializable]
  public class RaceResultsMessage
  {
      public string type = "race_results";
      public string configName;
      public string resultsJson;
  }
  ```
  Note: We use double-serialization (`resultsJson` as a string) because `JsonUtility.ToJson` cannot handle nested arrays of complex types (`CarResult[]` with nested `AttributeEntry[]`) in a single pass. This mirrors the exact pattern used by `SurveyImportMessage.exportJson` and `SurveyQuestionsMessage.configJson`.
- **MIRROR**: `SurveyImportMessage` pattern at NetworkMessages.cs:239-245
- **IMPORTS**: Already imported (`System`, `UnityEngine`)
- **GOTCHA**: `JsonUtility` requires `[Serializable]` on both the message and all nested types. `RaceResults`, `CarResult`, `EventLogEntry`, `AttributeEntry` are already `[Serializable]` in SessionData.cs.
- **VALIDATE**: Unity project compiles without errors

### Task 2: Send race_results from NetworkSync when race finishes

- **ACTION**: Subscribe to `RaceManager.OnRaceFinished` event and send results via WebSocket
- **IMPLEMENT**: In `NetworkSync.Start()`, add subscription:
  ```csharp
  if (RaceManager != null)
  {
      RaceManager.OnRaceFinished += OnRaceFinishedHandler;
  }
  ```
  In `NetworkSync.OnDestroy()`, add unsubscription:
  ```csharp
  if (RaceManager != null)
      RaceManager.OnRaceFinished -= OnRaceFinishedHandler;
  ```
  Add the handler method:
  ```csharp
  private void OnRaceFinishedHandler(CarIdentity winner)
  {
      if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
      if (ScoreManager == null) return;

      var results = ScoreManager.CollectResults(
          RaceManager.GetEventLog(),
          Time.time - RaceManager.RaceStartTime
      );

      string configName = "";
      if (RaceManager.SurveyConfigManager != null &&
          RaceManager.SurveyConfigManager.ActiveConfig != null)
      {
          configName = RaceManager.SurveyConfigManager.ActiveConfig.ConfigName ?? "";
      }

      var msg = new RaceResultsMessage
      {
          configName = configName,
          resultsJson = JsonUtility.ToJson(results)
      };
      NetworkManager.Send(JsonUtility.ToJson(msg));
      Debug.Log($"[NetworkSync] Race results sent ({results.Rankings.Length} cars)");
  }
  ```
- **MIRROR**: `OnStateChanged` method pattern at NetworkSync.cs:157-162
- **IMPORTS**: Already available
- **GOTCHA**: Need to expose `eventLog` and `raceStartTime` from `RaceManager` — currently private. Add two public read-only accessors:
  - `RaceManager.GetEventLog()` → returns `IReadOnlyList<EventLogEntry>` or `List<EventLogEntry>`
  - `RaceManager.RaceStartTime` → public getter for `raceStartTime`
- **VALIDATE**: When race finishes with WS connection active, check Unity console for the log message

### Task 3: Expose eventLog and raceStartTime from RaceManager

- **ACTION**: Add public accessors so NetworkSync can read them
- **IMPLEMENT**: In `RaceManager.cs`, add:
  ```csharp
  /// <summary>Race start time (Time.time), used for elapsed time calculation.</summary>
  public float RaceStartTime => raceStartTime;

  /// <summary>Event log for current race. Returns copy to avoid mutation.</summary>
  public List<EventLogEntry> GetEventLog() => new List<EventLogEntry>(eventLog);
  ```
- **MIRROR**: Existing public accessors like `SpawnedCars` (line 44) and `CurrentState` (line 41)
- **IMPORTS**: None needed
- **GOTCHA**: Return a copy of `eventLog` to prevent external mutation (immutability principle)
- **VALIDATE**: Compiles; `NetworkSync` can call both accessors

### Task 4: Handle race_results in WebSocket server

- **ACTION**: Add `race_results` as a recognized message type that gets relayed to webapp clients AND cached on the room
- **IMPLEMENT**: In `server.js`, add to the room data structure:
  ```js
  // Add to rooms.set() in create_room handler:
  raceResults: null,  // cached race results JSON
  ```
  In the `default` case for professor messages (around line 225), add:
  ```js
  } else if (msg.type === 'race_results') {
    room.raceResults = raw; // cache for late-joining web-app
    room.gamePhase = 'Finished';
  }
  ```
  Also relay to any webapp clients — add a new Set to track them:
  ```js
  // In room data structure:
  webapps: new Set(),
  ```
  In `web_join_room` handler (line 180), add the ws to webapps set:
  ```js
  webRoom.webapps.add(ws);
  ```
  After `broadcastToStudents` in the default professor handler (line 229), add:
  ```js
  // Also relay to web-app clients
  if (msg.type === 'race_results') {
    for (const webapp of room.webapps) {
      if (webapp.readyState === 1) webapp.send(raw);
    }
  }
  ```
  In cleanup, remove webapp from set.
- **MIRROR**: Existing `latestState` caching pattern at server.js:219
- **IMPORTS**: None
- **GOTCHA**: The webapp sends data TO professor (`survey_import`), but here professor sends data FOR webapp clients. The relay direction is reversed. Web-app clients need to be identifiable — they're already tagged as `role: 'webapp'` in `clientRooms`.
- **VALIDATE**: Start server, create room, send a `race_results` message, verify it's cached on room object

### Task 5: Add race_results table to SQLite schema

- **ACTION**: Create a new table to persist race results per survey
- **IMPLEMENT**: Append to `web-app/src/schema.sql`:
  ```sql
  -- Race results sent from Unity game
  CREATE TABLE IF NOT EXISTS race_results (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    survey_id INTEGER NOT NULL REFERENCES surveys(id),
    room_code TEXT NOT NULL,
    config_name TEXT NOT NULL DEFAULT '',
    rankings_json TEXT NOT NULL DEFAULT '[]',
    event_log_json TEXT NOT NULL DEFAULT '[]',
    total_race_time REAL NOT NULL DEFAULT 0,
    received_at TEXT NOT NULL DEFAULT (datetime('now'))
  );
  ```
- **MIRROR**: `responses` table pattern at schema.sql:30-38
- **IMPORTS**: N/A (SQL DDL)
- **GOTCHA**: No unique constraint needed — a professor may run multiple races for the same survey. Use `survey_id` as a foreign key but allow multiple results per survey.
- **VALIDATE**: Delete the SQLite DB file, restart server, verify table is created

### Task 6: Create results REST route

- **ACTION**: Create `web-app/src/routes/results.js` with GET and POST endpoints
- **IMPLEMENT**:
  ```js
  import { Router } from 'express';
  import { getDb } from '../db.js';
  import { requireAuth } from '../middleware/auth.js';

  const router = Router();

  // POST /api/surveys/:id/results — store race results from WS relay
  router.post('/:id/results', requireAuth, (req, res) => {
    const db = getDb();
    const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
      .get(req.params.id, req.user.userId);
    if (!survey) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }

    const { roomCode, configName, rankings, eventLog, totalRaceTime } = req.body;
    if (!rankings || !Array.isArray(rankings)) {
      return res.status(400).json({ success: false, error: 'rankings array is required' });
    }

    const result = db.prepare(
      `INSERT INTO race_results (survey_id, room_code, config_name, rankings_json, event_log_json, total_race_time)
       VALUES (?, ?, ?, ?, ?, ?)`
    ).run(
      survey.id,
      roomCode || '',
      configName || '',
      JSON.stringify(rankings),
      JSON.stringify(eventLog || []),
      totalRaceTime || 0
    );

    res.json({ success: true, data: { id: result.lastInsertRowid } });
  });

  // GET /api/surveys/:id/results — fetch all race results for a survey
  router.get('/:id/results', requireAuth, (req, res) => {
    const db = getDb();
    const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
      .get(req.params.id, req.user.userId);
    if (!survey) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }

    const results = db.prepare(
      'SELECT * FROM race_results WHERE survey_id = ? ORDER BY received_at DESC'
    ).all(survey.id);

    const parsed = results.map(r => ({
      id: r.id,
      roomCode: r.room_code,
      configName: r.config_name,
      rankings: JSON.parse(r.rankings_json),
      eventLog: JSON.parse(r.event_log_json),
      totalRaceTime: r.total_race_time,
      receivedAt: r.received_at,
    }));

    res.json({ success: true, data: parsed });
  });

  export default router;
  ```
- **MIRROR**: `export.js` GET handler pattern at lines 84-114
- **IMPORTS**: `Router` from express, `getDb` from db.js, `requireAuth` from middleware
- **GOTCHA**: Results are stored as JSON strings in SQLite (same as `answers_json`, `questions_json`). Parse them on retrieval.
- **VALIDATE**: `curl -X POST /api/surveys/1/results` with auth token and body → returns `{ success: true, data: { id: 1 } }`

### Task 7: Mount results route in index.js

- **ACTION**: Import and mount the results route
- **IMPLEMENT**: In `web-app/src/index.js`:
  ```js
  import resultsRoutes from './routes/results.js';
  // ... after other app.use() calls:
  app.use('/api/surveys', resultsRoutes);
  ```
- **MIRROR**: `app.use('/api/surveys', exportRoutes)` at index.js:26
- **IMPORTS**: `resultsRoutes` from `'./routes/results.js'`
- **GOTCHA**: The prefix is `/api/surveys` because the route internally defines `/:id/results`. This overlapping prefix pattern is already used for `exportRoutes` and `responseRoutes`.
- **VALIDATE**: Server starts without errors; route is accessible

### Task 8: Wire WebSocket server to POST results to web app

- **ACTION**: When the WS server receives `race_results` from the professor, also POST it to the web app's REST API to persist in SQLite. This requires the web-app to also expose an internal (no-auth) endpoint, OR the server stores results itself. **Chosen approach**: The web app frontend listens for results and stores them via its own API — simpler, no server-to-server auth needed.

  **Revised approach**: Instead of server→web-app HTTP call, the web-app frontend will poll for results (same pattern as `ResponsesTab` polling responses). The WebSocket server already caches `raceResults` on the room. The web app can:
  1. Poll room status to detect `gamePhase === 'Finished'`
  2. Fetch results from a new WS server HTTP endpoint

  **Final chosen approach**: Add an HTTP endpoint to the WS server that returns cached `raceResults` for a room. The web-app's `SendToGameModal` (which already polls room status) detects `Finished` phase and auto-fetches results, then POSTs them to the web-app API.

- **IMPLEMENT**: In `server.js`, add a new HTTP route:
  ```js
  // In the HTTP server handler, after the room-status match:
  const resultsMatch = req.url.match(/^\/api\/room-results\/([A-Za-z0-9]+)$/);
  if (req.method === 'GET' && resultsMatch) {
    const code = resultsMatch[1].toUpperCase();
    const room = rooms.get(code);
    if (!room || !room.raceResults) {
      res.writeHead(200);
      res.end(JSON.stringify({ exists: false }));
      return;
    }
    res.writeHead(200);
    res.end(room.raceResults); // raw JSON from professor
    return;
  }
  ```
  In `web-app/src/routes/game-status.js`, add a proxy route:
  ```js
  // GET /api/game/room-results/:code — proxy race results from WS server
  router.get('/room-results/:code', async (req, res) => {
    const code = req.params.code.toUpperCase();
    try {
      const response = await fetch(`${GAME_HTTP_URL}/api/room-results/${code}`);
      const data = await response.json();
      res.json({ success: true, data });
    } catch {
      res.json({ success: true, data: { exists: false, error: 'Game server unreachable' } });
    }
  });
  ```
  In `web-app/client/src/api.js`, add:
  ```js
  export async function getRoomResults(roomCode) {
    return request(`/game/room-results/${roomCode.toUpperCase()}`);
  }

  export async function getRaceResults(surveyId) {
    return request(`/surveys/${surveyId}/results`);
  }

  export async function saveRaceResults(surveyId, resultsData) {
    return request(`/surveys/${surveyId}/results`, {
      method: 'POST',
      body: JSON.stringify(resultsData),
    });
  }
  ```
- **MIRROR**: `room-status` proxy pattern at game-status.js:9-18
- **IMPORTS**: Uses existing `GAME_HTTP_URL` from game-status.js
- **GOTCHA**: The `raceResults` field stores the raw JSON string from the professor. The `race_results` message from Unity contains `{ type, configName, resultsJson }` — the `resultsJson` is double-serialized. The HTTP endpoint returns the full raw message, so the web app needs to parse `resultsJson` from within it.
- **VALIDATE**: Create room, finish race, `GET /api/room-results/:code` returns results JSON

### Task 9: Update SendToGameModal to detect and save results

- **ACTION**: When the modal detects `gamePhase === 'Finished'` in room status poll, auto-fetch results from the WS server and save them to the web-app DB
- **IMPLEMENT**: In `SendToGameModal.jsx`, add result detection:
  ```jsx
  const [resultsSaved, setResultsSaved] = useState(false);

  // Inside the room status polling callback, after setting roomStatus:
  if (data.gamePhase === 'Finished' && !resultsSaved) {
    const roomRes = await getRoomResults(code);
    if (roomRes.success && roomRes.data && roomRes.data.type === 'race_results') {
      const parsed = JSON.parse(roomRes.data.resultsJson);
      await saveRaceResults(surveyId, {
        roomCode: code,
        configName: roomRes.data.configName || '',
        rankings: parsed.Rankings || [],
        eventLog: parsed.EventLog || [],
        totalRaceTime: parsed.TotalRaceTime || 0,
      });
      setResultsSaved(true);
    }
  }
  ```
- **MIRROR**: Existing polling pattern in `SendToGameModal.jsx` lines 46-49
- **IMPORTS**: `getRoomResults`, `saveRaceResults` from api.js
- **GOTCHA**: Unity's `JsonUtility` uses PascalCase field names (`Rankings`, `EventLog`, `TotalRaceTime`) because the C# structs use PascalCase. The web app should store them as-is (preserve original casing from Unity) in the JSON column, but the REST GET endpoint can normalize to camelCase for display.
- **VALIDATE**: Open SendToGameModal, finish a race in Unity, verify results appear in SQLite

### Task 10: Add getRaceResults to API client

- **ACTION**: Already included in Task 8's api.js additions. This task validates the integration.
- **IMPLEMENT**: (already covered in Task 8)
- **VALIDATE**: Call `getRaceResults(surveyId)` from browser console, verify it returns stored results

### Task 11: Create ResultsTab component

- **ACTION**: Create a new React component to display race results
- **IMPLEMENT**: Create `web-app/client/src/components/ResultsTab.jsx`:
  ```jsx
  import { useState, useEffect } from 'react';
  import { getRaceResults } from '../api.js';

  export default function ResultsTab({ surveyId }) {
    const [sessions, setSessions] = useState([]);
    const [loading, setLoading] = useState(true);
    const [expandedId, setExpandedId] = useState(null);

    useEffect(() => {
      loadResults();
    }, [surveyId]);

    async function loadResults() {
      setLoading(true);
      const result = await getRaceResults(surveyId);
      if (result.success) setSessions(result.data);
      setLoading(false);
    }

    function downloadCsv(session) {
      const rankings = session.rankings || [];
      if (rankings.length === 0) return;

      // Collect all unique attribute keys
      const allKeys = [];
      for (const car of rankings) {
        for (const attr of (car.Attributes || [])) {
          if (attr.Key && !allKeys.includes(attr.Key)) allKeys.push(attr.Key);
        }
      }

      let csv = 'Rank,TeamName';
      for (const key of allKeys) csv += `,${key}`;
      csv += ',LapsCompleted,CheckpointsPassed,Time\n';

      for (const car of rankings) {
        csv += `${car.Rank},${car.TeamName}`;
        for (const key of allKeys) {
          const attr = (car.Attributes || []).find(a => a.Key === key);
          csv += `,${attr ? attr.Value : ''}`;
        }
        csv += `,${car.LapsCompleted},${car.CheckpointsPassed},${(car.TotalTime || 0).toFixed(2)}\n`;
      }

      const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `race-results-${session.id}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    }

    function downloadJson(session) {
      const json = JSON.stringify(session, null, 2);
      const blob = new Blob([json], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `race-results-${session.id}.json`;
      a.click();
      URL.revokeObjectURL(url);
    }

    if (loading) return <p className="loading">Loading results...</p>;

    if (sessions.length === 0) {
      return <p className="empty">No race results yet. Run a race in Unity with this survey to see results here.</p>;
    }

    return (
      <div className="results-tab">
        {sessions.map(session => (
          <div key={session.id} className="result-session">
            <div className="result-session-header"
                 onClick={() => setExpandedId(expandedId === session.id ? null : session.id)}>
              <span className="result-session-title">
                {session.configName || 'Race Session'} — {new Date(session.receivedAt).toLocaleString()}
              </span>
              <span className="result-session-meta">
                {(session.rankings || []).length} car(s) | {(session.totalRaceTime || 0).toFixed(1)}s
                {session.roomCode && ` | Room ${session.roomCode}`}
              </span>
              <span className="expand-icon">{expandedId === session.id ? '▼' : '▶'}</span>
            </div>

            {expandedId === session.id && (
              <div className="result-session-body">
                <table className="response-table">
                  <thead>
                    <tr>
                      <th>Rank</th>
                      <th>Team</th>
                      <th>Laps</th>
                      <th>Checkpoints</th>
                      <th>Time</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(session.rankings || []).map((car, i) => (
                      <tr key={i} className="response-row">
                        <td className={`rank rank-${car.Rank}`}>{car.Rank}</td>
                        <td>{car.TeamName}</td>
                        <td>{car.LapsCompleted}</td>
                        <td>{car.CheckpointsPassed}</td>
                        <td>{(car.TotalTime || 0).toFixed(2)}s</td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                {(session.eventLog || []).length > 0 && (
                  <div className="event-log">
                    <h4>Event Log</h4>
                    <table className="response-table">
                      <thead>
                        <tr>
                          <th>Time</th>
                          <th>Event</th>
                          <th>Affected</th>
                        </tr>
                      </thead>
                      <tbody>
                        {session.eventLog.map((e, i) => (
                          <tr key={i} className="response-row">
                            <td>{(e.Timestamp || 0).toFixed(1)}s</td>
                            <td>{e.EventName}</td>
                            <td>{e.AffectedCount}/{e.TotalCars}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}

                <div className="result-actions">
                  <button className="btn-primary btn-small" onClick={() => downloadCsv(session)}>
                    Download CSV
                  </button>
                  <button className="btn-secondary btn-small" onClick={() => downloadJson(session)}>
                    Download JSON
                  </button>
                </div>
              </div>
            )}
          </div>
        ))}

        <button className="btn-secondary" onClick={loadResults} style={{ marginTop: '12px' }}>
          Refresh
        </button>
      </div>
    );
  }
  ```
- **MIRROR**: `ResponsesTab` component pattern; `downloadExportJson()` download pattern from EditorPage.jsx:53-64
- **IMPORTS**: `useState`, `useEffect` from react; `getRaceResults` from api.js
- **GOTCHA**: Unity serializes with PascalCase (`Rank`, `TeamName`, `LapsCompleted`, etc.). The component must use PascalCase when accessing these fields from the stored JSON.
- **VALIDATE**: Import component, render with a surveyId, verify table displays correctly

### Task 12: Add Results tab to EditorPage

- **ACTION**: Add 'Results' to the TABS array and render ResultsTab
- **IMPLEMENT**: In `EditorPage.jsx`:
  1. Import: `import ResultsTab from '../components/ResultsTab.jsx';`
  2. Update TABS: `const TABS = ['Questions', 'Mappings', 'Rules', 'Responses', 'Results'];`
  3. Add render block after the Responses conditional:
     ```jsx
     {activeTab === 4 && (
       <ResultsTab surveyId={id} />
     )}
     ```
- **MIRROR**: Existing tab rendering at EditorPage.jsx:174-196
- **IMPORTS**: `ResultsTab` from components
- **GOTCHA**: All existing tab indices remain unchanged (0-3). Results is index 4.
- **VALIDATE**: Navigate to survey editor, see 5 tabs, click "Results" tab

### Task 13: Add CSS styles for ResultsTab

- **ACTION**: Add styles for the results tab components to index.css
- **IMPLEMENT**: Append to `web-app/client/src/index.css`:
  ```css
  /* --- Results Tab --- */
  .results-tab { padding: 0; }
  .result-session { border: 1px solid var(--border); border-radius: 6px; margin-bottom: 12px; overflow: hidden; }
  .result-session-header { display: flex; align-items: center; gap: 12px; padding: 12px 16px; cursor: pointer; background: var(--bg-card); }
  .result-session-header:hover { background: var(--bg-input); }
  .result-session-title { font-weight: 600; flex: 1; }
  .result-session-meta { font-size: 12px; color: var(--text-dim); }
  .expand-icon { font-size: 11px; color: var(--text-dim); }
  .result-session-body { padding: 16px; border-top: 1px solid var(--border); }
  .rank-1 { color: #ffd700; font-weight: 700; }
  .rank-2 { color: #c0c0c0; font-weight: 600; }
  .rank-3 { color: #cd7f32; font-weight: 600; }
  .event-log { margin-top: 16px; }
  .event-log h4 { font-size: 13px; color: var(--text-dim); margin-bottom: 8px; }
  .result-actions { display: flex; gap: 8px; margin-top: 12px; }
  ```
- **MIRROR**: `.responses-tab` styling pattern at index.css:158-166
- **IMPORTS**: N/A (CSS)
- **GOTCHA**: Reuses existing `.response-table`, `.response-row` classes for consistency
- **VALIDATE**: Visual inspection — result session cards with collapsible body, gold/silver/bronze rank colors

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| POST results stores in DB | Valid rankings array | `{ success: true, data: { id } }` | No |
| POST results validates auth | No auth token | 401 | No |
| POST results validates survey ownership | Wrong user's survey ID | 404 | No |
| POST results with empty rankings | `{ rankings: [] }` | 200 (allowed — race may have 0 cars) | Yes |
| GET results returns all sessions | Multiple POST'd results | Array sorted by receivedAt DESC | No |
| GET results for survey with no results | Valid survey, no races run | `{ success: true, data: [] }` | Yes |
| CSV download generates valid CSV | Rankings with attributes | CSV with dynamic attribute columns | No |
| Unity message serialization | `RaceResultsMessage` | Valid JSON with double-serialized resultsJson | No |

### Edge Cases Checklist
- [ ] Empty rankings array (race started but no cars)
- [ ] Race with no event log (no events triggered)
- [ ] Multiple races for same survey (multiple results)
- [ ] Very long team names with special characters (CSV escaping)
- [ ] Attributes with comma or quote characters
- [ ] WebSocket disconnects before race_results is sent
- [ ] Web app not connected when race finishes (results still cached on WS server)
- [ ] Professor refreshes Results tab while no results exist

---

## Validation Commands

### Static Analysis
```bash
# No TypeScript in this project — check for syntax errors via Vite build
cd web-app/client && npx vite build
```
EXPECT: Zero build errors

### Unit Tests
```bash
# Currently no test framework configured — validate manually
# Future: add to test suite when test framework is set up
```

### Server Start
```bash
cd web-app && node src/index.js
```
EXPECT: Server starts, `[DB] Initialized` message, no errors

### WebSocket Server
```bash
cd Server && node server.js
```
EXPECT: Server starts on port 8080

### Browser Validation
```bash
cd web-app/client && npx vite --open
```
EXPECT:
1. Navigate to survey editor
2. See 5 tabs: Questions, Mappings, Rules, Responses, Results
3. Results tab shows "No race results yet" when empty
4. After race finishes and results are saved, tab shows result sessions
5. Expanding a session shows rankings table + event log
6. Download CSV/JSON buttons produce correct files

### Manual Validation
- [ ] Start Unity game, host room, run race
- [ ] Verify "race_results sent" log in Unity console
- [ ] Verify WS server logs relay
- [ ] Open web app, navigate to survey editor
- [ ] Click Results tab — see race data
- [ ] Download CSV — verify format matches Unity's ResultsExporter output
- [ ] Download JSON — verify structure
- [ ] Run multiple races — verify all sessions appear
- [ ] Rank colors: gold (#1), silver (#2), bronze (#3)

---

## Acceptance Criteria
- [ ] After race finishes in Unity, results are automatically sent via WebSocket
- [ ] WebSocket server relays results to connected web-app clients
- [ ] Results are persisted in SQLite `race_results` table
- [ ] Web app shows a "Results" tab in the survey editor
- [ ] Results tab displays rankings table with rank, team, laps, checkpoints, time
- [ ] Results tab displays event log if events occurred
- [ ] Professor can download results as CSV or JSON
- [ ] Multiple race sessions are stored and displayed chronologically
- [ ] Existing CSV export (X key in Unity) still works unchanged
- [ ] No regression in other tabs or WebSocket functionality

## Completion Checklist
- [ ] Code follows discovered patterns (API response format, WS relay, component structure)
- [ ] Error handling matches codebase style (`{ success: false, error: '...' }`)
- [ ] Logging follows conventions (`[NetworkSync]`, `[Room CODE]`, `console.log`)
- [ ] CSS uses existing variables and class naming conventions
- [ ] No hardcoded values (colors use CSS vars, URLs from env)
- [ ] No mutation (immutable state updates in React)
- [ ] No unnecessary scope additions (no real-time streaming, no student view)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Unity JsonUtility double-serialization produces malformed JSON | Low | High | Already proven pattern — `SurveyImportMessage` uses same approach |
| WS server raceResults cache lost on restart | Medium | Low | Results are also saved to SQLite; cache is convenience only |
| Large race results exceed WS message size | Low | Medium | Practical limit: ~20 cars × ~20 attributes = ~10KB — well within limits |
| Professor closes room before web-app fetches results | Medium | Medium | `SendToGameModal` polls every 5s — race finish → poll → save is fast. Also, results are in SQLite after first save |
| PascalCase/camelCase mismatch between Unity and JS | Medium | High | Document field casing in plan; test end-to-end serialization |

## Notes
- Unity's `JsonUtility` always uses PascalCase for field names matching C# property names. The web app stores the raw JSON as-is and accesses fields with PascalCase (`car.Rank`, `car.TeamName`). This is intentional — normalizing would require an extra mapping layer with no real benefit.
- The `race_end` message type already exists in `NetworkMessages.cs` but is currently empty and unused for sending. We add a separate `race_results` message rather than overloading `race_end` to keep the protocol clean: `race_end` signals the event, `race_results` carries the data.
- The WS server's in-memory `raceResults` cache means results survive for the duration of the room, but are lost when the professor disconnects. The SQLite persistence via the web-app is the durable store.
