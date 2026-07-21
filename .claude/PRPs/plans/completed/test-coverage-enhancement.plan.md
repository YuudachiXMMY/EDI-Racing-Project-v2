# Plan: Test Coverage Enhancement (GAP 4)

## Summary
Bring Unity C# test coverage from ~5 EditMode tests to comprehensive coverage across all pure-logic systems and MonoBehaviour-backed systems that can be tested in EditMode. Add Node.js test infrastructure for the web-app. Target 80%+ coverage per coding standards.

## User Story
As a maintainer, I want comprehensive automated tests for all core systems, so that regressions are caught before they reach production and the 80% coverage standard is met.

## Problem -> Solution
Only 5 EditMode tests exist (CsvParser, ResultsExporter, RuleEngine, SurveyResponseMapper, JsonImporter). Core gameplay systems (EventManager, ScoreManager, LapTracker, CarIdentity) and pure data types (CarData, SavedEventRule, NetCarData, SurveyTemplates) have zero tests. Web-app has zero tests. -> Add ~15 new test files covering all testable systems in 4 sprints.

## Metadata
- **Complexity**: Large
- **Source PRD**: PRP-Gap-Analysis-Report.md (GAP 4)
- **PRD Phase**: N/A (standalone gap)
- **Estimated Files**: ~18 new test files + 2 config files

---

## UX Design

N/A -- internal change (test infrastructure only)

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Tests/EditMode/CsvParserTests.cs` | all | Test naming/style pattern to follow |
| P0 | `Assets/Tests/EditMode/RuleEngineTests.cs` | all | MonoBehaviour test setup pattern (SetUp/TearDown with GameObject) |
| P0 | `Assets/Tests/EditMode/Tests.asmdef` | all | Assembly definition structure for test project |
| P1 | `Assets/Scripts/Data/CarData.cs` | all | Pure struct with testable accessors |
| P1 | `Assets/Scripts/Data/SessionData.cs` | all | SavedEventRule, SavedRaceConfig, RaceResults structs |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | 88-144 | NetCarData round-trip conversion |
| P1 | `Assets/Scripts/Events/EventManager.cs` | all | MonoBehaviour event orchestrator |
| P1 | `Assets/Scripts/Race/ScoreManager.cs` | all | MonoBehaviour ranking system |
| P1 | `Assets/Scripts/Race/LapTracker.cs` | all | MonoBehaviour checkpoint/lap tracking |
| P1 | `Assets/Scripts/Data/SurveyTemplates.cs` | all | Static template factory |
| P2 | `Assets/Scripts/Data/SurveyConfigManager.cs` | 93-119 | ApplyRulesToSchedule logic |
| P2 | `Assets/Scripts/Car/CarIdentity.cs` | all | MonoBehaviour attribute accessors |
| P2 | `Assets/Scripts/Race/CheckpointTrigger.cs` | all | Trigger integration with LapTracker |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity Test Framework | com.unity.test-framework 1.6.0 (installed) | Use `[TestFixture]`, `[Test]`, `[SetUp]`, `[TearDown]` from NUnit |
| EditMode vs PlayMode | Unity docs | EditMode tests can instantiate GameObjects but cannot use `Awake()`/`Start()` lifecycle. Use `DestroyImmediate()` in TearDown. |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Tests/EditMode/CsvParserTests.cs:1-8
// File naming: {SystemName}Tests.cs
// Class naming: {SystemName}Tests
// Method naming: {MethodName}_{Scenario}_{ExpectedResult}
[TestFixture]
public class CsvParserTests
{
    [Test]
    public void Parse_EmptyString_ReturnsEmptyList()
```

### TEST_SETUP_TEARDOWN (for MonoBehaviour tests)
```csharp
// SOURCE: Assets/Tests/EditMode/RuleEngineTests.cs:7-29
// Pattern: create GameObject in SetUp, DestroyImmediate in TearDown
[TestFixture]
public class RuleEngineTests
{
    private GameObject testObj;
    private CarIdentity testCar;

    [SetUp]
    public void SetUp()
    {
        testObj = new GameObject("TestCar");
        testCar = testObj.AddComponent<CarIdentity>();
        testCar.Initialize(new CarData("TestTeam", new AttributeEntry[] { ... }));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(testObj);
    }
```

