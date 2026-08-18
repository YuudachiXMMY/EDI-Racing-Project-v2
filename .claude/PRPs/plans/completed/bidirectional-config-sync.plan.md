# Plan: Bi-directional Survey Config Sync (GAP 4)

## Summary
Enable professors to sync survey configurations between Unity's in-game SurveyBuilderPanel and the web app. Currently, the web app can only send **processed CarData** (losing raw questions/mappings) to Unity, and Unity has **no path** to export configs to the web app. This plan adds true bi-directional raw config sync: Unity can push configs to the web app, and the web app can push raw configs (not just processed data) to Unity.

## User Story
As a professor,
I want to create survey configs in either Unity or the web app and sync them to the other platform,
So that I can use whichever tool is convenient and always have my configs available in both places.

## Problem -> Solution
**Current:** Web -> Unity only sends processed CarData (loses raw config). Unity -> Web has no path. Configs created in Unity's SurveyBuilderPanel are trapped as local JSON files. Web app configs can only be "sent to game" as one-shot processed data.

**Desired:** Raw SurveyConfig (questions, mappings, rules) can flow in both directions via WebSocket relay. Professors can create in either tool and sync to the other.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/plans/web-unity-gap-analysis-and-hosting.plan.md`
- **PRD Phase**: GAP 4
- **Estimated Files**: 10

---

## UX Design

### Before
```
Unity SurveyBuilderPanel                    Web App Editor
  [Create Config]                           [Create Config]
  [Save to Local JSON]                      [Save to SQLite]
  [Load from Local JSON]                    [CRUD full editor]
        |                                         |
        X (no export to web)              "Send to Game" ---> processed CarData + rules
                                           (loses raw questions & mappings)
```

### After
```
Unity SurveyBuilderPanel                    Web App Editor
  [Create Config]                           [Create Config]
  [Save to Local JSON]                      [Save to SQLite]
  [Load from Local JSON]                    [CRUD full editor]
        |                                         |
  [Push Config to Web] ----WebSocket----> [Import from Game] (auto-imports)
        |                                         |
  [Pull Config from Web] <---WebSocket--- [Send Config to Game] (raw config)
                                                  |
                                           [Send to Game] (processed CarData, unchanged)
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Unity SetupScreen | No export option | "Push Config to Web App" button | Only visible when room is hosted and config is active |
| Unity SetupScreen | Only receives processed CarData | Also receives raw config via `config_import` | Loads config into SurveyConfigManager as active |
| Web EditorPage | Only "Send to Game" (processed data) | Also "Send Config to Game" (raw config) | New button next to existing "Send to Game" |
| Web DashboardPage | No import from game | "Import from Game" notification when config received | Auto-creates survey from received Unity config |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/Data/SurveyConfig.cs` | all | Core data model for config sync |
| P0 (critical) | `Server/server.js` | all | WebSocket message routing, room model |
| P0 (critical) | `web-app/src/routes/export.js` | 116-201 | Existing `send-to-game` pattern to mirror |
| P0 (critical) | `Assets/Scripts/Network/NetworkMessages.cs` | all | Message type conventions |
| P1 (important) | `Assets/Scripts/UI/SetupScreen.cs` | 334-401 | OnNetworkMessage handler for `survey_import` |
| P1 (important) | `web-app/src/routes/surveys.js` | 56-81 | POST survey creation pattern |
| P1 (important) | `web-app/client/src/api.js` | all | API client wrapper pattern |
| P1 (important) | `web-app/client/src/pages/EditorPage.jsx` | all | Tab-based editor UI pattern |
| P2 (reference) | `Assets/Scripts/Data/SurveyConfigManager.cs` | all | Config save/load/setActive pattern |
| P2 (reference) | `Assets/Scripts/Data/SurveyQuestion.cs` | all | SurveyQuestion struct |
| P2 (reference) | `Assets/Scripts/Data/AttributeMapping.cs` | all | AttributeMapping struct |
| P2 (reference) | `Assets/Scripts/Data/JsonImporter.cs` | all | Existing JSON parsing pattern |
| P2 (reference) | `web-app/src/schema.sql` | all | Database schema |
| P2 (reference) | `web-app/client/src/components/SendToGameModal.jsx` | all | Existing modal pattern for send-to-game |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| N/A | N/A | Feature uses only established internal patterns — no external libraries needed |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:267-272
// C# message classes: PascalCase, [Serializable], type field matches WS message type
[Serializable]
public class SurveyImportMessage
{
    public string type = "survey_import";
    public string configName;
    public string exportJson;
}
```

