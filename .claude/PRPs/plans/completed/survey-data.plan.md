# Plan: Survey & Data Pipeline

## Summary
Implement the full data pipeline: session save/load as JSON (car data, event configs, race settings, results), post-race CSV export, runtime CSV import, and race lifecycle management (reset, finish detection). Builds on the existing CsvParser and CarData model from Phase 1.

## User Story
As a professor, I want to save my race session (data, events, results) and export results as CSV, so that I can replay sessions across classes and analyze race outcomes for discussion.

## Problem -> Solution
CSV import exists but race sessions are ephemeral (lost on reload). No way to export results. -> Full session persistence (JSON save/load) and results export (CSV download).

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 3 — Survey & Data
- **Estimated Files**: 4 new, 3 modified (~350 lines)

---

## UX Design

### Before
```
1. Game loads -> auto-reads TextAsset CSV -> spawns cars -> race runs
2. Race ends -> results only visible in Debug.Log
3. Close game -> all data lost
4. No way to import different CSV at runtime
```

### After
```
1. Game loads -> auto-reads TextAsset CSV -> spawns cars -> race runs
2. During/after race -> F5 saves session JSON to persistent storage
3. F9 loads most recent session -> resets race -> replays with saved data
4. F10 exports results CSV to persistent storage
5. Race auto-detects first car finishing all laps
6. All session data persisted across game restarts
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Session persistence | None — ephemeral | JSON save/load via F5/F9 | Uses Application.persistentDataPath |
| Results export | Debug.Log only | CSV file via F10 | Rank, team, laps, time, events |
| Race lifecycle | Auto-start, no end detection | Start, finish detection, reset capability | First car crossing finish triggers "race complete" |
| Data input | TextAsset only (editor drag-drop) | TextAsset + runtime string input | Prepares for Phase 5 WebSocket CSV relay |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/Data/CarData.cs` | all | Data model to serialize in session |
| P0 (critical) | `Assets/Scripts/Data/CsvParser.cs` | all | Existing parser pattern to mirror |
| P0 (critical) | `Assets/Scripts/Race/RaceManager.cs` | all | Integration point — must understand initialization flow |
| P0 (critical) | `Assets/Scripts/Race/ScoreManager.cs` | all | Results source — need structured output |
| P1 (important) | `Assets/Scripts/Events/RaceEventConfig.cs` | all | Event config struct to serialize |
| P1 (important) | `Assets/Scripts/Events/EventSchedule.cs` | all | ScriptableObject whose values we snapshot |
| P1 (important) | `Assets/Scripts/Race/RaceConfig.cs` | all | ScriptableObject whose values we snapshot |
| P1 (important) | `Assets/Scripts/Events/EventManager.cs` | all | OnEventTriggered event for logging |
| P2 (reference) | `Assets/Scripts/Car/CarIdentity.cs` | all | Runtime car state for results collection |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| JsonUtility | Unity docs | Handles `[Serializable]` classes/structs with primitive fields, arrays, Lists. Cannot do dictionaries or top-level arrays. Enums serialize as int. |
| Application.persistentDataPath | Unity docs | WebGL: maps to IndexedDB via `/idbfs/`. Editor/Standalone: platform-specific app data folder. `System.IO.File` works on all platforms through Unity's VFS. |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Data/CarData.cs:1-20
// Class = PascalCase matching filename, struct with [Serializable]
// Public fields for Inspector/serialization, PascalCase
[Serializable]
public struct CarData
{
    public string TeamName;
    public int ColorIndex;
    public string[] Functions;
}
```

### STATIC_UTILITY_CLASS
```csharp
// SOURCE: Assets/Scripts/Data/CsvParser.cs:1-38
// Static class, no MonoBehaviour, pure functions
// Using minimal: System.Collections.Generic, System.Linq
public static class CsvParser
{
    public static List<CarData> Parse(string csvContent) { ... }
}
```

### ERROR_HANDLING
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:27-28
// Null guard before operations, early return pattern
if (DefaultCsvData != null)
{
    LoadAndStartRace(DefaultCsvData.text);
}

// SOURCE: Assets/Scripts/Data/CsvParser.cs:13
// Empty input guard returns empty collection
if (string.IsNullOrEmpty(csvContent)) return cars;

// SOURCE: Assets/Scripts/Race/CarSpawner.cs:37
// Clamp indices to valid range
int prefabIndex = Mathf.Clamp(data.ColorIndex, 0, CarPrefabs.Length - 1);
```

