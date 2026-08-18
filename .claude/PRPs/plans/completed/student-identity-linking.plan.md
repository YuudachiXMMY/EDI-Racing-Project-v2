# Plan: Student Identity Linking (GAP 5)

## Summary
Link students who fill out the web survey with their Unity game room sessions, enabling each student to see "their car" highlighted during the race. This bridges the identity gap between the web survey (which collects email + teamName) and the Unity room join flow (which currently requires only a room code with no identity).

## User Story
As a student,
I want to identify myself when joining a Unity room so that my car is highlighted during the race,
So that I can personally connect with the race outcome driven by my survey responses.

## Problem -> Solution
**Current:** Students fill a web survey (email + teamName) and join a Unity room (room code only). No link exists between these two actions. All cars look the same to all students.
**Desired:** Students enter their team name when joining a Unity room. The server matches their identity to a car in the race. Unity highlights "your car" for each student viewer.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/plans/web-unity-gap-analysis-and-hosting.plan.md`
- **PRD Phase**: GAP 5 (Student Identity Linking)
- **Estimated Files**: 10

---

## UX Design

### Before
```
Student Flow:
  Web Survey: Enter email + teamName + answers -> Submit
  Unity Game: Enter 6-char room code -> Join -> See ALL cars (no personalization)

Professor sees: "5 students connected" (anonymous count)
```

### After
```
Student Flow:
  Web Survey: Enter email + teamName + answers -> Submit  (unchanged)
  Unity Game: Enter room code + team name -> Join -> See all cars with YOUR car highlighted
                                                    (glow outline + "YOUR CAR" label)

Professor sees: "5 students: TeamAlpha, TeamBeta, ..." (named list)
Student sees:   Car #3 (TeamAlpha) with golden glow + "YOUR CAR" label above it
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| JoinScreen (Unity) | Room code only | Room code + Team Name input | New InputField added |
| Student race view | All cars identical styling | "Your car" has highlight glow + label | CarLabelSpawner enhanced |
| Professor setup | Anonymous count "5 students" | Named list "TeamAlpha, TeamBeta..." | StudentCountText enhanced |
| Server join_room | Anonymous WebSocket | WebSocket tagged with teamName | Server tracks identity |
| Server -> student | Same broadcast to all | Includes `yourCarIndex` on race_start | Per-student personalization |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/UI/JoinScreen.cs` | all | Must add team name field here |
| P0 (critical) | `Server/server.js` | all | Must track student identity in rooms |
| P0 (critical) | `Assets/Scripts/Network/NetworkMessages.cs` | 24-38 | JoinRoomMessage needs teamName field |
| P0 (critical) | `Assets/Scripts/Network/NetworkSync.cs` | 158-166, 306-332 | BroadcastRaceStart & HandleRaceStart — car assignment |
| P1 (important) | `Assets/Scripts/Network/NetworkManager.cs` | 151-166 | JoinRoom() sends the join message |
| P1 (important) | `Assets/Scripts/Car/CarIdentity.cs` | all | Runtime car state — TeamName is the key |
| P1 (important) | `Assets/Scripts/UI/CarLabel.cs` | all | Car label rendering above cars |
| P1 (important) | `Assets/Scripts/UI/CarLabelSpawner.cs` | all | Spawns labels for each car |
| P2 (reference) | `web-app/src/routes/export.js` | 47-81, 117-200 | How carData.teamName flows from web |
| P2 (reference) | `Assets/Scripts/UI/SetupScreen.cs` | 233-240 | Professor sees student count |
| P2 (reference) | `Assets/Scripts/Data/CarData.cs` | all | TeamName field definition |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| N/A | N/A | Feature uses only established internal patterns — no external research needed |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:24-30
[Serializable]
public class JoinRoomMessage
{
    public string type = "join_room";
    public string roomCode;
    public string sessionId;
}
// Pattern: lowercase camelCase for JSON fields, PascalCase for C# properties
```

### ERROR_HANDLING
```csharp
// SOURCE: Assets/Scripts/UI/JoinScreen.cs:56-69
private void OnJoinClicked()
{
    if (NetworkManager == null)
    {
        SetStatus("Network not available.");
        return;
    }
    string code = RoomCodeInput != null ? RoomCodeInput.text.Trim().ToUpper() : "";
    if (code.Length != 6)
    {
        SetStatus("Room code must be 6 characters.");
        return;
    }
    // Pattern: null-check references, validate input, show user-facing status message
}
```

