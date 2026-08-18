# Plan: Student Survey System

## Summary
Build the student-facing survey experience: students join a room via browser, see survey questions from the active SurveyConfig, submit responses over WebSocket, and the professor-side aggregates responses into CarData for the race. Requires new WebSocket message types, a SurveyCollector component, student survey UI, and integration with the existing response-to-CarData pipeline (SurveyResponseMapper).

## User Story
As a student participating in a class EDI activity, I want to answer survey questions on my device after joining a room, so that my responses drive my team's car attributes in the race.

## Problem -> Solution
Students have no way to participate in data collection — data only enters via CSV import -> Students join a room, answer dynamically-rendered survey questions, and their responses automatically become CarData entries for the race.

## Metadata
- **Complexity**: Large
- **Source PRD**: `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md`
- **PRD Phase**: Phase 5 — Student Survey System
- **Estimated Files**: 4 new + 3 modified = 7

---

## UX Design

### Before
```
+------------------------------+
| Student JoinScreen           |
|                              |
| Room Code: [______]          |
| [Join]                       |
|                              |
| Status: Enter room code.     |
+------------------------------+
       ↓ After joining:
(Student waits, sees nothing until race_start)
```

### After
```
+------------------------------+
| Student JoinScreen           |
|                              |
| Room Code: [______]          |
| [Join]                       |
+------------------------------+
       ↓ After joining:
+--------------------------------------------------+
| SURVEY                           Step 2 of 5     |
|                                                   |
| Team Name: [_______________]                      |
|                                                   |
| Q: "What is your primary language?"              |
|    [_______________]                              |
|                                                   |
| Q: "Are you a first-generation student?"         |
|    ( ) No   ( ) Yes                              |
|                                                   |
| Q: "Hours worked per week (0-40)"               |
|    [====|======] 15                              |
|                                                   |
|                              [Submit]            |
+--------------------------------------------------+
       ↓ After submit:
+--------------------------------------------------+
| Your responses have been submitted!              |
| Your team car is ready.                          |
| Waiting for the race to start...                 |
+--------------------------------------------------+

Professor Setup Screen (additions):
+--------------------------------------------------+
| Room: ABC123     3 student(s) connected          |
| Responses: 3/3 received                          |
| [Start Race with Responses]                      |
+--------------------------------------------------+
```

### Interaction Changes

| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Student after joining | Empty wait | Survey questions rendered | Questions from active SurveyConfig |
| Student submit | N/A | Responses sent via WebSocket | Confirmation shown immediately |
| Professor setup | Student count only | Response count + "Start Race with Responses" button | Can start race once responses arrive |
| Race start | CSV data only | CSV OR survey responses → CarData | SurveyResponseMapper converts responses |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Network/NetworkManager.cs` | all | WebSocket lifecycle, message routing, event delegates |
| P0 | `Assets/Scripts/Network/NetworkMessages.cs` | all | Message format patterns, serialization approach |
| P0 | `Assets/Scripts/Network/NetworkSync.cs` | 39-50, 191-218 | How game messages are subscribed/routed on student side |
| P0 | `Assets/Scripts/Data/SurveyConfig.cs` | all | SurveyConfig, SurveyQuestion shape |
| P0 | `Assets/Scripts/Data/SurveyResponseMapper.cs` | all | Response-to-CarData conversion logic |
| P0 | `Assets/Scripts/Data/AttributeMapping.cs` | all | Mapping data model |
| P1 | `Assets/Scripts/UI/JoinScreen.cs` | all | Existing student join flow — survey follows this |
| P1 | `Assets/Scripts/UI/SetupScreen.cs` | 100-165 | Survey builder integration, StartWithSurveyConfig pattern |
| P1 | `Assets/Scripts/UI/BuilderUIFactory.cs` | all | Runtime UI creation factory (reuse for student survey UI) |
| P1 | `Assets/Scripts/Race/RaceManager.cs` | 62-101 | LoadAndStartRace pipeline — entry point for survey-driven data |
| P2 | `Server/server.js` | all | Server relay — understand default relay behavior |
| P2 | `Assets/Scripts/Data/SurveyQuestion.cs` | all | QuestionType enum + SurveyQuestion struct |
| P2 | `Assets/Scripts/Data/CarData.cs` | all | Dynamic attribute model |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity UGUI Slider | Unity docs | Slider.minValue, Slider.maxValue, Slider.wholeNumbers for Numeric questions |
| Unity UGUI Toggle Group | Unity docs | ToggleGroup for single-select MultipleChoice rendering |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:1-10
// Message classes: PascalCase with "Message" suffix
// Message type strings: snake_case
[Serializable]
public class SurveyQuestionsMessage
{
    public string type = "survey_questions";
    // ...
}
```