### LOGGING_PATTERN
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:36,56
// [ClassName] prefix, f-string interpolation, counts in messages
Debug.Log($"[RaceManager] Parsed {carDataList.Count} cars from CSV");
Debug.Log($"[RaceManager] Race started with {spawnedCars.Count} cars");

// SOURCE: Assets/Scripts/Events/EventManager.cs:88
// Detailed event logging with counts and parameters
Debug.Log($"[EventManager] '{config.DisplayName}' triggered: {affectedCount}/{registeredCars.Count} cars affected");

// SOURCE: Assets/Scripts/Race/CarSpawner.cs:124
// Warning for non-fatal issues
Debug.LogWarning($"[CarSpawner] No NavMesh found within {NavMeshSampleRadius}m ...");
```

### MONOBEHAVIOUR_PATTERN
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:9-14
// [Header] groups, public references, no [SerializeField] private
[Header("References")]
public CarSpawner CarSpawner;
public LapTracker LapTracker;
public ScoreManager ScoreManager;

// SOURCE: Assets/Scripts/Events/EventManager.cs:46-57
// Update() with early-return guard, keyboard input via new Input System
private void Update()
{
    if (!isActive || Schedule == null) return;
    if (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame)
    { ... }
}
```

### EVENT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Events/EventManager.cs:18
// C# event with Action<> delegate, PascalCase with On prefix
public event Action<RaceEventConfig, int> OnEventTriggered;
// Invoked with null-conditional:
OnEventTriggered?.Invoke(config, affectedCount);
```

### SCRIPTABLEOBJECT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Race/RaceConfig.cs:7-8
// CreateAssetMenu with consistent naming, menuName = "EDI Racing/..."
[CreateAssetMenu(fileName = "RaceConfig", menuName = "EDI Racing/Race Config")]
public class RaceConfig : ScriptableObject
{
    [Header("Car Settings")]
    public float DefaultSpeed = 40f;
    ...
}
```

### INITIALIZE_PATTERN
```csharp
// SOURCE: Assets/Scripts/Car/CarIdentity.cs:20-29
// External Initialize() method, not Awake/Start
public void Initialize(CarData data)
{
    TeamName = data.TeamName;
    ColorIndex = data.ColorIndex;
    Functions = data.Functions;
    ...
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Data/SessionData.cs` | CREATE | Serializable data containers for session JSON |
| `Assets/Scripts/Data/SessionManager.cs` | CREATE | MonoBehaviour: save/load sessions, export results |
| `Assets/Scripts/Data/ResultsExporter.cs` | CREATE | Static class: format race results as CSV string |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Add SessionManager ref, event logging, race finish detection, reset, overloaded start method |
| `Assets/Scripts/Race/ScoreManager.cs` | UPDATE | Add Clear() and GetResults() returning structured data |
| `Assets/Scripts/Events/EventManager.cs` | UPDATE | Add ClearRegisteredCars() method for race reset |

## NOT Building

- In-game survey UI (deferred to Phase 4 / future iteration)
- File picker dialog (deferred to Phase 6 WebGL build — runtime CSV import via UI)
- Multiple save slot UI (deferred to Phase 4)
- Cloud/network session sync (deferred to Phase 5)
- Session auto-save on race end (manual save only via F5)
- Custom question configuration in session JSON (v2.1 scope)

---

## Step-by-Step Tasks