### SERVER_MESSAGE_HANDLING
```javascript
// SOURCE: Server/server.js:192-219
case 'join_room': {
    const code = (msg.roomCode || '').toUpperCase();
    const room = rooms.get(code);
    if (!room) {
        sendJSON(ws, { type: 'error', message: 'Room not found' });
        return;
    }
    room.students.add(ws);
    const clientInfo = { roomCode: code, role: 'student', sessionId: msg.sessionId || null };
    clientRooms.set(ws, clientInfo);
    // Pattern: validate input, check room exists, update data structures, notify client + professor
}
```

### NETWORK_BROADCAST_PATTERN
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:158-166
public void BroadcastRaceStart(List<CarData> carDataList)
{
    if (NetworkManager == null || !NetworkManager.IsHost) return;
    var msg = new RaceStartMessage();
    msg.cars = new NetCarData[carDataList.Count];
    for (int i = 0; i < carDataList.Count; i++)
        msg.cars[i] = NetCarData.FromCarData(carDataList[i]);
    NetworkManager.Send(JsonUtility.ToJson(msg));
}
// Pattern: guard with null/host check, build message, serialize with JsonUtility
```

### UI_BUILDING_PATTERN
```csharp
// SOURCE: Assets/Scripts/UI/JoinScreen.cs:10-16
[Header("UI Elements")]
public InputField RoomCodeInput;
public Button JoinButton;
public Text StatusText;
// Pattern: public fields with [Header], wired in Unity Inspector
```

### STUDENT_PANEL_TEAM_INPUT
```csharp
// SOURCE: Assets/Scripts/UI/StudentSurveyPanel.cs:119-127
// Team name row
GameObject teamRow = BuilderUIFactory.CreateRow(transform, "TeamRow", 34f);
BuilderUIFactory.CreateText(teamRow.transform, "TeamLabel", "Team Name:",
    14, TextAnchor.MiddleLeft, ...);
teamNameField = BuilderUIFactory.CreateInputField(teamRow.transform, "TeamName",
    "Enter your team name", ...);
// Pattern: BuilderUIFactory for programmatic UI, but JoinScreen uses Inspector wiring
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add `teamName` to JoinRoomMessage and RejoinRoomMessage; add `yourCarIndex` to RaceStartMessage; add `StudentJoinedMessage` |
| `Assets/Scripts/UI/JoinScreen.cs` | UPDATE | Add team name InputField, send teamName with join_room |
| `Assets/Scripts/Network/NetworkManager.cs` | UPDATE | Pass teamName through JoinRoom(), store locally |
| `Server/server.js` | UPDATE | Track student teamName in clientRooms/rooms; send per-student race_start with yourCarIndex; notify professor of student names |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Handle `yourCarIndex` on student side; apply highlight to own car |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Show student names instead of just count |
| `Assets/Scripts/Car/CarIdentity.cs` | UPDATE | Add `IsOwnCar` flag for highlight logic |
| `Assets/Scripts/UI/CarLabelSpawner.cs` | UPDATE | Apply visual highlight to own car's label |
| `Assets/Scripts/UI/CarLabel.cs` | UPDATE | Support highlight mode (different color/text) |

## NOT Building

- Web survey <-> Unity identity automatic matching (future — GAP 5 expansion)
- Student authentication/login system in Unity
- Email collection in Unity join flow (only team name for now)
- Persistent student profiles across sessions
- Student-specific camera follow mode

---

## Step-by-Step Tasks

### Task 1: Add teamName to Network Messages (C#)
- **ACTION**: Add `teamName` field to `JoinRoomMessage` and `RejoinRoomMessage`. Add `yourCarIndex` field to `RaceStartMessage` (sent individually per student). Add `StudentJoinedMessage` for professor notification.
- **IMPLEMENT**:
  ```csharp
  // JoinRoomMessage: add field
  public string teamName;
  
  // RejoinRoomMessage: add field  
  public string teamName;
  
  // Add to RaceStartMessage
  public int yourCarIndex = -1; // -1 = not matched
  
  // New message type
  [Serializable]
  public class StudentJoinedMessage
  {
      public string type = "student_joined";
      public string teamName;
      public int count;
  }
  
  // New message for student list
  [Serializable]
  public class StudentListMessage
  {
      public string type = "student_list";
      public string[] teamNames;
      public int count;
  }
  ```