### ERROR_HANDLING
```csharp
// SOURCE: Assets/Scripts/UI/JoinScreen.cs:52-56
// Validate input, show status via SetStatus/InfoText, disable buttons
if (NetworkManager == null)
{
    SetStatus("Network not available.");
    return;
}
```

### EVENT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:28-33
// C# Action<T> events, subscribe in OnEnable, unsubscribe in OnDisable
public event Action<int> OnStudentCountChanged;
public event Action<string> OnMessageReceived;
```

### UI_FACTORY_PATTERN
```csharp
// SOURCE: Assets/Scripts/UI/BuilderUIFactory.cs:93-96
// Static factory creates all UI programmatically — no prefabs needed
Button btn = BuilderUIFactory.CreateButton(parent, "SaveBtn", "Save",
    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
    new Color(0.15f, 0.45f, 0.15f, 0.9f));
```

### MONOBEHAVIOUR_LIFECYCLE
```csharp
// SOURCE: Assets/Scripts/UI/JoinScreen.cs:26-48
// Start() for one-time setup (AddListener)
// OnEnable() for event subscription
// OnDisable() for event unsubscription
private void OnEnable()
{
    if (NetworkManager != null)
    {
        NetworkManager.OnRoomJoined += OnRoomJoined;
        NetworkManager.OnConnectionError += OnError;
    }
}
```

### MESSAGE_ROUTING
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:191-218
// Parse base type, switch on type string, deserialize to specific class
private void HandleGameMessage(string json)
{
    var baseMsg = JsonUtility.FromJson<NetworkMessage>(json);
    switch (baseMsg.type)
    {
        case "race_start":
            HandleRaceStart(json);
            break;
        // ...
    }
}
```

### NETWORK_SEND_PATTERN
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:143-151
// Create message struct, serialize with JsonUtility.ToJson, call NetworkManager.Send
var msg = new RaceStartMessage();
msg.cars = new NetCarData[carDataList.Count];
NetworkManager.Send(JsonUtility.ToJson(msg));
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add survey-specific message types (SurveyQuestionsMessage, SurveyResponseMessage, SurveyCompleteMessage) |
| `Assets/Scripts/UI/StudentSurveyPanel.cs` | CREATE | Student-facing survey UI — renders questions from config, captures responses |
| `Assets/Scripts/Network/SurveyCollector.cs` | CREATE | Professor-side aggregator — receives responses via WebSocket, stores them, converts to CarData |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Add response counter, "Start Race with Responses" button, survey distribution trigger |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Route survey messages (survey_questions → student, survey_response → professor) |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | Wire StudentSurveyPanel visibility in student role |
| `Server/server.js` | UPDATE | Add bidirectional relay: student→professor messages (currently only professor→students) |

## NOT Building

- Survey branching logic (conditional questions)
- Response editing after submission
- Professor live preview of individual responses (only count)
- Multiple submissions per student (one-shot only)
- Survey timer / auto-close
- HTML overlay for text input (plain Unity InputField for now — sufficient for desktop/tablet)

---

## Step-by-Step Tasks

### Task 1: Add Survey Network Message Types
- **ACTION**: Add 4 new message classes to NetworkMessages.cs
- **IMPLEMENT**:
  ```csharp
  // Professor → Students (via server relay)
  [Serializable]
  public class SurveyQuestionsMessage
  {
      public string type = "survey_questions";
      public string configJson; // Full SurveyConfig JSON (questions + mappings for student rendering)
  }

  // Student → Professor (via server relay)
  [Serializable]
  public class SurveyResponseMessage
  {
      public string type = "survey_response";
      public string teamName;
      public NetAttribute[] responses; // questionId → answer pairs
  }

  // Professor → Students (survey closed, race starting)
  [Serializable]
  public class SurveyClosedMessage
  {
      public string type = "survey_closed";
  }

  // Professor → Students (acknowledgement that response was received)
  [Serializable]
  public class SurveyAckMessage
  {
      public string type = "survey_ack";
      public string teamName;
  }
  ```