### Task 1: Create SessionData models
- **ACTION**: Create `Assets/Scripts/Data/SessionData.cs` with all serializable data containers
- **IMPLEMENT**:
  ```csharp
  // SessionData — top-level container for JSON serialization
  [Serializable]
  public class SessionData
  {
      public string SessionName;
      public string CreatedAt;    // ISO 8601 timestamp
      public CarData[] Cars;
      public SavedEventConfig[] Events;
      public SavedRaceConfig RaceSettings;
      public RaceResults Results; // null until race produces results
  }

  // SavedRaceConfig — snapshot of RaceConfig ScriptableObject values
  // (ScriptableObjects can't be serialized standalone by JsonUtility)
  [Serializable]
  public struct SavedRaceConfig
  {
      public float DefaultSpeed;
      public float AngularSpeed;
      public float Acceleration;
      public int TotalLaps;
      public float CarScale;

      public static SavedRaceConfig FromScriptableObject(RaceConfig config) { ... }
      public void ApplyTo(RaceConfig config) { ... }
  }

  // SavedEventConfig — serializable copy of RaceEventConfig without runtime state
  [Serializable]
  public struct SavedEventConfig
  {
      public int EventType;      // cast from RaceEventType enum
      public string DisplayName;
      public float SpeedDelta;
      public float Duration;
      public int TargetColorIndex;
      public string TargetFunction;
      public int NameLengthThreshold;
      public bool AllowRepeat;

      public static SavedEventConfig FromConfig(RaceEventConfig config) { ... }
      public RaceEventConfig ToConfig(Key triggerKey) { ... }
  }

  // RaceResults — collected after race completes or on-demand
  [Serializable]
  public class RaceResults
  {
      public CarResult[] Rankings;
      public EventLogEntry[] EventLog;
      public float TotalRaceTime;
  }

  // CarResult — individual car's final standing
  [Serializable]
  public struct CarResult
  {
      public int Rank;
      public string TeamName;
      public int ColorIndex;
      public int LapsCompleted;
      public int CheckpointsPassed;
      public float TotalTime;
  }

  // EventLogEntry — record of a triggered event
  [Serializable]
  public struct EventLogEntry
  {
      public float Timestamp;
      public string EventName;
      public int AffectedCount;
      public int TotalCars;
  }
  ```
- **MIRROR**: NAMING_CONVENTION (PascalCase struct with `[Serializable]`), same style as `CarData`
- **IMPORTS**: `using System;` for `[Serializable]`, `using UnityEngine;` for `RaceConfig`/`Key` references
- **GOTCHA**: `JsonUtility` cannot serialize `null` class fields — initialize `Results` to `new RaceResults()` with empty arrays, not `null`. Use `Array.Empty<T>()` for empty arrays.
- **GOTCHA**: `RaceEventConfig.TriggerKey` is `UnityEngine.InputSystem.Key` — do NOT include it in `SavedEventConfig`. Key bindings are UI concerns, not data. When loading, re-assign keys from the default `EventSchedule` or by index (Digit1-Digit7).
- **VALIDATE**: File compiles in Unity without errors. `JsonUtility.ToJson(new SessionData())` produces valid JSON.

### Task 2: Create ResultsExporter utility
- **ACTION**: Create `Assets/Scripts/Data/ResultsExporter.cs` as a static utility class
- **IMPLEMENT**:
  ```csharp
  public static class ResultsExporter
  {
      // Format rankings as CSV string
      public static string ExportRankingsCsv(RaceResults results)
      {
          var sb = new StringBuilder();
          sb.AppendLine("Rank,TeamName,ColorIndex,LapsCompleted,CheckpointsPassed,Time");
          foreach (var car in results.Rankings)
          {
              sb.AppendLine($"{car.Rank},{EscapeCsv(car.TeamName)},{car.ColorIndex},{car.LapsCompleted},{car.CheckpointsPassed},{car.TotalTime:F2}");
          }
          return sb.ToString();
      }

      // Format event log as CSV string
      public static string ExportEventLogCsv(RaceResults results)
      {
          var sb = new StringBuilder();
          sb.AppendLine("Timestamp,EventName,AffectedCount,TotalCars");
          foreach (var entry in results.EventLog)
          {
              sb.AppendLine($"{entry.Timestamp:F2},{EscapeCsv(entry.EventName)},{entry.AffectedCount},{entry.TotalCars}");
          }
          return sb.ToString();
      }

      // Escape commas in team names for CSV safety
      private static string EscapeCsv(string value)
      {
          if (value.Contains(",") || value.Contains("\""))
              return "\"" + value.Replace("\"", "\"\"") + "\"";
          return value;
      }
  }
  ```