- **MIRROR**: NAMING_CONVENTION — lowercase camelCase for JSON fields
- **IMPORTS**: `using System;` (already present)
- **GOTCHA**: `JsonUtility` requires `[Serializable]` and public fields (no properties). All fields must have defaults for deserialization.
- **VALIDATE**: Project compiles without errors

### Task 2: Update JoinScreen to collect teamName (Unity UI)
- **ACTION**: Add a team name InputField to JoinScreen. Pass team name to NetworkManager.JoinRoom().
- **IMPLEMENT**:
  ```csharp
  // Add field
  public InputField TeamNameInput;
  
  // In Start(), set character limit
  if (TeamNameInput != null)
      TeamNameInput.characterLimit = 30;
  
  // In OnJoinClicked(), validate and pass
  string teamName = TeamNameInput != null ? TeamNameInput.text.Trim() : "";
  if (string.IsNullOrEmpty(teamName))
  {
      SetStatus("Please enter your team name.");
      return;
  }
  NetworkManager.JoinRoom(code, teamName);
  ```
- **MIRROR**: UI_BUILDING_PATTERN — public InputField field, wired in Inspector
- **IMPORTS**: None additional needed
- **GOTCHA**: Must handle null TeamNameInput gracefully for backward compatibility with existing scenes that don't have the field wired.
- **VALIDATE**: Unity scene can compile; JoinScreen shows team name field

### Task 3: Update NetworkManager to pass teamName
- **ACTION**: Modify `JoinRoom()` to accept and store teamName. Include it in join/rejoin messages.
- **IMPLEMENT**:
  ```csharp
  // Add property
  public string TeamName { get; private set; }
  
  // Update JoinRoom signature
  public void JoinRoom(string code, string teamName = "")
  {
      manualDisconnect = false;
      Connect();
      TeamName = teamName;
      var joinMsg = new JoinRoomMessage 
      { 
          roomCode = code.ToUpper(), 
          sessionId = sessionId,
          teamName = teamName
      };
      pendingAction = () =>
      {
          IsHost = false;
          Send(JsonUtility.ToJson(joinMsg));
      };
      if (bridge.IsConnected)
      {
          pendingAction();
          pendingAction = null;
      }
  }
  
  // In ReconnectCoroutine, include teamName in rejoin:
  var msg = new RejoinRoomMessage 
  { 
      roomCode = lastRoomCode, 
      sessionId = sessionId,
      teamName = TeamName ?? ""
  };
  ```
  Also store `TeamName` in the reconnect state (save before clearing):
  ```csharp
  // In HandleClose(), save TeamName alongside lastRoomCode
  private string lastTeamName;
  // Before clearing: lastTeamName = TeamName;
  // In Disconnect(): TeamName = null; lastTeamName = null;
  ```
- **MIRROR**: NETWORK_BROADCAST_PATTERN
- **IMPORTS**: None additional
- **GOTCHA**: Default parameter `teamName = ""` ensures backward compatibility with existing `JoinRoom(code)` calls.
- **VALIDATE**: Existing call sites (if any) still compile

