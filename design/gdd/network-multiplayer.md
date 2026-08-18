# Network & Multiplayer

> **Status**: Accepted — reverse-engineered from codebase (v2)
> **Last Updated**: 2026-07-23
> **Source**: `NetworkManager.cs`, `NetworkSync.cs`, `NetworkMessages.cs`,
> `WebSocketBridge.cs`
> **ADR**: ADR-0003 (WebSocket Multi-Client Sync), ADR-0004 (Docker Deployment)

---

## 1. Overview

The network system enables classroom multiplayer: a professor (host) creates a
room, students join via a short room code, and the race is broadcast in real
time. Communication uses WebSocket — the only browser-compatible bidirectional
protocol for WebGL builds. The architecture is event-driven: only race events and
periodic state updates are transmitted, not per-frame game state.

Students are passive observers — they see visual-only cars without AI or physics.

---

## 2. Player Fantasy

"I click 'Host Game' and get a room code — ABCD. I project it on the screen. 30
students type the code on their phones, and they all see the race on their own
screens. When I trigger events, everyone sees it happen simultaneously."

---

## 3. Detailed Rules

### Architecture

```
Professor (Unity WebGL)
    ↕ WebSocket
Node.js Server (:8080)
    ↕ WebSocket
Student Browsers (×N)
```

### Connection Flow

```
Host:
1. CreateRoom → connect to server → send CreateRoomMessage(sessionId)
2. Server responds with RoomCreatedMessage(roomCode)
3. Host broadcasts race start, car data, events, position updates

Student:
1. JoinRoom(code, teamName) → connect → send JoinRoomMessage
2. Server responds with RoomJoinedMessage
3. Student receives race data → spawns visual-only cars
4. Receives position updates → moves visual cars
```

### Server URL Resolution

| Platform | URL Source |
|----------|-----------|
| WebGL Build | Auto-detected from page hostname (`WebSocketBridge_GetPageWebSocketUrl()`) |
| Editor | `ServerUrl` field (default: `ws://localhost:8080`) |

### Message Types

| Message | Direction | Purpose |
|---------|-----------|---------|
| CreateRoomMessage | Host → Server | Request room creation |
| RoomCreatedMessage | Server → Host | Room code assigned |
| JoinRoomMessage | Student → Server | Join with code + teamName |
| RoomJoinedMessage | Server → Student | Join confirmed |
| StudentCountMessage | Server → Host | Updated participant count |
| StudentJoinedMessage | Server → All | New participant notification |
| RejoinRoomMessage | Client → Server | Reconnect with last room code |
| ReconnectStateMessage | Server → Client | State restore on reconnect |
| ErrorMessage | Server → Client | Error notification |
| (custom) | Any → Any | Forwarded via OnMessageReceived |

### Student-Side Rendering

- `LoadAndStartRaceVisualOnly()` spawns car prefabs WITHOUT:
  - NavMeshAgent (no AI navigation)
  - CarController (no autonomous movement)
  - Physics components
- Positions driven entirely by network position updates
- TrailRenderers still active for visual continuity
- Camera follows "own car" if `IsOwnCar` flag is set

### Reconnection

Auto-reconnect on unexpected disconnect:

```
for attempt = 1 to MaxAttempts:
    delay = InitialDelay × BackoffMultiplier^(attempt-1)
    delay = min(delay, MaxDelay)
    jitteredDelay = delay × Random(0.75, 1.25)
    wait(jitteredDelay)
    connect(lastServerUrl)
    if connected:
        send RejoinRoomMessage(lastRoomCode, sessionId, teamName)
        // server responds with ReconnectStateMessage
        return
// all attempts exhausted → fire OnReconnectFailed, OnDisconnected
```

### Disconnect Types

| Type | Auto-Reconnect? | State Cleared? |
|------|-----------------|----------------|
| Manual (`Disconnect()`) | No | Yes — all state reset |
| Unexpected (server down, network loss) | Yes (if `AutoReconnect` && had room) | Partial — room code saved for rejoin |

---

## 4. Formulas

### Reconnection Backoff

```
delay(n) = min(InitialDelay × BackoffMultiplier^(n-1), MaxDelay)
actual_delay = delay × Random(0.75, 1.25)   // jitter prevents thundering herd
```

With defaults: 1s → 2s → 4s → 8s → 16s → 30s → 30s → 30s → 30s → 30s

### Connection Timeout

```
per attempt: wait up to 5 seconds for connection to establish
if !connected after 5s → move to next attempt
```

---

## 5. Edge Cases

| Scenario | Handling |
|----------|----------|
| Student joins before host creates room | Server returns error message |
| Host disconnects mid-race | Auto-reconnect preserves `wasHost` flag; rejoin as host |
| Student disconnects mid-race | Auto-reconnect with `RejoinRoomMessage`; server restores state |
| All reconnect attempts fail | `OnReconnectFailed` + `OnDisconnected` fired; `lastRoomCode` cleared |
| WebGL auto-detection fails | Falls back to `ServerUrl` field value |
| Multiple rooms on same server | Each room has unique code; messages routed by room |
| Send while disconnected | `Send()` silently does nothing (null check) |
| Server sends unknown message type | Forwarded to `OnMessageReceived` for custom handling |

---

## 6. Dependencies

| Dependency | Role |
|-----------|------|
| WebSocketBridge | Low-level WebSocket connection (jslib in WebGL, C# in Editor) |
| Node.js Server | Room management, message routing |
| NetworkSync | Higher-level race state broadcasting |
| NetworkMessages | Message type definitions |
| RaceManager | Calls `LoadAndStartRaceVisualOnly()` for students |
| CarSpawner | `SpawnVisualCars()` for student-side rendering |
| JsonUtility (Unity) | Message serialization/deserialization |

---

## 7. Tuning Knobs

| Parameter | Default | Range | Effect |
|-----------|---------|-------|--------|
| ServerUrl | ws://localhost:8080 | URL | WebSocket server address (Editor only) |
| AutoReconnect | true | bool | Enable auto-reconnection |
| InitialDelay | 1 s | > 0 | First reconnect wait |
| MaxDelay | 30 s | > InitialDelay | Cap on backoff delay |
| BackoffMultiplier | 2 | > 1 | Exponential backoff factor |
| MaxAttempts | 10 | ≥ 1 | Reconnection attempt limit |
| Connection Timeout | 5 s | const | Per-attempt connection wait |

---

## 8. Acceptance Criteria

- [ ] Host creates room and receives a room code
- [ ] Students join with room code and see visual-only cars
- [ ] Race start broadcasts car data to all connected students
- [ ] Event triggers are visible on student screens
- [ ] Student count updates in real time on host side
- [ ] Unexpected disconnect triggers auto-reconnect with backoff
- [ ] Reconnected client receives `ReconnectStateMessage` with current state
- [ ] Manual disconnect does NOT trigger auto-reconnect
- [ ] WebGL build auto-detects server URL from page hostname
- [ ] System handles 30+ concurrent student connections