- **MIRROR**: STATIC_UTILITY_CLASS (matches `CsvParser` pattern — static class, pure functions, no MonoBehaviour)
- **IMPORTS**: `using System.Text;` for `StringBuilder`
- **GOTCHA**: Team names may contain commas (e.g., "The Diddy Party" is fine, but future data might have commas). Always escape CSV values.
- **VALIDATE**: `ResultsExporter.ExportRankingsCsv(testResults)` produces valid CSV that can be opened in Excel/Google Sheets.

### Task 3: Create SessionManager component
- **ACTION**: Create `Assets/Scripts/Data/SessionManager.cs` as a MonoBehaviour
- **IMPLEMENT**:
  ```csharp
  public class SessionManager : MonoBehaviour
  {
      [Header("Configuration")]
      [Tooltip("Subfolder name within Application.persistentDataPath for session files")]
      public string SaveFolder = "Sessions";

      // Save current session state to JSON file
      public string SaveSession(SessionData session)
      {
          // Ensure directory exists
          // Generate filename: session_{timestamp}.json
          // JsonUtility.ToJson(session, true) for pretty-print
          // File.WriteAllText(path, json)
          // Debug.Log($"[SessionManager] Session saved: {path}")
          // return path
      }

      // Load session from JSON file
      public SessionData LoadSession(string path)
      {
          // File.Exists check
          // File.ReadAllText(path)
          // JsonUtility.FromJson<SessionData>(json)
          // Debug.Log($"[SessionManager] Session loaded: {path}")
          // return session
      }

      // Find most recent session file
      public string FindLatestSession()
      {
          // Directory.GetFiles(saveDir, "*.json")
          // Sort by write time descending
          // Return first or null
      }

      // Export results CSV to file
      public string ExportResults(RaceResults results)
      {
          // Generate filename: results_{timestamp}.csv
          // ResultsExporter.ExportRankingsCsv(results)
          // Append event log section
          // File.WriteAllText(path, csv)
          // Debug.Log($"[SessionManager] Results exported: {path}")
          // return path
      }

      // List all saved sessions (for future UI)
      public string[] GetSavedSessionPaths()
      {
          // Directory.GetFiles(saveDir, "*.json")
          // Sort by write time descending
      }

      private string GetSaveDirectory()
      {
          return Path.Combine(Application.persistentDataPath, SaveFolder);
      }
  }
  ```
- **MIRROR**: MONOBEHAVIOUR_PATTERN (public fields with [Header], [Tooltip]), LOGGING_PATTERN (`[SessionManager]` prefix), ERROR_HANDLING (null/empty guards)
- **IMPORTS**: `using UnityEngine;`, `using System.IO;` for File/Directory/Path
- **GOTCHA**: `Directory.CreateDirectory()` is safe to call even if directory exists — no need for `Directory.Exists()` check first. But DO check `File.Exists()` before reading.
- **GOTCHA**: `Application.persistentDataPath` is empty string in some Editor edge cases — guard against it.
- **VALIDATE**: Save a session, verify JSON file appears in `Application.persistentDataPath/Sessions/`. Load it back, verify data matches.

### Task 4: Add Clear/GetResults to ScoreManager
- **ACTION**: Modify `Assets/Scripts/Race/ScoreManager.cs` — add `Clear()` and `CollectResults()` methods
- **IMPLEMENT**:
  ```csharp
  // Add to ScoreManager:

  public void Clear()
  {
      cars.Clear();
  }

  public RaceResults CollectResults(List<EventLogEntry> eventLog, float raceTime)
  {
      var ranked = GetRankedCars();
      var rankings = new CarResult[ranked.Count];
      for (int i = 0; i < ranked.Count; i++)
      {
          var c = ranked[i];
          rankings[i] = new CarResult
          {
              Rank = i + 1,
              TeamName = c.TeamName,
              ColorIndex = c.ColorIndex,
              LapsCompleted = c.CurrentLap,
              CheckpointsPassed = c.TotalCheckpointsPassed,
              TotalTime = c.CheckpointTime
          };
      }
      return new RaceResults
      {
          Rankings = rankings,
          EventLog = eventLog != null ? eventLog.ToArray() : Array.Empty<EventLogEntry>(),
          TotalRaceTime = raceTime
      };
  }
  ```