### Task 4: Update Server to track student identity
- **ACTION**: Store teamName in `clientRooms` map entry for each student. Track student team names in room data. Notify professor of student names. Send per-student `yourCarIndex` on race_start.
- **IMPLEMENT**:
  ```javascript
  // Room structure: add studentNames Set
  // rooms.set(roomCode, { ..., studentTeamNames: new Map() }) // ws -> teamName
  
  // join_room handler: store teamName
  case 'join_room': {
      const code = (msg.roomCode || '').toUpperCase();
      const room = rooms.get(code);
      if (!room) { ... }
      room.students.add(ws);
      const teamName = (msg.teamName || '').trim();
      const clientInfo = { roomCode: code, role: 'student', sessionId: msg.sessionId || null, teamName };
      clientRooms.set(ws, clientInfo);
      if (teamName) room.studentTeamNames.set(ws, teamName);
      
      // Notify professor with student name
      if (room.professor && room.professor.readyState === 1) {
          sendJSON(room.professor, { 
              type: 'student_joined', 
              teamName: teamName || '(anonymous)',
              count: room.students.size 
          });
          // Also send full list
          sendJSON(room.professor, {
              type: 'student_list',
              teamNames: [...room.studentTeamNames.values()],
              count: room.students.size
          });
      }
      ...
  }
  
  // Default relay handler: for race_start, send per-student with yourCarIndex
  if (msg.type === 'race_start') {
      room.raceStarted = true;
      room.gamePhase = 'Racing';
      room.latestState = raw;
      
      // Parse car list to find team name matches
      const cars = msg.cars || [];
      for (const student of room.students) {
          if (student.readyState !== 1) continue;
          const studentInfo = clientRooms.get(student);
          const studentTeam = (studentInfo && studentInfo.teamName) || '';
          let yourIndex = -1;
          if (studentTeam) {
              yourIndex = cars.findIndex(c => 
                  c.teamName && c.teamName.toLowerCase() === studentTeam.toLowerCase()
              );
          }
          // Send personalized race_start with yourCarIndex
          const personalizedMsg = { ...msg, yourCarIndex: yourIndex };
          student.send(JSON.stringify(personalizedMsg));
      }
      // Don't broadcast to students via normal path (already sent individually)
      // Still relay to webapps
  }
  ```
  For cleanup:
  ```javascript
  // In cleanupClient for students:
  room.studentTeamNames.delete(ws);
  // Notify professor of updated list
  if (room.professor && room.professor.readyState === 1) {
      sendJSON(room.professor, {
          type: 'student_list',
          teamNames: [...room.studentTeamNames.values()],
          count: room.students.size
      });
  }
  ```
- **MIRROR**: SERVER_MESSAGE_HANDLING
- **IMPORTS**: None additional
- **GOTCHA**: The `race_start` relay must be handled specially — individual messages per student instead of broadcast. Other message types (state_update, leaderboard) still broadcast normally. Must also handle the case where student has no teamName (fallback to -1).
- **VALIDATE**: `node Server/server.js` starts without errors; manual WebSocket test with team name

### Task 5: Handle yourCarIndex on student side (NetworkSync)
- **ACTION**: Parse `yourCarIndex` from race_start message. Store it and apply highlight to the matched car.
- **IMPLEMENT**:
  ```csharp
  // Add field
  private int ownCarIndex = -1;
  
  // In HandleRaceStart:
  private void HandleRaceStart(string json)
  {
      var msg = JsonUtility.FromJson<RaceStartMessage>(json);
      ownCarIndex = msg.yourCarIndex; // -1 if not matched
      
      var carDataList = new List<CarData>();
      foreach (var nc in msg.cars)
          carDataList.Add(nc.ToCarData());
  
      if (RaceManager != null)
          RaceManager.LoadAndStartRaceVisualOnly(carDataList);
  
      remoteCars = RaceManager != null ? RaceManager.SpawnedCars : null;
      // ... existing interpolation setup ...
      
      // Mark own car
      if (ownCarIndex >= 0 && remoteCars != null && ownCarIndex < remoteCars.Count)
      {
          var identity = remoteCars[ownCarIndex].GetComponent<CarIdentity>();
          if (identity != null)
              identity.IsOwnCar = true;
      }
  }
  ```
- **MIRROR**: NETWORK_BROADCAST_PATTERN (guard checks, JsonUtility parsing)
- **IMPORTS**: None additional
- **GOTCHA**: `yourCarIndex` defaults to -1 if not present in JSON (JsonUtility default for int). Ensure no out-of-bounds access.
- **VALIDATE**: No compile errors; student sees own car highlighted

### Task 6: Add IsOwnCar flag to CarIdentity
- **ACTION**: Add `IsOwnCar` boolean field to CarIdentity for highlight logic.
- **IMPLEMENT**:
  ```csharp
  [Header("Player Ownership")]
  public bool IsOwnCar;
  ```
  In `Initialize()`, reset it:
  ```csharp
  IsOwnCar = false;
  ```
- **MIRROR**: CarIdentity field pattern (public fields with `[Header]`)
- **IMPORTS**: None
- **GOTCHA**: None
- **VALIDATE**: Compiles

