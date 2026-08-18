# Plan: Survey Data Direct Pass from Web App to Unity Game

## Summary
Enable professors to directly send survey response data from the web-app dashboard into the running Unity WebGL game, eliminating the manual copy-paste JSON export/import step. The web-app will push data to Unity via the existing WebSocket relay server, so the professor clicks one button in the browser and the game loads the cars + rules automatically.

## User Story
As a professor, I want to send my survey data directly from the web dashboard to the running EDI Racing game, so that I don't have to manually export JSON, switch to the game, paste it, and confirm import.

## Problem → Solution
**Current state**: Professor must (1) open web-app editor → (2) click "Export for Unity" → (3) copy JSON to clipboard → (4) switch to Unity game → (5) click "Import JSON" → (6) paste into text field → (7) click "Confirm Import". Seven manual steps, context-switching between browser tabs.

**Desired state**: Professor (1) opens web-app dashboard → (2) clicks "Send to Game" → data flows automatically to the Unity game via WebSocket and the race starts. Two steps, no context switch.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md` (post Phase 6 enhancement)
- **PRD Phase**: N/A — standalone enhancement
- **Estimated Files**: 8-10

---

## UX Design

### Before
```
┌─ Web App (Browser Tab 1) ─────────────────┐    ┌─ Unity Game (Browser Tab 2) ──────────┐
│                                            │    │                                       │
│  [Export for Unity] → JSON preview panel   │    │  Setup Screen                         │
│  [Download JSON] [Copy to Clipboard]       │    │  [Import JSON] → shows import panel   │
│                                            │    │  [paste area.............]             │
│  Professor: copy JSON, switch tab ────────────→ │  [Confirm Import]                     │
│                                            │    │  → parses JSON, starts race            │
└────────────────────────────────────────────┘    └───────────────────────────────────────┘
      ⬆ 7 manual steps, error-prone
```

### After
```
┌─ Web App (Browser Tab 1) ─────────────────┐         ┌─ Unity Game (same or separate tab) ─┐
│                                            │   WS    │                                      │
│  [Send to Game]                            │ ──────→ │  Setup Screen                        │
│  Status: "Sent! 12 cars, 3 rules loaded"  │  relay  │  ← auto-receives data                │
│                                            │         │  ← auto-starts race (or shows ready) │
│  Professor: one click, done                │         │                                      │
└────────────────────────────────────────────┘         └──────────────────────────────────────┘
      ⬆ 1-click flow via existing WebSocket relay
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Web App Editor | "Export for Unity" → shows JSON preview | "Send to Game" button added alongside export | Export still available as fallback |
| Web App Dashboard | No direct send | "Send to Game" button on each survey card | Quick access without opening editor |
| Unity SetupScreen | Manual "Import JSON" paste flow | Auto-receives `survey_import` WS message | Import panel still works as fallback |
| WebSocket Server | Relays professor→students only | Also relays web-app→professor messages | New `survey_import` message type |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/export.js` | all | Existing export logic — `mapResponsesToCarData()` produces the exact JSON shape Unity expects |
| P0 | `Assets/Scripts/Data/JsonImporter.cs` | all | Unity-side JSON parser for web-app export format — must use identical data shape |
| P0 | `Assets/Scripts/UI/SetupScreen.cs` | 295-330 | Existing `OnConfirmImport()` — the import flow we're automating |
| P0 | `Server/server.js` | all | WebSocket relay — needs new message routing for web-app→professor |
| P1 | `Assets/Scripts/Network/NetworkManager.cs` | all | WS client in Unity — handles incoming messages |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | all | Message type definitions — add new `survey_import` type |
| P1 | `web-app/client/src/api.js` | all | Web-app API client — add WebSocket connection logic |
| P1 | `web-app/client/src/pages/EditorPage.jsx` | 42-67 | Export button location — add "Send to Game" alongside |
| P2 | `Assets/Scripts/Race/RaceManager.cs` | 70-100 | `LoadAndStartRaceWithRules()` — the method that receives imported data |
| P2 | `Deploy/docker-compose.yml` | all | Deployment topology — web-app can reach game container via `edi-racing-net` |
| P2 | `Deploy/nginx/nginx.conf` | 76-89 | Nginx proxying — `/api` routes go to web-app, `/ws` goes to game WS server |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| WebSocket from browser | Standard browser API | Web-app frontend can open a WS connection to the game's relay server at `/ws` |
| Unity JsonUtility | Unity docs | Already used throughout; export JSON shape must match `WebAppExport` class in `JsonImporter.cs` |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:1-13
[Serializable]
public class NetworkMessage
{
    public string type;
}
// All message types follow: PascalCase class name, camelCase fields, string `type` discriminator
```

