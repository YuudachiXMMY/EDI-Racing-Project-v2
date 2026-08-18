# Plan: Survey Config System

## Summary
Create a portable JSON-based configuration system for surveys, attribute mappings, and event rules. Professors save/load/share complete survey configurations as JSON files stored in `Application.persistentDataPath/SurveyConfigs/`. Built-in templates provide starting points for common EDI themes.

## User Story
As a professor, I want to define survey questions, attribute mappings, and event rules in a portable configuration file, so that I can reuse, share, and iterate on my EDI racing demonstrations without editing code or CSV files.

## Problem -> Solution
Currently event rules live only in `EventSchedule` ScriptableObject (Editor-only) and sessions save a snapshot of rules but not the survey questions or attribute mappings that produced the car data. -> A single `SurveyConfig` JSON file captures the full pipeline: questions -> attribute mappings -> event rules, loadable at runtime without Unity Editor access.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md`
- **PRD Phase**: Phase 3 — Survey Config System
- **Estimated Files**: 6 created, 2 modified

---

## UX Design

### Before
N/A — internal change. No professor-facing UI in this phase (that's Phase 4).

### After
N/A — internal change. This phase creates the data layer and file I/O that Phase 4's UI will consume.

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Config persistence | EventSchedule ScriptableObject only | JSON files in persistentDataPath | Runtime-loadable |
| Session save | Captures cars + events + race settings | Also captures SurveyConfig reference | Future phases wire this |
| Templates | None | 3 built-in templates loadable via code | v1 parity, accessibility, diversity |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Data/CarData.cs` | all | AttributeEntry pattern — the serialization workaround for Dictionary |
| P0 | `Assets/Scripts/Events/EventRule.cs` | all | Rule struct to reference in SurveyConfig |
| P0 | `Assets/Scripts/Data/SessionData.cs` | all | SavedEventRule pattern, SessionData shape, how enums are stored as int |
| P0 | `Assets/Scripts/Data/SessionManager.cs` | all | JSON file I/O pattern to mirror |
| P1 | `Assets/Scripts/Events/EventSchedule.cs` | all | Default event rules to reproduce as templates |
| P1 | `Assets/Scripts/Events/ComparisonOperator.cs` | all | Enum values for rules |
| P1 | `Assets/Scripts/Events/WeatherType.cs` | all | Enum values for rules |
| P1 | `Assets/Scripts/Data/CsvParser.cs` | all | Header-based parsing to understand attribute flow |
| P2 | `Assets/Scripts/Race/RaceManager.cs` | 216-270 | BuildSessionData / LoadSession — where SurveyConfig will integrate |
| P2 | `Assets/Scripts/Network/NetworkMessages.cs` | all | NetAttribute/NetCarData serialization pattern |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| JsonUtility limitations | Unity docs | No Dictionary, no null, no polymorphism — use array-of-structs pattern (already established via AttributeEntry) |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Data/CarData.cs:1-14
// Structs: PascalCase, [Serializable], public fields
[Serializable]
public struct AttributeEntry
{
    public string Key;
    public string Value;
}
```

### SERIALIZATION_WORKAROUND
```csharp
// SOURCE: Assets/Scripts/Data/SessionData.cs:66-107
// Enums stored as int for JsonUtility compatibility
// Separate "Saved" struct without runtime state or Unity-specific types
[Serializable]
public struct SavedEventRule
{
    public int Operator;  // (int)ComparisonOperator
    public int Weather;   // (int)WeatherType
    // ... no Key field (UI concern), no HasBeenTriggered (runtime state)
}
```

### FILE_IO_PATTERN
```csharp
// SOURCE: Assets/Scripts/Data/SessionManager.cs:24-35
// Directory.CreateDirectory before write, timestamp in filename
string dir = GetSaveDirectory();
Directory.CreateDirectory(dir);
string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
string json = JsonUtility.ToJson(session, true);
File.WriteAllText(path, json);
```

### LOAD_PATTERN
```csharp
// SOURCE: Assets/Scripts/Data/SessionManager.cs:40-53
// Null/existence check, log on success
if (string.IsNullOrEmpty(path) || !File.Exists(path))
{
    Debug.LogWarning($"[SessionManager] Session file not found: {path}");
    return null;
}
string json = File.ReadAllText(path);
var session = JsonUtility.FromJson<SessionData>(json);
Debug.Log($"[SessionManager] Session loaded: {path}");
```

### MONOBEHAVIOUR_MANAGER
```csharp
// SOURCE: Assets/Scripts/Data/SessionManager.cs:14-17
// MonoBehaviour with [Header] config, public methods, Debug.Log tracing
public class SessionManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Subfolder name within Application.persistentDataPath")]
    public string SaveFolder = "Sessions";
