# Plan: Survey Response Real-Time Sync (GAP 8)

## Summary
When students submit survey responses on the web, neither the Unity game nor the professor's web UI knows about it until the professor manually clicks "Send to Game". This plan adds a room-linking mechanism so the web app can push real-time response notifications to Unity (via the WebSocket server) and update the EditorPage response count live.

## User Story
As a professor,
I want to see new survey responses appear in real-time — both in the web app and in the Unity game,
So that I know when enough students have responded and can start the race without manually refreshing.

## Problem → Solution
**Current**: Student submits survey on web → sits in SQLite silently → Professor must click "Send to Game" to push data to Unity. No real-time visibility.

**Desired**: Student submits survey on web → web-app backend notifies WS server → WS server relays `new_web_response` to Unity professor → Unity shows live response count → EditorPage auto-updates response count. Optional: auto-send when threshold reached.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/plans/web-unity-gap-analysis-and-hosting.plan.md`
- **PRD Phase**: GAP 8 — Survey Response Real-Time Sync
- **Estimated Files**: 8

---

## UX Design

### Before
```
┌──────────────────────────────────────────────────────┐
│  Student fills survey on web  ──POST──>  SQLite DB   │
│                                                      │
│  Professor opens EditorPage:   "5 response(s)"       │
│    (number loaded once on mount, never updates)      │
│                                                      │
│  Professor clicks "Send to Game" manually             │
│    → web-app opens WS → joins room → sends data      │
│                                                      │
│  Unity game:  has no idea responses exist until sent  │
└──────────────────────────────────────────────────────┘
```

### After
```
┌──────────────────────────────────────────────────────┐
│  Professor links survey to room (enters room code)   │
│                                                      │
│  Student fills survey  ──POST──>  SQLite DB          │
│                          └──HTTP notify──> WS Server │
│                                            │         │
│               ┌────────────────────────────┘         │
│               ↓                                      │
│  Unity (professor): "Web responses: 5/10"            │
│  EditorPage:         "6 response(s)" (auto-updates)  │
│                                                      │
│  Optional: auto-send to game at threshold            │
└──────────────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| EditorPage header | Static response count loaded once | Live-updating count via polling | Polls every 10s while tab is open |
| EditorPage header | No room linking | "Link to Room" input next to "Send to Game" | Saves `linked_room_code` to DB |
| Unity SetupScreen | No web response awareness | Shows "Web responses: N" label | Only visible when room is linked |
| WS Server | No HTTP notify endpoint | `POST /api/notify-response` | Called by web-app on new response |
| Student survey page | No change | No change | Submission flow identical |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/responses.js` | 33-67 | The POST handler where notification logic will be added |
| P0 | `Server/server.js` | 98-140 | HTTP server section — add new endpoint here |
| P0 | `Server/server.js` | 160-405 | WebSocket message handling — add new message relay |
| P0 | `web-app/client/src/pages/EditorPage.jsx` | 15-44 | State and loading — add live response polling |
| P1 | `web-app/src/routes/export.js` | 116-201 | send-to-game flow — reference for WS connection pattern |
| P1 | `web-app/src/schema.sql` | 11-27 | surveys table — add linked_room_code column |
| P1 | `web-app/client/src/api.js` | 1-147 | API client — add new endpoints |
| P1 | `Assets/Scripts/UI/SetupScreen.cs` | 330-388 | OnNetworkMessage handler — add new_web_response handling |
| P2 | `Assets/Scripts/Network/NetworkMessages.cs` | 264-272 | Message type definitions — add new message class |
| P2 | `web-app/client/src/hooks/useRaceWebSocket.js` | all | Pattern reference for WS hooks |
| P2 | `web-app/src/routes/game-status.js` | all | Pattern reference for proxying to WS server |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| N/A | Internal patterns only | Feature uses established patterns — no external libs needed |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```javascript
// SOURCE: web-app/src/routes/responses.js:1-4
import { Router } from 'express';
import { getDb } from '../db.js';
const router = Router();
// Files: kebab-case. Functions: camelCase. DB columns: snake_case.
```

### ERROR_HANDLING
```javascript
// SOURCE: web-app/src/routes/responses.js:55-66
try {
  const result = db.prepare('INSERT INTO ...').run(...);
  res.status(201).json({ success: true, data: { id: result.lastInsertRowid } });
} catch (err) {
  if (err.message.includes('UNIQUE constraint failed')) {
    return res.status(409).json({ success: false, error: 'Duplicate' });
  }
  throw err;
}
```

### API_RESPONSE_FORMAT
```javascript
// SOURCE: web-app/src/routes/game-status.js:14
res.json({ success: true, data: { ... } });
// Always: { success: boolean, data?: T, error?: string }
```

### WS_SERVER_HTTP_ENDPOINT
```javascript
// SOURCE: Server/server.js:98-140
const match = req.url.match(/^\/api\/room-status\/([A-Za-z0-9]+)$/);
if (req.method === 'GET' && match) {
  const code = match[1].toUpperCase();
  const room = rooms.get(code);
  // ... return JSON
}
// HTTP endpoints in WS server use manual URL matching (no Express).
// CORS header: res.setHeader('Access-Control-Allow-Origin', '*')
```

### WS_MESSAGE_FORMAT
```javascript
// SOURCE: Server/server.js:25-29
function sendJSON(ws, obj) {
  if (ws.readyState === 1) {
    ws.send(JSON.stringify(obj));
  }
}
// All WS messages are JSON with a `type` field.
```

### UNITY_MESSAGE_CLASS
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:266-272
[Serializable]
public class SurveyImportMessage
{
    public string type = "survey_import";
    public string configName;
    public string exportJson;
}
// Use [Serializable] + public fields + default type string.
```