### ERROR_HANDLING
```csharp
// SOURCE: Assets/Scripts/Data/JsonImporter.cs:34-50
public static ImportResult Parse(string json)
{
    if (string.IsNullOrEmpty(json))
        return ImportResult.Fail("JSON content is empty");
    try { export = JsonUtility.FromJson<WebAppExport>(json); }
    catch (Exception e) { return ImportResult.Fail($"Failed to parse JSON: {e.Message}"); }
}
// Pattern: return error object, never throw
```

### LOGGING_PATTERN
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:329
Debug.Log($"[SetupScreen] Imported {result.Cars.Count} cars, {result.EventRules.Length} rules from Web App JSON");
// Pattern: [ClassName] action detail
```

### API_RESPONSE
```javascript
// SOURCE: web-app/src/routes/export.js:108-110
res.json({ success: true, data: exportData });
// Pattern: { success: bool, data?: T, error?: string }
```

### WEBSOCKET_MESSAGE
```javascript
// SOURCE: Server/server.js:94-107
switch (msg.type) {
  case 'create_room': { ... }
  case 'join_room': { ... }
}
// Pattern: JSON with `type` string discriminator, switch-case routing
```

### TEST_STRUCTURE
```csharp
// SOURCE: Assets/Tests/EditMode/JsonImporterTests.cs (exists)
// Tests for JsonImporter already exist — follow same pattern for new message handling
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Server/server.js` | UPDATE | Add new `survey_import` message type: web-app client sends to professor client |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add `SurveyImportMessage` class |
| `Assets/Scripts/Network/NetworkManager.cs` | UPDATE | Handle `survey_import` message type in `HandleMessage` |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Add handler for auto-import when `survey_import` arrives |
| `web-app/src/routes/export.js` | UPDATE | Add new endpoint `POST /api/surveys/:id/send-to-game` |
| `web-app/client/src/api.js` | UPDATE | Add `sendToGame(id, roomCode)` API function |
| `web-app/client/src/pages/EditorPage.jsx` | UPDATE | Add "Send to Game" button with room code input |
| `web-app/client/src/pages/DashboardPage.jsx` | UPDATE | Add "Send to Game" button on survey cards |
| `web-app/client/src/components/SendToGameModal.jsx` | CREATE | Reusable modal for room code entry + send action |

## NOT Building

- Real-time bi-directional sync between web-app and Unity (data flows one-way: web→game)
- Auto-discovery of running game instances (professor must enter room code)
- Persistent WebSocket connection from web-app (ephemeral: connect, send, disconnect)
- Changes to the existing "Export for Unity" / "Import JSON" manual flow (kept as fallback)
- Authentication between web-app and game server (room code is sufficient — same trust model as students)
- Survey response collection via web-app while race is running (use existing WebSocket survey flow for that)

---

## Step-by-Step Tasks

### Task 1: Add `survey_import` message type to WebSocket server
- **ACTION**: Extend `Server/server.js` to recognize `survey_import` messages from a new "web-app" client role
- **IMPLEMENT**: 
  - When a client sends `{ type: "web_join_room", roomCode: "XXXX" }`, register it with role `"webapp"` in `clientRooms` (similar to student join, but different role)
  - When a webapp-role client sends `{ type: "survey_import", ... }`, relay the full message to the room's professor client (not students)
  - After relay, send acknowledgement back to webapp client: `{ type: "survey_import_ack", success: true }`
  - The webapp client can then disconnect
- **MIRROR**: `WEBSOCKET_MESSAGE` — switch-case for new types; `sendJSON()` for responses
- **IMPORTS**: None (pure Node.js)
- **GOTCHA**: Don't add webapp to `room.students` set — it should not receive race state updates or survey distributions. Use a separate tracking or just relay immediately without adding to any set.
- **VALIDATE**: Run server locally, connect 3 WS clients (professor, student, webapp). Professor creates room, webapp joins with `web_join_room`, sends `survey_import`, professor receives it.

### Task 2: Define `SurveyImportMessage` in Unity
- **ACTION**: Add new message class in `NetworkMessages.cs`
- **IMPLEMENT**:
  ```csharp
  [Serializable]
  public class SurveyImportMessage
  {
      public string type = "survey_import";
      public string configName;
      public string exportJson; // The full export JSON string (same format as manual export)
  }
  ```
  The `exportJson` field is a double-serialized string containing the web-app export JSON (same format `JsonImporter.Parse()` already handles). This avoids JsonUtility nested object limitations.
- **MIRROR**: `NAMING_CONVENTION` — follows existing message class pattern
- **IMPORTS**: `System;`
- **GOTCHA**: Keep field names camelCase to match JS → JSON convention used by all other messages.
- **VALIDATE**: Builds without errors.

### Task 3: Handle `survey_import` in Unity NetworkManager → SetupScreen
- **ACTION**: Route the `survey_import` message from `NetworkManager.HandleMessage()` to `SetupScreen`
- **IMPLEMENT**:
  - In `NetworkManager.HandleMessage()` `switch`, add case `"survey_import"` that fires `OnMessageReceived` (same as default — the message will be handled by whoever listens)
  - In `SetupScreen`, subscribe to `NetworkManager.OnMessageReceived` and check for `survey_import` type
  - When received, extract `exportJson`, pass to `JsonImporter.Parse()`, and call `RaceManager.LoadAndStartRaceWithRules()` — reusing the exact same code path as `OnConfirmImport()`
  - Show feedback in `InfoText`: "Received survey data from web app: X cars, Y rules. Starting race..."
- **MIRROR**: `ERROR_HANDLING` — use `ImportResult.Fail()` pattern; `LOGGING_PATTERN` — `[SetupScreen]` prefix
- **IMPORTS**: None new (all types already imported)
- **GOTCHA**: Only handle if `NetworkManager.IsHost` — don't let students accidentally trigger import. Also only handle during `GameState.Setup` — ignore if race already running.
- **VALIDATE**: Unit test: mock a `survey_import` JSON string → parse → verify CarData list matches expected.

### Task 4: Add web-app API endpoint for send-to-game
- **ACTION**: Add `POST /api/surveys/:id/send-to-game` in `web-app/src/routes/export.js`
- **IMPLEMENT**:
  - Accept `{ roomCode: string }` in request body
  - Reuse the existing export logic (`mapResponsesToCarData`) to build the export payload
  - Open a WebSocket connection to the game server (URL from env: `WS_GAME_URL` or default `ws://edi-racing-game:3000/ws`)
  - Send `{ type: "web_join_room", roomCode }` → wait for `room_joined`
  - Send `{ type: "survey_import", configName, exportJson: JSON.stringify(exportPayload) }`
  - Wait for `survey_import_ack` → respond `{ success: true }` to HTTP client
  - Close WebSocket
  - Timeout after 5 seconds → respond `{ success: false, error: "Game server not reachable" }`