- **MIRROR**: Follows existing `GetRankedCars()` and `GetScoreboardText()` method style
- **IMPORTS**: Add `using System;` for `Array.Empty<T>()`
- **GOTCHA**: `CollectResults()` can be called at any time (mid-race or post-race) — it snapshots current state.
- **VALIDATE**: After a race runs for a few laps, `CollectResults()` returns populated `RaceResults` with correct rankings matching `GetScoreboardText()` output.

### Task 5: Add ClearRegisteredCars to EventManager
- **ACTION**: Modify `Assets/Scripts/Events/EventManager.cs` — add `ClearRegisteredCars()` method
- **IMPLEMENT**:
  ```csharp
  // Add to EventManager:
  public void ClearRegisteredCars()
  {
      registeredCars.Clear();
      isActive = false;
  }
  ```
- **MIRROR**: Follows `Activate()`/`Deactivate()` pattern — simple state mutation with clear name
- **IMPORTS**: None needed (already has all required usings)
- **GOTCHA**: Must also set `isActive = false` to prevent Update() from polling keyboard for events with no cars.
- **VALIDATE**: After calling `ClearRegisteredCars()`, `RegisteredCarCount` returns 0 and `IsActive` returns false.

### Task 6: Integrate SessionManager into RaceManager
- **ACTION**: Modify `Assets/Scripts/Race/RaceManager.cs` — add session management, event logging, race finish detection, reset capability
- **IMPLEMENT**:
  ```csharp
  // Add fields:
  [Header("Session")]
  public SessionManager SessionManager;

  private readonly List<EventLogEntry> eventLog = new List<EventLogEntry>();
  private float raceStartTime;
  private bool raceFinished;

  // Refactor LoadAndStartRace to support CarData list directly:
  public void LoadAndStartRace(List<CarData> carDataList)
  {
      // existing spawning + registration logic
      // + clear event log
      // + record raceStartTime = Time.time
      // + set raceFinished = false
  }

  // Keep the string overload as a wrapper:
  public void LoadAndStartRace(string csvContent)
  {
      var carDataList = CsvParser.Parse(csvContent);
      Debug.Log($"[RaceManager] Parsed {carDataList.Count} cars from CSV");
      LoadAndStartRace(carDataList);
  }

  // Add event logging to OnEventTriggered:
  private void OnEventTriggered(RaceEventConfig config, int affectedCount)
  {
      eventLog.Add(new EventLogEntry
      {
          Timestamp = Time.time - raceStartTime,
          EventName = config.DisplayName,
          AffectedCount = affectedCount,
          TotalCars = spawnedCars != null ? spawnedCars.Count : 0
      });
      // existing weather effect logic...
  }

  // Add race finish detection to OnCarCompletedLap:
  private void OnCarCompletedLap(CarIdentity car)
  {
      // existing logging...
      if (car.CurrentLap >= Config.TotalLaps && !raceFinished)
      {
          raceFinished = true;
          Debug.Log($"[RaceManager] Race complete! Winner: {car.TeamName}");
      }
  }

  // Add ResetRace method:
  public void ResetRace()
  {
      LapTracker.OnLapCompleted -= OnCarCompletedLap;
      if (EventManager != null)
          EventManager.OnEventTriggered -= OnEventTriggered;

      if (spawnedCars != null)
      {
          foreach (var car in spawnedCars)
              if (car != null) Destroy(car);
          spawnedCars.Clear();
      }
      ScoreManager.Clear();
      if (EventManager != null) EventManager.ClearRegisteredCars();
      eventLog.Clear();
      raceStarted = false;
      raceFinished = false;
  }

  // Add keyboard shortcuts in Update():
  // F5 = save session, F9 = load latest session, F10 = export results
  // (alongside existing F1 debug scoreboard)

  // Save session helper:
  private SessionData BuildSessionData()
  {
      return new SessionData
      {
          SessionName = "Race Session",
          CreatedAt = System.DateTime.Now.ToString("o"),
          Cars = /* collect CarData from CarIdentity components on spawnedCars */,
          Events = /* snapshot from EventManager.Schedule.Events */,
          RaceSettings = SavedRaceConfig.FromScriptableObject(Config),
          Results = ScoreManager.CollectResults(eventLog, Time.time - raceStartTime)
      };
  }
  ```
