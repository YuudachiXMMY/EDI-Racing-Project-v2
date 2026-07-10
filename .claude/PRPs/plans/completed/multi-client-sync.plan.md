# Plan: Multi-Client WebSocket Sync

## Summary
Implement real-time multi-client synchronization so that multiple browsers see the same race simultaneously. A Node.js WebSocket server manages rooms and relays professor state. The professor client runs the full simulation and broadcasts car positions, events, and leaderboard data to student clients, which render a synchronized view.

## User Story
As a professor, I want students to view the race on their own devices in real-time, so that every student can engage with the EDI demonstration from their seat instead of only watching a projected screen.

As a student, I want to join a race session via room code or URL, so that I can watch the race and leaderboard on my own browser.

## Problem -> Solution
Currently the game runs single-client only; students must watch the professor's projected screen -> Professor's browser acts as the simulation authority and broadcasts state to all connected student browsers via WebSocket, enabling real-time synchronized viewing.

## Metadata
- **Complexity**: Large
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 5 — Multi-Client Sync
- **Estimated Files**: 9 new files (4 C# Unity, 1 jslib, 1 jslib meta, 2 Node.js server, 1 UI) + 4 updated files

---

## UX Design

### Before
```
Professor: Opens browser -> sets up race -> starts race -> only this browser sees anything
Students: Watch projected screen or have no access
```

### After
```
Professor: Opens browser -> clicks "Host Race" -> gets room code "ABC123"
           -> sets up race -> starts race
           -> professor browser runs full simulation + broadcasts state

Student:   Opens same URL -> enters room code "ABC123" -> clicks "Join"
           -> sees synchronized race with spectator camera + leaderboard
           -> no controls (view-only)
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| SetupScreen | "Start Race" / "Load Session" only | Adds "Host Race" button (professor) | Creates room, shows room code |
| Student entry | N/A | New JoinScreen: room code input + "Join" button | Replaces SetupScreen for student role |
| During race | Single-client only | Students see synced car positions, leaderboard, events | ~10Hz position updates |
| Role detection | Manual `RaceUI.Role` setting | Auto-detected: host=Professor, join=Student | Based on how user entered |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/Race/RaceManager.cs` | all | Central orchestrator — network hooks attach here |
| P0 (critical) | `Assets/Scripts/UI/RaceUI.cs` | all | Role-based UI — needs JoinScreen + network status |
| P0 (critical) | `Assets/Scripts/Car/CarIdentity.cs` | all | Car state to broadcast (position, rotation, lap, checkpoint) |
| P0 (critical) | `Assets/Scripts/Car/CarController.cs` | all | Must be disabled on student side — cars are position-synced |
| P1 (important) | `Assets/Scripts/Race/CarSpawner.cs` | all | Student needs visual-only spawn (no NavMeshAgent) |
| P1 (important) | `Assets/Scripts/UI/SetupScreen.cs` | all | Add "Host Race" button |
| P1 (important) | `Assets/Scripts/Events/EventManager.cs` | all | Event triggers must be broadcast to students |
| P1 (important) | `Assets/Scripts/Data/SessionData.cs` | all | Serializable data structures (reuse for network messages) |
| P2 (reference) | `Assets/Scripts/Data/CarData.cs` | all | Car data model sent to students |
| P2 (reference) | `Assets/Scripts/Race/ScoreManager.cs` | all | Leaderboard data to broadcast |
| P2 (reference) | `Assets/Scripts/UI/GameState.cs` | all | State enum broadcast to students |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity WebGL WebSocket | Unity docs: WebGL interacting with browser scripting | Must use `.jslib` plugin for WebSocket in WebGL; `System.Net.WebSockets` not available |
| Node.js `ws` package | npmjs.com/package/ws | Lightweight WebSocket server; `ws.Server` handles connections, rooms are a Map of Sets |
| Unity JsonUtility | Unity docs | Cannot serialize `Dictionary` or `List<T>` at top level; use wrapper classes with arrays |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:1-12
// MonoBehaviour classes: PascalCase, no namespaces, [Header] attributes for Inspector groups
public class RaceManager : MonoBehaviour
{
    [Header("References")]
    public CarSpawner CarSpawner;
```

### EVENT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:36-42
// C# Action<T> delegates, not UnityEvents
public event Action<GameState> OnStateChanged;
private void SetState(GameState state)
{
    CurrentState = state;
    OnStateChanged?.Invoke(state);
}
```

### DATA_STRUCT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Data/SessionData.cs:10-22
// [Serializable] classes/structs for JSON persistence via JsonUtility
[Serializable]
public class SessionData
{
    public string SessionName = "";
    public CarData[] Cars = Array.Empty<CarData>();
}
```

### COMPONENT_INITIALIZATION
```csharp
// SOURCE: Assets/Scripts/Car/CarController.cs:41-71
// Initialize() method pattern instead of Awake/Start for controlled setup
public void Initialize(WaypointPath path, float speed, ...)
{
    agent = GetComponent<NavMeshAgent>();
    // ... set fields ...
}
```

### UI_PANEL_PATTERN
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:1-65
// UI panels reference RaceManager, hook button listeners in Start(), gameObject.SetActive() for visibility
public class SetupScreen : MonoBehaviour
{
    [Header("References")]
    public RaceManager RaceManager;
    [Header("UI Elements")]
    public Button StartDefaultButton;
    public Text InfoText;
}
```

### ROLE_BASED_UI
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:8-9,39-54
// UserRole enum controls panel visibility and camera mode
public enum UserRole { Professor, Student }
private void ApplyRole()
{
    bool isProfessor = Role == UserRole.Professor;
    if (Events != null) Events.gameObject.SetActive(isProfessor);
}
```

---

## Architecture

### Sync Strategy: Professor-Authoritative Broadcast

```
┌─────────────────┐         ┌──────────────────┐         ┌─────────────────┐
│ Professor Client │ ──WS──> │ Node.js WS Server │ ──WS──> │ Student Client 1 │
│ (full simulation)│         │ (relay + rooms)   │         │ (visual only)    │
│                  │         │                   │ ──WS──> │ Student Client 2 │
│ Runs: NavMesh,   │         │ Manages:          │         │ ...              │
│ Events, Scoring  │         │ - Room codes      │         │ Student Client N │
│                  │         │ - Prof/Student    │         │                  │
│ Sends: positions,│         │   role tracking   │         │ Receives: car    │
│ events, rankings │         │ - Message relay   │         │ positions, events│
│ at 10Hz          │         │   prof → students │         │ rankings         │
└─────────────────┘         └──────────────────┘         └─────────────────┘
```

**Why NOT deterministic simulation**: `CarController` uses `Random.Range` for avoidance priority (line 62), stuck recovery direction (line 171), and lateral waypoint offset (line 205). NavMeshAgent behavior is frame-rate dependent. Achieving cross-client determinism would require fixed timestep, seeded random, and NavMesh reproducibility — fragile and hard to validate.

**Why professor-authoritative**: Professor runs the full simulation unchanged. Students receive position/rotation snapshots at 10Hz (~1.5KB per update for 50 cars) and interpolate. Simple, reliable, and bandwidth is trivial for a classroom.

### Message Protocol (JSON over WebSocket)

```
Direction: C = Client, S = Server, P = Professor, St = Student

P→S   {"type":"create_room"}
S→P   {"type":"room_created","roomCode":"ABC123"}

St→S  {"type":"join_room","roomCode":"ABC123"}
S→St  {"type":"room_joined","roomCode":"ABC123"}
S→P   {"type":"student_count","count":5}

P→S→St  {"type":"race_start","cars":[{"teamName":"...","colorIndex":0,"functions":["..."]},...]}
P→S→St  {"type":"game_state","state":"Racing"}
P→S→St  {"type":"state_update","t":12.5,"cars":[{"i":0,"px":1.0,"py":0.5,"pz":2.0,"ry":90.0,"l":1,"c":3},...]}
P→S→St  {"type":"event_triggered","index":0,"name":"Snow Weather","affected":5,"total":30}
P→S→St  {"type":"leaderboard","rankings":[{"rank":1,"name":"Team1","lap":2},...]}
P→S→St  {"type":"race_end"}
```

### Bandwidth Estimate
- 50 cars × 28 bytes (i:2, px/py/pz:12, ry:4, l:2, c:2 + JSON overhead ~6) ≈ ~1.7KB per state_update
- At 10Hz = 17KB/s per student — trivially low
- Leaderboard at 2Hz ≈ 1KB/s
- Events are sporadic (7 total per race)

### Alternatives Considered

| Approach | Pros | Cons | Verdict |
|---|---|---|---|
| Deterministic sim (all clients run NavMesh) | Low bandwidth; no position sync needed | Random calls + frame-rate variance = desync; extremely hard to debug | Rejected |
| Full state streaming (positions at 30Hz) | Perfect fidelity | 3x bandwidth; unnecessary for classroom | Rejected |
| Professor-auth at 10Hz with interpolation | Reliable; simple; testable; low bandwidth | Slight visual lag (~100ms) | **Selected** |

### Scope
- Node.js WebSocket server with room management
- Unity WebSocket client (jslib for WebGL, ClientWebSocket for Editor)
- NetworkManager + NetworkSync MonoBehaviours
- Room creation/join UI flow
- Professor broadcasts car state at 10Hz
- Student receives and interpolates car positions
- Events and leaderboard broadcast
- Student car spawning (visual-only, no NavMeshAgent)

### NOT Building
- Student survey submission via WebSocket (Phase 3 already has CSV import; in-game survey is a future "Should" feature)
- Chat or messaging between clients
- Student camera controls (students get auto-follow spectator only, per existing RaceUI logic)
- Reconnection recovery (if student disconnects, they refresh and rejoin)
- Authentication or user accounts (anonymous by design per PRD)
- Docker deployment (that's Phase 6)
- Load balancing or multiple server instances (single classroom use case)
- WebRTC or peer-to-peer (WebSocket relay is simpler and sufficient)

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Server/package.json` | CREATE | Node.js server dependencies (`ws` package) |
| `Server/server.js` | CREATE | WebSocket server: room management, message relay |
| `Assets/Plugins/WebGL/WebSocketBridge.jslib` | CREATE | JavaScript WebSocket bridge for WebGL builds |
| `Assets/Plugins/WebGL/WebSocketBridge.cs` | CREATE | C# wrapper: dual-mode (jslib for WebGL, ClientWebSocket for Editor) |
| `Assets/Scripts/Network/NetworkMessages.cs` | CREATE | [Serializable] message types for JSON serialization |
| `Assets/Scripts/Network/NetworkManager.cs` | CREATE | Connection lifecycle, send/receive, message routing |
| `Assets/Scripts/Network/NetworkSync.cs` | CREATE | Professor: broadcasts state at 10Hz. Student: receives + interpolates cars |
| `Assets/Scripts/UI/JoinScreen.cs` | CREATE | Student room-code entry UI panel |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Add network hooks: broadcast state changes, accept remote commands |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | Add JoinScreen panel reference, auto-detect role from network |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Add "Host Race" button, show room code |
| `Assets/Scripts/Race/CarSpawner.cs` | UPDATE | Add `SpawnVisualCars()` for student-side (no NavMeshAgent/CarController) |

---

## Step-by-Step Tasks

### Task 1: Create Node.js WebSocket Server
- **ACTION**: Create `Server/package.json` and `Server/server.js`
- **IMPLEMENT**: 
  - Simple WebSocket server using `ws` package on configurable port (default 8080)
  - Room management: `Map<roomCode, {professor: WebSocket, students: Set<WebSocket>}>`
  - Room code generation: 6-character uppercase alphanumeric
  - Message routing: professor messages are broadcast to all students in the room
  - Handle: `create_room`, `join_room`, professor broadcast relay
  - Heartbeat/ping-pong for connection health
  - Graceful cleanup when professor disconnects (notify students)
- **MIRROR**: Standard Node.js patterns; no Unity patterns apply
- **IMPORTS**: `ws` package only
- **GOTCHA**: Must handle room cleanup when professor disconnects. Students should get a `room_closed` message.
- **GOTCHA**: Room codes must be unique. Use a `Set` to track active codes.
- **VALIDATE**: `node server.js` starts without errors; manual WebSocket test with `wscat`

### Task 2: Create WebSocket Bridge (jslib + C# wrapper)
- **ACTION**: Create `Assets/Plugins/WebGL/WebSocketBridge.jslib` and `Assets/Plugins/WebGL/WebSocketBridge.cs`
- **IMPLEMENT**:
  - `.jslib`: JavaScript functions `WebSocketConnect(url)`, `WebSocketSend(msg)`, `WebSocketClose()`, `WebSocketGetState()` that call `SendMessage()` back to Unity for callbacks (`OnOpen`, `OnMessage`, `OnClose`, `OnError`)
  - `.cs`: C# wrapper class `WebSocketBridge` with:
    - `#if UNITY_WEBGL && !UNITY_EDITOR` block using `[DllImport("__Internal")]` for jslib functions
    - `#else` block using `System.Net.WebSockets.ClientWebSocket` for Editor testing
    - Unified API: `Connect(url)`, `Send(string)`, `Close()`, events for `OnOpen`, `OnMessage`, `OnClose`, `OnError`
    - MonoBehaviour attached to a persistent GameObject (DontDestroyOnLoad)
    - Receives jslib callbacks via `SendMessage` targeting this GameObject
- **MIRROR**: No direct codebase mirror; standard Unity WebGL plugin pattern
- **IMPORTS**: `System.Net.WebSockets` (Editor path), `System.Runtime.InteropServices` (WebGL path)
- **GOTCHA**: jslib functions receive/return raw strings. UTF-8 encoding handled by Unity's marshaling.
- **GOTCHA**: `ClientWebSocket.ReceiveAsync` needs a background task in Editor — use `async/await` with `Update()` dispatch.
- **GOTCHA**: jslib must use `mergeInto(LibraryManager.library, {...})` pattern.
- **VALIDATE**: Can connect to the Node.js server from Editor (play mode) and send/receive messages

### Task 3: Create Network Message Types
- **ACTION**: Create `Assets/Scripts/Network/NetworkMessages.cs`
- **IMPLEMENT**:
  - `[Serializable]` classes for each message type, matching the JSON protocol above
  - Base envelope: `NetworkMessage { public string type; }`
  - Specific types: `CreateRoomMessage`, `RoomCreatedMessage`, `JoinRoomMessage`, `RoomJoinedMessage`, `StudentCountMessage`, `RaceStartMessage`, `GameStateMessage`, `StateUpdateMessage`, `EventTriggeredMessage`, `LeaderboardMessage`, `RaceEndMessage`, `RoomClosedMessage`
  - `StateUpdateMessage.CarState` compact struct: `int i; float px,py,pz,ry; int l,c;`
  - `LeaderboardMessage.RankEntry` struct: `int rank; string name; int lap;`
  - Static helper: `NetworkMessageSerializer.Serialize<T>(T msg)` and `Deserialize(string json)` using `JsonUtility`
- **MIRROR**: DATA_STRUCT_PATTERN — `[Serializable]` classes with public fields and `Array.Empty<T>()` defaults
- **IMPORTS**: `System; UnityEngine`
- **GOTCHA**: `JsonUtility` cannot deserialize polymorphic types. Must parse `type` field first, then deserialize to the specific class. Use `JsonUtility.FromJson<NetworkMessage>(json)` to get the type, then switch.
- **GOTCHA**: `JsonUtility` cannot serialize top-level arrays. Wrap in a class with an array field.
- **VALIDATE**: Round-trip serialize/deserialize of each message type in a simple test

### Task 4: Create NetworkManager
- **ACTION**: Create `Assets/Scripts/Network/NetworkManager.cs`
- **IMPLEMENT**:
  - MonoBehaviour (singleton pattern via `FindFirstObjectByType`, not `DontDestroyOnLoad` — consistent with codebase which doesn't use singletons)
  - Fields: `public string ServerUrl = "ws://localhost:8080"`, `WebSocketBridge bridge`
  - Connection lifecycle: `Connect()`, `Disconnect()`, `Send(string json)`
  - Events: `Action OnConnected`, `Action OnDisconnected`, `Action<string> OnMessageReceived`
  - Message routing: `OnMessageReceived` invoked for each incoming message
  - `IsConnected` property
  - `IsHost` property (true if this client created the room)
  - `RoomCode` property
  - Public methods: `CreateRoom()`, `JoinRoom(string code)`
  - Internal: handles `room_created`, `room_joined`, `student_count` messages
  - Events: `Action<string> OnRoomCreated`, `Action<string> OnRoomJoined`, `Action<int> OnStudentCountChanged`
- **MIRROR**: EVENT_PATTERN — `Action<T>` delegates; COMPONENT_INITIALIZATION — Awake/Start setup
- **IMPORTS**: `System; UnityEngine`
- **GOTCHA**: Must dispatch WebSocket callbacks to the main Unity thread. In Editor, the `ClientWebSocket` receive loop runs on a background thread. Use a `ConcurrentQueue<string>` drained in `Update()`.
- **VALIDATE**: Connect to server, create room, get room code back; join from another client in Editor

### Task 5: Create NetworkSync
- **ACTION**: Create `Assets/Scripts/Network/NetworkSync.cs`
- **IMPLEMENT**:
  - MonoBehaviour with references: `RaceManager`, `NetworkManager`, `ScoreManager`
  - **Professor mode** (IsHost):
    - `Update()`: every 0.1s, collect car positions/rotations/lap/checkpoint into `StateUpdateMessage` and send
    - Subscribe to `RaceManager.OnStateChanged` → send `GameStateMessage`
    - Subscribe to `EventManager.OnEventTriggered` → send `EventTriggeredMessage`
    - Every 0.5s, send `LeaderboardMessage` with `ScoreManager.GetRankedCars()`
    - On `LoadAndStartRace`: send `RaceStartMessage` with car data list
  - **Student mode** (!IsHost):
    - On `RaceStartMessage`: call `CarSpawner.SpawnVisualCars()` to create visual-only cars
    - On `StateUpdateMessage`: update each car's target position/rotation for interpolation
    - On `GameStateMessage`: update local GameState (for UI panel visibility)
    - On `EventTriggeredMessage`: display event in UI (EventPanel-like feedback)
    - On `LeaderboardMessage`: update LeaderboardPanel directly
    - On `RoomClosedMessage`: show "Room closed" message, return to join screen
  - Car interpolation: store target positions, lerp in `Update()` at configurable smooth speed (e.g., `Mathf.Lerp` with `Time.deltaTime * 15f`)
- **MIRROR**: EVENT_PATTERN — subscribe to `Action<T>` events; COMPONENT_INITIALIZATION
- **IMPORTS**: `System; System.Collections.Generic; UnityEngine`
- **GOTCHA**: Car index in `StateUpdateMessage` must match spawn order. Use `RaceManager.SpawnedCars` list index as the car ID.
- **GOTCHA**: Student car references must be stored in the same order as the professor's spawn order. `RaceStartMessage` sends car data in order; `SpawnVisualCars` preserves that order.
- **GOTCHA**: On student side, `CarIdentity.CheckpointTime` via `Update()` keeps running — but it doesn't matter since student leaderboard comes from professor broadcast.
- **VALIDATE**: Start race on professor, student sees cars appear and move smoothly; event triggers display on student side

### Task 6: Create JoinScreen UI
- **ACTION**: Create `Assets/Scripts/UI/JoinScreen.cs`
- **IMPLEMENT**:
  - MonoBehaviour panel for student room-code entry
  - Fields: `NetworkManager NetworkManager`, `InputField RoomCodeInput`, `Button JoinButton`, `Text StatusText`
  - `JoinButton.onClick` → calls `NetworkManager.JoinRoom(RoomCodeInput.text.ToUpper())`
  - Subscribe to `NetworkManager.OnRoomJoined` → hide panel, show "Waiting for race..."
  - Subscribe to `NetworkManager.OnDisconnected` → show error "Connection lost"
  - Input validation: room code must be 6 alphanumeric characters
- **MIRROR**: UI_PANEL_PATTERN — mirrors `SetupScreen` structure exactly
- **IMPORTS**: `UnityEngine; UnityEngine.UI`
- **GOTCHA**: InputField should auto-capitalize and limit to 6 characters
- **VALIDATE**: Enter valid room code → JoinScreen hides → student waits for race start

### Task 7: Update SetupScreen for Hosting
- **ACTION**: Update `Assets/Scripts/UI/SetupScreen.cs`
- **IMPLEMENT**:
  - Add fields: `public NetworkManager NetworkManager`, `public Button HostButton`, `public Text RoomCodeText`
  - `HostButton.onClick` → calls `NetworkManager.CreateRoom()`
  - Subscribe to `NetworkManager.OnRoomCreated` → display room code in large text
  - Subscribe to `NetworkManager.OnStudentCountChanged` → display "X students connected"
  - Existing "Start Race" button still works (now also triggers `RaceStartMessage` broadcast via NetworkSync)
  - If `NetworkManager` is null or not connected, hosting features are hidden (standalone mode still works)
- **MIRROR**: UI_PANEL_PATTERN — same structure as existing SetupScreen
- **IMPORTS**: `UnityEngine; UnityEngine.UI` (already imported)
- **GOTCHA**: Must not break standalone (no server) mode. All network features are null-checked.
- **VALIDATE**: Click "Host Race" → room code appears; click "Start Race" → students receive race data

### Task 8: Update RaceUI for Network Role Detection
- **ACTION**: Update `Assets/Scripts/UI/RaceUI.cs`
- **IMPLEMENT**:
  - Add field: `public JoinScreen JoinScreen`
  - In `ApplyRole()`: if Student, show JoinScreen; if Professor, show SetupScreen
  - Add method `SetRoleFromNetwork(bool isHost)`: sets `Role` based on network state
  - NetworkSync calls `SetRoleFromNetwork` when room is created (professor) or joined (student)
  - Student role: disable Events panel, Controls panel, free camera (already handled)
  - Add: `public NetworkManager NetworkManager` field, `Text ConnectionStatus` for showing "Connected: X students"
- **MIRROR**: ROLE_BASED_UI — extend existing role pattern
- **IMPORTS**: `UnityEngine` (already imported)
- **GOTCHA**: Role can be set before or after `Start()`. Ensure `ApplyRole()` is callable at any time.
- **VALIDATE**: Host → professor UI shown. Join → student UI shown (spectator camera, leaderboard only)

### Task 9: Update CarSpawner for Visual-Only Mode
- **ACTION**: Update `Assets/Scripts/Race/CarSpawner.cs`
- **IMPLEMENT**:
  - Add method `SpawnVisualCars(List<CarData> carDataList)`:
    - Same as `SpawnCars` but does NOT add: NavMeshAgent, CarController, Rigidbody, BoxCollider
    - Adds CarIdentity and initializes it (for team name labels)
    - Adds a simple `RemoteCarInterpolator` component (or inline logic in NetworkSync)
    - Cars are positioned at spawn point initially; NetworkSync moves them
  - This method is called by NetworkSync on student clients
- **MIRROR**: Existing `SpawnCars` method pattern — same prefab selection, scaling, CarIdentity init
- **IMPORTS**: No new imports needed
- **GOTCHA**: Must still use `SpawnPoint` for initial position so cars aren't at origin
- **GOTCHA**: Student cars should still have correct scale and name (for car labels to work)
- **VALIDATE**: `SpawnVisualCars` creates cars that are visible but don't move on their own

### Task 10: Update RaceManager for Network Hooks
- **ACTION**: Update `Assets/Scripts/Race/RaceManager.cs`
- **IMPLEMENT**:
  - Add optional field: `public NetworkSync NetworkSync`
  - In `LoadAndStartRace(List<CarData>)`: if NetworkSync exists and IsHost, NetworkSync broadcasts `RaceStartMessage`
  - In `SetState()`: NetworkSync is notified (it subscribes via event — already handled by NetworkSync subscribing to `OnStateChanged`)
  - Add method `LoadAndStartRaceVisualOnly(List<CarData> carDataList)`:
    - Called by NetworkSync on student side
    - Calls `CarSpawner.SpawnVisualCars(carDataList)` instead of `SpawnCars`
    - Sets state to Racing but does NOT register with LapTracker, ScoreManager, EventManager
    - Students don't run event system or scoring locally — they receive broadcasts
  - No changes to debug keyboard shortcuts (they only affect professor client)
- **MIRROR**: Existing `LoadAndStartRace` method pattern
- **IMPORTS**: No new imports needed
- **GOTCHA**: Student-side `spawnedCars` list must be populated for NetworkSync to find cars by index.
- **GOTCHA**: `ResetRace()` on student side must also clear visual-only cars.
- **VALIDATE**: Professor starts race → students see cars spawn; professor pauses → students see paused state

---

## Testing Strategy

### Manual Integration Tests

Since this is a Unity project with no CLI test pipeline, validation is manual in-Editor + browser:

| Test | Setup | Expected Result | Edge Case? |
|---|---|---|---|
| Server starts | `cd Server && node server.js` | "WebSocket server listening on port 8080" | No |
| Room creation | Professor client connects, sends `create_room` | Receives `room_created` with 6-char code | No |
| Room join | Student enters valid code | Receives `room_joined`, professor gets `student_count` | No |
| Invalid room code | Student enters non-existent code | Error message "Room not found" | Yes |
| Race start sync | Professor starts race | Students see cars spawn at correct positions | No |
| Position sync | Cars move on professor side | Students see smooth car movement with slight lag | No |
| Event sync | Professor triggers event | Students see event notification | No |
| Leaderboard sync | Race progresses | Student leaderboard matches professor's | No |
| Pause/Resume | Professor pauses | Students see paused state | No |
| Professor disconnect | Professor closes tab | Students see "Room closed" message | Yes |
| Student disconnect | Student closes tab | No effect on professor; `student_count` decrements | Yes |
| Standalone mode | No server running, professor starts normally | Race works exactly as before (no network features) | Yes |
| 30+ cars | Large CSV loaded | Smooth sync at 10Hz (monitor bandwidth) | Yes |
| Multiple students | 5+ students join | All see synchronized race | No |

### Edge Cases Checklist
- [ ] Empty room code input
- [ ] Room code with lowercase (auto-uppercase)
- [ ] Server not running (connection timeout → error message)
- [ ] Professor disconnects mid-race
- [ ] Student joins after race has started (late join → receives current state)
- [ ] Student refreshes browser mid-race (rejoin flow)
- [ ] 50+ concurrent students (stress test)
- [ ] Network latency >500ms (interpolation should still look smooth)
- [ ] Race reset while students connected

---

## Validation Commands

### Server Startup
```bash
cd Server && npm install && node server.js
```
EXPECT: "WebSocket server listening on port 8080"

### WebSocket Manual Test
```bash
# Terminal 1: Start server
cd Server && node server.js

# Terminal 2: Professor
npx wscat -c ws://localhost:8080
> {"type":"create_room"}
# Expect: {"type":"room_created","roomCode":"XXXXXX"}

# Terminal 3: Student
npx wscat -c ws://localhost:8080
> {"type":"join_room","roomCode":"XXXXXX"}
# Expect: {"type":"room_joined","roomCode":"XXXXXX"}
```

### Unity Editor Test
1. Enter Play Mode in `complete_track_demo` scene
2. Verify no errors in Console related to NetworkManager when server is not running
3. Start server, enter Play Mode again
4. Click "Host Race" → room code appears
5. Open second Unity Editor window or build → enter room code → join
6. Start race on professor → verify student sees cars
EXPECT: No errors, smooth sync

### Build Verification
```bash
# Verify no compile errors (check Unity Console)
# Verify WebGL build succeeds (Phase 6 concern, but ensure no compile issues)
```

### Manual Validation
- [ ] Server starts and accepts connections
- [ ] Room creation returns unique 6-character codes
- [ ] Students join with room code successfully
- [ ] Race start is synchronized to students
- [ ] Car positions update smoothly on student clients
- [ ] Events display on student side
- [ ] Leaderboard is synchronized
- [ ] Professor disconnect shows message to students
- [ ] Standalone mode (no server) still works identically to pre-Phase-5

---

## Acceptance Criteria
- [ ] Professor can host a race session and receive a room code
- [ ] Students can join with room code and see the race in their browser
- [ ] Car positions are synchronized within ~100ms visual delay
- [ ] Events triggered by professor are visible to students
- [ ] Leaderboard is synchronized in real-time
- [ ] Game state changes (Racing/Paused/Finished) are synchronized
- [ ] Standalone mode (no server) works exactly as before
- [ ] Server handles 30+ concurrent student connections
- [ ] Professor disconnect gracefully notifies students
- [ ] No Unity compile errors

## Completion Checklist
- [ ] Code follows discovered patterns (Action events, [Serializable] data, MonoBehaviour structure)
- [ ] Error handling: connection failures show user-friendly messages in InfoText/StatusText
- [ ] No hardcoded values (server URL is configurable, sync rates are constants)
- [ ] Null checks on all network references (standalone mode must work)
- [ ] Car interpolation is smooth (no teleporting)
- [ ] Student cars have no NavMeshAgent/CarController (visual-only)
- [ ] Room codes are unique and cleaned up on disconnect
- [ ] Message serialization handles all edge cases (empty arrays, null strings)

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| WebGL WebSocket jslib complexity | MEDIUM | HIGH | Use proven jslib pattern; test extensively in WebGL build early |
| NavMeshAgent not on student cars causes visual issues (floating, wrong scale) | LOW | MEDIUM | Students spawn cars with same prefab + scale, just no AI components |
| JsonUtility limitations (no polymorphism, no top-level arrays) | MEDIUM | LOW | Already designed message types with wrapper classes; parse type field first |
| Car interpolation jitter at high latency | LOW | MEDIUM | Use `Mathf.Lerp` with tunable smoothing; snap if delta too large |
| Editor testing requires two Unity instances | MEDIUM | LOW | Use `wscat` for quick protocol testing; second instance only for visual verification |
| Memory leak from WebSocket on student disconnect | LOW | MEDIUM | Server removes student from room Set on `close` event; C# disposes socket |

## Notes
- The jslib WebSocket bridge is the most critical and platform-specific component. It should be developed and tested first against the Node.js server before integrating with NetworkManager.
- Late-join (student joins after race started) should be supported: NetworkSync sends current state snapshot to newly joined students. Server can request a "full state" from professor on student join, or professor periodically sends full state.
- Room codes use 6 uppercase alphanumeric characters (26 letters + 10 digits = 36^6 ≈ 2.2 billion combinations). Collision is negligible for classroom use.
- Car interpolation uses `Vector3.Lerp` and `Quaternion.Slerp` in Update(), not fixed-rate. The 10Hz network rate means ~6 frames between updates at 60fps — smooth enough with lerp factor ~15.
- The WebSocket server is intentionally minimal (~150 lines). It's a relay, not a game server. All game logic stays in Unity.