```javascript
// SOURCE: web-app/src/routes/export.js:116-117
// Express routes: Router(), requireAuth middleware, RESTful paths
router.post('/:id/send-to-game', requireAuth, (req, res) => {
```

```javascript
// SOURCE: web-app/client/src/api.js:19-33
// API client: request() wrapper with token handling
async function request(path, options = {}) {
  const token = getToken();
  const headers = { 'Content-Type': 'application/json', ...options.headers };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const res = await fetch(`/api${path}`, { ...options, headers });
  const json = await res.json();
  ...
  return json;
}
```

### ERROR_HANDLING
```javascript
// SOURCE: web-app/src/routes/export.js:118-121
// Express: validate required fields, return { success: false, error: '...' }
if (!roomCode || !roomCode.trim()) {
    return res.status(400).json({ success: false, error: 'roomCode is required' });
}
```

```javascript
// SOURCE: Server/server.js:24-29
// WebSocket server: sendJSON helper, readyState check
function sendJSON(ws, obj) {
  if (ws.readyState === 1) {
    ws.send(JSON.stringify(obj));
  }
}
```

```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:377-389
// Unity: null-check message fields, show error via InfoText
var msg = JsonUtility.FromJson<SurveyImportMessage>(json);
if (string.IsNullOrEmpty(msg.exportJson))
{
    if (InfoText != null) InfoText.text = "Received empty data from web app.";
    return;
}
```

### LOGGING_PATTERN
```javascript
// SOURCE: Server/server.js:237
// Server: [Room CODE] prefix for room-scoped logs
console.log(`[Room ${roomCode}] Created${msg.sessionId ? ` (session: ${msg.sessionId})` : ''}`);
```

```csharp
// SOURCE: Assets/Scripts/Data/SurveyConfigManager.cs:39
// Unity: [ClassName] prefix for all Debug.Log calls
Debug.Log($"[SurveyConfigManager] Config saved: {path}");
```

### WEBSOCKET_MESSAGE_PATTERN
```javascript
// SOURCE: Server/server.js:359-374
// Server: handle specific message type in switch/case, validate role, relay to target
case 'survey_import': {
    const webInfo = clientRooms.get(ws);
    if (!webInfo || webInfo.role !== 'webapp') {
        sendJSON(ws, { type: 'error', message: 'Not authorized' });
        return;
    }
    const importRoom = rooms.get(webInfo.roomCode);
    if (!importRoom || !importRoom.professor || importRoom.professor.readyState !== 1) {
        sendJSON(ws, { type: 'survey_import_ack', success: false, error: 'Professor not connected' });
        return;
    }
    importRoom.professor.send(data.toString());
    sendJSON(ws, { type: 'survey_import_ack', success: true });
    break;
}
```

### API_RESPONSE_PATTERN
```javascript
// SOURCE: web-app/src/routes/surveys.js:77-80
// Express: { success: true, data: { ... } } envelope
res.status(201).json({
    success: true,
    data: { id: result.lastInsertRowid, shareCode }
});
```

### SEND_TO_GAME_PATTERN
```javascript
// SOURCE: web-app/src/routes/export.js:156-191
// Express: temporary WS connection for one-shot relay
const ws = new WebSocket(WS_GAME_URL);
let responded = false;
const timeout = setTimeout(() => { ... }, 5000);
ws.on('open', () => {
    ws.send(JSON.stringify({ type: 'web_join_room', roomCode: code }));
});
ws.on('message', (data) => {
    // Handle room_joined -> send payload -> wait for ack
});
```