### PURE_DATA_TEST (for struct/static tests)
```csharp
// SOURCE: Assets/Tests/EditMode/CsvParserTests.cs:29-38
// Pattern: no SetUp/TearDown, direct assertions, no GameObject needed
[Test]
public void Parse_ValidCsvWithHeaders_ReturnsCorrectCarData()
{
    string csv = "teamName,colorIndex,functions\nAlpha,2,password/glasses";
    List<CarData> result = CsvParser.Parse(csv);

    Assert.AreEqual(1, result.Count);
    Assert.AreEqual("Alpha", result[0].TeamName);
}
```

### ASSERTION_STYLE
```csharp
// SOURCE: Assets/Tests/EditMode/ResultsExporterTests.cs:10-15
// Use Assert.AreEqual for equality, Assert.IsTrue/IsFalse for boolean,
// Assert.IsNotNull for null checks, Assert.Contains for collection membership
Assert.AreEqual(0, result.Count);
Assert.IsTrue(csv.StartsWith("Rank,TeamName"));
Assert.IsTrue(csv.Contains("LapsCompleted"));
Assert.IsFalse(result.Success);
Assert.IsNotNull(result.Error);
```

### ASSEMBLY_DEFINITION
```json
// SOURCE: Assets/Tests/EditMode/Tests.asmdef
{
    "name": "Tests.EditMode",
    "references": ["UnityEngine.TestRunner", "UnityEditor.TestRunner", "EDIRacing.Runtime", "Unity.InputSystem"],
    "includePlatforms": ["Editor"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "autoReferenced": false
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Tests/EditMode/CarDataTests.cs` | CREATE | Test CarData struct accessors, backward compat, edge cases |
| `Assets/Tests/EditMode/CarIdentityTests.cs` | CREATE | Test Initialize, attribute accessors on MonoBehaviour |
| `Assets/Tests/EditMode/SavedEventRuleTests.cs` | CREATE | Test FromRule/ToRule round-trip serialization |
| `Assets/Tests/EditMode/SavedRaceConfigTests.cs` | CREATE | Test FromScriptableObject/ApplyTo round-trip |
| `Assets/Tests/EditMode/NetCarDataTests.cs` | CREATE | Test FromCarData/ToCarData round-trip |
| `Assets/Tests/EditMode/EventManagerTests.cs` | CREATE | Test RegisterCar, TriggerEvent, repeat logic |
| `Assets/Tests/EditMode/ScoreManagerTests.cs` | CREATE | Test RegisterCar, GetRankedCars ordering, CollectResults |
| `Assets/Tests/EditMode/LapTrackerTests.cs` | CREATE | Test OnCarPassedCheckpoint, lap completion, out-of-order rejection |
| `Assets/Tests/EditMode/SurveyTemplatesTests.cs` | CREATE | Test GetTemplate returns valid configs, unknown returns null |
| `Assets/Tests/EditMode/SurveyConfigManagerTests.cs` | CREATE | Test ApplyRulesToSchedule, key binding assignment |
| `Assets/Tests/EditMode/EventScheduleTests.cs` | CREATE | Test ResetRuntimeState |
| `Assets/Tests/EditMode/GameStateTests.cs` | CREATE | Test GameState enum coverage, state transitions |
| `web-app/package.json` | UPDATE | Add vitest + test script |
| `web-app/vitest.config.js` | CREATE | Vitest configuration |
| `web-app/__tests__/db.test.js` | CREATE | Test DB schema, seed, CRUD |
| `web-app/__tests__/routes/surveys.test.js` | CREATE | Test survey CRUD endpoints |
| `web-app/__tests__/routes/auth.test.js` | CREATE | Test auth middleware |
| `web-app/__tests__/routes/export.test.js` | CREATE | Test export endpoints |