- **MIRROR**: `API_RESPONSE` — `{ success, data/error }` envelope
- **IMPORTS**: `ws` package (add to `web-app/package.json` — already a dep in Server)
- **GOTCHA**: The web-app server connects to the WS server as a backend client, not the browser. In Docker, the WS server hostname is `edi-racing-game` on port `3000` (internal). In dev, it's `localhost:8080`. Use env var `WS_GAME_URL`.
- **VALIDATE**: `curl -X POST http://localhost:3001/api/surveys/1/send-to-game -H 'Content-Type: application/json' -d '{"roomCode":"ABCDEF"}'` → returns success if game server is running with that room.

### Task 5: Add `ws` dependency to web-app
- **ACTION**: Add `ws` to `web-app/package.json` dependencies
- **IMPLEMENT**: Add `"ws": "^8.18.0"` (same version as Server)
- **MIRROR**: Match Server/package.json version
- **IMPORTS**: N/A
- **GOTCHA**: The web-app server-side code (Node.js) uses this — the client-side (React) uses the native browser WebSocket API if needed.
- **VALIDATE**: `cd web-app && npm install` succeeds.

### Task 6: Add `sendToGame` API function in web-app client
- **ACTION**: Add new function to `web-app/client/src/api.js`
- **IMPLEMENT**:
  ```javascript
  export async function sendToGame(id, roomCode) {
    return request(`/surveys/${id}/send-to-game`, {
      method: 'POST',
      body: JSON.stringify({ roomCode }),
    });
  }
  ```
