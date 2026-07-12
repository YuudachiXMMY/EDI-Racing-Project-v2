# Plan: Integration & Testing (Phase 6)

## Summary
Wire all Phase 1-5 systems together into a complete end-to-end workflow, add comprehensive Edit Mode tests for pure-logic utilities, validate edge cases (empty responses, disconnections, 50-car stress), persist SurveyConfig within SessionData, write a v1 migration guide, and update documentation.

## User Story
As a professor, I want the complete survey-to-race-to-export workflow to function seamlessly so that I can run a full EDI demonstration from start to finish without encountering integration bugs.

## Problem → Solution
Individual subsystems (dynamic data model, rule engine, survey config, professor UI, student survey) are built and working in isolation → A fully integrated, tested, and documented system where all pieces cooperate end-to-end.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md`
- **PRD Phase**: Phase 6 - Integration & Testing
- **Estimated Files**: 8-12 files (3 new test files, 5-7 modifications)

---

## UX Design

### Before
N/A — internal integration change. All UI was built in Phase 4/5.

### After
N/A — internal change. The external behavior remains identical; this phase ensures all paths work correctly together.

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Session save/load | Does not persist SurveyConfig name in session | Persists full SurveyConfig reference, reloads rules on session load | SessionData already has `SurveyConfigName` field |
| CSV export | Exports rankings with dynamic attributes | Same, but verified working with survey-sourced data | Already implemented in ResultsExporter |
| Survey → Race flow | All pieces exist but not integration-tested | Validated end-to-end with automated tests | Gap: no automated tests exist |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/Race/RaceManager.cs` | all | Central orchestrator — all flows pass through here |
| P0 (critical) | `Assets/Scripts/Network/SurveyCollector.cs` | all | Survey response → CarData pipeline |
| P0 (critical) | `Assets/Scripts/Data/SurveyResponseMapper.cs` | all | Pure mapping logic — primary test target |
| P0 (critical) | `Assets/Scripts/Events/RuleEngine.cs` | all | Pure evaluation logic — primary test target |
| P1 (important) | `Assets/Scripts/Data/CsvParser.cs` | all | Pure parsing logic — primary test target |
| P1 (important) | `Assets/Scripts/Data/SessionData.cs` | all | Session persistence structure |
| P1 (important) | `Assets/Scripts/Data/SurveyConfigManager.cs` | all | Config save/load and ApplyRulesToSchedule |
| P1 (important) | `Assets/Scripts/UI/SetupScreen.cs` | 226-277 | Survey collection → race start flow |
| P2 (reference) | `Assets/Scripts/Network/NetworkSync.cs` | all | Message routing for survey + race |
| P2 (reference) | `Assets/Scripts/Data/ResultsExporter.cs` | all | Export format — verify with dynamic attributes |
| P2 (reference) | `Server/server.js` | all | WebSocket relay — survey caching logic |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity Test Framework | Unity Docs | Use `[Test]` for Edit Mode tests on pure logic; no Play Mode needed for static utilities |
| NUnit assertions | NUnit 3.x | `Assert.AreEqual`, `Assert.IsTrue`, `Assert.Throws` for Unity Test Framework |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Data/CsvParser.cs:9
public static class CsvParser  // PascalCase class
{
    public static List<CarData> Parse(string csvContent)  // PascalCase method
```

### ERROR_HANDLING
```csharp
// SOURCE: Assets/Scripts/Data/SurveyConfigManager.cs:44-49
public SurveyConfig LoadConfig(string path)
{
    if (string.IsNullOrEmpty(path) || !File.Exists(path))
    {
        Debug.LogWarning($"[SurveyConfigManager] Config file not found: {path}");
        return null;
    }
```

### LOGGING_PATTERN
```csharp
// SOURCE: Assets/Scripts/Network/SurveyCollector.cs:63
Debug.Log($"[SurveyCollector] Survey distributed ({config.Questions.Length} questions)");
// Pattern: [ClassName] Action description (context)
```

### STATIC_UTILITY_PATTERN
```csharp
// SOURCE: Assets/Scripts/Events/RuleEngine.cs:1-11
/// <summary>
/// Evaluates EventRule conditions against car attributes.
/// Pure static utility — no MonoBehaviour, no state.
/// </summary>
public static class RuleEngine
{
    public static bool IsAffected(EventRule rule, CarIdentity car)
```

### TEST_STRUCTURE
```csharp
// No existing project tests — use Unity Test Framework standard pattern:
// SOURCE: Unity Test Framework convention
using NUnit.Framework;

[TestFixture]
public class CsvParserTests
{
    [Test]
    public void Parse_ValidCsvWithHeaders_ReturnsCorrectCarData()
    {
        // Arrange
        string csv = "teamName,colorIndex,functions\nAlpha,2,password/glasses";
        // Act
        var result = CsvParser.Parse(csv);
        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Alpha", result[0].TeamName);
    }
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Tests/EditMode/CsvParserTests.cs` | CREATE | Unit tests for CsvParser (pure static, no MonoBehaviour) |
| `Assets/Tests/EditMode/RuleEngineTests.cs` | CREATE | Unit tests for RuleEngine evaluation logic |
| `Assets/Tests/EditMode/SurveyResponseMapperTests.cs` | CREATE | Unit tests for response-to-CarData mapping |
| `Assets/Tests/EditMode/ResultsExporterTests.cs` | CREATE | Unit tests for CSV export with dynamic attributes |
| `Assets/Tests/EditMode/Tests.asmdef` | CREATE | Assembly definition for Edit Mode tests |
| `Assets/Scripts/Data/SessionData.cs` | UPDATE | Add SurveyConfig serialization in SessionData (full config, not just name) |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Restore SurveyConfig from SessionData on load |
| `Assets/Scripts/Data/SurveyConfigManager.cs` | UPDATE | Add method to build SurveyConfig from SessionData for session reload |
| `Assets/Scripts/Data/ResultsExporter.cs` | UPDATE | Include SurveyConfig metadata in export header |
| `docs/MIGRATION_V1.md` | CREATE | Migration guide for v1 CSV format users |

## NOT Building

- Play Mode tests (require scene setup, NavMesh — out of scope for this integration pass)
- Performance profiling of 50 cars (Phase 7 concern)
- Weather VFX polish (Phase 7 concern)
- WebGL build validation (Phase 6 of the main project, already done)
- New UI elements or panels
- Network integration tests (require running WebSocket server — manual testing only)

---

## Step-by-Step Tasks

### Task 1: Create Test Assembly Definition
- **ACTION**: Create `Assets/Tests/EditMode/Tests.asmdef` for Edit Mode test assembly
- **IMPLEMENT**: Assembly definition referencing the main scripts assembly, with NUnit and UnityEngine.TestRunner
- **MIRROR**: Unity Test Framework convention — `includePlatforms: ["Editor"]`
- **IMPORTS**: N/A (JSON file)
- **GOTCHA**: The `name` field must be unique across the project. Reference assembly must match the project's script assembly name (likely `Assembly-CSharp`).
- **VALIDATE**: Unity Editor recognizes test assembly (Test Runner window shows test fixture)

### Task 2: CsvParser Unit Tests
- **ACTION**: Create `Assets/Tests/EditMode/CsvParserTests.cs`
- **IMPLEMENT**: Test cases:
  - `Parse_EmptyString_ReturnsEmptyList`
  - `Parse_HeaderOnly_ReturnsEmptyList`
  - `Parse_ValidCsvWithHeaders_ReturnsCorrectCarData`
  - `Parse_MultipleRows_ParsesAllCars`
  - `Parse_ExtraColumnsInRow_IgnoresExtraData`
  - `Parse_FewerColumnsThanHeaders_MapsAvailableOnly`
  - `Parse_EmptyTeamName_SkipsRow`
  - `Parse_WhitespaceHandling_TrimsValues`
  - `Parse_ManyColumns_CreatesAllAttributes` (10+ columns)
  - `Parse_V1Format_WorksWithOldColumns` (teamName,colorIndex,functions)
- **MIRROR**: STATIC_UTILITY_PATTERN, TEST_STRUCTURE
- **IMPORTS**: `using NUnit.Framework; using System.Collections.Generic;`
- **GOTCHA**: CsvParser splits on commas — test values containing commas are not currently escaped-aware (known limitation). Do not test quoted CSV fields.
- **VALIDATE**: All tests pass in Unity Test Runner (Edit Mode)

### Task 3: RuleEngine Unit Tests
- **ACTION**: Create `Assets/Tests/EditMode/RuleEngineTests.cs`
- **IMPLEMENT**: Test cases covering all ComparisonOperator values:
  - `IsAffected_AllOperator_AlwaysReturnsTrue`
  - `IsAffected_Equals_MatchesExact`
  - `IsAffected_Equals_CaseInsensitive`
  - `IsAffected_NotEquals_ReturnsTrueWhenDifferent`
  - `IsAffected_Contains_MatchesSlashSeparatedList`
  - `IsAffected_Contains_MatchesSubstring`
  - `IsAffected_NotContains_ReturnsTrueWhenAbsent`
  - `IsAffected_GreaterThan_NumericComparison`
  - `IsAffected_LessThan_NumericComparison`
  - `IsAffected_LengthGreaterThan_ChecksStringLength`
  - `IsAffected_LengthLessThan_ChecksStringLength`
  - `IsAffected_MissingAttribute_UsesEmptyString`
  - `IsAffected_TeamNameAttribute_ResolvesCorrectly`
  - `IsAffected_NonNumericForGreaterThan_ReturnsFalse`
- **MIRROR**: STATIC_UTILITY_PATTERN, TEST_STRUCTURE
- **IMPORTS**: `using NUnit.Framework; using UnityEngine;`
- **GOTCHA**: RuleEngine.IsAffected takes a CarIdentity (MonoBehaviour). In Edit Mode tests, create a new GameObject, add CarIdentity, call Initialize. Destroy after test with `Object.DestroyImmediate`.
- **VALIDATE**: All tests pass in Unity Test Runner (Edit Mode)

### Task 4: SurveyResponseMapper Unit Tests
- **ACTION**: Create `Assets/Tests/EditMode/SurveyResponseMapperTests.cs`
- **IMPLEMENT**: Test cases:
  - `MapResponses_NoMappings_ReturnsEmptyAttributes`
  - `MapResponses_DirectTransform_PassesThroughValue`
  - `MapResponses_LookupTransform_MapsCorrectly`
  - `MapResponses_LookupTransform_UnknownValue_UsesDefault`
  - `MapResponses_NumericTransform_ValidNumber_Passes`
  - `MapResponses_NumericTransform_InvalidNumber_UsesDefault`
  - `MapResponses_MissingResponse_UsesDefaultValue`
  - `MapResponses_MultipleMappings_AllApplied`
  - `MapResponses_CaseInsensitiveQuestionIdMatch`
- **MIRROR**: STATIC_UTILITY_PATTERN, TEST_STRUCTURE
- **IMPORTS**: `using NUnit.Framework; using System;`
- **GOTCHA**: SurveyResponseMapper is pure static — no GameObject creation needed.
- **VALIDATE**: All tests pass in Unity Test Runner (Edit Mode)

### Task 5: ResultsExporter Unit Tests
- **ACTION**: Create `Assets/Tests/EditMode/ResultsExporterTests.cs`
- **IMPLEMENT**: Test cases:
  - `ExportRankingsCsv_EmptyResults_ReturnsHeaderOnly`
  - `ExportRankingsCsv_SingleCar_CorrectFormat`
  - `ExportRankingsCsv_MultipleCars_DynamicAttributeColumns`
  - `ExportRankingsCsv_CarsWithDifferentAttributes_UnionOfAllKeys`
  - `ExportRankingsCsv_ValueWithComma_IsEscaped`
  - `ExportEventLogCsv_NoEvents_ReturnsHeaderOnly`
  - `ExportEventLogCsv_MultipleEvents_CorrectFormat`
- **MIRROR**: STATIC_UTILITY_PATTERN, TEST_STRUCTURE
- **IMPORTS**: `using NUnit.Framework;`
- **GOTCHA**: ResultsExporter is pure static — no GameObject creation needed.
- **VALIDATE**: All tests pass in Unity Test Runner (Edit Mode)

### Task 6: Persist Full SurveyConfig in SessionData
- **ACTION**: Update `Assets/Scripts/Data/SessionData.cs` to store the full SurveyConfig
- **IMPLEMENT**: Add `public SurveyConfig SurveyConfig;` field to SessionData. This allows session reload to fully restore the survey configuration (questions, mappings, and rules) without requiring the original JSON file.
- **MIRROR**: Existing field patterns in SessionData (e.g., `public CarData[] Cars`)
- **IMPORTS**: Already available (SurveyConfig is in the same assembly)
- **GOTCHA**: `SurveyConfig` is a class (not struct), so JsonUtility serializes it as nested JSON. This is fine — already used for `RaceResults`.
- **VALIDATE**: Save and reload a session in Play Mode; verify SurveyConfig persists.

### Task 7: Restore SurveyConfig on Session Load
- **ACTION**: Update `RaceManager.LoadSession()` to restore SurveyConfig from SessionData
- **IMPLEMENT**: In `LoadSession(SessionData session)`, after applying race settings and events, check if `session.SurveyConfig != null`. If so, call `SurveyConfigManager.SetActiveConfig(session.SurveyConfig)` and `SurveyConfigManager.ApplyRulesToSchedule(EventManager.Schedule)` to restore the full config.
- **MIRROR**: Existing LoadSession pattern (line 261-278 of RaceManager.cs)
- **IMPORTS**: Already available
- **GOTCHA**: `SurveyConfigManager` is marked `[Header("Survey (Optional)")]` — null-check before use. Session events are already being loaded (lines 267-275); the SurveyConfig restore should happen BEFORE event loading so that `ApplyRulesToSchedule` can overwrite with the correct rules.
- **VALIDATE**: Load a session that was saved with survey data; verify rules and config are restored.

### Task 8: Save Full SurveyConfig in BuildSessionData
- **ACTION**: Update `RaceManager.BuildSessionData()` to include full SurveyConfig
- **IMPLEMENT**: In `BuildSessionData()`, after setting `SurveyConfigName`, also set `SurveyConfig = SurveyConfigManager.ActiveConfig` (deep copy not needed since SurveyConfig fields are all serializable value types or arrays).
- **MIRROR**: Existing BuildSessionData pattern (line 219-258 of RaceManager.cs)
- **IMPORTS**: Already available
- **GOTCHA**: SurveyConfig could be null if race was started from CSV without a config. Keep the null check.
- **VALIDATE**: Save session; inspect JSON file; verify SurveyConfig section is populated.

### Task 9: Include SurveyConfig Metadata in Results Export
- **ACTION**: Update `ResultsExporter.ExportRankingsCsv()` to include a metadata header
- **IMPLEMENT**: Add optional `SurveyConfig config` parameter (default null). If provided, prepend a comment block before the CSV data: `# Survey: {config.ConfigName}`, `# Questions: {count}`, `# Rules: {count}`, then a blank line, then the existing CSV. This is non-breaking — CSV parsers ignore lines starting with `#`.
- **MIRROR**: Existing ExportRankingsCsv signature pattern
- **IMPORTS**: Already available
- **GOTCHA**: Keep the existing no-parameter overload working (backward compat for callers without a config). Add a new overload rather than changing the existing signature.
- **VALIDATE**: Export results with a survey config active; verify metadata appears in CSV.

### Task 10: Update SessionManager.ExportResults to Pass Config
- **ACTION**: Update the export call chain to pass SurveyConfig through to ResultsExporter
- **IMPLEMENT**: In `RaceManager.ExportCurrentResults()` and the debug key (`X`) handler, pass `SurveyConfigManager?.ActiveConfig` to a new `SessionManager.ExportResults(results, config)` overload. SessionManager passes it to `ResultsExporter.ExportRankingsCsv(results, config)`.
- **MIRROR**: Existing ExportResults pattern in SessionManager.cs
- **IMPORTS**: Already available
- **GOTCHA**: Keep existing overload without config for backward compat.
- **VALIDATE**: Export with config → see metadata; export without config → no metadata (no crash).

### Task 11: Write V1 Migration Guide
- **ACTION**: Create `docs/MIGRATION_V1.md`
- **IMPLEMENT**: Document:
  - Old CSV format: `teamName,colorIndex,functionList` (no header row)
  - New CSV format: requires header row; first column must be `teamName`
  - How to convert: add header row `teamName,colorIndex,functions` as first line
  - Backward-compat: V1 Parity template reproduces all 7 original events
  - New capabilities: any number of columns, custom rules, student survey
  - Example old → new conversion
- **MIRROR**: N/A (documentation)
- **IMPORTS**: N/A
- **GOTCHA**: Keep concise. Target audience is professors, not developers.
- **VALIDATE**: Read through for clarity and accuracy.

### Task 12: Update CLAUDE.md with Phase 6 Completion
- **ACTION**: Update implementation status in `CLAUDE.md` to reflect Phase 6 complete
- **IMPLEMENT**: Update the "Implementation Status" section: change "Phases 1-4 complete" to "Phases 1-6 complete" and update the remaining work list to only include Phase 7.
- **MIRROR**: Existing CLAUDE.md format
- **IMPORTS**: N/A
- **GOTCHA**: Don't change any other sections.
- **VALIDATE**: CLAUDE.md accurately reflects project state.

---

## Testing Strategy

### Unit Tests

| Test File | Coverage Area | Test Count |
|---|---|---|
| `CsvParserTests.cs` | CSV parsing, header mapping, edge cases | ~10 tests |
| `RuleEngineTests.cs` | All comparison operators, attribute resolution | ~14 tests |
| `SurveyResponseMapperTests.cs` | Transform types, defaults, missing data | ~9 tests |
| `ResultsExporterTests.cs` | CSV generation, escaping, dynamic columns | ~7 tests |

### Edge Cases Checklist
- [x] Empty CSV input (handled by CsvParser — returns empty list)
- [x] Header-only CSV (handled — returns empty list)
- [x] CSV with more columns than headers (handled — extra ignored)
- [x] Missing attribute in rule evaluation (handled — returns empty string)
- [x] Non-numeric value for numeric comparison (handled — returns 0/false)
- [x] Null/empty SurveyConfig (handled — all callers null-check)
- [x] Survey response with missing questions (handled — uses DefaultValue)
- [x] Session save with no SurveyConfig active (handled — field stays null)
- [ ] Student disconnect mid-survey (server cleanup handles this — manual test)
- [ ] 50 cars with custom rules (manual performance test — Phase 7)
- [ ] Concurrent survey submissions (server serializes via single WebSocket — safe by design)

### Manual Integration Test Checklist
1. Professor creates survey via Builder UI → saves as JSON
2. Professor hosts room → students join
3. Professor distributes survey → students see questions
4. Students submit responses → professor sees response count
5. Professor starts race with responses → cars spawn with survey attributes
6. Professor triggers custom event rules → correct cars affected
7. Race finishes → export CSV includes all custom attributes
8. Professor saves session → reload session → same config, rules, cars
9. Load V1 Parity template → import old-format CSV (with header added) → all 7 events work

---

## Validation Commands

### Static Analysis
```bash
# Unity compiles all scripts on import — no separate type-check command
# Verify no compiler errors in Unity Editor Console
```
EXPECT: Zero compile errors

### Unit Tests
```
# In Unity Editor: Window > General > Test Runner > EditMode > Run All
```
EXPECT: All ~40 tests pass (green)

### Manual Validation
- [ ] Full professor flow: create survey → host room → distribute → collect → start race → trigger events → export
- [ ] Full student flow: join room → answer survey → see confirmation → watch race
- [ ] Session persistence: save mid-race → reload → verify config and rules restored
- [ ] V1 parity: load V1 Parity template → import CSV → all 7 events work
- [ ] Edge: empty survey responses → cars still spawn with defaults
- [ ] Edge: disconnect student mid-survey → professor can still start with received responses

---

## Acceptance Criteria
- [ ] All unit tests written and passing (~40 tests across 4 test files)
- [ ] SessionData persists full SurveyConfig (save + load verified)
- [ ] RaceManager restores SurveyConfig on session load
- [ ] Results export includes survey metadata when config is active
- [ ] Migration guide written and accurate
- [ ] CLAUDE.md updated to reflect Phase 6 complete
- [ ] No compile errors in Unity Editor

## Completion Checklist
- [ ] Code follows discovered patterns (PascalCase, Debug.Log with [ClassName], static utilities)
- [ ] Error handling matches codebase style (null-check + Debug.LogWarning)
- [ ] Logging follows codebase conventions ([ClassName] prefix)
- [ ] Tests follow NUnit/Unity Test Framework patterns
- [ ] No hardcoded values (paths use Application.persistentDataPath)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Unity Test Framework not installed | LOW | Tests won't compile | Check `manifest.json` for `com.unity.test-framework`; add if missing |
| JsonUtility nested SurveyConfig serialization issues | LOW | Session save/load breaks | SurveyConfig uses only serializable types; test save/load cycle |
| Edit Mode tests can't instantiate GameObjects for CarIdentity | LOW | RuleEngine tests fail | Use `new GameObject()` + `AddComponent<CarIdentity>()` + `DestroyImmediate()` in teardown |
| Results export metadata breaks downstream CSV parsers | LOW | External tools confused by `#` lines | `#` comment prefix is standard CSV convention; document in migration guide |

## Notes
- No Play Mode tests are included because they require scene setup (NavMesh baked, prefabs assigned, etc.) which is impractical for CI and adds fragility. The core logic is all in pure static utilities (`CsvParser`, `RuleEngine`, `SurveyResponseMapper`, `ResultsExporter`) which are fully testable in Edit Mode.
- The WebSocket integration path (survey distribute → collect → start race) is validated manually because it requires a running server and multiple clients. The server already caches survey data for late-joiners (`room.surveyData`).
- Session persistence of SurveyConfig is the key integration gap — everything else is already wired through `SetupScreen.OnStartWithResponses()` and `NetworkSync.HandleGameMessage()`.
