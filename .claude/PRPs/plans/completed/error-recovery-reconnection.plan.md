# Plan: Error Recovery & WebSocket Reconnection (GAP 7)

## Summary
Add automatic WebSocket reconnection with exponential backoff to both Unity client and Node.js server, so that dropped connections during a race do not result in frozen cars or lost rooms. The server preserves room state during a configurable grace period, allowing professors and students to seamlessly rejoin after network interruptions.

## User Story
As a professor running a classroom race,
I want dropped WebSocket connections to automatically recover,
So that a brief network glitch does not destroy the room and force everyone to restart.

## Problem -> Solution
**Current state**: When a WebSocket connection drops (network blip, laptop sleep, browser tab swap), the server immediately deletes the room (professor) or removes the student. Students see frozen cars with no recovery path. The professor must re-host, students must re-join, and all race state is lost.

**Desired state**: When a connection drops, the client automatically attempts to reconnect with exponential backoff. The server preserves the room for a grace period. On reconnection, clients re-identify themselves and receive the latest cached state. A reconnection banner shows progress. If the grace period expires without reconnection, the room is cleaned up as before.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/plans/web-unity-gap-analysis-and-hosting.plan.md`
- **PRD Phase**: GAP 7 — Error Recovery & Reconnection
- **Estimated Files**: 7 files (3 modified, 4 new concepts within existing files)

---

## UX Design

### Before
```
┌──────────────────────────────────┐
│  Professor hosting race          │
│  Connection drops...             │
│                                  │
│  Server: room deleted            │
│  Students: frozen cars forever   │
│  Professor: "Disconnected."      │
│  No recovery. Must restart.      │
└──────────────────────────────────┘
```

### After
```
┌──────────────────────────────────┐
│  Professor hosting race          │
│  Connection drops...             │
│                                  │
│  ┌─────────────────────────┐     │
│  │ ⚠ Reconnecting (2/10)  │     │  ← overlay banner
│  │ Next attempt in 3s...   │     │
│  └─────────────────────────┘     │
│                                  │
│  Server: room suspended (60s)    │
│  Students: "Host reconnecting.." │
│  Connection restored!            │
│  Race continues seamlessly.      │
└──────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Professor disconnect | Room destroyed, students kicked | Room suspended for 60s grace period | Students see "Host reconnecting..." |
| Student disconnect | Student removed silently | Student auto-reconnects, receives latest state | Interpolation resumes |
| WebSocket error | Error event logged, no action | Triggers reconnection state machine | Exponential backoff 1s-30s |
| Network restored | Manual re-host + re-join required | Automatic rejoin with cached `sessionId` + `roomCode` | Transparent to user |
| Grace period expires | N/A (instant delete) | Room deleted, students get `room_closed` | Same as current behavior after timeout |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Server/server.js` | 1-275 | Server room lifecycle, heartbeat, cleanup logic |
| P0 (critical) | `Assets/Scripts/Network/NetworkManager.cs` | 1-200 | Client connection lifecycle, events, message routing |
| P0 (critical) | `Assets/Plugins/WebGL/WebSocketBridge.cs` | 1-170 | Cross-platform WebSocket wrapper (WebGL + Editor) |
| P1 (important) | `Assets/Plugins/WebGL/WebSocketBridge.jslib` | 1-76 | JavaScript WebSocket bridge for WebGL builds |
| P1 (important) | `Assets/Scripts/Network/NetworkSync.cs` | 1-365 | State sync — handles `room_closed`, late-joiner state |
| P1 (important) | `Assets/Scripts/Network/NetworkMessages.cs` | 1-260 | All message types — must add reconnection messages |
| P2 (reference) | `Assets/Scripts/UI/JoinScreen.cs` | 1-93 | Student UI — shows disconnect status |
| P2 (reference) | `Assets/Scripts/UI/SetupScreen.cs` | 1-420 | Professor UI — shows network error |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| WebSocket reconnection patterns | Industry standard | Exponential backoff with jitter prevents thundering herd |
| Unity WebGL limitations | Unity docs | WebGL runs on browser's main thread; no System.Threading.Timer; use coroutines |
| `ws` library ping/pong | npm ws docs | Server already uses `ws.ping()` heartbeat at 30s intervals |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:27-33
public event Action OnConnected;
public event Action OnDisconnected;
public event Action<string> OnRoomCreated;
public event Action<string> OnConnectionError;
// Pattern: PascalCase events with On prefix, Action delegate
```