```

### STATIC_UTILITY
```csharp
// SOURCE: Assets/Scripts/Data/CsvParser.cs:9
// Static class, no state, pure functions
public static class CsvParser
{
    public static List<CarData> Parse(string csvContent) { ... }
}
```

### SCRIPTABLEOBJECT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Events/EventSchedule.cs:9
[CreateAssetMenu(fileName = "EventSchedule", menuName = "EDI Racing/Event Schedule")]
public class EventSchedule : ScriptableObject
```

### DEBUG_LOG_PREFIX
```csharp
// SOURCE: multiple files
// All logs use [ClassName] prefix
Debug.Log($"[SessionManager] Session saved: {path}");
Debug.LogWarning($"[SessionManager] Session file not found: {path}");
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Data/SurveyQuestion.cs` | CREATE | Question type enum + SurveyQuestion struct |
| `Assets/Scripts/Data/AttributeMapping.cs` | CREATE | Mapping from question response to car attribute |
| `Assets/Scripts/Data/SurveyConfig.cs` | CREATE | Top-level config container (questions + mappings + rules) |
| `Assets/Scripts/Data/SurveyConfigManager.cs` | CREATE | MonoBehaviour for JSON file I/O (save/load/list/templates) |
| `Assets/Scripts/Data/SurveyTemplates.cs` | CREATE | Static class with built-in template definitions |
| `Assets/Scripts/Data/SurveyResponseMapper.cs` | CREATE | Static utility: applies AttributeMappings to survey responses -> CarData |
| `Assets/Scripts/Data/SessionData.cs` | UPDATE | Add SurveyConfigName field to SessionData |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Wire SurveyConfigManager reference, save/load config name in sessions |

## NOT Building

- Professor-facing UI for survey builder (Phase 4)
- Student-facing survey UI (Phase 5)
- WebSocket message types for survey distribution (Phase 5)
- Compound rule conditions (AND/OR) — Could scope per PRD
- Survey branching/conditional logic — Won't scope per PRD

---

## Step-by-Step Tasks

### Task 1: Create QuestionType enum and SurveyQuestion struct
- **ACTION**: Create `Assets/Scripts/Data/SurveyQuestion.cs`
- **IMPLEMENT**:
  ```csharp
  using System;

  public enum QuestionType
  {
      Text,            // Free-form text input
      MultipleChoice,  // Select from predefined options
      Numeric          // Number within a range (slider or input)
  }

  [Serializable]
  public struct SurveyQuestion
  {
      public string Id;
      public string Text;
      public int Type; // (int)QuestionType — JsonUtility compat
      public string[] Options; // for MultipleChoice
      public float MinValue;   // for Numeric
      public float MaxValue;   // for Numeric
      public bool Required;

      public QuestionType QuestionType
      {
          get => (QuestionType)Type;
          set => Type = (int)value;
      }
  }
  ```
- **MIRROR**: NAMING_CONVENTION (PascalCase struct), SERIALIZATION_WORKAROUND (enum as int)
- **IMPORTS**: `System`
- **GOTCHA**: Store `QuestionType` as `int` field for JsonUtility, with property accessor for convenience. Mirror the `SavedEventRule` pattern where enums are `int`.
- **VALIDATE**: File compiles in Unity Editor. `JsonUtility.ToJson(new SurveyQuestion { QuestionType = QuestionType.MultipleChoice })` serializes correctly.

### Task 2: Create AttributeMapping struct
- **ACTION**: Create `Assets/Scripts/Data/AttributeMapping.cs`
- **IMPLEMENT**:
  ```csharp
  using System;

  [Serializable]
  public struct AttributeMapping
  {
      public string QuestionId;
      public string AttributeName;
      public string DefaultValue;

      // Transform types:
      // "direct" — raw response becomes attribute value
      // "lookup" — map specific responses to values via LookupEntries
      // "numeric" — parse as number, clamp to question range
      public string TransformType;

      // For "lookup" transform: maps response text -> attribute value
      public AttributeEntry[] LookupEntries;
  }
  ```