### UNITY_MESSAGE_HANDLER
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:337-361
var baseMsg = JsonUtility.FromJson<NetworkMessage>(json);
if (baseMsg.type == "student_joined") { ... return; }
if (baseMsg.type == "student_list") { ... return; }
if (baseMsg.type != "survey_import") return;
// Parse base message first, check type, then deserialize specific class.
```

### CLIENT_API_FUNCTION
```javascript
// SOURCE: web-app/client/src/api.js:100-105
export async function sendToGame(id, roomCode) {
  return request(`/surveys/${id}/send-to-game`, {
    method: 'POST',
    body: JSON.stringify({ roomCode }),
  });
}
// Use the `request()` helper which adds auth token automatically.
```

### GAME_STATUS_PROXY
```javascript
// SOURCE: web-app/src/routes/game-status.js:9-17
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
// Web-app proxies to WS server HTTP. Silently handles connection failures.
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/schema.sql` | UPDATE | Add `linked_room_code` column to surveys table |
| `web-app/src/routes/surveys.js` | UPDATE | Add PATCH endpoint for linking/unlinking room code |
| `web-app/src/routes/responses.js` | UPDATE | After INSERT, notify WS server if survey has linked room |
| `web-app/client/src/api.js` | UPDATE | Add `linkRoom()`, `unlinkRoom()` API functions |
| `web-app/client/src/pages/EditorPage.jsx` | UPDATE | Add room linking UI + polling response count |
| `Server/server.js` | UPDATE | Add `POST /api/notify-response` HTTP endpoint + relay to professor |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add `NewWebResponseMessage` class |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Handle `new_web_response` message, show response count |

## NOT Building

- Auto-send when response threshold reached (deferred — can be added later as enhancement)
- SSE or persistent WebSocket from web-app to EditorPage (polling is simpler and sufficient)
- Notification to Unity when survey is created or deleted
- Real-time answer content sync to Unity (only count + team name)
- Changes to the student survey page (submission flow stays the same)

---

## Step-by-Step Tasks

### Task 1: Add `linked_room_code` column to surveys table
- **ACTION**: Add a nullable `linked_room_code` column to the `surveys` table in the schema
- **IMPLEMENT**: Add `linked_room_code TEXT DEFAULT NULL` to the CREATE TABLE statement. Since we use `CREATE TABLE IF NOT EXISTS`, existing databases won't get the new column automatically. Add an `ALTER TABLE` migration with try-catch for the case where column already exists.
- **MIRROR**: Schema pattern from `web-app/src/schema.sql`
- **IMPORTS**: None
- **GOTCHA**: SQLite `ALTER TABLE ADD COLUMN` silently succeeds if column already exists? No — it throws. Wrap in try-catch. Also, `better-sqlite3` uses `exec()` for DDL.
- **VALIDATE**: Start web-app, check that `surveys` table has `linked_room_code` column via `.pragma('table_info', 'surveys')`

### Task 2: Add room linking API endpoints
- **ACTION**: Add `PATCH /api/surveys/:id/link-room` and `DELETE /api/surveys/:id/link-room` to surveys routes
- **IMPLEMENT**:
  ```javascript
  // PATCH /api/surveys/:id/link-room
  router.patch('/:id/link-room', requireAuth, (req, res) => {
    const { roomCode } = req.body;
    if (!roomCode || !roomCode.trim()) {
      return res.status(400).json({ success: false, error: 'roomCode is required' });
    }
    const db = getDb();
    const result = db.prepare('UPDATE surveys SET linked_room_code = ? WHERE id = ? AND user_id = ?')
      .run(roomCode.trim().toUpperCase(), req.params.id, req.user.userId);
    if (result.changes === 0) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }
    res.json({ success: true, data: { linkedRoomCode: roomCode.trim().toUpperCase() } });
  });

  // DELETE /api/surveys/:id/link-room
  router.delete('/:id/link-room', requireAuth, (req, res) => {
    const db = getDb();
    const result = db.prepare('UPDATE surveys SET linked_room_code = NULL WHERE id = ? AND user_id = ?')
      .run(req.params.id, req.user.userId);
    if (result.changes === 0) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }
    res.json({ success: true });
  });
  ```
- **MIRROR**: `ERROR_HANDLING`, `API_RESPONSE_FORMAT` patterns from surveys.js
- **IMPORTS**: `requireAuth` already imported in surveys.js
- **GOTCHA**: Must verify ownership via `user_id` check. Room code must be uppercased.
- **VALIDATE**: `curl -X PATCH .../link-room -d '{"roomCode":"ABCDEF"}'` returns success; verify DB has the value

### Task 3: Add `POST /api/notify-response` to WS server
- **ACTION**: Add an HTTP POST endpoint to `Server/server.js` that receives response notifications and relays to the professor in the specified room
- **IMPLEMENT**:
  ```javascript
  // Inside the http.createServer callback, add:
  const notifyMatch = req.url === '/api/notify-response';
  if (req.method === 'POST' && notifyMatch) {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      try {
        const { roomCode, responseCount, teamName, surveyId } = JSON.parse(body);
        const code = (roomCode || '').toUpperCase();
        const room = rooms.get(code);
        if (room && room.professor && room.professor.readyState === 1) {
          sendJSON(room.professor, {
            type: 'new_web_response',
            responseCount,
            teamName: teamName || '',
            surveyId: surveyId || 0,
          });
        }
        // Also notify web-app viewers
        if (room) {
          for (const webapp of room.webapps) {
            if (webapp.readyState === 1) {
              sendJSON(webapp, {
                type: 'new_web_response',
                responseCount,
                teamName: teamName || '',
              });
            }
          }
        }
        res.writeHead(200);
        res.end(JSON.stringify({ success: true }));
      } catch {
        res.writeHead(400);
        res.end(JSON.stringify({ success: false, error: 'Invalid JSON' }));
      }
    });
    return;
  }
  ```
- **MIRROR**: `WS_SERVER_HTTP_ENDPOINT` pattern from server.js:98-140
- **IMPORTS**: None — uses existing `rooms`, `sendJSON`
- **GOTCHA**: Must support POST body parsing (no Express, use raw `req.on('data')`). Must add `Access-Control-Allow-Methods: 'GET, POST'` and handle OPTIONS preflight for CORS. Set `Access-Control-Allow-Headers: 'Content-Type'`.
- **VALIDATE**: `curl -X POST localhost:8080/api/notify-response -H 'Content-Type: application/json' -d '{"roomCode":"TEST","responseCount":5,"teamName":"Alpha"}'`

### Task 4: Notify WS server from response submission endpoint
- **ACTION**: After a successful INSERT in `POST /api/s/:shareCode/respond`, check if the survey has a `linked_room_code` and send a notification to the WS server
- **IMPLEMENT**:
  ```javascript
  // After successful INSERT, before returning response:
  // Look up the survey's linked room code
  const surveyFull = db.prepare('SELECT id, linked_room_code FROM surveys WHERE id = ?').get(survey.id);
  if (surveyFull && surveyFull.linked_room_code) {
    const count = db.prepare('SELECT COUNT(*) as c FROM responses WHERE survey_id = ?').get(survey.id).c;
    // Fire-and-forget notification to WS server
    fetch(`${GAME_HTTP_URL}/api/notify-response`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        roomCode: surveyFull.linked_room_code,
        responseCount: count,
        teamName: teamName.trim(),
        surveyId: survey.id,
      }),
    }).catch(() => {}); // Silently ignore if WS server unreachable
  }
  ```
- **MIRROR**: `GAME_STATUS_PROXY` pattern — fire-and-forget fetch with catch
- **IMPORTS**: Add `const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';` and derive `GAME_HTTP_URL` at top of file (same pattern as `game-status.js`)
- **GOTCHA**: Must not block the student's response. Use fire-and-forget. The `survey` variable in the current scope only has `id` and `is_active` — need to query `linked_room_code` separately.
- **VALIDATE**: Submit a response with a linked survey, verify WS server logs the notification