### ERROR_HANDLING
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:156-160
private void HandleError(string error)
{
    Debug.LogWarning($"[NetworkManager] Error: {error}");
    OnConnectionError?.Invoke(error);
}
// Pattern: Debug.LogWarning for non-fatal errors, invoke event for UI
```

### LOGGING_PATTERN
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:70,138
Debug.Log($"[NetworkManager] Connecting to {url}...");
Debug.Log("[NetworkManager] Connected");
// Pattern: [ClassName] prefix, interpolated strings
```

### SERVER_MESSAGE_PATTERN
```javascript
// SOURCE: Server/server.js:23-27,29-38
function sendJSON(ws, obj) {
  if (ws.readyState === 1) {
    ws.send(JSON.stringify(obj));
  }
}
function broadcastToStudents(roomCode, message) {
  const room = rooms.get(roomCode);
  if (!room) return;
  const data = typeof message === 'string' ? message : JSON.stringify(message);
  for (const student of room.students) {
    if (student.readyState === 1) student.send(data);
  }
}
// Pattern: Guard readyState === 1 before sending
```

### SERVER_SWITCH_PATTERN
```javascript
// SOURCE: Server/server.js:144-265
switch (msg.type) {
  case 'create_room': { ... break; }
  case 'join_room': { ... break; }
  default: { ... break; }
}
// Pattern: Block-scoped cases with braces
```

### MESSAGE_TYPE_PATTERN
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:9-13,17-21
[Serializable]
public class NetworkMessage
{
    public string type;
}
[Serializable]
public class CreateRoomMessage
{
    public string type = "create_room";
}
// Pattern: [Serializable] class, `type` field with default, JsonUtility
```

### TEST_STRUCTURE
No automated test suite exists for the WebSocket server or Unity networking code. Tests would be manual validation.

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Server/server.js` | UPDATE | Add grace period, session ID tracking, `rejoin_room` handler, `host_reconnecting` broadcast |
| `Assets/Scripts/Network/NetworkManager.cs` | UPDATE | Add reconnection state machine with coroutine-based exponential backoff |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add `RejoinRoomMessage`, `HostReconnectingMessage`, `ReconnectStateMessage` types |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Handle `host_reconnecting`, `host_reconnected` messages; reset interpolation on reconnect |
| `Assets/Plugins/WebGL/WebSocketBridge.cs` | UPDATE | Add `OnClose` close code parameter for distinguishing clean vs dirty disconnect |
| `Assets/Plugins/WebGL/WebSocketBridge.jslib` | UPDATE | Pass close code to Unity in `onclose` handler (already does this: `evt.code.toString()`) |
| `Assets/Scripts/UI/JoinScreen.cs` | UPDATE | Show reconnection banner during auto-reconnect |

## NOT Building

- Persistent room storage across server restarts (GAP 6 scope)
- Student identity linking (GAP 5 scope)
- Web-app WebSocket reconnection (web-app uses short-lived REST-initiated connections)
- Custom reconnection UI panel/prefab (use existing `StatusText` / `InfoText`)
- Server-side session persistence to database
- Multi-server room migration

---

## Step-by-Step Tasks

### Task 1: Add Session ID and Grace Period to Server

- **ACTION**: Modify `Server/server.js` to support session-based reconnection
- **IMPLEMENT**:
  1. Add `sessionId` tracking: each client sends a `sessionId` on `create_room` or `join_room`. Server stores `sessionId -> { roomCode, role }` in a new `sessions` Map.
  2. Add grace period to `cleanupClient()`: when professor disconnects, don't delete room immediately. Set `room.professorSuspended = true` and `room.suspendedAt = Date.now()`. Broadcast `{ type: 'host_reconnecting' }` to students. Set a timeout (60s) that calls actual room deletion.
  3. Add `rejoin_room` message handler: client sends `{ type: 'rejoin_room', roomCode, sessionId }`. Server checks if the room exists and the sessionId matches. If valid, reassign the WebSocket to the room role, cancel grace timer, broadcast `{ type: 'host_reconnected' }` to students. Send cached state back to the reconnected client via `{ type: 'reconnect_state', gamePhase, latestState, studentCount, raceStarted }`.
  4. For student reconnection: on `rejoin_room`, re-add student to room, send `latestState` if available.
  5. Update heartbeat cleanup: when heartbeat kills a dead connection, trigger the same grace period logic.