- **MIRROR**: `API_RESPONSE` — follows existing `request()` pattern
- **IMPORTS**: None (uses existing `request()` helper)
- **GOTCHA**: None — straightforward.
- **VALIDATE**: Call succeeds when server is running.

### Task 7: Create `SendToGameModal` component
- **ACTION**: Create `web-app/client/src/components/SendToGameModal.jsx`
- **IMPLEMENT**:
  - Modal with room code input field
  - "Send" button that calls `sendToGame(surveyId, roomCode)`
  - Status display: sending → success / error
  - "Close" button
  - Remember last-used room code in `localStorage` key `edi-last-room-code`
  - Props: `{ surveyId, onClose }`
- **MIRROR**: Follow existing component patterns in `web-app/client/src/components/` (functional components, inline event handlers)
- **IMPORTS**: `import { sendToGame } from '../api.js';`
- **GOTCHA**: Room code should be auto-uppercased (match server convention). Show clear error if game server unreachable.
- **VALIDATE**: Modal renders, input works, sends request.

### Task 8: Add "Send to Game" button to EditorPage
- **ACTION**: Add button in `EditorPage.jsx` header alongside "Export for Unity"
- **IMPLEMENT**:
  - Add state: `const [showSendModal, setShowSendModal] = useState(false);`
  - Add button: `<button onClick={() => setShowSendModal(true)} className="btn-primary">Send to Game</button>`
  - Render modal: `{showSendModal && <SendToGameModal surveyId={id} onClose={() => setShowSendModal(false)} />}`
- **MIRROR**: Follows existing export button pattern
- **IMPORTS**: `import SendToGameModal from '../components/SendToGameModal.jsx';`
- **GOTCHA**: Button should be disabled if response count is 0 (same logic as export — no data to send).
- **VALIDATE**: Button visible in editor, opens modal, sends data.

### Task 9: Add "Send to Game" button to DashboardPage
- **ACTION**: Add quick-send button on each survey card in `DashboardPage.jsx`
- **IMPLEMENT**:
  - Add state: `const [sendModalSurveyId, setSendModalSurveyId] = useState(null);`
  - Add button inside each card (next to Delete): `<button onClick={e => { e.stopPropagation(); setSendModalSurveyId(s.id); }}>Send to Game</button>`
  - Render modal: `{sendModalSurveyId && <SendToGameModal surveyId={sendModalSurveyId} onClose={() => setSendModalSurveyId(null)} />}`
- **MIRROR**: Follows existing card button pattern (stopPropagation to avoid card click navigation)
- **IMPORTS**: `import SendToGameModal from '../components/SendToGameModal.jsx';`
- **GOTCHA**: Only show if survey has responses (`response_count > 0`).
- **VALIDATE**: Button visible on cards with responses, opens modal.

### Task 10: Add `WS_GAME_URL` environment variable to Docker Compose
- **ACTION**: Add env var to `web-app` service in `Deploy/docker-compose.yml`
- **IMPLEMENT**:
  ```yaml
  environment:
    - WS_GAME_URL=ws://edi-racing-game:3000
  ```
  In Docker network, the game container is `edi-racing-game` and its internal WS server runs on port 3000 (started by `start.sh`).
- **MIRROR**: Follows existing env var pattern (`API_PORT`, `DB_PATH`)
- **IMPORTS**: N/A
- **GOTCHA**: No `/ws` path suffix — the Node.js WS server in the game container listens on the root path (port 3000 directly, not proxied through nginx).
- **VALIDATE**: `docker compose config` shows the env var.

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `JsonImporter.Parse` with survey_import payload | `{ configName: "test", carData: [...], eventRules: [...] }` | `ImportResult.Success == true`, correct car count | No |
| `JsonImporter.Parse` with empty carData | `{ configName: "test", carData: [], eventRules: [] }` | `ImportResult.Success == true`, 0 cars | Yes |
| `JsonImporter.Parse` with null/empty string | `""` | `ImportResult.Success == false` | Yes |
| `mapResponsesToCarData` with mappings | `answers={q1:"yes"}`, mapping q1→attr1 | `carData.attributes[0] = {key:"attr1", value:"yes"}` | No |
| WS relay: webapp sends survey_import | WS message to professor | Professor receives identical message | No |
| WS relay: webapp sends to invalid room | `roomCode: "INVALID"` | Error response | Yes |