## NOT Building

- PlayMode tests (require scene setup, physics, NavMesh -- separate future sprint)
- CarController tests (heavy NavMeshAgent dependency, needs PlayMode)
- CarSpawner tests (needs prefabs, NavMesh, PlayMode)
- NetworkSync tests (needs WebSocket infrastructure)
- NetworkManager tests (WebSocket lifecycle)
- UI tests (SetupScreen, RaceUI, panels -- visual verification)
- E2E tests (requires running Unity + web-app together)
- CI/CD pipeline (separate GAP -- no .github/workflows/ exists)

---

## Step-by-Step Tasks

### Sprint 1: Pure Data Type Tests (EditMode, no MonoBehaviour)

#### Task 1: CarDataTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/CarDataTests.cs`
- **IMPLEMENT**: Test all CarData struct methods:
  - `GetAttribute` — existing key, missing key, default value, case-insensitive match
  - `GetIntAttribute` — valid int, invalid string, default fallback
  - `GetFloatAttribute` — valid float, invalid string, default fallback
  - `HasAttribute` — present key, absent key, null Attributes array
  - `ToDictionary` — normal case, empty attributes, duplicate-safe
  - `GetAttributeKeys` — normal, empty
  - `ColorIndex` — with/without "colorIndex" attribute
  - `Functions` — slash-separated parsing, empty, trimming, lowercasing
  - Constructor with Dictionary
  - Constructor with null attributes
- **MIRROR**: `PURE_DATA_TEST` pattern
- **IMPORTS**: `using NUnit.Framework; using System; using System.Collections.Generic;`
- **GOTCHA**: CarData is a `struct` — no null checks needed on the instance itself, but `Attributes` array can be null
- **VALIDATE**: Run `Unity Test Runner > EditMode > CarDataTests` — all green

#### Task 2: SavedEventRuleTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/SavedEventRuleTests.cs`
- **IMPLEMENT**: Test round-trip fidelity:
  - `FromRule` preserves all fields (DisplayName, AttributeName, Operator, CompareValue, SpeedDelta, Duration, Weather, AllowRepeat)
  - `FromRule` handles null strings (DisplayName, AttributeName, CompareValue default to "")
  - `ToRule` restores correct enum values (ComparisonOperator, WeatherType)
  - `ToRule` assigns provided triggerKey
  - `ToRule` resets HasBeenTriggered to false
  - Round-trip: `FromRule(rule).ToRule(key)` matches original (except TriggerKey and HasBeenTriggered)
- **MIRROR**: `PURE_DATA_TEST` pattern
- **IMPORTS**: `using NUnit.Framework; using UnityEngine.InputSystem;`
- **GOTCHA**: `Operator` and `Weather` are stored as `int` in SavedEventRule — cast correctly
- **VALIDATE**: All round-trip assertions pass

#### Task 3: SavedRaceConfigTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/SavedRaceConfigTests.cs`
- **IMPLEMENT**: Test ScriptableObject snapshot:
  - `FromScriptableObject` captures DefaultSpeed, AngularSpeed, Acceleration, TotalLaps, CarScale
  - `ApplyTo` restores all values to a fresh ScriptableObject
  - Round-trip: create RaceConfig SO -> snapshot -> apply to new SO -> values match
- **MIRROR**: `PURE_DATA_TEST` pattern (but needs ScriptableObject.CreateInstance for RaceConfig)
- **IMPORTS**: `using NUnit.Framework; using UnityEngine;`
- **GOTCHA**: Use `ScriptableObject.CreateInstance<RaceConfig>()` in test, `Object.DestroyImmediate()` in TearDown
- **VALIDATE**: Round-trip values identical

#### Task 4: NetCarDataTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/NetCarDataTests.cs`
- **IMPLEMENT**: Test network serialization round-trip:
  - `FromCarData` — preserves teamName, converts AttributeEntry[] to NetAttribute[]
  - `ToCarData` — restores teamName and AttributeEntry[] from NetAttribute[]
  - Round-trip: `FromCarData(cd).ToCarData()` produces equivalent CarData
  - Empty attributes — array is empty, not null
  - Null attributes — handled gracefully
  - Multiple attributes preserved in order