### TEST_STRUCTURE
No existing tests found in the web app or Unity C# code for these integration points. New tests should follow Jest conventions for the web app side.

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Server/server.js` | UPDATE | Add `config_export` and `config_import` message handlers |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add ConfigExportMessage, ConfigImportMessage types |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Add Push/Pull config buttons and `config_import` handler |
| `web-app/src/routes/export.js` | UPDATE | Add `POST /:id/send-config-to-game` and `POST /import-config` endpoints |
| `web-app/client/src/api.js` | UPDATE | Add `sendConfigToGame()` and `importConfigFromGame()` functions |
| `web-app/client/src/pages/EditorPage.jsx` | UPDATE | Add "Send Config to Game" button |
| `web-app/client/src/components/SendConfigModal.jsx` | CREATE | Modal for sending raw config to Unity |
| `web-app/src/index.js` | UPDATE | Mount new import-config route (if separate router) |

## NOT Building

- Template sync between Unity's hardcoded `SurveyTemplates.cs` and web app's `templates` table — these are separate template systems
- Auto-sync (real-time bidirectional config mirroring) — this is manual push/pull only
- Deprecation of Unity's SurveyBuilderPanel — both builders remain functional
- Merging/conflict resolution — import always creates new or overwrites, no merge logic
- Authentication from Unity to web app — config flows via WebSocket relay through the server, not direct HTTP
- Versioning or diff tracking of config changes between platforms

---

## Step-by-Step Tasks

### Task 1: Add C# message types for config sync
- **ACTION**: Add `ConfigExportMessage` and `ConfigImportMessage` to `NetworkMessages.cs`
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Professor -> Server -> Web App: exports raw SurveyConfig for import into web app.
  /// configJson is a serialized SurveyConfig (questions, mappings, rules).
  /// </summary>
  [Serializable]
  public class ConfigExportMessage
  {
      public string type = "config_export";
      public string configName;
      public string configJson;
  }

  /// <summary>
  /// Web App -> Server -> Professor: sends raw SurveyConfig for loading into Unity.
  /// configJson is a serialized SurveyConfig (questions, mappings, rules).
  /// </summary>
  [Serializable]
  public class ConfigImportMessage
  {
      public string type = "config_import";
      public string configName;
      public string configJson;
  }

  /// <summary>
  /// Server -> Client: acknowledges config export/import was processed.
  /// </summary>
  [Serializable]
  public class ConfigSyncAckMessage
  {
      public string type = "config_sync_ack";
      public bool success;
      public string error;
      public string direction; // "export" or "import"
  }
  ```
- **MIRROR**: `SurveyImportMessage` pattern at line 267-272
- **IMPORTS**: `using System;`
- **GOTCHA**: `JsonUtility` cannot serialize nested complex types directly — use double-serialization (configJson as string) like existing `SurveyImportMessage.exportJson` and `SurveyQuestionsMessage.configJson`
- **VALIDATE**: File compiles without errors in Unity

### Task 2: Add WebSocket handler for `config_export` in server.js
- **ACTION**: Add `config_export` case to the switch statement in `server.js`
- **IMPLEMENT**:
  ```javascript
  case 'config_export': {
      const profInfo = clientRooms.get(ws);
      if (!profInfo || profInfo.role !== 'professor') {
          sendJSON(ws, { type: 'config_sync_ack', success: false, error: 'Not authorized', direction: 'export' });
          return;
      }
      const exportRoom = rooms.get(profInfo.roomCode);
      if (!exportRoom) {
          sendJSON(ws, { type: 'config_sync_ack', success: false, error: 'Room not found', direction: 'export' });
          return;
      }
      // Cache config in room for web-app clients to fetch
      exportRoom.latestConfig = data.toString();
      // Relay to all connected web-app clients
      for (const webapp of exportRoom.webapps) {
          if (webapp.readyState === 1) webapp.send(data.toString());
      }
      sendJSON(ws, { type: 'config_sync_ack', success: true, direction: 'export' });
      console.log(`[Room ${profInfo.roomCode}] Config exported from Unity: ${msg.configName || '(unnamed)'}`);
      break;
  }
  ```
- **MIRROR**: `survey_import` case at line 359-374 for role validation and relay pattern
- **IMPORTS**: None (all in same file)
- **GOTCHA**: Must be added as a **named case** before the `default` block, not inside the default's professor relay. The `config_export` comes from a professor client but needs special handling (relay to webapps, not students).
- **VALIDATE**: Start server, send `config_export` from a WebSocket client, verify relay and ack