- **MIRROR**: NAMING_CONVENTION (struct with public fields), reuses `AttributeEntry` from CarData.cs
- **IMPORTS**: `System`
- **GOTCHA**: Reuse existing `AttributeEntry` struct for `LookupEntries` instead of creating a new type — keeps serialization consistent.
- **VALIDATE**: File compiles. Struct is serializable by JsonUtility.

### Task 3: Create SurveyConfig class
- **ACTION**: Create `Assets/Scripts/Data/SurveyConfig.cs`
- **IMPLEMENT**:
  ```csharp
  using System;

  [Serializable]
  public class SurveyConfig
  {
      public string ConfigName = "";
      public string Description = "";
      public string CreatedAt = "";
      public string Version = "1.0";

      public SurveyQuestion[] Questions = Array.Empty<SurveyQuestion>();
      public AttributeMapping[] Mappings = Array.Empty<AttributeMapping>();
      public SavedEventRule[] Rules = Array.Empty<SavedEventRule>();
  }
  ```
- **MIRROR**: Follows `SessionData` pattern — class (not struct) with default initializers, uses `SavedEventRule` from SessionData.cs for rules
- **IMPORTS**: `System`
- **GOTCHA**: Must be `class` not `struct` for `JsonUtility.FromJson<T>()` to work with nullable return. Uses existing `SavedEventRule` to avoid duplicating event rule serialization. `Array.Empty<T>()` for all arrays to prevent null issues.
- **VALIDATE**: `JsonUtility.ToJson(new SurveyConfig())` produces valid JSON. `JsonUtility.FromJson<SurveyConfig>(json)` round-trips correctly.

### Task 4: Create SurveyTemplates static class
- **ACTION**: Create `Assets/Scripts/Data/SurveyTemplates.cs`
- **IMPLEMENT**: Static class with methods returning pre-configured `SurveyConfig` instances:
  1. `V1Parity()` — reproduces the original ENGG*1100 setup (teamName, colorIndex, functions columns + 7 default event rules from EventSchedule defaults)
  2. `AccessibilitySurvey()` — questions about disability, assistive tech, language; rules that penalize/boost based on responses
  3. `DiversitySurvey()` — questions about gender, ethnicity, first-generation status; rules demonstrating systemic barriers
- **MIRROR**: STATIC_UTILITY pattern (like CsvParser, RuleEngine)
- **IMPORTS**: `System`, `UnityEngine.InputSystem` (for Key enum in rule conversion)
- **GOTCHA**: Template rules use `SavedEventRule` (no Key bindings) — keys are assigned when loaded into EventSchedule. Default TriggerKey mapping: rules[0]=Digit1 through rules[n]=Digit(n+1), max 9.
- **VALIDATE**: Each template produces a valid SurveyConfig that serializes/deserializes without data loss.

### Task 5: Create SurveyResponseMapper static utility
- **ACTION**: Create `Assets/Scripts/Data/SurveyResponseMapper.cs`
- **IMPLEMENT**: Static class that converts survey responses into CarData using AttributeMappings:
  ```csharp
  public static class SurveyResponseMapper
  {
      // responses: key=questionId, value=answer text
      public static CarData MapResponses(string teamName,
          AttributeEntry[] responses, AttributeMapping[] mappings)
      {
          // For each mapping:
          //   1. Find the response for mapping.QuestionId
          //   2. Apply transform (direct/lookup/numeric)
          //   3. Add result as AttributeEntry with mapping.AttributeName
          // Return new CarData with teamName + generated attributes
      }
  }
  ```
- **MIRROR**: STATIC_UTILITY (CsvParser pattern), uses `AttributeEntry[]` not Dictionary
- **IMPORTS**: `System`, `System.Collections.Generic`, `System.Linq`
- **GOTCHA**: Responses come as `AttributeEntry[]` (not Dictionary) for JsonUtility compat. Use case-insensitive matching for QuestionId lookup. If a response is missing and mapping has a DefaultValue, use the default.
- **VALIDATE**: Given a set of responses and mappings, produces correct CarData with expected attributes. Empty responses produce CarData with defaults.