- **MIRROR**: NetworkMessages.cs existing patterns (Serializable struct, string type field with snake_case)
- **IMPORTS**: `using System;` (already present)
- **GOTCHA**: `configJson` is a serialized string (not nested object) because JsonUtility cannot handle nested complex types dynamically. Professor serializes SurveyConfig to string, student deserializes on receipt.
- **VALIDATE**: File compiles without errors

### Task 2: Update Server for Bidirectional Relay
- **ACTION**: Modify server.js to relay student→professor messages
- **IMPLEMENT**:
  ```javascript
  // In the default case of the message switch, add student→professor relay:
  default: {
    const info = clientRooms.get(ws);
    if (!info) return;
    const room = rooms.get(info.roomCode);
    if (!room) return;

    const raw = data.toString();

    if (info.role === 'professor') {
      // Professor → Students (existing behavior)
      if (msg.type === 'race_start') {
        room.raceStarted = true;
        room.latestState = raw;
      } else if (msg.type === 'state_update') {
        room.latestState = raw;
      }
      broadcastToStudents(info.roomCode, raw);
    } else if (info.role === 'student') {
      // Student → Professor relay
      if (room.professor && room.professor.readyState === 1) {
        room.professor.send(raw);
      }
    }
    break;
  }
  ```
- **MIRROR**: Existing server.js patterns (sendJSON, broadcastToStudents, readyState check)
- **IMPORTS**: None (Node.js ws library already imported)
- **GOTCHA**: Must keep existing professor→student relay intact. The change only adds an `else if` branch for student role.
- **VALIDATE**: Start server, connect professor + student, student sends JSON → professor receives it

### Task 3: Create SurveyCollector Component
- **ACTION**: Create a new MonoBehaviour that aggregates survey responses on professor side
- **IMPLEMENT**: `Assets/Scripts/Network/SurveyCollector.cs`
  - Listens on NetworkManager.OnMessageReceived for "survey_response" messages
  - Stores responses in a List<SurveyResponseData>
  - Uses SurveyResponseMapper.MapResponses() to convert each response to CarData
  - Exposes: `List<CarData> CollectedCarData`, `int ResponseCount`, `event Action<int> OnResponseReceived`
  - Method: `DistributeSurvey(SurveyConfig config)` — sends SurveyQuestionsMessage to students
  - Method: `List<CarData> GetAllCarData()` — returns all mapped CarData
  - Method: `Clear()` — resets for new session
- **MIRROR**: NetworkSync.cs lifecycle pattern (subscribe OnMessageReceived, parse type, dispatch)
- **IMPORTS**: `using System; using System.Collections.Generic; using UnityEngine;`
- **GOTCHA**: Must only process messages when IsHost is true (ignore own broadcasts). Send SurveyAckMessage back to confirm receipt.
- **VALIDATE**: Professor creates room, distributes survey, student sends response → ResponseCount increments, CarData list grows

### Task 4: Create StudentSurveyPanel UI
- **ACTION**: Create student-facing survey panel that renders questions and captures responses
- **IMPLEMENT**: `Assets/Scripts/UI/StudentSurveyPanel.cs`
  - Listens on NetworkManager.OnMessageReceived for "survey_questions" message
  - Parses configJson → SurveyConfig, extracts Questions array
  - Dynamically renders:
    - Team name InputField (always first)
    - For each question by QuestionType:
      - Text → InputField
      - MultipleChoice → ToggleGroup with Toggles
      - Numeric → Slider (minValue..maxValue)
    - Submit button at bottom
    - Progress indicator ("Step X of Y")
  - On submit: collects all responses as NetAttribute[], sends SurveyResponseMessage
  - After submit: shows confirmation panel, hides survey
  - Listens for "survey_closed" to transition state
  - Uses BuilderUIFactory for all UI creation