- **MIRROR**: SERVER_SWITCH_PATTERN, SERVER_MESSAGE_PATTERN
- **IMPORTS**: None (uses built-in `setTimeout`, `Map`)
- **GOTCHA**: 
  - Must clear the grace timer if professor reconnects before it fires
  - Must handle the case where professor reconnects but room was already deleted (race condition)
  - `clientRooms` Map must be updated on reconnect to point the new WebSocket to the correct room
  - Grace period timer must be stored on the room object so it can be cancelled
- **VALIDATE**: 
  - Start server, create room, disconnect professor → room survives for 60s
  - Reconnect within 60s with same sessionId → professor regains control
  - Wait 60s without reconnecting → room is deleted, students get `room_closed`
  - Student reconnects → receives `latestState`, car positions resume

### Task 2: Add Reconnection Messages to NetworkMessages.cs

- **ACTION**: Add new message types for reconnection protocol
- **IMPLEMENT**:
  ```csharp
  [Serializable]
  public class RejoinRoomMessage
  {
      public string type = "rejoin_room";
      public string roomCode;
      public string sessionId;
  }

  [Serializable]
  public class ReconnectStateMessage
  {
      public string type = "reconnect_state";
      public string gamePhase;
      public int studentCount;
      public bool raceStarted;
  }

  [Serializable]
  public class HostReconnectingMessage
  {
      public string type = "host_reconnecting";
  }

  [Serializable]
  public class HostReconnectedMessage
  {
      public string type = "host_reconnected";
  }
  ```
- **MIRROR**: MESSAGE_TYPE_PATTERN
- **IMPORTS**: `System` (already imported)
- **GOTCHA**: JsonUtility requires all fields to be public and serializable. No properties.
- **VALIDATE**: Messages serialize/deserialize correctly with `JsonUtility.ToJson/FromJson`

### Task 3: Add Reconnection State Machine to NetworkManager.cs

- **ACTION**: Add automatic reconnection with exponential backoff using coroutines
- **IMPLEMENT**:
  1. Add reconnection configuration fields:
     ```csharp
     [Header("Reconnection")]
     [Tooltip("Enable automatic reconnection on disconnect")]
     public bool AutoReconnect = true;
     public float InitialDelay = 1f;
     public float MaxDelay = 30f;
     public float BackoffMultiplier = 2f;
     public int MaxAttempts = 10;
     public float GracePeriod = 60f; // must match server
     ```
  2. Add reconnection state:
     ```csharp
     public bool IsReconnecting { get; private set; }
     public int ReconnectAttempt { get; private set; }
     public event Action<int, float> OnReconnecting; // attempt, nextDelay
     public event Action OnReconnected;
     public event Action OnReconnectFailed;
     private string sessionId;
     private string lastRoomCode;
     private bool wasHost;
     private string lastServerUrl;
     private Coroutine reconnectCoroutine;
     ```
  3. Generate `sessionId` on first connect: `sessionId = System.Guid.NewGuid().ToString("N").Substring(0, 12);`
  4. Store `lastRoomCode`, `wasHost`, `lastServerUrl` before disconnect.
  5. Modify `HandleClose()`: instead of just firing `OnDisconnected`, check if reconnection should be attempted (must have a `lastRoomCode` and `AutoReconnect` enabled). If so, start reconnection coroutine.
  6. Reconnection coroutine:
     ```csharp
     private IEnumerator ReconnectCoroutine()
     {
         IsReconnecting = true;
         float delay = InitialDelay;
         for (int attempt = 1; attempt <= MaxAttempts; attempt++)
         {
             ReconnectAttempt = attempt;
             float jitteredDelay = delay * UnityEngine.Random.Range(0.75f, 1.25f);
             OnReconnecting?.Invoke(attempt, jitteredDelay);
             Debug.Log($"[NetworkManager] Reconnect attempt {attempt}/{MaxAttempts} in {jitteredDelay:F1}s");
             yield return new WaitForSeconds(jitteredDelay);
             
             // Attempt connection
             bridge.Connect(lastServerUrl);
             
             // Wait for connection result (up to 5s)
             float timeout = 5f;
             while (timeout > 0 && !bridge.IsConnected)
             {
                 timeout -= Time.deltaTime;
                 yield return null;
             }
             
             if (bridge.IsConnected)
             {
                 // Send rejoin
                 var msg = new RejoinRoomMessage { roomCode = lastRoomCode, sessionId = sessionId };
                 Send(JsonUtility.ToJson(msg));
                 IsReconnecting = false;
                 ReconnectAttempt = 0;
                 OnReconnected?.Invoke();
                 yield break;
             }
             
             delay = Mathf.Min(delay * BackoffMultiplier, MaxDelay);
         }
         
         // All attempts failed
         IsReconnecting = false;
         ReconnectAttempt = 0;
         OnReconnectFailed?.Invoke();
         OnDisconnected?.Invoke();
     }
     ```
  7. Modify `CreateRoom()` and `JoinRoom()` to include `sessionId` in messages and store `lastServerUrl`.
  8. Add `CancelReconnect()` method for manual disconnect or UI cancel.
  9. Handle `reconnect_state` message in `HandleMessage()`: restore `IsHost`, `RoomCode`, `StudentCount` from message, fire `OnReconnected`.