### Task 6: Create SurveyConfigManager MonoBehaviour
- **ACTION**: Create `Assets/Scripts/Data/SurveyConfigManager.cs`
- **IMPLEMENT**: MonoBehaviour mirroring SessionManager patterns:
  ```csharp
  public class SurveyConfigManager : MonoBehaviour
  {
      [Header("Configuration")]
      public string SaveFolder = "SurveyConfigs";

      // Active config loaded for current session
      public SurveyConfig ActiveConfig { get; private set; }

      public string SaveConfig(SurveyConfig config) { ... }
      public SurveyConfig LoadConfig(string path) { ... }
      public string[] GetSavedConfigPaths() { ... }
      public SurveyConfig LoadTemplate(string templateName) { ... }
      public string[] GetTemplateNames() { ... }
      public void SetActiveConfig(SurveyConfig config) { ... }

      // Apply active config's rules to an EventSchedule
      public void ApplyRulesToSchedule(EventSchedule schedule) { ... }

      private string GetSaveDirectory() { ... }
  }
  ```
- **MIRROR**: MONOBEHAVIOUR_MANAGER (SessionManager), FILE_IO_PATTERN, LOAD_PATTERN, DEBUG_LOG_PREFIX
- **IMPORTS**: `System`, `System.IO`, `System.Linq`, `UnityEngine`, `UnityEngine.InputSystem`
- **GOTCHA**: `ApplyRulesToSchedule()` must assign TriggerKey (Digit1-Digit9) when converting SavedEventRule to EventRule, matching the EventSchedule convention. Use `JsonUtility.ToJson(config, true)` for pretty-printed JSON. WebGL: `File` operations work in Unity WebGL via Emscripten's virtual filesystem (IndexedDB-backed), same as SessionManager.
- **VALIDATE**: Save -> Load round-trip preserves all data. GetSavedConfigPaths returns correct files sorted by date. Templates load without error.

### Task 7: Update SessionData to reference SurveyConfig
- **ACTION**: Update `Assets/Scripts/Data/SessionData.cs`
- **IMPLEMENT**: Add `SurveyConfigName` field to `SessionData`:
  ```csharp
  public class SessionData
  {
      public string SessionName = "";
      public string CreatedAt = "";
      public string SurveyConfigName = ""; // NEW: name of the SurveyConfig used
      public CarData[] Cars = Array.Empty<CarData>();
      ...
  }
  ```
- **MIRROR**: Existing SessionData field pattern (public field with default)
- **IMPORTS**: None new
- **GOTCHA**: Only store the config NAME (not the full config object) — the config file lives separately and may be updated between sessions. Backward-compatible: old session JSONs without this field will deserialize with the default empty string.
- **VALIDATE**: Existing session save/load still works (JsonUtility ignores missing fields). New field round-trips.

### Task 8: Wire SurveyConfigManager into RaceManager
- **ACTION**: Update `Assets/Scripts/Race/RaceManager.cs`
- **IMPLEMENT**: Add optional SurveyConfigManager reference and wire it into session save/load:
  ```csharp
  [Header("Survey (Optional)")]
  public SurveyConfigManager SurveyConfigManager;
  ```
  In `BuildSessionData()`: set `session.SurveyConfigName` from `SurveyConfigManager.ActiveConfig?.ConfigName`.
  In `LoadSession()`: if `SurveyConfigName` is not empty and `SurveyConfigManager` exists, log which config was used (actual config reload is Phase 4 UI concern).
- **MIRROR**: Existing optional reference pattern (like `NetworkSync` field)
- **IMPORTS**: None new
- **GOTCHA**: SurveyConfigManager is optional — guard all access with null checks. Don't change existing behavior when SurveyConfigManager is null.
- **VALIDATE**: RaceManager works identically when SurveyConfigManager is null. When present, session saves include SurveyConfigName.

---

## Testing Strategy

### Unit Tests

This is a Unity project — no CLI test pipeline. Validation is via Unity Editor Play Mode and console checks.

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| SurveyConfig round-trip | Create config, serialize, deserialize | All fields match | No |
| Empty config round-trip | Default `new SurveyConfig()` | No null errors, empty arrays | Yes |
| Template V1Parity | Load template | 0 questions, 0 mappings, 7 rules matching EventSchedule defaults | No |
| Template Accessibility | Load template | 3+ questions, 3+ mappings, 3+ rules | No |
| SurveyResponseMapper direct | Response "English" + direct mapping | Attribute value = "English" | No |
| SurveyResponseMapper lookup | Response "male" + lookup {male->0, female->1} | Attribute value = "0" | No |
| SurveyResponseMapper missing | No response for mapping | DefaultValue used | Yes |
| SurveyResponseMapper empty | No responses at all | CarData with teamName only | Yes |
| Config save/load | Save config, load by path | Identical config | No |
| GetSavedConfigPaths | Multiple configs saved | All paths returned, newest first | No |
| ApplyRulesToSchedule | Config with 3 rules | EventSchedule.Events has 3 rules with Digit1-3 keys | No |
| ApplyRulesToSchedule overflow | Config with 12 rules | Only first 9 rules applied (Digit1-9 limit) | Yes |
| Old session compat | Load session JSON without SurveyConfigName | SurveyConfigName = "" | Yes |

