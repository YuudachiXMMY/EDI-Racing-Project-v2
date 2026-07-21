# Plan: Multi-Room / Session History (GAP 6)

## Summary
Persist game room sessions so professors can review past races across all surveys. Currently, room state is ephemeral (in-memory `Map` in server.js) and race results are only saved if the web app client happens to auto-save them. This plan adds server-side session archival on room close, a `game_sessions` table for richer metadata, and a cross-survey "Session History" page in the web app.

## User Story
As a professor,
I want to see a history of all past game sessions with their results, participants, and configs,
So that I can review, compare, and export data from previous classroom activities without relying on Unity's local files.

## Problem -> Solution
**Current:** Rooms are in-memory. When a room is destroyed (professor disconnects after grace period), all data is lost from the server. Race results are only persisted if a webapp client was connected at race end. No cross-survey history view exists.

**Desired:** Server auto-archives session data to the web app DB when a room closes. A new "Session History" page shows all past sessions across surveys with metadata (room code, participants, duration, config). Professors can review and export any past session.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/plans/web-unity-gap-analysis-and-hosting.plan.md`
- **PRD Phase**: GAP 6
- **Estimated Files**: 8

---

## UX Design

### Before
```
Web App Dashboard                      Web App Editor (per-survey)
  [Survey 1]                             Results Tab:
  [Survey 2]                               [Race at 3pm - 5 cars]
  [Survey 3]                               [Race at 1pm - 8 cars]
                                           (only if webapp auto-saved)

  No cross-survey history.
  No session metadata (who joined, when, how long).
  If webapp wasn't connected at race end, results are lost.
