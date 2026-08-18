# Plan: Data Export + Unity Integration (Phase 4)

## Summary
Implement the data export pipeline from Web App to Unity: the Express API transforms student survey responses into `CarData[]` using `AttributeMapping` rules, bundles them with `SavedEventRule[]`, and serves them via `GET /api/surveys/:id/export`. Unity receives a new `JsonImporter` class that parses this JSON into `List<CarData>` + `SavedEventRule[]`, and the `SetupScreen` gains an "Import from Web App" flow (paste JSON or fetch via URL). The frontend gets an export button with JSON download + copy support.

## User Story
As a professor,
I want to export survey data from the Web App and import it into Unity,
So that student survey responses automatically generate racing cars with correct attributes and event rules.

## Problem → Solution
Currently the export endpoint returns empty `carData: []` with a `// Phase 4 will add response->CarData mapping here` comment. Unity has no way to import JSON from the Web App. → After this phase, the complete pipeline works: Web App collects responses, maps them to CarData via AttributeMappings, exports JSON, and Unity imports it to start the race.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-survey-web-app.prd.md`
- **PRD Phase**: Phase 4 — 数据导出 + Unity 集成
- **Estimated Files**: 8

---

## UX Design

### Before
```
┌──────────────────────────────────┐
│ Web App Dashboard                │
│                                  │
│  Survey Card: "Accessibility"    │
│  [Share Code: A1B2C3D4]         │
│  [No export option]              │
│                                  │
└──────────────────────────────────┘

┌──────────────────────────────────┐
│ Unity SetupScreen                │
│                                  │
│  [Start Default]  [Load Session] │
│  [New Survey] [Load Config]      │
│  [No Web App import]             │
│                                  │
└──────────────────────────────────┘
```

### After
```
┌──────────────────────────────────┐
│ Web App Editor Page              │
│                                  │
│  [Questions] [Mappings] [Rules]  │
│  [★ Export for Unity]            │
│    ↓                             │
│  Modal: Download JSON / Copy     │
│  Responses: 12 students          │
│  Cars generated: 12              │
│                                  │
└──────────────────────────────────┘