### Edge Cases Checklist
- [x] Empty input (empty config, no questions, no mappings)
- [x] Maximum size (12+ rules exceeding Digit1-9 keys)
- [x] Invalid types (unrecognized TransformType defaults to "direct")
- [ ] Concurrent access — N/A, single-threaded Unity
- [ ] Network failure — N/A for this phase
- [x] Permission denied (persistentDataPath always writable)
- [x] Backward compatibility (old sessions without SurveyConfigName)

---

## Validation Commands

### Static Analysis
```
# Open Unity Editor, check Console for compilation errors
# All scripts in Assets/Scripts/Data/ and Assets/Scripts/Race/ must compile
```
EXPECT: Zero compilation errors

### Unit Tests
```
# In Unity Play Mode, verify via Debug.Log:
# 1. SurveyConfigManager.SaveConfig() -> file appears in persistentDataPath/SurveyConfigs/
# 2. SurveyConfigManager.LoadConfig() -> data matches
# 3. SurveyTemplates.V1Parity() -> 7 rules match EventSchedule defaults
# 4. SurveyResponseMapper.MapResponses() -> correct CarData
```
EXPECT: All Debug.Log outputs show correct data

### Manual Validation
- [ ] Unity Editor compiles with zero errors
- [ ] SurveyConfig JSON serializes with pretty-print formatting
- [ ] SurveyConfig JSON deserializes without data loss
- [ ] All 3 templates produce valid configs
- [ ] SurveyConfigManager saves to `persistentDataPath/SurveyConfigs/`
- [ ] SurveyConfigManager lists saved configs sorted by date
- [ ] SurveyResponseMapper correctly handles direct, lookup, and numeric transforms
- [ ] SessionData backward-compatible with old session files
- [ ] RaceManager session save includes SurveyConfigName when SurveyConfigManager is present
- [ ] RaceManager works normally when SurveyConfigManager is null
- [ ] `ApplyRulesToSchedule` correctly assigns TriggerKeys Digit1-9
- [ ] WebGL build still compiles (File I/O works via Emscripten)

---

## Acceptance Criteria
- [ ] All 8 tasks completed
- [ ] Zero Unity compilation errors
- [ ] SurveyConfig JSON round-trips without data loss
- [ ] 3 built-in templates available and valid
- [ ] SurveyResponseMapper correctly transforms responses to CarData
- [ ] SurveyConfigManager saves/loads/lists configs
- [ ] SessionData preserves SurveyConfigName
- [ ] No breaking changes to existing race flow
- [ ] No hardcoded values (use constants or config)

## Completion Checklist
- [ ] Code follows discovered patterns (NAMING, SERIALIZATION, FILE_IO)
- [ ] All Debug.Log uses [ClassName] prefix
- [ ] All arrays initialized to Array.Empty<T>() (no nulls)
- [ ] Enums stored as int in serializable structs
- [ ] No namespaces (global namespace per convention)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| JsonUtility silent data loss on nested arrays | LOW | MEDIUM | Test round-trip for every struct with nested arrays; SurveyQuestion.Options is string[], same pattern as CarData.Attributes |
| Template rules diverge from EventSchedule defaults | LOW | LOW | Templates generated programmatically from same constants; test equivalence |
| File path issues in WebGL builds | LOW | MEDIUM | Mirror SessionManager pattern exactly; it already works in WebGL |
| Future phases need schema changes to SurveyConfig | MEDIUM | LOW | Version field allows migration; keep schema minimal for Phase 3 |

## Notes
- `SurveyConfig` uses `SavedEventRule` (from SessionData.cs) for rules — avoids duplicating the event rule serialization format
- Templates don't include TriggerKey bindings — keys are assigned in `ApplyRulesToSchedule()` when config is loaded into an EventSchedule
- The V1Parity template has 0 questions and 0 mappings (CSV import path doesn't need them) but has all 7 default event rules — this preserves the existing workflow
- `SurveyResponseMapper` is designed for Phase 5 (student survey) consumption but implemented now to validate the mapping data model
- `SurveyConfigManager` is optional on RaceManager (null-safe) to avoid breaking existing scene setups