```

### After
```
Web App Dashboard                      Web App Session History (NEW)
  [Survey 1]                             [Session ABCDEF - Jan 15 3pm]
  [Survey 2]                               Survey: Accessibility
  [Survey 3]                               5 participants | 2m 30s
  [Session History] <-- NEW                [View Results] [Export]

                                         [Session XYZ123 - Jan 14 1pm]
                                           Survey: Diversity
                                           8 participants | 3m 15s
                                           [View Results] [Export]
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Dashboard | No history link | "Session History" nav link | Top-level navigation |
| Room close (server) | Data discarded silently | Server POSTs session archive to web app API | Fire-and-forget, no blocking |
| Results viewing | Per-survey only in Results tab | Also available in cross-survey History page | Both views coexist |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Server/server.js` | 42-56 | `destroyRoom()` — where session archival hooks in |
| P0 (critical) | `web-app/src/routes/results.js` | all | Existing race results pattern to mirror |
| P0 (critical) | `web-app/src/schema.sql` | all | DB schema — add game_sessions table here |
| P1 (important) | `web-app/client/src/components/ResultsTab.jsx` | all | Results display pattern to reuse |
| P1 (important) | `web-app/client/src/pages/DashboardPage.jsx` | all | Page and navigation pattern |
| P1 (important) | `web-app/client/src/api.js` | all | API client pattern |
| P2 (reference) | `Assets/Scripts/Data/SessionData.cs` | all | Unity session data model |
| P2 (reference) | `web-app/src/db.js` | all | DB initialization and migration pattern |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| N/A | N/A | Feature uses established internal patterns only |

---

## Patterns to Mirror

### NAMING_CONVENTION
```javascript
// SOURCE: web-app/src/routes/results.js:7-8
// Express routes: router.verb('/path', requireAuth, handler)
router.post('/:id/results', requireAuth, (req, res) => {
```

```javascript
// SOURCE: web-app/client/src/api.js:111-113
// API client: async function, return request() call
export async function getRaceResults(surveyId) {
  return request(`/surveys/${surveyId}/results`);
}
```

### ERROR_HANDLING
```javascript
// SOURCE: web-app/src/routes/results.js:12-14
// 404 with success:false envelope
if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
}
```

### LOGGING_PATTERN
```javascript
// SOURCE: Server/server.js:55
// Server: [Room CODE] prefix
console.log(`[Room ${roomCode}] Destroyed`);
```

### DB_SCHEMA_PATTERN
```sql
-- SOURCE: web-app/src/schema.sql:47-57
-- Tables: snake_case, TEXT for dates, JSON columns suffixed _json
CREATE TABLE IF NOT EXISTS race_results (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  survey_id INTEGER NOT NULL REFERENCES surveys(id),
  room_code TEXT NOT NULL DEFAULT '',
  config_name TEXT NOT NULL DEFAULT '',
  rankings_json TEXT NOT NULL DEFAULT '[]',
  event_log_json TEXT NOT NULL DEFAULT '[]',
  total_race_time REAL NOT NULL DEFAULT 0,
  received_at TEXT NOT NULL DEFAULT (datetime('now'))
);
```

### DB_MIGRATION_PATTERN
```javascript
// SOURCE: web-app/src/db.js:24-28
// Inline ALTER TABLE in try/catch for migrations
try {
    db.exec('ALTER TABLE surveys ADD COLUMN linked_room_code TEXT DEFAULT NULL');
} catch {
    // Column already exists — ignore
}
```

### REACT_PAGE_PATTERN
```jsx
// SOURCE: web-app/client/src/pages/DashboardPage.jsx:6-11
// Page component: useState for data, useEffect to loadData, loading/empty states
export default function DashboardPage() {
  const [surveys, setSurveys] = useState([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => { loadData(); }, []);
```

### FIRE_AND_FORGET_HTTP_PATTERN
```javascript
// SOURCE: web-app/src/routes/responses.js:67-76
// Fire-and-forget POST with .catch(() => {})
fetch(`${GAME_HTTP_URL}/api/notify-response`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ... }),
}).catch(() => {}); // Silently ignore if unreachable
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/schema.sql` | UPDATE | Add `game_sessions` table |
| `web-app/src/db.js` | UPDATE | Add migration for new table (existing DBs) |
| `web-app/src/routes/results.js` | UPDATE | Add session CRUD endpoints |
| `web-app/src/index.js` | UPDATE | Mount sessions routes (if separate file) |
| `web-app/client/src/api.js` | UPDATE | Add session history API functions |
| `web-app/client/src/pages/HistoryPage.jsx` | CREATE | New session history page |
| `web-app/client/src/App.jsx` | UPDATE | Add route for /history |
| `Server/server.js` | UPDATE | Archive session on room destroy |

## NOT Building

- Session replay (re-playing a race animation from saved data)
- Side-by-side comparison of multiple sessions (future enhancement)
- Unity-side session upload (Unity already saves locally; web archive is server-driven)
- Session editing or modification (read-only history)
- Pagination (not needed until hundreds of sessions; simple list is fine)
- Session deletion (admin concern, not in scope)

---

## Step-by-Step Tasks

### Task 1: Add `game_sessions` table to schema
- **ACTION**: Add table definition to `web-app/src/schema.sql`
- **IMPLEMENT**:
  ```sql
  -- Game session history (archived when room closes)
  CREATE TABLE IF NOT EXISTS game_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER REFERENCES users(id),
    survey_id INTEGER REFERENCES surveys(id),
    room_code TEXT NOT NULL DEFAULT '',
    config_name TEXT NOT NULL DEFAULT '',
    student_count INTEGER NOT NULL DEFAULT 0,
    student_names_json TEXT NOT NULL DEFAULT '[]',
    game_phase TEXT NOT NULL DEFAULT 'Setup',
    race_started INTEGER NOT NULL DEFAULT 0,
    rankings_json TEXT NOT NULL DEFAULT '[]',
    event_log_json TEXT NOT NULL DEFAULT '[]',
    total_race_time REAL NOT NULL DEFAULT 0,
    started_at TEXT NOT NULL DEFAULT (datetime('now')),
    ended_at TEXT NOT NULL DEFAULT (datetime('now'))
  );
  ```
- **MIRROR**: `race_results` table pattern (snake_case, JSON columns, datetime defaults)
- **IMPORTS**: None
- **GOTCHA**: `user_id` and `survey_id` are nullable (references without NOT NULL) because the server may not know which professor/survey is associated — the archive comes from the WS server which doesn't have auth context. These fields are populated later if the web app can match room_code to a linked survey.
- **VALIDATE**: `sqlite3 data/edi-survey.db ".schema game_sessions"` shows table

### Task 2: Add migration in db.js for existing databases
- **ACTION**: Add try/catch migration block in `web-app/src/db.js` after the existing migration
- **IMPLEMENT**:
  ```javascript
  // Migration: create game_sessions table for existing DBs
  try {
      db.exec(`CREATE TABLE IF NOT EXISTS game_sessions (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        user_id INTEGER REFERENCES users(id),
        survey_id INTEGER REFERENCES surveys(id),
        room_code TEXT NOT NULL DEFAULT '',
        config_name TEXT NOT NULL DEFAULT '',
        student_count INTEGER NOT NULL DEFAULT 0,
        student_names_json TEXT NOT NULL DEFAULT '[]',
        game_phase TEXT NOT NULL DEFAULT 'Setup',
        race_started INTEGER NOT NULL DEFAULT 0,
        rankings_json TEXT NOT NULL DEFAULT '[]',
        event_log_json TEXT NOT NULL DEFAULT '[]',
        total_race_time REAL NOT NULL DEFAULT 0,
        started_at TEXT NOT NULL DEFAULT (datetime('now')),
        ended_at TEXT NOT NULL DEFAULT (datetime('now'))
      )`);
  } catch {
      // Table already exists
  }
  ```
- **MIRROR**: `ALTER TABLE surveys ADD COLUMN linked_room_code` migration at db.js:24-28
- **IMPORTS**: None
- **GOTCHA**: Using `CREATE TABLE IF NOT EXISTS` instead of `ALTER TABLE` since this is a new table, not a column addition. The try/catch is a safety net in case of any parse issue.
- **VALIDATE**: Server starts without error on fresh and existing DBs

### Task 3: Add session archive endpoints in results.js
- **ACTION**: Add `POST /api/sessions/archive` (no auth — called by WS server) and `GET /api/sessions` (auth — professor history)
- **IMPLEMENT**:
  ```javascript
  // POST /api/sessions/archive — store archived session (called by WS server, no auth)
  router.post('/sessions/archive', (req, res) => {
      const { roomCode, configName, studentCount, studentNames, gamePhase,
              raceStarted, rankings, eventLog, totalRaceTime } = req.body;

      if (!roomCode) {
          return res.status(400).json({ success: false, error: 'roomCode is required' });
      }

      const db = getDb();

      // Try to find the survey linked to this room
      const linked = db.prepare(
          'SELECT id, user_id FROM surveys WHERE linked_room_code = ? COLLATE NOCASE'
      ).get(roomCode);

      const result = db.prepare(
          `INSERT INTO game_sessions
           (user_id, survey_id, room_code, config_name, student_count, student_names_json,
            game_phase, race_started, rankings_json, event_log_json, total_race_time)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
      ).run(
          linked ? linked.user_id : null,
          linked ? linked.id : null,
          roomCode,
          configName || '',
          studentCount || 0,
          JSON.stringify(studentNames || []),
          gamePhase || 'Setup',
          raceStarted ? 1 : 0,
          JSON.stringify(rankings || []),
          JSON.stringify(eventLog || []),
          totalRaceTime || 0
      );

      res.json({ success: true, data: { id: Number(result.lastInsertRowid) } });
  });

  // GET /api/sessions — list all game sessions for the logged-in professor
  router.get('/sessions', requireAuth, (req, res) => {
      const db = getDb();
      const sessions = db.prepare(
          `SELECT gs.*, s.config_name as survey_config_name
           FROM game_sessions gs
           LEFT JOIN surveys s ON gs.survey_id = s.id
           WHERE gs.user_id = ?
           ORDER BY gs.ended_at DESC
           LIMIT 100`
      ).all(req.user.userId);

      const parsed = sessions.map(s => ({
          id: s.id,
          surveyId: s.survey_id,
          roomCode: s.room_code,
          configName: s.config_name || s.survey_config_name || '',
          studentCount: s.student_count,
          studentNames: JSON.parse(s.student_names_json),
          gamePhase: s.game_phase,
          raceStarted: !!s.race_started,
          rankings: JSON.parse(s.rankings_json),
          eventLog: JSON.parse(s.event_log_json),
          totalRaceTime: s.total_race_time,
          startedAt: s.started_at,
          endedAt: s.ended_at,
      }));

      res.json({ success: true, data: parsed });
  });
  ```
- **MIRROR**: `POST /:id/results` and `GET /:id/results` in results.js
- **IMPORTS**: Uses existing `getDb`, `requireAuth` imports
- **GOTCHA**: The `POST /sessions/archive` endpoint has NO auth — it's called by the WS server which doesn't have JWT tokens. This is acceptable because: (1) it only writes data, doesn't expose any, (2) the WS server is on the same Docker network, (3) the endpoint is not exposed via nginx to external clients. Add a simple shared-secret header check if needed later.
- **VALIDATE**: `curl -X POST /api/sessions/archive -d '{"roomCode":"TEST"}'` returns 200

### Task 4: Mount session routes in index.js
- **ACTION**: The new endpoints are added to `results.js`, which is already mounted at `/api/surveys`. But `/api/sessions/archive` and `/api/sessions` don't have the `/surveys` prefix. Mount the router at `/api` as well.
- **IMPLEMENT**: In `web-app/src/index.js`, add:
  ```javascript
  app.use('/api', resultsRoutes);
  ```
- **MIRROR**: `app.use('/api', responseRoutes);` at line 30 (already mounts responses at /api for public endpoints)
- **IMPORTS**: `resultsRoutes` already imported
- **GOTCHA**: The results router already has `/:id/results` routes mounted under `/api/surveys`. Adding it under `/api` as well means `/api/sessions/archive` and `/api/sessions` become accessible. The `/:id/results` routes won't conflict because `sessions` won't match the `/:id` param pattern for numeric IDs in practice (no survey has id "sessions").
- **VALIDATE**: Server starts, `GET /api/sessions` returns 401 (no auth), `POST /api/sessions/archive` returns 400 (missing roomCode)

### Task 5: Archive session data on room destroy in server.js
- **ACTION**: In `destroyRoom()`, POST the room's accumulated data to the web app API before deleting
- **IMPLEMENT**:
  ```javascript
  function destroyRoom(roomCode) {
      const room = rooms.get(roomCode);
      if (!room) return;
      if (room.graceTimer) clearTimeout(room.graceTimer);

      // Archive session to web app DB (fire-and-forget)
      const archivePayload = {
          roomCode,
          configName: '',
          studentCount: room.students.size,
          studentNames: [...room.studentTeamNames.values()],
          gamePhase: room.gamePhase || 'Setup',
          raceStarted: room.raceStarted,
          rankings: [],
          eventLog: [],
          totalRaceTime: 0,
      };

      // Extract results if available
      if (room.raceResults) {
          try {
              const parsed = JSON.parse(room.raceResults);
              archivePayload.configName = parsed.configName || '';
              if (parsed.resultsJson) {
                  const results = JSON.parse(parsed.resultsJson);
                  archivePayload.rankings = results.Rankings || [];
                  archivePayload.eventLog = results.EventLog || [];
                  archivePayload.totalRaceTime = results.TotalRaceTime || 0;
              }
          } catch { /* ignore parse errors */ }
      }

      const API_URL = process.env.API_URL || 'http://localhost:3001';
      fetch(`${API_URL}/api/sessions/archive`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(archivePayload),
      }).catch(() => {});

      broadcastToStudents(roomCode, { type: 'room_closed' });
      // ... rest of existing cleanup
  ```
- **MIRROR**: Fire-and-forget `fetch()` pattern from responses.js:67-76
- **IMPORTS**: None (`fetch` is built into Node 18+)
- **GOTCHA**: `room.raceResults` is a raw JSON string stored by the `race_results` message handler at server.js:422. It's the full `RaceResultsMessage` which has `configName` and `resultsJson` (double-serialized). Must parse twice. Also add `API_URL` env var to docker-compose for inter-container communication.
- **VALIDATE**: Create room, run race, disconnect professor — verify session appears in game_sessions table

### Task 6: Add API client functions for session history
- **ACTION**: Add `getSessionHistory()` to `web-app/client/src/api.js`
- **IMPLEMENT**:
  ```javascript
  export async function getSessionHistory() {
      return request('/sessions');
  }
  ```
- **MIRROR**: `getSurveys()` at line 58-60
- **IMPORTS**: None
- **GOTCHA**: None
- **VALIDATE**: Returns 200 with auth, 401 without

### Task 7: Find and update the React router to add /history route
- **ACTION**: Find the React router file and add HistoryPage route
- **IMPLEMENT**: Add `<Route path="/history" element={<HistoryPage />} />` and import
- **MIRROR**: Existing route pattern in App.jsx
- **IMPORTS**: `import HistoryPage from './pages/HistoryPage.jsx'`
- **GOTCHA**: Must check the actual router file location — could be App.jsx or main.jsx
- **VALIDATE**: Navigate to /history, page renders

### Task 8: Create HistoryPage component
- **ACTION**: Create `web-app/client/src/pages/HistoryPage.jsx`
- **IMPLEMENT**: A page showing all past sessions in reverse chronological order. Each session card shows: config name, room code, participant count, duration, phase, and date. Clicking expands to show rankings table and event log (reusing ResultsTab patterns). Download CSV/JSON buttons per session.
- **MIRROR**: `DashboardPage.jsx` for page structure, `ResultsTab.jsx` for results display
- **IMPORTS**: `useState, useEffect` from react; `useNavigate` from react-router-dom; `getSessionHistory` from api.js
- **GOTCHA**: Sessions without race results (e.g., room closed during Setup) should show "No race completed" instead of empty rankings table. Handle nullable `rankings` gracefully.
- **VALIDATE**: Page renders with session cards, expand/collapse works, downloads work

### Task 9: Add navigation link to Dashboard
- **ACTION**: Add "Session History" link in `DashboardPage.jsx` header
- **IMPLEMENT**:
  ```jsx
  <button onClick={() => navigate('/history')} className="btn-secondary">Session History</button>
  ```
- **MIRROR**: Existing header buttons pattern in DashboardPage
- **IMPORTS**: `useNavigate` already imported
- **GOTCHA**: None
- **VALIDATE**: Button visible on dashboard, navigates to /history

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| POST /sessions/archive valid | `{roomCode:"TEST", studentCount:5}` | 201 with session ID | No |
| POST /sessions/archive no room | `{}` | 400 with error | Yes |
| GET /sessions with auth | Valid JWT | 200 with sessions array | No |
| GET /sessions no auth | No JWT | 401 | Yes |
| destroyRoom archival | Room with race results | Session archived to DB | No |
| destroyRoom no results | Room closed during Setup | Session archived with empty rankings | Yes |
| Session linked to survey | Room linked via linked_room_code | user_id and survey_id populated | No |
| Session unlinked | Room not linked to any survey | user_id and survey_id null | Yes |

### Edge Cases Checklist
- [ ] Room destroyed before race starts (Setup phase, no results)
- [ ] Room destroyed after race with full results
- [ ] Multiple rooms for same survey
- [ ] Room not linked to any survey (no linked_room_code match)
- [ ] Race results with double-serialized JSON parsing
- [ ] Professor with no sessions (empty history page)
- [ ] Web app API unreachable when server tries to archive (fire-and-forget, should not crash)

---

## Validation Commands

### Static Analysis
```bash
node -c Server/server.js && node -c web-app/src/routes/results.js
```
EXPECT: No syntax errors

### Database Validation
```bash
cd web-app && node -e "const {getDb}=require('./src/db.js'); const db=getDb(); console.log(db.prepare('SELECT sql FROM sqlite_master WHERE name=?').get('game_sessions'))"
```
EXPECT: Table exists with correct schema

### Manual Validation
- [ ] Start full stack (docker compose up)
- [ ] Host room in Unity, invite students, run race
- [ ] Disconnect professor / wait for room to close
- [ ] Open web app > Session History — verify session appears
- [ ] Verify session shows correct participant count, config name, results
- [ ] Expand session — verify rankings and event log display correctly
- [ ] Download CSV/JSON — verify file contents
- [ ] Sessions without race results show gracefully

---

## Acceptance Criteria
- [ ] `game_sessions` table created in schema
- [ ] Server auto-archives session data when room is destroyed
- [ ] GET /api/sessions returns professor's session history
- [ ] HistoryPage shows sessions with metadata and expandable results
- [ ] CSV/JSON download works for each session
- [ ] Sessions without race results display gracefully
- [ ] Existing functionality unchanged (race results, send-to-game, etc.)

## Completion Checklist
- [ ] Code follows discovered patterns (Express routes, React pages, DB schema)
- [ ] Error handling matches codebase style (success/false envelope)
- [ ] Logging follows conventions ([Room CODE] prefix)
- [ ] No hardcoded URLs (API_URL from env var)
- [ ] Fire-and-forget pattern used for non-critical archival
- [ ] No unnecessary scope additions
- [ ] Self-contained

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Double-serialized raceResults parsing | Medium | Medium | Defensive try/catch with fallback to empty arrays |
| No auth on /sessions/archive endpoint | Low | Low | Only writable, not readable. Internal network only. Add shared secret later if needed. |
| API_URL env var not configured | Medium | Low | Defaults to localhost:3001. Document in docker-compose.yml. |
| Large number of sessions over time | Low | Low | LIMIT 100 in query. Add pagination later if needed. |

## Notes
- The `race_results` table and `ResultsTab` continue to work unchanged. `game_sessions` is a parallel archival system that captures richer metadata (participant names, game phase, room code mapping). Over time, these could be unified, but for now they serve different purposes: `race_results` is tied to a specific survey, `game_sessions` captures the full room lifecycle regardless of survey linkage.
- The server-side archive happens in `destroyRoom()` which fires after the professor's grace period expires. This means the archive includes the final state of the room including any race results.
- The fire-and-forget pattern means a failed archive attempt is silently lost. This is acceptable because: (1) race results are also saved by the web app client via `SendToGameModal`, (2) Unity saves sessions locally, (3) the archive is a convenience, not a critical path.