### Edge Cases Checklist
- [ ] Room code doesn't exist → clear error in web-app UI
- [ ] Game server not running → timeout, clear error
- [ ] Survey has 0 responses → "Send to Game" disabled
- [ ] Race already started in Unity → `survey_import` ignored (only handle in Setup state)
- [ ] Multiple rapid sends → idempotent (last one wins, re-loads cars)
- [ ] Large survey (50 teams, 20 attributes each) → JSON fits in single WS frame (< 100KB, well within limits)
- [ ] Professor not hosting a room → Unity ignores message (IsHost check)
- [ ] Room code case sensitivity → auto-uppercase in web-app

---

## Validation Commands

### Static Analysis
```bash
# Web-app lint
cd web-app && npx eslint src/ client/src/ --ext .js,.jsx 2>/dev/null || echo "ESLint not configured — manual review"
```
EXPECT: No new errors

### Unit Tests
```bash
# Unity EditMode tests (run from Unity Editor or CI)
# Existing JsonImporter tests should still pass
```
EXPECT: All tests pass

### Integration Test
```bash
# 1. Start WS server
cd Server && node server.js &

# 2. Start web-app
cd web-app && npm start &

# 3. Test sequence:
# - Create a survey with responses via web-app API
# - Start Unity game, host a room → get room code
# - Send to game via: curl -X POST http://localhost:3001/api/surveys/1/send-to-game -H 'Authorization: Bearer TOKEN' -H 'Content-Type: application/json' -d '{"roomCode":"ROOMCODE"}'
# - Verify Unity game receives data and loads cars
```
EXPECT: Cars spawn in Unity after API call

### Docker Validation
```bash
cd Deploy && docker compose config
```
EXPECT: `WS_GAME_URL` present in web-app service environment

### Manual Validation
- [ ] Open web-app dashboard, create survey with 2+ responses
- [ ] Open Unity game in another tab, host a room, note room code
- [ ] In web-app, click "Send to Game", enter room code, click Send
- [ ] Verify Unity game receives data: InfoText updates, race starts with correct cars
- [ ] Verify "Export for Unity" manual flow still works as fallback
- [ ] Test error case: enter invalid room code → see error in modal

---

## Acceptance Criteria
- [ ] Professor can send survey data to Unity game with one click + room code
- [ ] Unity game auto-receives and starts race with correct car data and rules
- [ ] Existing manual export/import flow still works
- [ ] Error states handled: invalid room code, game not running, no responses
- [ ] Works in Docker deployment topology
- [ ] No changes to student WebSocket flow
- [ ] Room code remembered between sends (localStorage)

## Completion Checklist
- [ ] Code follows discovered patterns (message types, API envelope, logging)
- [ ] Error handling matches codebase style (ImportResult.Fail, { success, error })
- [ ] Logging follows `[ClassName]` prefix convention
- [ ] WebSocket message routing follows switch-case pattern
- [ ] No hardcoded URLs (use env vars for WS_GAME_URL)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| WS server not reachable from web-app container in Docker | LOW | HIGH | Use Docker network hostname; add env var for URL override |
| Large export JSON exceeds WS frame size | VERY LOW | MEDIUM | 50 teams × 20 attrs ≈ 30KB — well under WS limits. Log payload size. |
| Professor clicks "Send to Game" before hosting a room in Unity | MEDIUM | LOW | Show clear instruction in modal: "Host a room in the game first, then enter the room code" |
| Race auto-starts unexpectedly | MEDIUM | LOW | Add confirmation in Unity before auto-starting, or just load data without starting (show "Ready: X cars loaded, click Start") |

## Notes
- The architecture decision to use the existing WebSocket relay (rather than direct HTTP from web-app to Unity) is intentional: Unity WebGL cannot run an HTTP server, but it already has a WebSocket client connected to the relay. The relay acts as a bridge.
- The web-app backend (Node.js) initiates the WS connection as a transient client — connect, send, get ack, disconnect. This avoids keeping a persistent connection.
- The `exportJson` field in the WS message is double-serialized (JSON string inside JSON) to match the existing `SurveyQuestionsMessage.configJson` pattern used for survey distribution.
- Consider a future enhancement: instead of room code entry, the Unity game could display a QR code or deep link that pre-fills the room code. Out of scope for this plan.