### Task 7: Visual highlight for own car (CarLabel + CarLabelSpawner)
- **ACTION**: When a car has `IsOwnCar = true`, show a distinctive highlight — different label color and "YOUR CAR" text prefix.
- **IMPLEMENT**: Read `CarLabel.cs` and `CarLabelSpawner.cs` first to understand current label logic. Then:
  - In CarLabelSpawner or CarLabel update loop, check `CarIdentity.IsOwnCar`
  - If true: set label text to `">> {TeamName} <<"`, set color to gold `(1f, 0.84f, 0f)`
  - Optionally add a simple glow/outline effect on the car itself (emission material change)
  
  For the car highlight visual:
  ```csharp
  // In NetworkSync.HandleRaceStart, after marking IsOwnCar:
  if (identity.IsOwnCar)
  {
      // Set emissive glow on car renderers
      var renderers = remoteCars[ownCarIndex].GetComponentsInChildren<Renderer>();
      foreach (var r in renderers)
      {
          foreach (var mat in r.materials)
          {
              mat.EnableKeyword("_EMISSION");
              mat.SetColor("_EmissionColor", new Color(1f, 0.84f, 0f) * 0.3f);
          }
      }
  }
  ```
- **MIRROR**: LeaderboardPanel highlight pattern (gold/silver/bronze colors at lines 66-72)
- **IMPORTS**: `UnityEngine.Rendering` may be needed
- **GOTCHA**: Material modification creates instances — acceptable for visual-only student cars. Must check if URP materials support `_EMISSION` keyword (may need `_EmissionColor` with URP).
- **VALIDATE**: Visual inspection — own car has golden glow and label distinction

### Task 8: Update SetupScreen to show student names
- **ACTION**: Subscribe to `student_joined` and `student_list` messages. Display team names instead of just count.
- **IMPLEMENT**:
  ```csharp
  // In OnNetworkMessage, handle new message types:
  if (baseMsg.type == "student_joined")
  {
      var msg = JsonUtility.FromJson<StudentJoinedMessage>(json);
      if (StudentCountText != null)
      {
          StudentCountText.gameObject.SetActive(true);
          StudentCountText.text = $"{msg.count} student(s): {msg.teamName} joined";
      }
  }
  else if (baseMsg.type == "student_list")
  {
      var msg = JsonUtility.FromJson<StudentListMessage>(json);
      if (StudentCountText != null)
      {
          StudentCountText.gameObject.SetActive(true);
          string names = msg.teamNames != null && msg.teamNames.Length > 0
              ? string.Join(", ", msg.teamNames)
              : "(none named)";
          StudentCountText.text = $"{msg.count} student(s): {names}";
      }
  }
  ```
- **MIRROR**: SetupScreen.OnStudentCountChanged pattern (line 233-240)
- **IMPORTS**: None
- **GOTCHA**: `StudentListMessage.teamNames` is a string array — `JsonUtility` handles arrays in `[Serializable]` classes. Text may overflow for many students — truncate if needed.
- **VALIDATE**: Professor sees student team names in setup screen

### Task 9: Update server rejoin to preserve teamName
- **ACTION**: When a student reconnects via `rejoin_room`, restore their teamName in the room's tracking data.
- **IMPLEMENT**:
  ```javascript
  // In rejoin_room handler, for student role:
  } else {
      room.students.add(ws);
      const teamName = (msg.teamName || sessionInfo.teamName || '').trim();
      clientRooms.set(ws, { roomCode: code, role: 'student', sessionId: sid, teamName });
      if (teamName) room.studentTeamNames.set(ws, teamName);
      // ... existing reconnect logic ...
  }
  ```
  Also update `sessions` map to store teamName:
  ```javascript
  // In join_room: 
  if (msg.sessionId) {
      sessions.set(msg.sessionId, { roomCode: code, role: 'student', teamName });
  }
  ```