- **MIRROR**: `PURE_DATA_TEST` pattern
- **IMPORTS**: `using NUnit.Framework; using System;`
- **GOTCHA**: NetAttribute uses short field names `k`/`v` vs AttributeEntry `Key`/`Value`
- **VALIDATE**: Round-trip equivalence confirmed

#### Task 5: SurveyTemplatesTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/SurveyTemplatesTests.cs`
- **IMPLEMENT**: Test template factory:
  - `GetTemplate("V1 Parity")` returns non-null config with correct name
  - `GetTemplate("Accessibility")` returns config with 3 questions, 3 mappings, 3 rules
  - `GetTemplate("Diversity")` returns config with 3 questions, 3 mappings, 4 rules
  - `GetTemplate("ENGG*1100 Survey")` returns config with 14 questions, 7 mappings, 7 rules
  - `GetTemplate("Unknown")` returns null
  - `TemplateNames` contains all 4 template names
  - Each template's rules have valid Operator/Weather int values (within enum range)
  - Each template's mappings have non-empty QuestionId and AttributeName
- **MIRROR**: `PURE_DATA_TEST` pattern
- **IMPORTS**: `using NUnit.Framework; using System;`
- **GOTCHA**: Templates create DateTime.Now in CreatedAt — don't assert exact value
- **VALIDATE**: All templates structurally valid

#### Task 6: EventScheduleTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/EventScheduleTests.cs`
- **IMPLEMENT**: Test ScriptableObject:
  - Default Events array has 8 rules
  - `ResetRuntimeState` clears HasBeenTriggered on all events
  - After manually setting HasBeenTriggered=true, ResetRuntimeState resets all to false
- **MIRROR**: `PURE_DATA_TEST` pattern with ScriptableObject.CreateInstance
- **IMPORTS**: `using NUnit.Framework; using UnityEngine;`
- **GOTCHA**: Use `ScriptableObject.CreateInstance<EventSchedule>()`, destroy in TearDown
- **VALIDATE**: Reset behavior confirmed

### Sprint 2: MonoBehaviour Tests (EditMode with GameObject setup)

#### Task 7: CarIdentityTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/CarIdentityTests.cs`
- **IMPLEMENT**: Test MonoBehaviour attribute storage:
  - `Initialize` — sets TeamName, clones Attributes, resets progress fields
  - `GetAttribute` — existing key, missing key, default value, case-insensitive
  - `GetIntAttribute` — valid, invalid, default
  - `HasAttribute` — present, absent
  - `ColorIndex` — backward compat accessor
  - `Functions` — backward compat, slash parsing
  - Initialize with null attributes — graceful, Attributes becomes empty
- **MIRROR**: `TEST_SETUP_TEARDOWN` pattern
- **IMPORTS**: `using NUnit.Framework; using UnityEngine; using System;`
- **GOTCHA**: CarIdentity.Update() increments CheckpointTime — but in EditMode, Update() is not called automatically. No issue for attribute tests.
- **VALIDATE**: All accessor tests pass

#### Task 8: ScoreManagerTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/ScoreManagerTests.cs`
- **IMPLEMENT**: Test ranking system:
  - `RegisterCar` — increases internal car count
  - `GetRankedCars` — orders by TotalCheckpointsPassed (descending), then CheckpointTime (ascending)
  - `GetRankedCars` — with tied checkpoints, lower time wins
  - `GetScoreboardText` — contains team names and lap info
  - `Clear` — empties car list
  - `CollectResults` — produces correct CarResult array with ranks
  - `CollectResults` — empty event log produces empty EventLog array
  - `CollectResults` — null event log handled