┌──────────────────────────────────┐
│ Unity SetupScreen                │
│                                  │
│  [Start Default]  [Load Session] │
│  [★ Import Web App JSON]         │
│    ↓                             │
│  Paste JSON or enter URL →       │
│  Preview: 12 cars, 3 rules       │
│  [Start Race]                    │
│                                  │
└──────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Web App Editor | No export UI | "Export for Unity" button in header | Shows response count + download/copy |
| Web App API | Export returns empty carData | Export maps responses → CarData via mappings | Replicates SurveyResponseMapper logic in JS |
| Unity SetupScreen | CSV/Session import only | + "Import Web App JSON" button | Paste JSON text or fetch from URL |
| Unity Data | CsvParser only | + JsonImporter for Web App JSON | Parses `{ configName, carData, eventRules }` |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/export.js` | all | Existing stub to complete |
| P0 | `Assets/Scripts/Data/SurveyResponseMapper.cs` | all | JS reimplementation source |
| P0 | `Assets/Scripts/Data/CarData.cs` | all | Target data structure |
| P0 | `Assets/Scripts/Data/SessionData.cs` | 63-109 | SavedEventRule struct definition |
| P0 | `Assets/Scripts/UI/SetupScreen.cs` | all | UI to modify for import |
| P1 | `Assets/Scripts/Data/AttributeMapping.cs` | all | Mapping transform types |
| P1 | `web-app/src/routes/responses.js` | all | How responses are stored/retrieved |
| P1 | `web-app/src/routes/surveys.js` | all | Survey data access patterns |
| P1 | `web-app/client/src/api.js` | all | Frontend API client pattern |
| P1 | `web-app/client/src/pages/EditorPage.jsx` | all | Where export button goes |
| P2 | `Assets/Scripts/Data/SurveyConfigManager.cs` | 93-120 | ApplyRulesToSchedule pattern |
| P2 | `Assets/Scripts/Race/RaceManager.cs` | 62-101 | LoadAndStartRace entry points |
| P2 | `Assets/Scripts/Data/CsvParser.cs` | all | Parallel parser pattern |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### API_RESPONSE_FORMAT
```javascript
// SOURCE: web-app/src/routes/surveys.js:13-18
res.json({ success: true, data: surveys });
// and error:
res.status(404).json({ success: false, error: 'Survey not found' });
```

### AUTH_GUARD
```javascript
// SOURCE: web-app/src/routes/export.js:8
router.get('/:id/export', requireAuth, (req, res) => {
  // req.user.userId available from middleware
```

### DB_QUERY_PATTERN
```javascript
// SOURCE: web-app/src/routes/surveys.js:35-38
const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
  .get(req.params.id, req.user.userId);
if (!survey) {
  return res.status(404).json({ success: false, error: 'Survey not found' });
}
```

### JSON_PARSE_PATTERN
```javascript
// SOURCE: web-app/src/routes/surveys.js:47-49
questions: JSON.parse(survey.questions_json),
mappings: JSON.parse(survey.mappings_json),
rules: JSON.parse(survey.rules_json),
```

### FRONTEND_API_CLIENT
```javascript
// SOURCE: web-app/client/src/api.js:58-60
export async function getSurveys() {
  return request('/surveys');
}
```

### REACT_STATE_PATTERN
```javascript
// SOURCE: web-app/client/src/pages/EditorPage.jsx:36-41
const handleChange = useCallback((field, value) => {
  setSurvey(prev => {
    const updated = { ...prev, [field]: value };
    // ...
    return updated;
  });
}, [id]);
```

### UNITY_JSONUTILITY
```csharp
// SOURCE: Assets/Scripts/Data/SessionManager.cs:48-49
string json = File.ReadAllText(path);
var session = JsonUtility.FromJson<SessionData>(json);
```

### UNITY_SETUP_BUTTON
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:50-53
if (StartDefaultButton != null)
    StartDefaultButton.onClick.AddListener(StartWithDefaultData);
if (LoadSessionButton != null)
    LoadSessionButton.onClick.AddListener(LoadLatestSession);
```

### UNITY_RACEMANAGER_START
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:69-101
public void LoadAndStartRace(List<CarData> carDataList)
{
    spawnedCars = CarSpawner.SpawnCars(carDataList);
    // ... registers cars, activates events
    SetState(GameState.Racing);
}
```

### APPLY_RULES_TO_SCHEDULE
```csharp
// SOURCE: Assets/Scripts/Data/SurveyConfigManager.cs:93-120
public void ApplyRulesToSchedule(EventSchedule schedule)
{
    Key[] keys = { Key.Digit1, Key.Digit2, ... Key.Digit9 };
    int count = Mathf.Min(ActiveConfig.Rules.Length, keys.Length);
    var eventRules = new EventRule[count];
    for (int i = 0; i < count; i++)
        eventRules[i] = ActiveConfig.Rules[i].ToRule(keys[i]);
    schedule.Events = eventRules;
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/routes/export.js` | UPDATE | Complete response→CarData mapping logic |
| `web-app/client/src/api.js` | UPDATE | Add `exportSurvey(id)` function |
| `web-app/client/src/pages/EditorPage.jsx` | UPDATE | Add Export button + export modal/UI |
| `Assets/Scripts/Data/JsonImporter.cs` | CREATE | Parse Web App JSON → CarData[] + SavedEventRule[] |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Add "Import Web App JSON" button and handler |
| `Assets/Tests/EditMode/JsonImporterTests.cs` | CREATE | Unit tests for JsonImporter |
| `web-app/client/src/constants.js` | UPDATE | Add export-related constants (if needed) |
| `web-app/client/src/index.css` | UPDATE | Styles for export modal |

## NOT Building

- REST API auto-fetch from Unity (Unity fetching from URL) — manual JSON paste only for MVP
- WebSocket real-time push of export data
- Export format version negotiation
- Batch export across multiple surveys
- Unity Editor custom inspector for JSON import
- Frontend analytics/statistics dashboard for responses

---

## Step-by-Step Tasks

### Task 1: Complete Export API — Response→CarData Mapping
- **ACTION**: Rewrite `web-app/src/routes/export.js` to fetch responses, apply attribute mappings, and produce `CarData[]`
- **IMPLEMENT**: 
  1. Fetch survey (questions_json, mappings_json, rules_json)
  2. Fetch all responses for that survey from `responses` table
  3. For each response: apply mappings to transform answers → attributes (reimplement `SurveyResponseMapper.MapResponses` in JS)
  4. Build `CarData` objects: `{ teamName, attributes: [{ key, value }] }`
  5. Return `{ configName, carData: [...], eventRules: [...] }`
- **MIRROR**: `DB_QUERY_PATTERN`, `AUTH_GUARD`, `API_RESPONSE_FORMAT`, `JSON_PARSE_PATTERN`
- **IMPORTS**: `{ Router } from 'express'`, `{ getDb } from '../db.js'`, `{ requireAuth } from '../middleware/auth.js'`
- **GOTCHA**: 
  - SurveyResponseMapper uses case-insensitive matching for questionId — replicate this
  - "lookup" transform must check LookupEntries array, fall back to DefaultValue
  - "numeric" transform must validate the value is a number, else use DefaultValue
  - "direct" is the default transform type when TransformType is null/empty
  - Response `answers_json` stores `{ questionId: answer }` as object (not array), while mappings use `QuestionId` field — match by key
- **VALIDATE**: 
  - `curl -H "Authorization: Bearer <token>" http://localhost:3001/api/surveys/1/export` returns populated carData
  - Each carData entry has teamName and correct attributes from mappings

### Task 2: Frontend Export UI
- **ACTION**: Add "Export for Unity" button to `EditorPage.jsx` and an export modal/section
- **IMPLEMENT**:
  1. Add `exportSurvey(id)` to `api.js` — calls `GET /api/surveys/${id}/export`
  2. In `EditorPage.jsx` header, add "Export for Unity" button alongside save status
  3. On click: call `exportSurvey(id)`, show result in a modal/section with:
     - Response count and car count summary
     - "Download JSON" button (creates Blob + download link)
     - "Copy to Clipboard" button
     - The raw JSON in a readonly textarea for quick viewing
  4. Add CSS for the export section in `index.css`
- **MIRROR**: `FRONTEND_API_CLIENT`, `REACT_STATE_PATTERN`
- **IMPORTS**: `{ exportSurvey } from '../api.js'` in EditorPage
- **GOTCHA**: 
  - JSON.stringify with 2-space indent for readability
  - Download filename should be `{configName}-export.json` with sanitized name
  - Show "No responses yet" warning if carData is empty
- **VALIDATE**: 
  - Export button appears in editor header
  - Clicking downloads valid JSON file
  - Copy to clipboard works
  - Empty responses shows appropriate message

### Task 3: Unity JsonImporter
- **ACTION**: Create `Assets/Scripts/Data/JsonImporter.cs` — static utility class to parse Web App export JSON
- **IMPLEMENT**:
  ```csharp
  public static class JsonImporter
  {
      // Wrapper class for JsonUtility deserialization
      [Serializable]
      private class WebAppExport
      {
          public string configName;
          public WebAppCarData[] carData;
          public SavedEventRule[] eventRules;
      }
      
      [Serializable]
      private class WebAppCarData
      {
          public string teamName;
          public AttributeEntry[] attributes;
      }
      
      public static ImportResult Parse(string json)
      // Returns: List<CarData>, SavedEventRule[], configName, error message
  }
  
  public class ImportResult
  {
      public bool Success;
      public string Error;
      public string ConfigName;
      public List<CarData> Cars;
      public SavedEventRule[] EventRules;
  }
  ```
- **MIRROR**: `UNITY_JSONUTILITY` (JsonUtility.FromJson pattern)
- **IMPORTS**: `System`, `System.Collections.Generic`, `UnityEngine`
- **GOTCHA**: 
  - `JsonUtility` requires wrapper classes with matching field names (camelCase in JSON, matching C# field names)
  - JSON field `carData` requires the C# field to also be named `carData` (not `CarData`) for JsonUtility
  - `JsonUtility` cannot deserialize top-level arrays — must wrap in object
  - Empty/null JSON should return error result, not throw
  - Validate that TeamName is non-empty for each car
- **VALIDATE**: Parse sample JSON from Web App export → correct CarData count and attribute values

### Task 4: Unity JsonImporter Tests
- **ACTION**: Create `Assets/Tests/EditMode/JsonImporterTests.cs` with NUnit tests
- **IMPLEMENT**: Tests covering:
  1. Valid JSON with multiple cars and rules → correct parsing
  2. Empty carData array → success with 0 cars
  3. Empty/null JSON string → error result
  4. Malformed JSON → error result
  5. Missing fields (no eventRules) → defaults to empty array
  6. Attribute values correctly mapped (key/value pairs)
  7. SavedEventRule fields correctly deserialized (Operator as int, Weather as int)
- **MIRROR**: Test structure from `SurveyResponseMapperTests.cs` (NUnit, `[TestFixture]`, `[Test]`, Assert.AreEqual)
- **IMPORTS**: `System`, `System.Collections.Generic`, `NUnit.Framework`
- **GOTCHA**: Tests run in Edit Mode — no MonoBehaviour lifecycle
- **VALIDATE**: All tests pass in Unity Test Runner

### Task 5: SetupScreen — Import Web App JSON
- **ACTION**: Add "Import Web App JSON" button and handler to `SetupScreen.cs`
- **IMPLEMENT**:
  1. Add new UI references:
     ```csharp
     [Header("Web App Import")]
     public Button ImportJsonButton;
     public InputField JsonInputField;  // For pasting JSON
     public Button ConfirmImportButton;
     public GameObject ImportPanel;     // Container for import UI
     ```
  2. In `Start()`: wire up `ImportJsonButton.onClick` → show ImportPanel
  3. `ConfirmImportButton.onClick` → parse JSON via `JsonImporter.Parse()`
  4. On success: 
     - Apply event rules to EventSchedule (reuse `ApplyRulesToSchedule` pattern)
     - Call `RaceManager.LoadAndStartRace(result.Cars)`
     - Hide SetupScreen
  5. On error: show error in InfoText
- **MIRROR**: `UNITY_SETUP_BUTTON`, `APPLY_RULES_TO_SCHEDULE`, `UNITY_RACEMANAGER_START`
- **IMPORTS**: `UnityEngine`, `UnityEngine.UI`, `UnityEngine.InputSystem`
- **GOTCHA**: 
  - All new UI references are optional (null-checked) — scene may not have them wired up
  - ImportPanel starts hidden (`SetActive(false)`)
  - InputField uses legacy UI `UnityEngine.UI.InputField`, not TMP (matching existing SetupScreen pattern)
  - Must apply rules to EventSchedule BEFORE starting race
  - Key binding assignment for rules: Digit1-Digit9 sequentially (same as SurveyConfigManager)
- **VALIDATE**: 
  - Paste valid JSON → race starts with correct cars
  - Paste invalid JSON → error message shown
  - Button hidden when ImportPanel not assigned in scene

### Task 6: Update RaceManager for JSON Import Path
- **ACTION**: Add a `LoadAndStartRace` overload that accepts both CarData and SavedEventRule
- **IMPLEMENT**:
  ```csharp
  public void LoadAndStartRaceWithRules(List<CarData> carDataList, SavedEventRule[] rules)
  {
      // Apply rules to EventSchedule
      if (EventManager != null && EventManager.Schedule != null && rules != null && rules.Length > 0)
      {
          Key[] keys = { Key.Digit1, Key.Digit2, ... Key.Digit9 };
          int count = Mathf.Min(rules.Length, keys.Length);
          var eventRules = new EventRule[count];
          for (int i = 0; i < count; i++)
              eventRules[i] = rules[i].ToRule(keys[i]);
          EventManager.Schedule.Events = eventRules;
      }
      
      LoadAndStartRace(carDataList);
  }
  ```
- **MIRROR**: `APPLY_RULES_TO_SCHEDULE`, `UNITY_RACEMANAGER_START`
- **IMPORTS**: `UnityEngine.InputSystem` (for Key enum)
- **GOTCHA**: 
  - Reuse same Digit1-9 key assignment pattern from SurveyConfigManager
  - Rules must be applied BEFORE LoadAndStartRace, because that method calls EventManager.Activate()
  - Max 9 rules (limited by keyboard bindings)
- **VALIDATE**: Calling with rules → EventSchedule populated correctly → events trigger with keyboard

---

## Testing Strategy

### Unit Tests (Unity — EditMode)

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Parse valid full JSON | JSON with 2 cars, 3 rules | Success, 2 cars, 3 rules | No |
| Parse empty carData | JSON with `carData: []` | Success, 0 cars | Yes |
| Parse null JSON | null | Error result | Yes |
| Parse empty string | "" | Error result | Yes |
| Parse malformed JSON | "{ invalid" | Error result | Yes |
| Attributes mapped correctly | Car with 3 attributes | All key-value pairs match | No |
| EventRules deserialized | Rule with operator=2, weather=1 | ComparisonOperator.Contains, WeatherType.Snow | No |
| Missing eventRules field | JSON without eventRules | Empty array, no crash | Yes |

### Integration Tests (Web App API — manual/curl)

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Export with responses | Survey with 3 responses + mappings | 3 carData entries with correct attributes | No |
| Export no responses | Survey with 0 responses | Empty carData array | Yes |
| Export no mappings | Survey with responses but no mappings | carData with teamName only, empty attributes | Yes |
| Export unauthorized | No auth token | 401 error | Yes |
| Export wrong user | Different user's survey | 404 error | Yes |
| Lookup transform | Response "Yes" with lookup mapping | Mapped value "yes" | No |
| Numeric transform | Response "7.5" | Value "7.5" passed through | No |
| Numeric transform invalid | Response "abc" | DefaultValue used | Yes |
| Direct transform | Response "French" | Value "French" | No |

### Edge Cases Checklist
- [x] Empty input (no responses)
- [x] Invalid types (malformed JSON)
- [x] Missing fields (no eventRules, no mappings)
- [x] Large input (50 responses — per success metrics)
- [ ] Concurrent access (SQLite WAL mode handles this)
- [ ] Network failure (N/A — manual JSON paste)
- [x] Permission denied (unauthorized export)

---

## Validation Commands

### Web App Server
```bash
cd web-app && npm run dev
```
EXPECT: Server starts on port 3001

### API Test — Export with Data
```bash
# 1. Register/login to get token
TOKEN=$(curl -s -X POST http://localhost:3001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"test123"}' | python3 -c "import sys,json; print(json.load(sys.stdin).get('data',{}).get('token',''))")

# 2. Create survey with mappings from template
# 3. Submit test response
# 4. Export
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:3001/api/surveys/1/export | python3 -m json.tool
```
EXPECT: JSON with populated carData array matching responses

### Frontend Build
```bash
cd web-app/client && npm run build
```
EXPECT: Build succeeds, dist/ updated

### Unity Tests
```
Unity Editor → Window → General → Test Runner → EditMode → Run All
```
EXPECT: All JsonImporterTests pass (+ existing tests still pass)

### Manual Validation
- [ ] Create survey in Web App with Accessibility template
- [ ] Submit 2 student responses via share link
- [ ] Click "Export for Unity" in editor page
- [ ] Download JSON file
- [ ] Verify JSON structure: configName, carData (2 entries), eventRules (3 entries)
- [ ] Copy JSON to clipboard
- [ ] In Unity Play Mode, paste JSON into import field
- [ ] Verify: 2 cars spawn with correct attributes
- [ ] Verify: Events 1-3 trigger correctly via keyboard
- [ ] Verify: Empty survey export shows warning message

---

## Acceptance Criteria
- [ ] `GET /api/surveys/:id/export` returns correct carData mapped from responses
- [ ] Each carData entry has teamName from response and attributes from mappings
- [ ] Transform types (direct, lookup, numeric) work correctly
- [ ] Frontend export button downloads valid JSON file
- [ ] Frontend shows response/car count summary
- [ ] Unity `JsonImporter.Parse()` correctly parses Web App export JSON
- [ ] Unity `SetupScreen` has "Import Web App JSON" button
- [ ] Pasting valid JSON in Unity starts race with correct cars and rules
- [ ] Invalid JSON shows error message
- [ ] All unit tests pass
- [ ] Existing tests not broken

## Completion Checklist
- [ ] Code follows discovered patterns (API response format, auth guard, etc.)
- [ ] Error handling matches codebase style (success/error envelope)
- [ ] No hardcoded values
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| JsonUtility field name mismatch (camelCase) | Medium | High — silent data loss | Use wrapper classes with exact field names matching JSON; add tests |
| Mapping logic divergence between C# and JS | Low | Medium — different CarData from same input | Port logic line-by-line from SurveyResponseMapper.cs; test with same inputs |
| Large response set (50 students) slow export | Low | Low — SQLite handles reads well | WAL mode already configured; single SELECT query |
| SetupScreen UI not wired in scene | Low | Low — graceful degradation | All references null-checked; buttons hidden if not assigned |

## Notes
- The export endpoint already exists as a stub in `web-app/src/routes/export.js:8` with `// Phase 4 will add response->CarData mapping here` comment
- `SurveyResponseMapper.cs` is the C# reference implementation to port to JavaScript — it's a pure static utility with no dependencies
- The response `answers_json` column stores answers as `{ "questionId": "answerValue" }` object (see `responses.js:58`)
- AttributeMapping transform types: "direct" (passthrough), "lookup" (map via LookupEntries), "numeric" (validate as number)
- Unity's `JsonUtility` uses field names directly (no `[JsonProperty]` attributes) — C# wrapper class fields must exactly match JSON keys
- The existing `LoadAndStartRace(List<CarData>)` on RaceManager is the final entry point — rules must be applied to EventSchedule before calling it
- Nginx already proxies `/api` to `web-app:3001` in Docker Compose setup (see `Deploy/nginx/nginx.conf:76-81`)