- **MIRROR**: BuilderUIFactory patterns, JoinScreen lifecycle, SurveyBuilderPanel structure (scroll + content area)
- **IMPORTS**: `using System; using System.Collections.Generic; using UnityEngine; using UnityEngine.UI;`
- **GOTCHA**: Validate required questions before allowing submit. Team name cannot be empty. Use ScrollRect for questions overflow. Panel must be hidden initially and shown only when survey_questions arrives.
- **VALIDATE**: Student joins room → receives survey_questions → sees rendered questions → fills answers → submits → sees confirmation

### Task 5: Update NetworkSync for Survey Message Routing
- **ACTION**: Add survey message handling in NetworkSync.HandleGameMessage
- **IMPLEMENT**:
  ```csharp
  // In HandleGameMessage switch:
  case "survey_questions":
      HandleSurveyQuestions(json);
      break;
  case "survey_response":
      HandleSurveyResponse(json);
      break;
  case "survey_closed":
      HandleSurveyClosed(json);
      break;
  case "survey_ack":
      HandleSurveyAck(json);
      break;
  ```
  - Add reference to SurveyCollector (professor-side)
  - Add reference to StudentSurveyPanel (student-side)
  - `HandleSurveyQuestions`: forward to StudentSurveyPanel.ShowSurvey(json)
  - `HandleSurveyResponse`: forward to SurveyCollector.ProcessResponse(json)
  - `HandleSurveyClosed`: forward to StudentSurveyPanel.OnSurveyClosed()
  - `HandleSurveyAck`: forward to StudentSurveyPanel.OnAckReceived(json)
- **MIRROR**: Existing case statements in HandleGameMessage (same pattern)
- **IMPORTS**: None new
- **GOTCHA**: SurveyCollector is professor-side only, StudentSurveyPanel is student-side only. Null-check both before forwarding.
- **VALIDATE**: Messages flow correctly: professor distributes → student receives and shows survey; student submits → professor collector aggregates

### Task 6: Update SetupScreen for Survey Distribution
- **ACTION**: Add survey distribution controls to SetupScreen
- **IMPLEMENT**:
  - Add field: `public SurveyCollector SurveyCollector;`
  - Add UI elements: Response counter text, "Distribute Survey" button, "Start Race with Responses" button
  - "Distribute Survey" button: calls `SurveyCollector.DistributeSurvey(SurveyConfigManager.ActiveConfig)`
  - Subscribe to `SurveyCollector.OnResponseReceived` to update counter text
  - "Start Race with Responses" button: gets `SurveyCollector.GetAllCarData()`, passes to `RaceManager.LoadAndStartRace(List<CarData>)`
  - Button only enabled when ResponseCount > 0
- **MIRROR**: Existing SetupScreen patterns (optional reference fields, null checks, button listeners, InfoText status)
- **IMPORTS**: None new (already has UnityEngine, UnityEngine.UI)
- **GOTCHA**: "Distribute Survey" should only be available after hosting a room AND having an active config. Both conditions required. Disable the button after distributing to prevent re-sending.
- **VALIDATE**: Professor hosts room → has active config → clicks "Distribute Survey" → students see survey → responses arrive → counter updates → professor clicks "Start Race" → race begins with survey-derived CarData

### Task 7: Update RaceUI for StudentSurveyPanel Visibility
- **ACTION**: Wire StudentSurveyPanel into RaceUI's role-based visibility system
- **IMPLEMENT**:
  - Add field: `public StudentSurveyPanel StudentSurvey;`
  - In `ApplyRole()`: `if (StudentSurvey != null) StudentSurvey.gameObject.SetActive(false);` (initially hidden for both roles)
  - StudentSurveyPanel self-activates when it receives survey_questions, so RaceUI just needs to know it exists for cleanup
  - In `OnStateChanged()`: hide StudentSurveyPanel when state transitions to Racing
- **MIRROR**: Existing panel visibility management in RaceUI (Setup, JoinScreen, Events, Controls)
- **IMPORTS**: None new
- **GOTCHA**: StudentSurveyPanel manages its own show/hide via messages. RaceUI only forces hide on race start to clean up.
- **VALIDATE**: Student role: survey panel hidden until questions arrive; auto-hides when race starts

---

## Testing Strategy

### Unit Tests