- **MIRROR**: `TEST_SETUP_TEARDOWN` pattern
- **IMPORTS**: `using NUnit.Framework; using UnityEngine; using System; using System.Collections.Generic;`
- **GOTCHA**: ScoreManager extends MonoBehaviour — create via `go.AddComponent<ScoreManager>()`. CarIdentity fields (TotalCheckpointsPassed, CheckpointTime) must be set directly for test scenarios.
- **VALIDATE**: Ranking order correct in all scenarios

#### Task 9: LapTrackerTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/LapTrackerTests.cs`
- **IMPLEMENT**: Test checkpoint/lap tracking:
  - `OnCarPassedCheckpoint` — correct index increments TotalCheckpointsPassed
  - `OnCarPassedCheckpoint` — wrong index (out-of-order) is ignored
  - After passing all checkpoints once → CurrentLap increments
  - `OnLapCompleted` event fires on lap boundary
  - `OnCheckpointPassed` event fires on valid checkpoint
  - Multiple laps: checkpoint sequence wraps correctly
- **MIRROR**: `TEST_SETUP_TEARDOWN` pattern
- **IMPORTS**: `using NUnit.Framework; using UnityEngine; using System;`
- **GOTCHA**: LapTracker.Start() calls FindObjectsByType to count checkpoints. In EditMode, there are no CheckpointTrigger objects. You must set the `totalCheckpoints` field via reflection or create a test subclass/helper. Alternative: use `System.Reflection` to set private `totalCheckpoints` field before calling `OnCarPassedCheckpoint`.
- **VALIDATE**: Checkpoint sequence enforcement works; lap events fire

#### Task 10: EventManagerTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/EventManagerTests.cs`
- **IMPLEMENT**: Test event orchestration:
  - `RegisterCar` — increases RegisteredCarCount
  - `RegisterCars` — registers all cars with CarIdentity
  - `Activate` / `Deactivate` — sets IsActive flag
  - `TriggerEvent` — valid index applies effect, increments affected count
  - `TriggerEvent` — invalid index (negative, out of range) is no-op
  - `TriggerEvent` — already triggered + AllowRepeat=false → skipped
  - `TriggerEvent` — already triggered + AllowRepeat=true → triggers again
  - `TriggerEventByName` — finds by name (case-insensitive)
  - `TriggerEventByName` — unknown name logs warning, no crash
  - `OnEventTriggered` event fires with correct rule and affected count
  - `ClearRegisteredCars` — resets state
- **MIRROR**: `TEST_SETUP_TEARDOWN` pattern
- **IMPORTS**: `using NUnit.Framework; using UnityEngine; using System; using System.Collections.Generic; using UnityEngine.InputSystem;`
- **GOTCHA**: EventManager.TriggerEvent calls `car.GetComponent<CarController>()` to apply speed modifier. In EditMode without CarController, `affectedCount` will be 0 even when rule matches. To properly test affected count, add a mock CarController component. Alternative: test that OnEventTriggered fires and HasBeenTriggered is set, accept affectedCount=0 in EditMode.
- **VALIDATE**: Event trigger/repeat logic works; events fire correctly

#### Task 11: SurveyConfigManagerTests.cs
- **ACTION**: Create `Assets/Tests/EditMode/SurveyConfigManagerTests.cs`
- **IMPLEMENT**: Test config management logic:
  - `SetActiveConfig` — stores reference, null clears it
  - `ApplyRulesToSchedule` — maps rules to EventSchedule.Events with correct keys (Digit1-Digit9)
  - `ApplyRulesToSchedule` — max 9 rules (surplus skipped)
  - `ApplyRulesToSchedule` — no active config → early return, no crash
  - `ApplyRulesToSchedule` — empty rules array → early return
  - `GetTemplateNames` — returns all 4 names
  - `LoadTemplate` — delegates to SurveyTemplates.GetTemplate
- **MIRROR**: `TEST_SETUP_TEARDOWN` pattern
- **IMPORTS**: `using NUnit.Framework; using UnityEngine; using System; using UnityEngine.InputSystem;`
- **GOTCHA**: ApplyRulesToSchedule modifies the EventSchedule.Events array in-place. Create fresh ScriptableObject for each test.
- **VALIDATE**: Key assignments Digit1-Digit9 correct; surplus rules handled