### Task 5: Add client API functions for room linking
- **ACTION**: Add `linkRoom()` and `unlinkRoom()` functions to `web-app/client/src/api.js`
- **IMPLEMENT**:
  ```javascript
  export async function linkRoom(surveyId, roomCode) {
    return request(`/surveys/${surveyId}/link-room`, {
      method: 'PATCH',
      body: JSON.stringify({ roomCode }),
    });
  }

  export async function unlinkRoom(surveyId) {
    return request(`/surveys/${surveyId}/link-room`, { method: 'DELETE' });
  }
  ```
- **MIRROR**: `CLIENT_API_FUNCTION` pattern from api.js
- **IMPORTS**: None — uses existing `request()` helper
- **GOTCHA**: None
- **VALIDATE**: Call from browser console, verify response

### Task 6: Add room linking UI + polling to EditorPage
- **ACTION**: Add a room code input (link/unlink) in the EditorPage header and add periodic polling for response count
- **IMPLEMENT**:
  - Add state: `linkedRoom`, `linkInput`, `linkStatus`
  - Load `linked_room_code` from the survey data (needs to be returned from `getSurvey`)
  - Add a small inline form next to "Send to Game" button:
    - If linked: show badge "Linked: ABCDEF" + unlink button
    - If not linked: show input + "Link" button
  - Add polling for `getResponseCount(id)` every 10 seconds:
    ```javascript
    useEffect(() => {
      const timer = setInterval(async () => {
        const countRes = await getResponseCount(id);
        if (countRes.success) setResponseCount(countRes.data.count);
      }, 10000);
      return () => clearInterval(timer);
    }, [id]);
    ```