- **MIRROR**: MONOBEHAVIOUR_PATTERN, LOGGING_PATTERN, EVENT_PATTERN, ERROR_HANDLING
- **IMPORTS**: Add `using UnityEngine.InputSystem;` (already present), `using System;` for DateTime
- **GOTCHA**: When resetting, unsubscribe from events BEFORE destroying objects to prevent callbacks on destroyed objects.
- **GOTCHA**: `LoadAndStartRace(List<CarData>)` overload must contain the FULL initialization logic (spawning, registration, event activation). The string overload becomes a thin wrapper that parses then delegates.
- **GOTCHA**: `CarIdentity` components on spawned cars hold the original `CarData` values — extract them to build `Cars[]` for session save. Don't re-parse CSV.
- **VALIDATE**: 
  1. Press Play -> race starts normally (unchanged behavior)
  2. Press F5 -> JSON file appears in persistentDataPath/Sessions/
  3. Press F9 -> race resets, reloads from saved JSON, new race starts
  4. Press F10 -> CSV file appears in persistentDataPath/Sessions/
  5. Trigger events during race -> event log appears in saved session JSON

---

## Testing Strategy

### Unit Tests

Note: Unity Test Runner not yet configured in this project. Validation is manual for now. These test cases document expected behavior.

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| CsvParser handles existing v1 data | vehicleGroupData.csv content (37 lines) | 37 CarData entries with correct fields | No |
| SessionData round-trip | Create SessionData, ToJson, FromJson | All fields match original | No |
| SavedRaceConfig round-trip | Snapshot RaceConfig, apply to new RaceConfig | All values match | No |
| SavedEventConfig round-trip | Snapshot RaceEventConfig, convert back | All non-key fields match | No |
| ResultsExporter CSV format | RaceResults with 3 cars | Valid CSV with header + 3 data rows | No |
| ResultsExporter escapes commas | Team name "The Diddy Party" | No corruption in CSV output | Yes |
| ResultsExporter empty results | RaceResults with 0 cars, 0 events | CSV with header row only | Yes |
| SessionManager save + load | Save SessionData, load from same path | Loaded data matches saved data | No |
| SessionManager FindLatestSession | Multiple saved files | Returns most recently written file | No |
| SessionManager missing file | Load from non-existent path | Returns null, logs warning | Yes |
| RaceManager reset | Call ResetRace mid-race | All cars destroyed, scores cleared, events cleared | No |
| RaceManager re-start after reset | ResetRace then LoadAndStartRace | New race runs correctly | No |
| Race finish detection | All cars complete TotalLaps | raceFinished = true, winner logged | No |
| Event log recording | Trigger 3 events during race | eventLog has 3 entries with timestamps | No |

### Edge Cases Checklist
- [x] Empty CSV input (handled by existing CsvParser — returns empty list)
- [x] CSV with commas in team names for export (EscapeCsv handles it)
- [x] Session JSON with empty Results (initial save before race runs)
- [x] Load session when no files exist (FindLatestSession returns null)
- [x] Reset race when no race has started (guard against null spawnedCars)
- [x] Multiple resets in sequence (should be idempotent)
- [x] Save during race vs after race (both valid — captures current state)

---

## Validation Commands

### Static Analysis
```bash
# Unity compiles all C# on domain reload — open Unity Editor and check Console
# No standalone type checker needed for Unity C# (uses Roslyn via Unity)
```
EXPECT: Zero compile errors in Unity Console

### Manual Validation — Session Save
```
1. Open complete_track_demo scene
2. Press Play
3. Wait for cars to start racing (verify Debug.Log: "[RaceManager] Race started with 37 cars")
4. Press F5
5. Check Unity Console for "[SessionManager] Session saved: {path}"
6. Open the JSON file at the logged path
7. Verify: Cars array has 37 entries, Events array has 7 entries, RaceSettings populated, Results has Rankings
```
EXPECT: Valid JSON with all session data

### Manual Validation — Session Load
```
1. With a saved session from above
2. Press F9 during an active race
3. Verify: All current cars destroyed
4. Verify: New cars spawned from saved data
5. Verify: Debug.Log shows "[RaceManager] Race started with 37 cars"
6. Verify: Race runs normally with same car data
```
EXPECT: Race resets and restarts with saved data