- **MIRROR**: NAMING_CONVENTION, LOGGING_PATTERN, ERROR_HANDLING
- **IMPORTS**: `System.Collections` (for IEnumerator — already available via MonoBehaviour)
- **GOTCHA**:
  - Must use `Coroutine` not `Task` for WebGL compatibility (no threads)
  - Must cancel coroutine in `OnDestroy()` and on manual `Disconnect()`
  - `HandleClose()` fires on main thread (WebGL via jslib SendMessage, Editor via mainThreadActions queue) — safe to start coroutine
  - Must NOT fire `OnDisconnected` during reconnection attempts — only on final failure
  - Generate `sessionId` once per NetworkManager lifetime, not per connection
- **VALIDATE**:
  - Kill WS server while connected → client shows "Reconnecting..." log
  - Restart WS server within 30s → client reconnects, room resumes
  - Keep server down for all 10 attempts → `OnReconnectFailed` fires, `OnDisconnected` fires

### Task 4: Handle Reconnection in NetworkSync.cs

- **ACTION**: Handle `host_reconnecting` and `host_reconnected` messages on student side; reset interpolation state on reconnect
- **IMPLEMENT**:
  1. Add cases to `HandleGameMessage()`:
     ```csharp
     case "host_reconnecting":
         HandleHostReconnecting();
         break;
     case "host_reconnected":
         HandleHostReconnected();
         break;
     ```
  2. `HandleHostReconnecting()`: log message, optionally freeze interpolation (set a `hostSuspended` flag that pauses `UpdateStudent()`)
  3. `HandleHostReconnected()`: clear `hostSuspended` flag, resume interpolation
  4. Subscribe to `NetworkManager.OnReconnected` in `Start()`: on reconnect, if student, clear and re-initialize interpolation arrays (set `remoteCars = null` to let next `race_start` or `state_update` reinitialize them)
- **MIRROR**: LOGGING_PATTERN, existing `HandleGameMessage` switch pattern
- **IMPORTS**: None
- **GOTCHA**: 
  - After professor reconnects, the next `state_update` will resume car movement naturally
  - Don't destroy `remoteCars` GameObjects — just clear the interpolation targets
  - Professor side: on reconnect, `UpdateHost()` will naturally resume broadcasting since `RaceManager.CurrentState` is still `Racing`
- **VALIDATE**:
  - Student connected during race → professor disconnects → student sees "Host reconnecting" log
  - Professor reconnects → student receives `host_reconnected` → cars start moving again

### Task 5: Update JoinScreen.cs for Reconnection UI

- **ACTION**: Show reconnection status in student UI
- **IMPLEMENT**:
  1. Subscribe to `NetworkManager.OnReconnecting` and `NetworkManager.OnReconnectFailed` in `OnEnable()`, unsubscribe in `OnDisable()`
  2. On `OnReconnecting(attempt, delay)`: show status text "Reconnecting... ({attempt}/{max})" and disable join button
  3. On `OnReconnected`: hide the reconnection status (JoinScreen may be hidden since student is in-race — this handles the case where student is still on join screen)
  4. On `OnReconnectFailed`: show "Connection lost. Please re-enter room code." and re-enable join button