- **MIRROR**: Polling pattern from SendToGameModal.jsx (debounce + interval)
- **IMPORTS**: Add `linkRoom`, `unlinkRoom` to imports from api.js
- **GOTCHA**: Must also update `GET /api/surveys/:id` to include `linked_room_code` in response. The getSurvey response currently doesn't include it — update surveys.js GET handler.
- **VALIDATE**: Open EditorPage, link a room, submit a response from another tab, verify count updates

### Task 7: Update surveys.js GET to return linked_room_code
- **ACTION**: Ensure the `GET /api/surveys/:id` endpoint returns `linked_room_code` in its response
- **IMPLEMENT**: The `GET /:id` handler in surveys.js likely does `SELECT *` — check if it serializes all columns. If it maps specific fields, add `linked_room_code` to the mapping.
- **MIRROR**: Existing survey serialization pattern in surveys.js
- **IMPORTS**: None
- **GOTCHA**: Check both the list endpoint (GET /api/surveys) and the single endpoint (GET /api/surveys/:id). Both should return the new field.
- **VALIDATE**: `curl /api/surveys/1` includes `linked_room_code` field (null or string)

### Task 8: Add Unity message class for new_web_response
- **ACTION**: Add `NewWebResponseMessage` to `Assets/Scripts/Network/NetworkMessages.cs`
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Notification from web app that a new survey response was submitted.
  /// </summary>
  [Serializable]
  public class NewWebResponseMessage
  {
      public string type = "new_web_response";
      public int responseCount;
      public string teamName;
      public int surveyId;
  }
  ```
- **MIRROR**: `UNITY_MESSAGE_CLASS` pattern from NetworkMessages.cs
- **IMPORTS**: `System` namespace (already imported for `[Serializable]`)
- **GOTCHA**: Use `int` for counts (not string). `JsonUtility` requires public fields, not properties.
- **VALIDATE**: Compiles without errors

### Task 9: Handle new_web_response in Unity SetupScreen
- **ACTION**: Add handling for `new_web_response` messages in `SetupScreen.OnNetworkMessage()`
- **IMPLEMENT**:
  - Add a new UI Text field `WebResponseCountText` (serialized field)
  - In `OnNetworkMessage`, after the `student_list` check and before the `survey_import` check:
    ```csharp
    if (baseMsg.type == "new_web_response")
    {
        var webMsg = JsonUtility.FromJson<NewWebResponseMessage>(json);
        if (WebResponseCountText != null)
        {
            WebResponseCountText.gameObject.SetActive(true);
            WebResponseCountText.text = $"Web responses: {webMsg.responseCount} (latest: {webMsg.teamName})";
        }
        return;
    }
    ```
  - Show the label only when messages are received (hidden by default)
- **MIRROR**: `UNITY_MESSAGE_HANDLER` pattern from SetupScreen.cs:337-361
- **IMPORTS**: None new
- **GOTCHA**: `WebResponseCountText` must be assigned in Unity Inspector — add `[Header("Web Response Sync")]` group. If not assigned, code gracefully does nothing (null check).
- **VALIDATE**: Build and run Unity; create a linked room; submit web response; verify text appears

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| link-room sets linked_room_code | PATCH with `{"roomCode":"ABCDEF"}` | DB has `linked_room_code = 'ABCDEF'` for survey | No |
| link-room uppercases code | PATCH with `{"roomCode":"abcdef"}` | DB has `linked_room_code = 'ABCDEF'` | Yes |
| link-room rejects empty code | PATCH with `{"roomCode":""}` | 400 error | Yes |
| unlink-room clears code | DELETE after linking | DB has `linked_room_code = NULL` | No |
| response POST notifies WS server | Submit response to linked survey | WS server receives POST at `/api/notify-response` | No |
| response POST skips notify when unlinked | Submit response to unlinked survey | No HTTP call to WS server | Yes |
| WS notify endpoint relays to professor | POST with valid roomCode | Professor receives `new_web_response` WS message | No |
| WS notify for nonexistent room | POST with unknown roomCode | Returns 200 success (no-op) | Yes |
| EditorPage polls response count | Wait 10s | Count refreshes from API | No |

### Edge Cases Checklist
- [x] Empty room code (rejected by API)
- [x] Room doesn't exist when response submitted (notification silently fails)
- [x] WS server unreachable (fire-and-forget, student gets normal success response)
- [x] Professor disconnected (room exists but professor WS is null — sendJSON checks readyState)
- [x] Concurrent response submissions (each triggers independent notification)
- [x] Survey deleted while linked (foreign key constraint handles cleanup)
- [x] Multiple surveys linked to same room (each notifies independently — fine)

---

## Validation Commands

### Static Analysis
```bash
# TypeScript/JS lint (if configured)
cd web-app && npm run lint 2>/dev/null || echo "No lint script configured"
```
EXPECT: No new errors

### Manual Test — Web-App Backend
```bash
# Start web-app
cd web-app && npm run dev