This is a Unity project — no CLI test runner. Validation is manual:

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Empty survey config distributed | Config with 0 questions | Student sees only team name + submit | Yes |
| All question types rendered | Config with Text + MultipleChoice + Numeric | Each renders correct widget | No |
| Required field validation | Empty required field, hit submit | Submit blocked, warning shown | Yes |
| Team name duplicate | Two students submit same team name | Both accepted (professor handles duplicates) | Yes |
| Student joins after survey distributed | Late joiner | Should receive survey_questions via server cache | Yes |
| 50 responses | 50 students submit | All 50 CarData entries created correctly | No |
| Disconnect mid-survey | Student disconnects | Response count accurate, no crash | Yes |
| No active config, distribute clicked | Null ActiveConfig | Button disabled / info text warning | Yes |

### Edge Cases Checklist
- [x] Empty input (no questions in config)
- [x] Maximum size input (20 questions, 50 students)
- [x] Invalid types (malformed JSON in survey_questions)
- [x] Concurrent access (multiple responses arriving simultaneously)
- [x] Network failure (student disconnects mid-survey)
- [x] Permission denied (N/A — no auth)

---

## Validation Commands

### Static Analysis
```
# Unity compile check — open project in Unity Editor, check Console for errors
# OR: build with BuildScript if available
```
EXPECT: Zero compile errors in Console

### Manual Validation
- [ ] Professor: Host room, load "Accessibility" template, click "Distribute Survey"
- [ ] Student: Join room with code → survey questions appear
- [ ] Student: Answer all questions, click Submit → confirmation shown
- [ ] Professor: Response counter increments from 0/0 to 1/1
- [ ] Professor: Click "Start Race with Responses" → race starts with survey-derived cars
- [ ] Verify car attributes match survey responses (check CarIdentity in Inspector)
- [ ] Test with 3+ students submitting simultaneously
- [ ] Test late-joiner receives survey questions
- [ ] Test student disconnect doesn't crash professor

---

## Acceptance Criteria
- [ ] All tasks completed
- [ ] All validation commands pass
- [ ] Student can join room and see dynamically-rendered survey questions
- [ ] All 3 question types render correctly (Text, MultipleChoice, Numeric)
- [ ] Student submits responses → professor receives them via WebSocket
- [ ] Professor sees response count update in real-time
- [ ] Professor can start race using collected survey responses
- [ ] SurveyResponseMapper correctly converts responses to CarData with attribute mappings
- [ ] Late-joining students receive survey questions
- [ ] Required field validation prevents empty submissions
- [ ] No type errors / compile errors

## Completion Checklist
- [ ] Code follows discovered patterns (Action events, MonoBehaviour lifecycle, BuilderUIFactory)
- [ ] Error handling matches codebase style (null checks, Debug.LogWarning, status text)
- [ ] Logging follows codebase conventions (`[ClassName] Message`)
- [ ] No hardcoded values (question limits, colors use constants)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| JsonUtility cannot serialize nested SurveyConfig in message | MEDIUM | Would block survey distribution | Use double-serialization: config → JSON string → embed as `configJson` string field |
| Server doesn't relay student→professor messages | HIGH (current behavior) | Student responses lost | Task 2 explicitly adds student→professor relay |
| Large survey config exceeds WebSocket frame size | LOW | Message dropped | SurveyConfig with 20 questions is ~5KB — well within limits |
| Unity UI Slider doesn't render well on mobile WebGL | LOW | Poor UX for Numeric questions | Acceptable for v2.1; can add HTML overlay later |
| Late-joiner misses survey_questions (server doesn't cache it) | MEDIUM | Student sees blank screen | Cache survey_questions on server like race_start is cached |

## Notes
- The server.js currently only relays professor→student messages. Task 2 is critical — without it, student responses never reach the professor.
- `configJson` uses double-serialization pattern: `JsonUtility.ToJson(config)` produces a string, which is then embedded as a string field in `SurveyQuestionsMessage`. This avoids JsonUtility's inability to handle nested complex types dynamically.
- The existing `SurveyResponseMapper.MapResponses()` is already perfectly suited for this — it takes `teamName`, `AttributeEntry[] responses`, and `AttributeMapping[] mappings` and returns `CarData`. No modifications needed.
- Late-joiner caching: the server already caches `race_start` and `state_update` for late joiners. We should add `survey_questions` to this cache (stored as `room.surveyData`).