- **MIRROR**: existing event subscription pattern in `OnEnable/OnDisable`
- **IMPORTS**: None
- **GOTCHA**: 
  - JoinScreen might be `SetActive(false)` during race — `OnDisable` will unsubscribe. That's fine — NetworkSync handles in-race reconnection.
  - Must handle the case where reconnect fires while JoinScreen is disabled
- **VALIDATE**:
  - Student on join screen → connection drops → status shows "Reconnecting..."
  - Connection restored → status clears

### Task 6: Update SetupScreen.cs for Professor Reconnection UI

- **ACTION**: Show reconnection status for professor
- **IMPLEMENT**:
  1. Subscribe to `NetworkManager.OnReconnecting`, `OnReconnected`, `OnReconnectFailed` in `OnEnable()`, unsubscribe in `OnDisable()`
  2. On `OnReconnecting`: show `InfoText = "Reconnecting... (attempt {n}/{max})"`, disable HostButton
  3. On `OnReconnected`: show `InfoText = "Reconnected! Room restored."`, update room code and student count from `reconnect_state`
  4. On `OnReconnectFailed`: show `InfoText = "Connection lost. Room may have expired."`, re-enable HostButton
- **MIRROR**: existing `OnNetworkError` pattern
- **IMPORTS**: None
- **GOTCHA**: SetupScreen may be `SetActive(false)` during racing — same as JoinScreen. The reconnection logic in NetworkManager operates independently of UI.
- **VALIDATE**: Professor hosting → disconnect → SetupScreen shows reconnecting status

### Task 7: Pass Session ID in Create/Join Messages

- **ACTION**: Extend existing `CreateRoomMessage` and `JoinRoomMessage` to include `sessionId`
- **IMPLEMENT**:
  1. In `NetworkMessages.cs`, add `public string sessionId;` to `CreateRoomMessage` and `JoinRoomMessage`
  2. In `NetworkManager.CreateRoom()`, set `sessionId` on the message before sending
  3. In `NetworkManager.JoinRoom()`, set `sessionId` on the message before sending
  4. In `Server/server.js`, extract `msg.sessionId` from `create_room` and `join_room` handlers, store in `sessions` Map and on the room object
- **MIRROR**: MESSAGE_TYPE_PATTERN
- **IMPORTS**: None
- **GOTCHA**: 
  - Existing clients without `sessionId` should still work (server treats missing sessionId as no-reconnect)
  - Server must handle `sessionId === undefined` gracefully
- **VALIDATE**: Send `create_room` with sessionId → server logs session tracking

---

## Testing Strategy

### Unit Tests

No automated test framework exists for this project. Validation is manual.

### Manual Test Cases

| Test | Steps | Expected Outcome | Edge Case? |
|---|---|---|---|
| Professor reconnect during setup | Host room → kill WS server → restart server within 30s | Professor reconnects, room code preserved | No |
| Professor reconnect during race | Start race → kill WS server → restart within 30s | Race resumes, students see cars move again | No |
| Professor grace period expires | Host room → kill WS server → wait 60s+ → restart | Room deleted, students get `room_closed` | Yes |
| Student reconnect | Student joins → kill student network → restore within 30s | Student reconnects, receives latest state | No |
| Multiple students reconnect | 3 students joined → kill WS server → restart | All 3 reconnect, counts correct | Yes |
| Reconnect max attempts exhausted | Kill WS server permanently | After 10 attempts (~2.5 min), `OnReconnectFailed` fires | Yes |
| Manual disconnect during reconnect | Trigger reconnect → user clicks "Leave" | Reconnection cancelled, clean state | Yes |
| Clean disconnect (user action) | User clicks Disconnect/Leave | No reconnection attempted | No |
| Browser tab visibility (WebGL) | Start race → switch tab → switch back | Connection may drop and reconnect | Yes |
| Concurrent professor+student reconnect | Both disconnect → both reconnect | Professor first restores room, then students rejoin | Yes |