### Sprint 3: Web App Tests (Node.js / Vitest)

#### Task 12: Set up web-app test infrastructure
- **ACTION**: Update `web-app/package.json` to add vitest as devDependency + test script. Create `web-app/vitest.config.js`.
- **IMPLEMENT**:
  - Add `"vitest": "^3.0.0"` to devDependencies
  - Add `"test": "vitest run"` and `"test:watch": "vitest"` to scripts
  - Create vitest.config.js with `{ test: { globals: true } }`
- **MIRROR**: N/A (new framework)
- **IMPORTS**: N/A
- **GOTCHA**: Project uses `"type": "module"` — vitest handles ESM natively
- **VALIDATE**: `cd web-app && npm install && npm test` runs (even with 0 tests)

#### Task 13: Database tests
- **ACTION**: Create `web-app/__tests__/db.test.js`
- **IMPLEMENT**: Test SQLite schema and operations:
  - Schema creates tables (surveys, questions, responses, response_answers, professors)
  - Seed templates populates expected survey count
  - CRUD operations on surveys table
  - Foreign key constraints enforced
- **MIRROR**: N/A
- **IMPORTS**: `better-sqlite3`
- **GOTCHA**: Use in-memory SQLite (`:memory:`) for test isolation. Import schema.sql and seed-templates.js directly.
- **VALIDATE**: `npm test` passes

#### Task 14: Auth middleware tests
- **ACTION**: Create `web-app/__tests__/routes/auth.test.js`
- **IMPLEMENT**: Test authentication:
  - Login with valid credentials succeeds
  - Login with invalid credentials fails
  - Protected routes reject unauthenticated requests
- **MIRROR**: N/A
- **IMPORTS**: `express`, `supertest` (add as devDependency), `bcryptjs`
- **GOTCHA**: Auth uses bcryptjs for password hashing. Seed a test professor account.
- **VALIDATE**: Auth flow tested end-to-end

#### Task 15: Survey route tests
- **ACTION**: Create `web-app/__tests__/routes/surveys.test.js`
- **IMPLEMENT**: Test survey CRUD endpoints:
  - GET /api/surveys — returns list
  - POST /api/surveys — creates new survey
  - GET /api/surveys/:id — returns single survey with questions
  - DELETE /api/surveys/:id — removes survey
- **MIRROR**: N/A
- **IMPORTS**: `express`, `supertest`
- **GOTCHA**: Routes depend on `req.db` (SQLite instance). Mount routes with test DB.
- **VALIDATE**: All CRUD operations tested

### Sprint 4: Additional Coverage & Cleanup

#### Task 16: GameState enum coverage
- **ACTION**: Create `Assets/Tests/EditMode/GameStateTests.cs`
- **IMPLEMENT**: Verify GameState enum values exist and are distinct:
  - Setup, Racing, Paused, Finished all defined
  - String round-trip via Enum.Parse works (used by NetworkSync)
- **MIRROR**: `PURE_DATA_TEST` pattern
- **IMPORTS**: `using NUnit.Framework; using System;`
- **GOTCHA**: Simple validation — ensure the enum contract that NetworkSync depends on is stable
- **VALIDATE**: Enum parse round-trip passes

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| CarData_GetAttribute_MissingKey | key="nonexistent" | default "" | Yes |
| CarData_Functions_SlashSeparated | "password/glasses" | ["password","glasses"] | No |
| CarData_Functions_Empty | "" | empty array | Yes |
| SavedEventRule_RoundTrip | EventRule with all fields | Identical after FromRule->ToRule | No |
| SavedEventRule_NullStrings | null DisplayName | "" after FromRule | Yes |
| NetCarData_RoundTrip | CarData with attrs | Equivalent after FromCarData->ToCarData | No |
| NetCarData_EmptyAttrs | empty attrs | Empty array, not null | Yes |
| ScoreManager_Ranking | 3 cars, different progress | Ordered by checkpoints desc, time asc | No |
| ScoreManager_TiedCheckpoints | 2 cars, same checkpoints | Lower time ranked higher | Yes |
| LapTracker_OutOfOrder | wrong checkpoint index | No state change | Yes |
| LapTracker_LapCompletion | pass all checkpoints | CurrentLap++ | No |
| EventManager_NoRepeat | trigger twice, AllowRepeat=false | Second trigger ignored | No |
| EventManager_InvalidIndex | index=-1 or 999 | No crash, no effect | Yes |
| SurveyTemplates_Unknown | "Nonexistent" | null | Yes |