# Start WS server
cd Server && node server.js

# 1. Link a survey to a room
curl -X PATCH http://localhost:3001/api/surveys/1/link-room \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer <token>' \
  -d '{"roomCode":"TEST01"}'

# 2. Submit a response
curl -X POST http://localhost:3001/api/s/<shareCode>/respond \
  -H 'Content-Type: application/json' \
  -d '{"email":"test@test.com","teamName":"Alpha","answers":{}}'

# 3. Check WS server logs for notification
```
EXPECT: WS server logs notification; Unity professor gets `new_web_response` message

### Unity Build
```bash
# Build from Unity Editor — verify no compilation errors
```
EXPECT: Zero compilation errors

### Browser Validation
```bash
# Open EditorPage in browser
# 1. Enter room code and click "Link"
# 2. Open student survey in another tab, submit response
# 3. Observe response count auto-updates within 10s
```
EXPECT: Response count increments without manual refresh

### Manual Validation
- [ ] Link a survey to a room in EditorPage
- [ ] Unlink and verify badge disappears
- [ ] Submit a student response to linked survey
- [ ] Verify Unity SetupScreen shows "Web responses: N"
- [ ] Verify EditorPage response count auto-updates
- [ ] Submit response to unlinked survey — no errors, no notifications
- [ ] WS server down during response submission — student still gets success

---

## Acceptance Criteria
- [ ] Professor can link a survey to a room code in the EditorPage
- [ ] When a student submits a web survey response for a linked survey, the WS server is notified
- [ ] Unity professor client receives `new_web_response` and displays live response count
- [ ] EditorPage auto-updates response count every 10 seconds
- [ ] Notification failures (WS server down, room gone) do not affect student submission
- [ ] No changes to the student survey submission flow

## Completion Checklist
- [ ] Code follows discovered patterns (API response format, WS message format, Unity message class)
- [ ] Error handling matches codebase style (try-catch, fire-and-forget for notifications)
- [ ] No hardcoded values (use env vars for WS_GAME_URL, constants for poll interval)
- [ ] Tests written and passing
- [ ] No unnecessary scope additions (auto-send threshold deferred)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| WS server HTTP body parsing issues (no Express) | Medium | Medium | Test thoroughly with curl; handle chunked bodies |
| CORS preflight for POST from web-app backend to WS server | Low | Low | Backend-to-backend calls don't have CORS; only browser calls need it |
| SQLite ALTER TABLE migration for existing DBs | Medium | Low | Wrap in try-catch; column idempotently added |
| Polling overhead on EditorPage | Low | Low | 10s interval is lightweight; cleanup on unmount |
| Unity UI element not assigned in Inspector | Medium | Low | All null-checks in place; feature degrades gracefully |

## Notes
- The `linked_room_code` is intentionally stored in the `surveys` table (not a separate mapping table) because the relationship is 1:1 and ephemeral — a survey is linked to one room at a time.
- Auto-send at threshold is listed as "optional" in the gap analysis and is deferred to a future enhancement. The infrastructure (response count notification) makes it easy to add later — just add a threshold field and trigger send-to-game automatically in the Unity handler.
- The notification is fire-and-forget by design: a student's survey submission should never fail because of game server issues. The professor can always fall back to manual "Send to Game".
- The polling approach for EditorPage was chosen over SSE/WebSocket because: (1) it's simpler, (2) the web-app Express server doesn't currently have WS support, (3) 10s polling is adequate for this use case, (4) it follows the existing pattern in SendToGameModal.jsx which also polls room status.