### Edge Cases Checklist
- [x] Professor disconnects — room suspended, not deleted
- [x] Grace period expires — room deleted normally
- [x] Student disconnects during setup (no race state) — reconnects, no state to restore
- [x] Student disconnects during race — reconnects, receives `latestState`
- [x] Both professor and students disconnect — professor must reconnect first
- [x] Server restart (all connections drop) — rooms are lost (in-memory), all clients fail reconnect
- [x] WebSocket error (not close) — should trigger same reconnection path
- [x] `sessionId` collision — effectively impossible with 12-char hex (16^12 combinations)
- [x] Rapid connect/disconnect — debounce via coroutine state machine

---

## Validation Commands

### Server
```bash
cd Server && node server.js
```
EXPECT: Server starts on port 8080, logs "WebSocket + HTTP server listening"

### Unity Editor Test
```
1. Open Unity, enter Play mode
2. Click Host Room
3. Kill server process (Ctrl+C)
4. Observe "[NetworkManager] Reconnect attempt 1/10 in ~1.0s" in Console
5. Restart server
6. Observe "[NetworkManager] Connected" and room restored
```
EXPECT: Reconnection succeeds, room code preserved

### WebGL Build Test
```
1. Build WebGL and deploy via Docker
2. Open game in browser, host room
3. Stop Docker container for WS server only
4. Observe reconnection attempts in browser console
5. Restart container
6. Observe reconnection
```
EXPECT: Same behavior as Editor test

### Manual Validation
- [ ] Professor disconnect + reconnect during Setup phase
- [ ] Professor disconnect + reconnect during Racing phase
- [ ] Student disconnect + reconnect during Racing phase
- [ ] Grace period expiration (wait >60s)
- [ ] All reconnect attempts exhausted (server stays down)
- [ ] Manual disconnect (no reconnection attempted)
- [ ] Multiple students reconnecting simultaneously
- [ ] Reconnect state message restores correct gamePhase and studentCount

---

## Acceptance Criteria
- [ ] All tasks completed
- [ ] Professor disconnect triggers 60s grace period (room not deleted)
- [ ] Students receive `host_reconnecting` / `host_reconnected` messages
- [ ] Client auto-reconnects with exponential backoff (1s → 30s, 10 attempts max)
- [ ] Reconnected clients receive latest cached state
- [ ] Clean manual disconnect does NOT trigger auto-reconnect
- [ ] Grace period expiration deletes room and notifies students normally
- [ ] No regressions: existing create/join/disconnect flow works unchanged
- [ ] UI shows reconnection progress (attempt count)

## Completion Checklist
- [ ] Code follows discovered patterns (PascalCase events, `[ClassName]` logs, `sendJSON` guard)
- [ ] Error handling matches codebase style (`Debug.LogWarning` + event invoke)
- [ ] Logging follows `[NetworkManager]` prefix convention
- [ ] No hardcoded values (all reconnection params are configurable fields)
- [ ] No unnecessary scope additions (no persistent storage, no web-app reconnection)
- [ ] Backward compatible: missing `sessionId` handled gracefully on server
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Server restart loses all rooms (in-memory) | Medium | High | Document limitation; GAP 6 (persistence) addresses this |
| Professor reconnects but students already left | Low | Medium | Students auto-reconnect independently; student count updates on rejoin |
| Coroutine not running in WebGL after tab switch | Low | Medium | WebGL `onclose` fires when tab loses focus; `OnEnable` resumes coroutine |
| Race condition: grace timer fires during reconnect handshake | Low | High | Server cancels timer before processing rejoin; atomic check-and-cancel |
| Thundering herd: many students reconnect simultaneously | Low | Low | Jitter (±25%) staggers reconnection attempts |

## Notes
- The server already has a heartbeat mechanism (30s ping/pong) at `Server/server.js:117-128`. The reconnection system works alongside it: heartbeat detects dead connections, which then trigger the grace period instead of immediate cleanup.
- `latestState` is already cached on the room object (`Server/server.js:7,181,237`), so reconnected students receive current car positions without any additional server work.
- `surveyData` is also cached (`Server/server.js:239,177-179`), so reconnected students in Setup phase also receive the survey.
- The web-app `SendToGameModal` uses short-lived REST-initiated WebSocket connections (`export.js:156`). These don't need reconnection — they complete in <5s.
- The `reconnect_state` message is separate from `room_joined` to distinguish initial join from reconnection. This lets the client skip animations or re-initialization that only applies to first join.