### Manual Validation — Results Export
```
1. Run a race, trigger a few events (keys 1-7)
2. Press F10
3. Check Unity Console for "[SessionManager] Results exported: {path}"
4. Open the CSV file
5. Verify: Header row + 37 data rows with Rank, TeamName, etc.
6. Verify: Event log section present with triggered events
```
EXPECT: Valid CSV openable in Excel/Google Sheets

### Manual Validation — Race Finish Detection
```
1. Set TotalLaps to 1 in RaceConfig asset (for faster testing)
2. Press Play
3. Wait for first car to complete 1 lap
4. Verify: Debug.Log shows "[RaceManager] Race complete! Winner: {name}"
```
EXPECT: Race completion detected, winner announced

### Manual Validation — Round Trip
```
1. Start race -> trigger 2 events -> wait for some laps
2. F5 to save
3. Close Play mode
4. Press Play again
5. F9 to load saved session
6. Verify: Same 37 cars spawn, race runs
7. F5 to save again
8. Compare both JSON files — Cars and Events arrays should match
```
EXPECT: Data integrity preserved across save/load cycles

---

## Acceptance Criteria
- [x] `SessionData` serializable to/from JSON via `JsonUtility`
- [ ] Session save (F5) writes valid JSON to `Application.persistentDataPath/Sessions/`
- [ ] Session load (F9) resets current race and starts new race from saved data
- [ ] Results export (F10) writes valid CSV to `Application.persistentDataPath/Sessions/`
- [ ] Race finish detected when first car completes all laps
- [ ] Event log tracks all triggered events with timestamps
- [ ] RaceManager.ResetRace() cleanly destroys all cars and clears state
- [ ] LoadAndStartRace(List<CarData>) overload works for direct data input
- [ ] All 37 cars from vehicleGroupData.csv import correctly (unchanged from Phase 1)
- [ ] No compile errors in Unity

## Completion Checklist
- [ ] Code follows discovered patterns (PascalCase, [Header], [Tooltip], public fields, [ClassName] logs)
- [ ] Error handling matches codebase style (null guards, early returns, Debug.LogWarning for non-fatal)
- [ ] Logging follows `[ClassName]` prefix convention
- [ ] No hardcoded values (paths use Application.persistentDataPath, folder name is configurable)
- [ ] No unnecessary scope additions (no UI, no file picker, no network sync)
- [ ] Self-contained — no questions needed during implementation
- [ ] No `[SerializeField] private` — use public fields per project convention
- [ ] No namespaces — all classes in global namespace per project convention
- [ ] No interfaces — direct component references per project convention

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `JsonUtility` cannot serialize `null` class fields | HIGH | Session JSON has missing fields | Initialize all class fields to non-null defaults (empty arrays, empty strings) |
| `Application.persistentDataPath` varies by platform | LOW | Files saved in unexpected location | Log full path on every save; use consistent subfolder |
| Event unsubscribe order during reset | MEDIUM | NullReferenceException callbacks | Unsubscribe BEFORE destroying GameObjects |
| Large session files with 50+ cars | LOW | Slow save/load | JsonUtility is fast for simple data; 50 cars = ~5KB JSON |
| ScriptableObject values modified at runtime persist in Editor | MEDIUM | Saved event configs differ from asset defaults | Snapshot values into plain structs, don't modify ScriptableObjects directly |

## Notes
- The `Key` enum from Input System is intentionally excluded from `SavedEventConfig` — keyboard bindings are a UI concern that belongs in Phase 4. When loading a session, trigger keys are re-assigned from the `EventSchedule` ScriptableObject defaults (Digit1-Digit7 by index).
- `RaceManager.LoadAndStartRace(string)` becomes a thin wrapper around `LoadAndStartRace(List<CarData>)`. This prepares the codebase for Phase 5 (multi-client sync) where car data arrives via WebSocket, not CSV file.
- `SessionManager` is a MonoBehaviour (not static) to allow Inspector configuration and scene-level flexibility. It follows the same pattern as `ScoreManager` — referenced by `RaceManager`.
- The existing `TrackSetupEditor` looks for `Assets/Data/vehicleGroupData.csv` to wire as `DefaultCsvData`. This TextAsset pathway remains the primary import method until Phase 4 adds a file picker UI.
- Future Phase 4 will replace F5/F9/F10 keyboard shortcuts with proper UI buttons.