### Task 3: Add WebSocket handler for `config_import` in server.js
- **ACTION**: Add `config_import` case to the switch statement in `server.js`
- **IMPLEMENT**:
  ```javascript
  case 'config_import': {
      const webInfo = clientRooms.get(ws);
      if (!webInfo || webInfo.role !== 'webapp') {
          sendJSON(ws, { type: 'config_sync_ack', success: false, error: 'Not authorized', direction: 'import' });
          return;
      }
      const importRoom = rooms.get(webInfo.roomCode);
      if (!importRoom || !importRoom.professor || importRoom.professor.readyState !== 1) {
          sendJSON(ws, { type: 'config_sync_ack', success: false, error: 'Professor not connected', direction: 'import' });
          return;
      }
      importRoom.professor.send(data.toString());
      sendJSON(ws, { type: 'config_sync_ack', success: true, direction: 'import' });
      console.log(`[Room ${webInfo.roomCode}] Config imported from web-app: ${msg.configName || '(unnamed)'}`);
      break;
  }
  ```
- **MIRROR**: `survey_import` case at line 359-374 (identical pattern: webapp -> professor relay)
- **IMPORTS**: None
- **GOTCHA**: This parallels the existing `survey_import` handler but sends raw config instead of processed CarData. They are distinct message types with different payloads.
- **VALIDATE**: Start server, send `config_import` from a webapp-role client, verify relay to professor

### Task 4: Add `latestConfig` to room initialization in server.js
- **ACTION**: Add `latestConfig: null` to the room object in `create_room` handler
- **IMPLEMENT**: In line 217-230, add `latestConfig: null` to the room initialization object
- **MIRROR**: Follows existing `latestState`, `surveyData` caching pattern
- **IMPORTS**: None
- **GOTCHA**: Also send cached config to late-joining webapp clients (in `web_join_room` handler), same as `latestState` is sent
- **VALIDATE**: Verify room object has the field after creation

### Task 5: Add REST endpoint for sending raw config to game
- **ACTION**: Add `POST /:id/send-config-to-game` route in `web-app/src/routes/export.js`
- **IMPLEMENT**:
  ```javascript
  // POST /api/surveys/:id/send-config-to-game — send raw survey config to Unity
  router.post('/:id/send-config-to-game', requireAuth, (req, res) => {
      const { roomCode } = req.body;
      if (!roomCode || !roomCode.trim()) {
          return res.status(400).json({ success: false, error: 'roomCode is required' });
      }

      const db = getDb();
      const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
          .get(req.params.id, req.user.userId);
      if (!survey) {
          return res.status(404).json({ success: false, error: 'Survey not found' });
      }

      const configPayload = {
          ConfigName: survey.config_name,
          Description: survey.description || '',
          CreatedAt: survey.created_at,
          Version: '1.0',
          Questions: JSON.parse(survey.questions_json),
          Mappings: JSON.parse(survey.mappings_json),
          Rules: JSON.parse(survey.rules_json),
      };

      const code = roomCode.trim().toUpperCase();
      const ws = new WebSocket(WS_GAME_URL);
      let responded = false;

      const timeout = setTimeout(() => {
          if (!responded) {
              responded = true;
              ws.close();
              res.status(504).json({ success: false, error: 'Game server did not respond in time' });
          }
      }, 5000);

      ws.on('open', () => {
          ws.send(JSON.stringify({ type: 'web_join_room', roomCode: code }));
      });

      ws.on('message', (data) => {
          if (responded) return;
          const msg = JSON.parse(data.toString());

          if (msg.type === 'error') {
              responded = true;
              clearTimeout(timeout);
              ws.close();
              return res.status(400).json({ success: false, error: msg.message || 'Room not found' });
          }

          if (msg.type === 'room_joined') {
              ws.send(JSON.stringify({
                  type: 'config_import',
                  configName: survey.config_name,
                  configJson: JSON.stringify(configPayload),
              }));
          }

          if (msg.type === 'config_sync_ack') {
              responded = true;
              clearTimeout(timeout);
              ws.close();
              if (msg.success) {
                  return res.json({ success: true, data: { configName: survey.config_name } });
              }
              return res.status(400).json({ success: false, error: msg.error || 'Config sync failed' });
          }
      });

      ws.on('error', () => {
          if (!responded) {
              responded = true;
              clearTimeout(timeout);
              res.status(502).json({ success: false, error: 'Cannot connect to game server' });
          }
      });
  });
  ```