### Edge Cases Checklist
- [x] Empty input (empty strings, empty arrays)
- [x] Null input (null attributes, null strings)
- [x] Maximum size input (9+ event rules hitting key limit)
- [x] Invalid types (non-numeric strings for numeric fields)
- [x] Boundary values (checkpoint index wrapping at totalCheckpoints)
- [ ] Concurrent access (not applicable — Unity is single-threaded)
- [ ] Network failure (not applicable — excluded from scope)

---

## Validation Commands

### Static Analysis
```bash
# Unity will report compile errors on domain reload
# Open Unity Editor and check Console for errors
```
EXPECT: Zero compile errors in test assembly

### Unit Tests (Unity)
```bash
# Run via Unity Test Runner (EditMode tab)
# Or via command line:
# Unity -runTests -testPlatform EditMode -projectPath . -testResults results.xml
```
EXPECT: All tests pass

### Unit Tests (Web App)
```bash
cd web-app && npm test
```
EXPECT: All tests pass

### Full Test Suite
```bash
# Unity EditMode tests + web-app tests
# Unity: game-ci/unity-test-runner@v4 (when CI added)
cd web-app && npm test
```
EXPECT: No regressions

---

## Acceptance Criteria
- [ ] All 12 Unity test files created and passing
- [ ] Web-app test infrastructure set up (vitest)
- [ ] At least 3 web-app test files created and passing
- [ ] All existing 5 test files still pass (no regressions)
- [ ] Test naming follows `{Method}_{Scenario}_{Expected}` pattern
- [ ] Each test has clear Arrange/Act/Assert structure
- [ ] No test depends on external state or execution order
- [ ] Coverage measurably improved toward 80% target

## Completion Checklist
- [ ] Code follows discovered patterns (NUnit, [TestFixture], SetUp/TearDown)
- [ ] Error handling matches codebase style (no try-catch in tests)
- [ ] Tests follow test patterns (PURE_DATA_TEST or TEST_SETUP_TEARDOWN)
- [ ] No hardcoded values (use constants or factory methods for test data)
- [ ] No unnecessary scope additions
- [ ] Self-contained -- no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| EventManager.TriggerEvent calls CarController which isn't available in EditMode | High | Medium | Accept affectedCount=0 in tests; test event firing and HasBeenTriggered flag separately |
| LapTracker.Start() sets totalCheckpoints via scene query | High | Medium | Use reflection to set private field, or extract testable logic into separate method |
| Web-app route tests need isolated DB | Medium | Low | Use in-memory SQLite per test |
| Some MonoBehaviour tests may hit Unity lifecycle quirks in EditMode | Low | Medium | Use SetUp/TearDown pattern from RuleEngineTests as proven template |

## Notes
- Sprint 1 (Tasks 1-6) is fully independent -- pure data types, no MonoBehaviour setup
- Sprint 2 (Tasks 7-11) follows RuleEngineTests pattern for MonoBehaviour testing
- Sprint 3 (Tasks 12-15) is fully independent of Unity -- Node.js web-app
- Sprint 4 (Task 16) is a small cleanup task
- Recommended execution order: Sprint 1 -> Sprint 2 -> Sprint 3 (or Sprint 1 + Sprint 3 in parallel)
- Each Sprint can be implemented as a separate PR for easier review
- The plan intentionally excludes PlayMode tests, E2E tests, and CI/CD -- these should be separate plans