- **MIRROR**: SERVER_MESSAGE_HANDLING (rejoin_room pattern at line 222-277)
- **IMPORTS**: None
- **GOTCHA**: Must handle case where teamName is in the session but not in the rejoin message (prefer message, fallback to session).
- **VALIDATE**: Student reconnects and retains identity

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| JoinRoomMessage serialization | `{ type: "join_room", roomCode: "ABCDEF", sessionId: "abc", teamName: "Alpha" }` | Valid JSON with all fields | No |
| JoinRoom with empty teamName | `JoinRoom("ABCDEF", "")` | Sends message with empty teamName, no error | Yes |
| Server: student joins with teamName | WS message `{ type: "join_room", roomCode: "X", teamName: "Alpha" }` | clientRooms entry has teamName: "Alpha" | No |
| Server: race_start personalization | race_start with cars [{ teamName: "Alpha" }, { teamName: "Beta" }], student is "Alpha" | student receives yourCarIndex: 0 | No |
| Server: race_start with unmatched student | student teamName is "Gamma", cars are "Alpha" and "Beta" | yourCarIndex: -1 | Yes |
| CarIdentity.IsOwnCar default | new CarIdentity.Initialize(data) | IsOwnCar = false | No |
| NetworkSync own car highlight | yourCarIndex = 2, 5 cars | remoteCars[2].GetComponent<CarIdentity>().IsOwnCar == true | No |
| NetworkSync yourCarIndex = -1 | yourCarIndex = -1 | No car highlighted | Yes |

### Edge Cases Checklist
- [ ] Empty team name on join (should still work, just no highlighting)
- [ ] Team name with special characters (spaces, unicode)
- [ ] Multiple students with same team name (first match wins for yourCarIndex)
- [ ] Student joins after race already started (latestState has no yourCarIndex — acceptable)
- [ ] Professor sends race_start before any students join with names
- [ ] Student reconnects after disconnect — teamName preserved
- [ ] Very long team name (30 char limit in UI, but validate server-side too)
- [ ] Case-insensitive matching ("alpha" matches "Alpha")

---

## Validation Commands

### Static Analysis
```bash
# Unity C# compilation check — open Unity and check Console for errors
# Or use Unity batch mode:
# /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -quit -logFile -
```
EXPECT: Zero compilation errors

### Server Test
```bash
cd Server && node -e "require('./server.js')" && echo "OK"
```
EXPECT: Server starts without syntax errors

### Manual Validation
- [ ] Open Unity Editor, no compile errors
- [ ] Start a room as professor, note room code
- [ ] In another browser tab, join room with team name "TestTeam"
- [ ] Professor sees "1 student(s): TestTeam" on SetupScreen
- [ ] Start race with CSV data containing a car named "TestTeam"
- [ ] Student viewer sees golden glow on the "TestTeam" car
- [ ] Student viewer sees ">> TestTeam <<" label on their car
- [ ] Disconnect student, reconnect — identity preserved
- [ ] Student with unmatched name sees no highlight (graceful fallback)

---

## Acceptance Criteria
- [ ] All tasks completed
- [ ] All validation commands pass
- [ ] JoinScreen has team name input field
- [ ] Server tracks student teamName per connection
- [ ] Professor sees student team names on setup screen
- [ ] race_start includes per-student yourCarIndex
- [ ] Student sees their car highlighted with golden glow
- [ ] Student sees distinctive label on their car
- [ ] Unmatched students see no highlight (no crash)
- [ ] Reconnection preserves student identity
- [ ] No type errors
- [ ] No lint errors

## Completion Checklist
- [ ] Code follows discovered patterns (camelCase JSON, PascalCase C#)
- [ ] Error handling matches codebase style (null-checks, SetStatus)
- [ ] No hardcoded values (colors could be const or config, but matching LeaderboardPanel gold is fine)
- [ ] No unnecessary scope additions (no web survey <-> Unity auto-matching)
- [ ] Backward compatible — existing scenes without TeamNameInput still work
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| URP emission keyword not supported | Medium | Low | Fallback: just change label color, skip material glow |
| JsonUtility can't serialize string arrays in StudentListMessage | Low | Medium | Test first; if fails, use comma-separated string instead |
| race_start individual send breaks broadcast flow | Medium | Medium | Carefully split: individual to students, broadcast to webapps only |
| Scene breakage — existing JoinScreen missing TeamNameInput reference | Low | Low | Null-check TeamNameInput, default to empty string |

## Notes
- This plan deliberately keeps the scope tight: team name input in Unity + server matching + visual highlight. The more ambitious "automatic matching via email between web survey and Unity room" is deferred.
- The `yourCarIndex` approach is simple but effective: the server does a case-insensitive string match between the student's teamName and the car list in the race_start message. No database query needed.
- The highlight approach (emission glow + label change) mirrors the gold/silver/bronze pattern already used in LeaderboardPanel, maintaining visual consistency.
- The race_start message personalization requires the server to parse the JSON and find car indices — this is the only place where the server needs to understand game data structure. All other messages continue as dumb relay.