- **MIRROR**: Existing `POST /:id/send-to-game` at lines 117-201 for WS connection pattern
- **IMPORTS**: `WebSocket` already imported at line 2
- **GOTCHA**: The config payload uses **PascalCase** field names (ConfigName, Questions, etc.) to match Unity's `JsonUtility.FromJson<SurveyConfig>()` which expects PascalCase. This differs from the web app's internal camelCase convention, but is necessary for Unity compatibility.
- **VALIDATE**: `curl -X POST /api/surveys/1/send-config-to-game -d '{"roomCode":"ABCDEF"}'` with auth header

### Task 6: Add REST endpoint for importing config from Unity
- **ACTION**: Add `POST /import-config` route in `web-app/src/routes/export.js`
- **IMPLEMENT**:
  ```javascript
  // POST /api/surveys/import-config — import a SurveyConfig from Unity format
  router.post('/import-config', requireAuth, (req, res) => {
      const { configName, configJson } = req.body;
      if (!configJson) {
          return res.status(400).json({ success: false, error: 'configJson is required' });
      }

      let config;
      try {
          config = JSON.parse(configJson);
      } catch {
          return res.status(400).json({ success: false, error: 'Invalid config JSON' });
      }

      const name = configName || config.ConfigName || 'Imported Config';
      const description = config.Description || '';
      const questions = config.Questions || [];
      const mappings = config.Mappings || [];
      const rules = config.Rules || [];

      const db = getDb();
      const shareCode = require('crypto').randomBytes(4).toString('hex').toUpperCase();

      const result = db.prepare(
          `INSERT INTO surveys (user_id, config_name, description, questions_json, mappings_json, rules_json, share_code)
           VALUES (?, ?, ?, ?, ?, ?, ?)`
      ).run(
          req.user.userId,
          name,
          description,
          JSON.stringify(questions),
          JSON.stringify(mappings),
          JSON.stringify(rules),
          shareCode
      );

      res.status(201).json({
          success: true,
          data: { id: result.lastInsertRowid, configName: name, shareCode }
      });
  });
  ```
- **MIRROR**: `POST /` (create survey) at lines 56-81 in surveys.js
- **IMPORTS**: Need to import `randomBytes` from 'crypto' or use the existing `generateShareCode` (but it's in surveys.js, not export.js). Inline `require('crypto')` or refactor to shared utility.
- **GOTCHA**: Unity sends PascalCase field names (ConfigName, Questions). The web app stores them in its own schema (config_name, questions_json). This endpoint must map between the two conventions.
- **VALIDATE**: `curl -X POST /api/surveys/import-config -d '{"configJson":"{...}"}'` with auth header

### Task 7: Add API client functions in web app
- **ACTION**: Add `sendConfigToGame()` and `importConfigFromGame()` to `web-app/client/src/api.js`
- **IMPLEMENT**:
  ```javascript
  export async function sendConfigToGame(id, roomCode) {
      return request(`/surveys/${id}/send-config-to-game`, {
          method: 'POST',
          body: JSON.stringify({ roomCode }),
      });
  }

  export async function importConfigFromGame(configData) {
      return request('/surveys/import-config', {
          method: 'POST',
          body: JSON.stringify(configData),
      });
  }
  ```
- **MIRROR**: `sendToGame()` at line 100-105 and `createSurvey()` at line 66-71
- **IMPORTS**: None (uses existing `request` wrapper)
- **GOTCHA**: None
- **VALIDATE**: Call functions from browser console, verify correct API paths

### Task 8: Create SendConfigModal component
- **ACTION**: Create `web-app/client/src/components/SendConfigModal.jsx` for sending raw config to Unity
- **IMPLEMENT**: A modal similar to `SendToGameModal.jsx` but calls `sendConfigToGame()` instead of `sendToGame()`. Shows room code input, room status badge, and send button. Success message says "Config sent! Unity can now load it as active config."
- **MIRROR**: `SendToGameModal.jsx` pattern — same modal overlay, room code input, debounced status check, send button
- **IMPORTS**: `useState, useEffect, useRef` from react; `sendConfigToGame, getRoomStatus` from api.js; `RoomStatusBadge` component
- **GOTCHA**: This modal is simpler than SendToGameModal — no need to check response count or save race results. Only needs room code + send.
- **VALIDATE**: Renders correctly, sends config when clicked, shows success/error

### Task 9: Add "Send Config to Game" button in EditorPage
- **ACTION**: Add a button next to the existing "Send to Game" button that opens SendConfigModal
- **IMPLEMENT**:
  ```jsx
  // Add state
  const [showConfigModal, setShowConfigModal] = useState(false);

  // Add button in header (after "Send to Game" button)
  <button onClick={() => setShowConfigModal(true)} className="btn-secondary">
    Send Config to Game
  </button>

  // Add modal at bottom
  {showConfigModal && <SendConfigModal surveyId={id} onClose={() => setShowConfigModal(false)} />}
  ```
- **MIRROR**: Existing `showSendModal` state and `SendToGameModal` rendering pattern at lines 25, 186, 263
- **IMPORTS**: Add `SendConfigModal` import
- **GOTCHA**: The "Send to Game" button sends processed CarData (requires responses). The "Send Config to Game" button sends raw config (no responses needed). The button should always be enabled regardless of response count.
- **VALIDATE**: Both buttons visible in editor, each opens correct modal

### Task 10: Handle `config_import` in Unity SetupScreen
- **ACTION**: Add `config_import` handler in `SetupScreen.OnNetworkMessage()`
- **IMPLEMENT**:
  ```csharp
  if (baseMsg.type == "config_import")
  {
      var msg = JsonUtility.FromJson<ConfigImportMessage>(json);
      if (string.IsNullOrEmpty(msg.configJson))
      {
          if (InfoText != null) InfoText.text = "Received empty config from web app.";
          return;
      }

      SurveyConfig config;
      try
      {
          config = JsonUtility.FromJson<SurveyConfig>(msg.configJson);
      }
      catch (System.Exception e)
      {
          if (InfoText != null) InfoText.text = $"Config import error: {e.Message}";
          return;
      }

      if (SurveyConfigManager != null)
      {
          SurveyConfigManager.SetActiveConfig(config);
          SurveyConfigManager.SaveConfig(config);
          RefreshActiveConfigDisplay();
      }

      int qCount = config.Questions != null ? config.Questions.Length : 0;
      int mCount = config.Mappings != null ? config.Mappings.Length : 0;
      int rCount = config.Rules != null ? config.Rules.Length : 0;

      Debug.Log($"[SetupScreen] Imported config from web app: {config.ConfigName} ({qCount}Q, {mCount}M, {rCount}R)");
      if (InfoText != null) InfoText.text = $"Config imported: {config.ConfigName} ({qCount} questions, {mCount} mappings, {rCount} rules)";

      // Show distribute button if in a room
      bool canDistribute = SurveyConfigManager != null && SurveyConfigManager.ActiveConfig != null
          && SurveyCollector != null && NetworkManager != null && NetworkManager.RoomCode != null;
      if (DistributeSurveyButton != null) DistributeSurveyButton.gameObject.SetActive(canDistribute);
      return;
  }
  ```
- **MIRROR**: Existing `survey_import` handler at lines 375-401
- **IMPORTS**: None (types already available)
- **GOTCHA**: Unlike `survey_import` which receives processed CarData, `config_import` receives raw SurveyConfig. Parse with `JsonUtility.FromJson<SurveyConfig>()`, not `JsonImporter.Parse()`. The config is saved locally AND set as active so the professor can immediately use it.
- **VALIDATE**: Send config_import message via WS, verify config appears as active in Unity UI

### Task 11: Add "Push Config to Web App" button in Unity SetupScreen
- **ACTION**: Add a new button and handler for exporting active config to the web app
- **IMPLEMENT**:
  ```csharp
  // In SetupScreen class, add new UI reference
  [Header("Config Sync (Optional)")]
  public Button PushConfigButton;

  // In Start(), add button listener
  if (PushConfigButton != null)
  {
      PushConfigButton.gameObject.SetActive(false);
      PushConfigButton.onClick.AddListener(OnPushConfig);
  }

  // In OnRoomCreated(), show button if config is active
  bool canPush = SurveyConfigManager != null && SurveyConfigManager.ActiveConfig != null;
  if (PushConfigButton != null) PushConfigButton.gameObject.SetActive(canPush);

  // New method
  private void OnPushConfig()
  {
      if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
      if (SurveyConfigManager == null || SurveyConfigManager.ActiveConfig == null)
      {
          if (InfoText != null) InfoText.text = "No active config to push.";
          return;
      }

      var config = SurveyConfigManager.ActiveConfig;
      string configJson = JsonUtility.ToJson(config);

      var msg = new ConfigExportMessage
      {
          configName = config.ConfigName,
          configJson = configJson
      };

      NetworkManager.Send(JsonUtility.ToJson(msg));
      if (InfoText != null) InfoText.text = $"Config '{config.ConfigName}' sent to web app.";
      Debug.Log($"[SetupScreen] Pushed config to web app: {config.ConfigName}");
  }
  ```
- **MIRROR**: `OnDistributeSurvey()` at lines 282-301 for button pattern
- **IMPORTS**: None
- **GOTCHA**: Button should only be visible when: (1) room is hosted, (2) active config exists, (3) network is connected. Also add handler for `config_sync_ack` to confirm the export was received.
- **VALIDATE**: Host room, load config, click push button — verify config appears in web app

### Task 12: Handle `config_export` reception in web app (WebSocket listener)
- **ACTION**: The web app needs to handle receiving `config_export` messages when connected to a room as a webapp client. This happens in the `SendConfigModal` or `SendToGameModal` when they create a temporary WebSocket connection, or could be a persistent listener in EditorPage.
- **IMPLEMENT**: When the web app receives a `config_export` message (Unity pushing config), it should auto-import:
  ```javascript
  // In SendToGameModal.jsx or EditorPage.jsx, when connected to WS:
  if (msg.type === 'config_export') {
      const result = await importConfigFromGame({
          configName: msg.configName,
          configJson: msg.configJson,
      });
      if (result.success) {
          // Show notification: "Config imported from Unity game"
      }
  }
  ```
- **MIRROR**: `SendToGameModal.jsx` WebSocket message handling pattern
- **IMPORTS**: `importConfigFromGame` from api.js
- **GOTCHA**: The simplest approach is to add a notification in the EditorPage when a config_export arrives via the room's WebSocket connection. Since the web app doesn't maintain a persistent WS connection in most views, the most practical UX is: the professor clicks "Push to Web" in Unity, and the config is cached in the room. When the web app professor opens the room-linked editor page (or SendToGameModal), the cached config is available for import. Alternatively, add the import as a manual trigger: "Import from Game" button that fetches the cached config via the server's HTTP API.
- **VALIDATE**: Push config from Unity, see notification in web app, verify survey created

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `config_export` handler | Professor sends config_export | Relayed to webapps, ack returned | No |
| `config_export` unauthorized | Student sends config_export | Error: "Not authorized" | Yes |
| `config_import` handler | Webapp sends config_import | Relayed to professor, ack returned | No |
| `config_import` no professor | Webapp sends with no professor connected | Error: "Professor not connected" | Yes |
| `import-config` endpoint | Valid configJson body | 201 with new survey ID | No |
| `import-config` invalid JSON | `configJson: "not json"` | 400 with error message | Yes |
| `import-config` empty config | `configJson: "{}"` | 201 with defaults | Yes |
| `send-config-to-game` endpoint | Valid survey ID + room code | 200 with success | No |
| `send-config-to-game` no room | Valid ID + invalid room code | 400 with "Room not found" | Yes |
| Unity config_import parse | Valid SurveyConfig JSON | Config set as active, saved locally | No |
| Unity config_import empty | Empty configJson | InfoText shows error | Yes |
| Unity push config | Active config exists | config_export sent via WS | No |
| Unity push config no config | No active config | InfoText shows "No active config" | Yes |

### Edge Cases Checklist
- [ ] Empty SurveyConfig (no questions, no mappings, no rules)
- [ ] Config with maximum questions (20), mappings (20), rules (9)
- [ ] Special characters in ConfigName (unicode, quotes, slashes)
- [ ] Config with LookupEntries containing empty keys/values
- [ ] Concurrent push/pull from both directions
- [ ] WebSocket disconnection during config transfer
- [ ] Room doesn't exist when trying to send config
- [ ] Professor not connected when web app tries to send config
- [ ] Duplicate config names on import (should create new survey, not conflict)

---

## Validation Commands

### Static Analysis
```bash
# TypeScript/JavaScript lint
cd web-app && npm run lint 2>/dev/null || echo "No lint script configured"
```
EXPECT: Zero lint errors in changed files

### Unit Tests
```bash
# Run web app tests if they exist
cd web-app && npm test 2>/dev/null || echo "No test script configured"
```
EXPECT: All tests pass (or no test script)

### Manual Validation — Unity -> Web
- [ ] Host room in Unity
- [ ] Load or create a survey config in Unity's SurveyBuilderPanel
- [ ] Click "Push Config to Web App"
- [ ] Verify config_sync_ack success message appears in Unity
- [ ] Open web app, verify new survey appears in dashboard with correct questions/mappings/rules
- [ ] Edit the imported survey in web app to confirm data integrity

### Manual Validation — Web -> Unity
- [ ] Create a survey config in web app with questions, mappings, and rules
- [ ] Host room in Unity, note room code
- [ ] In web app editor, click "Send Config to Game" and enter room code
- [ ] Verify config loads as active config in Unity
- [ ] Verify questions/mappings/rules match what was configured in web app
- [ ] Start race with the imported config to confirm rules work

### Manual Validation — Round Trip
- [ ] Create config in Unity -> Push to web -> Edit in web -> Send back to Unity
- [ ] Verify data survives the round trip without loss or corruption

---

## Acceptance Criteria
- [ ] All tasks completed
- [ ] Unity can push active SurveyConfig to web app via WebSocket
- [ ] Web app can send raw SurveyConfig (not just CarData) to Unity via WebSocket
- [ ] Imported configs preserve all fields: questions, mappings, rules, name, description
- [ ] Both directions show clear success/error feedback to the user
- [ ] Existing "Send to Game" (processed CarData) still works unchanged
- [ ] No type errors or build failures

## Completion Checklist
- [ ] Code follows discovered patterns (message types, REST endpoints, API client)
- [ ] Error handling matches codebase style (success/false envelope, InfoText messages)
- [ ] Logging follows codebase conventions ([ClassName] prefix, [Room CODE] prefix)
- [ ] No hardcoded values (URLs from env vars, limits from constants)
- [ ] No mutation (immutable state updates in React, new objects in C#)
- [ ] PascalCase used in config JSON for Unity compatibility
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| PascalCase/camelCase mismatch between Unity and web app | Medium | High | Explicitly document and test field name mapping. Unity uses PascalCase; web stores whatever is received. |
| JsonUtility limitations (no nested arrays-of-arrays) | Low | Medium | SurveyConfig structure is already proven to work with JsonUtility. LookupEntries are flat structs. |
| Race condition: push and pull simultaneously | Low | Low | Each operation is independent (creates new survey or sets active config). No shared mutable state. |
| WebSocket timeout during config transfer | Low | Medium | 5s timeout with clear error message. Config can be retried. |
| Large configs exceeding message size limits | Very Low | Low | Max 20 questions * ~500 bytes each = ~10KB. Well within WS frame limits. |

## Notes
- The data models are already highly compatible. The web app schema comments say "mirrors Unity SurveyConfig" and both store questions, mappings, rules in the same format. The main difference is field name casing (PascalCase in Unity, snake_case in DB columns, camelCase in JS).
- The `config_import` message is distinct from the existing `survey_import` message. `survey_import` sends **processed CarData** (team names + mapped attribute values + event rules — ready to start a race). `config_import` sends **raw SurveyConfig** (questions + mappings + rules — for editing and later processing). Both should coexist.
- The approach of caching `latestConfig` in the room object enables late-joining webapp clients to retrieve configs exported by Unity, even if they weren't connected when the export happened.
