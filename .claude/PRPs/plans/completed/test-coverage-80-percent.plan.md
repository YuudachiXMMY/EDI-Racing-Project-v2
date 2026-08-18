# Plan: 测试覆盖率达标 — 检查并提升至 80% 目标

## Summary

检查 EDI Racing 项目的实际单元测试覆盖率，识别覆盖缺口，制定分阶段补齐计划以达到 80% 覆盖率目标。当前有 17 个测试文件（171 个 `[Test]` 方法，2,603 LOC），覆盖 51 个生产文件中的 17 个（33% 文件覆盖率）。估计实际代码行覆盖率约 20-25%，距离 80% 目标差距显著。

## User Story

As a **project maintainer**,
I want **to measure actual test coverage and systematically close the gap to 80%**,
So that **regression bugs are caught early, gameplay systems are validated, and CI can enforce quality gates**.

## Problem → Solution

**当前状态**: 17/51 文件有测试（33%），无覆盖率度量工具，无 CI/CD 测试管线。核心逻辑层（Data、Events、Race logic）覆盖良好，但 UI、Camera、Network、CarController 等系统完全无测试。

**目标状态**: 安装覆盖率工具量化真实覆盖率，为所有可测试系统编写测试，通过提取逻辑层使 MonoBehaviour 密集代码可测试，最终达到 80% 行覆盖率。

## Metadata

- **Complexity**: Large
- **Source PRD**: N/A
- **PRD Phase**: N/A
- **Estimated Files**: 25-30（新测试文件 + 部分生产代码重构以提升可测试性）

---

## UX Design

N/A — 内部质量改进，无用户可见变化。

---

## Current Coverage Audit

### File-Level Coverage Map

#### TESTED (17 files — 2,049 LOC)

| Production File | LOC | Test File | Tests | Coverage Depth |
|---|---|---|---|---|
| RuleEngine.cs | 122 | RuleEngineTests.cs | 23 | **DEEP** — compound conditions, all operators |
| CarData.cs | 104 | CarDataTests.cs | 26 | **DEEP** — constructor, attributes, conversion |
| EventManager.cs | 117 | EventManagerTests.cs | 13 | **GOOD** — triggering, effects, logging |
| SessionData.cs (SavedEventRule) | 205 | SavedEventRuleTests.cs | 9 | **GOOD** — round-trip serialization |
| CarIdentity.cs | 82 | CarIdentityTests.cs | 13 | **GOOD** — initialization, attributes |
| ResultsExporter.cs | 93 | ResultsExporterTests.cs | 7 | **GOOD** — CSV generation |
| SurveyResponseMapper.cs | 96 | SurveyResponseMapperTests.cs | 9 | **GOOD** — mapping logic |
| ScoreManager.cs | 69 | ScoreManagerTests.cs | 8 | **GOOD** — ranking, results |
| LapTracker.cs | 43 | LapTrackerTests.cs | 10 | **DEEP** — all paths |
| SurveyConfigManager.cs | 135 | SurveyConfigManagerTests.cs | 9 | **GOOD** — config management |
| JsonImporter.cs | 101 | JsonImporterTests.cs | 8 | **GOOD** — parsing |
| SurveyTemplates.cs | 407 | SurveyTemplatesTests.cs | 9 | **MODERATE** — template validity |
| NetworkMessages.cs (NetCarData) | 337 | NetCarDataTests.cs | 8 | **PARTIAL** — only NetCarData struct |
| CsvParser.cs | 50 | CsvParserTests.cs | 10 | **DEEP** — all paths |
| RaceConfig.cs | 83 | SavedRaceConfigTests.cs | 3 | **SHALLOW** — basic round-trip |
| EventSchedule.cs | 124 | EventScheduleTests.cs | 3 | **SHALLOW** — basic structure |
| GameState.cs | 10 | GameStateTests.cs | 3 | **DEEP** — enum validation |

#### UNTESTED (34 files — 6,502 LOC)

| File | LOC | Testability | Priority | Reason |
|---|---|---|---|---|
| **HIGH TESTABILITY — Pure logic / data** | | | | |
| SessionData.cs (RaceResults, CarResult) | 205 | HIGH | P0 | Pure data structs, CarResult.ColorIndex logic |
| EventRule.cs | 76 | HIGH | P0 | Struct definition + LogicOperator enum |
| SurveyQuestion.cs | 34 | HIGH | P2 | Pure data struct |
| AttributeMapping.cs | 23 | HIGH | P2 | Pure data struct |
| SurveyConfig.cs | 18 | HIGH | P2 | Pure data class |
| ComparisonOperator.cs | 16 | HIGH | P2 | Enum only |
| WeatherType.cs | 11 | HIGH | P2 | Enum only |
| **MEDIUM TESTABILITY — Can create in EditMode** | | | | |
| NetworkMessages.cs (remaining types) | 337 | MEDIUM | P1 | 20+ message types, serialization untested |
| SessionManager.cs | 108 | MEDIUM | P1 | File I/O logic (can mock with temp dirs) |
| WaypointPath.cs | 63 | MEDIUM | P1 | Waypoint logic testable with mock transforms |
| CarSpawner.cs | 242 | MEDIUM | P1 | Spawn logic extractable |
| ScoreManager.cs (edge cases) | 69 | MEDIUM | P1 | Already tested but shallow areas remain |
| **LOW TESTABILITY — Heavy MonoBehaviour / physics** | | | | |
| CarController.cs | 454 | LOW | P2 | Physics-heavy, needs PlayMode or logic extraction |
| WeatherEffect.cs | 386 | LOW | P3 | Particle effects, visual-only |
| RuntimeSetup.cs | 462 | LOW | P3 | Scene wiring, runtime factory |
| NetworkSync.cs | 402 | LOW | P2 | WebSocket-dependent |
| NetworkManager.cs | 328 | LOW | P2 | WebSocket lifecycle |
| RaceManager.cs | 359 | LOW | P2 | Orchestrator, many dependencies |
| **EDITOR-ONLY (excluded from coverage target)** | | | | |
| TrackSetupEditor.cs | 1,074 | N/A | — | Editor tool, not runtime code |
| SceneWiring.cs | 426 | N/A | — | Editor tool |
| CreateImportUI.cs | 180 | N/A | — | Editor tool |
| BuildScript.cs | 35 | N/A | — | Build script |
| **UI (LOW — UGUI-dependent)** | | | | |
| SetupScreen.cs | 436 | LOW | P3 | UGUI interaction-heavy |
| RaceFinishPanel.cs | 117 | LOW | P3 | UGUI panel |
| JoinScreen.cs | 127 | LOW | P3 | UGUI panel |
| CarLabelSpawner.cs | 119 | LOW | P3 | Runtime instantiation |
| EventPanel.cs | 95 | LOW | P3 | UGUI panel |
| RaceControlPanel.cs | 94 | LOW | P3 | UGUI panel |
| RaceUI.cs | 84 | LOW | P3 | UGUI coordinator |
| LeaderboardPanel.cs | 82 | LOW | P3 | UGUI panel |
| CarLabel.cs | 56 | LOW | P3 | UGUI component |
| FpsCounter.cs | 23 | LOW | P3 | Debug utility |
| **CAMERA (LOW — Transform/Camera-dependent)** | | | | |
| CameraManager.cs | 88 | LOW | P3 | Camera switching |
| RaceCameraController.cs | 78 | LOW | P3 | Input-driven camera |
| SpectatorCamera.cs | 61 | LOW | P3 | Transform tracking |
| FixedCameraPoint.cs | 18 | LOW | P3 | Trivial MonoBehaviour |
| CheckpointTrigger.cs | 28 | LOW | P3 | Physics trigger |

### Coverage Arithmetic

```
Total production LOC:            8,551
Editor-only (excluded):         -1,715 (4 files)
─────────────────────────────────────
Coverable runtime LOC:           6,836

Currently "tested" file LOC:     2,049
Estimated line coverage within tested files: ~70%
Estimated covered LOC:           ~1,434

Current estimated coverage:      1,434 / 6,836 ≈ 21%
Target:                          80%
Required covered LOC:            5,469
Gap:                             ~4,035 LOC to cover
```

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Tests/EditMode/Tests.asmdef` | all | Test assembly configuration |
| P0 | `Assets/Tests/EditMode/ScoreManagerTests.cs` | all | Canonical test pattern (SetUp/TearDown/CreateCar helper) |
| P0 | `Assets/Tests/EditMode/RuleEngineTests.cs` | all | Deepest test — reference for comprehensive coverage |
| P1 | `.claude/rules/test-standards.md` | all | Naming & structure conventions |
| P1 | `.claude/docs/coding-standards.md` | all | Test evidence requirements |
| P2 | `Assets/Tests/EditMode/CarDataTests.cs` | all | Data model test patterns |
| P2 | `Assets/Tests/EditMode/NetCarDataTests.cs` | all | Network serialization test patterns |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity Code Coverage | `com.unity.testtools.codecoverage` package | Provides HTML reports + OpenCover XML; integrates with Unity Test Runner |
| Unity Test Framework | `com.unity.test-framework` 1.6.0 (already installed) | Supports EditMode and PlayMode; NUnit-based |
| GitHub Actions Unity | `game-ci/unity-test-runner@v4` | CI test runner (referenced in coding-standards.md) |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Tests/EditMode/ScoreManagerTests.cs:42-57
[Test]
public void GetRankedCars_OrdersByCheckpointsDescending()
{
    // Arrange
    var car1 = CreateCar("Slow", 5, 10f);
    var car2 = CreateCar("Fast", 10, 20f);
    // Act
    var ranked = scoreManager.GetRankedCars();
    // Assert
    Assert.AreEqual("Fast", ranked[0].TeamName);
}
```
Pattern: `MethodUnderTest_Scenario_ExpectedResult`, PascalCase.

### TEST_SETUP
```csharp
// SOURCE: Assets/Tests/EditMode/ScoreManagerTests.cs:8-27
[TestFixture]
public class ScoreManagerTests
{
    private GameObject managerObj;
    private ScoreManager scoreManager;
    private List<GameObject> carObjects;

    [SetUp]
    public void SetUp()
    {
        managerObj = new GameObject("ScoreManager");
        scoreManager = managerObj.AddComponent<ScoreManager>();
        carObjects = new List<GameObject>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in carObjects)
            UnityEngine.Object.DestroyImmediate(obj);
        UnityEngine.Object.DestroyImmediate(managerObj);
    }
}
```
Pattern: Create GameObjects in SetUp, DestroyImmediate in TearDown. Track all created objects.

### HELPER_FACTORY
```csharp
// SOURCE: Assets/Tests/EditMode/ScoreManagerTests.cs:29-39
private CarIdentity CreateCar(string name, int checkpoints, float time, int lap = 0)
{
    var obj = new GameObject(name);
    carObjects.Add(obj);
    var identity = obj.AddComponent<CarIdentity>();
    identity.Initialize(new CarData(name, Array.Empty<AttributeEntry>()));
    identity.TotalCheckpointsPassed = checkpoints;
    identity.CheckpointTime = time;
    identity.CurrentLap = lap;
    return identity;
}
```
Pattern: Private factory methods for complex test objects. Track for cleanup.

### PURE_DATA_TEST
```csharp
// SOURCE: Assets/Tests/EditMode/CarDataTests.cs (inferred pattern)
[Test]
public void Constructor_SetsTeamName()
{
    var data = new CarData("TestTeam", Array.Empty<AttributeEntry>());
    Assert.AreEqual("TestTeam", data.TeamName);
}
```
Pattern: Pure data structs/classes tested without GameObjects.

### SERIALIZATION_ROUNDTRIP
```csharp
// SOURCE: Assets/Tests/EditMode/SavedEventRuleTests.cs (inferred pattern)
[Test]
public void FromRule_ToRule_RoundTrip_PreservesData()
{
    var original = CreateTestRule();
    var saved = SavedEventRule.FromRule(original);
    var restored = saved.ToRule(Key.Alpha1);
    Assert.AreEqual(original.DisplayName, restored.DisplayName);
}
```
Pattern: Test serialization by round-tripping: Original → Saved → Restored, then compare fields.

### ASSEMBLY_DEFINITION
```json
// SOURCE: Assets/Tests/EditMode/Tests.asmdef
{
    "name": "Tests.EditMode",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "EDIRacing.Runtime",
        "Unity.InputSystem"
    ],
    "includePlatforms": ["Editor"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "autoReferenced": false
}
```
Pattern: All tests go in `Assets/Tests/EditMode/`, use `Tests.EditMode` assembly.

---

## Files to Change

### Phase 0: Coverage Tooling (2 files)

| File | Action | Justification |
|---|---|---|
| `Packages/manifest.json` | UPDATE | Add `com.unity.testtools.codecoverage` package |
| `.github/workflows/test.yml` | CREATE | CI pipeline running Unity tests on every PR |

### Phase 1: High-Testability Gaps (8 files)

| File | Action | Justification |
|---|---|---|
| `Assets/Tests/EditMode/SessionDataTests.cs` | CREATE | Test RaceResults, CarResult, EventLogEntry structs |
| `Assets/Tests/EditMode/SavedRaceConfigTests.cs` | UPDATE | Add ApplyTo, edge cases (currently only 3 shallow tests) |
| `Assets/Tests/EditMode/EventScheduleTests.cs` | UPDATE | Add comprehensive scheduling tests (currently 3 tests) |
| `Assets/Tests/EditMode/NetworkMessagesTests.cs` | CREATE | Test all 20+ message type serialization |
| `Assets/Tests/EditMode/SessionManagerTests.cs` | CREATE | Test save/load/export logic with temp directory |
| `Assets/Tests/EditMode/WaypointPathTests.cs` | CREATE | Test waypoint indexing and distance calculation |
| `Assets/Tests/EditMode/CarSpawnerTests.cs` | CREATE | Test spawn positioning logic |
| `Assets/Tests/EditMode/EventRuleTests.cs` | CREATE | Test EventRule struct, LogicOperator enum |

### Phase 2: Logic Extraction + Tests (6-8 files)

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Car/CarPhysicsConfig.cs` | CREATE | Extract physics constants/formulas from CarController |
| `Assets/Tests/EditMode/CarPhysicsConfigTests.cs` | CREATE | Test extracted physics formulas |
| `Assets/Scripts/Network/MessageSerializer.cs` | CREATE | Extract serialization logic from NetworkManager |
| `Assets/Tests/EditMode/MessageSerializerTests.cs` | CREATE | Test message routing/serialization |
| `Assets/Scripts/Race/RaceStateMachine.cs` | CREATE | Extract state transitions from RaceManager |
| `Assets/Tests/EditMode/RaceStateMachineTests.cs` | CREATE | Test state transition validity |

### Phase 3: Deepen Existing Tests (5 files)

| File | Action | Justification |
|---|---|---|
| `Assets/Tests/EditMode/SurveyTemplatesTests.cs` | UPDATE | Add per-template question count validation |
| `Assets/Tests/EditMode/NetCarDataTests.cs` | UPDATE | Add edge cases (empty arrays, null attrs) |
| `Assets/Tests/EditMode/RuleEngineTests.cs` | UPDATE | Add compound condition edge cases |
| `Assets/Tests/EditMode/EventManagerTests.cs` | UPDATE | Add duration expiry, weather effect application |
| `Assets/Tests/EditMode/LapTrackerTests.cs` | UPDATE | Add multi-lap edge cases |

## NOT Building

- PlayMode tests (requires runtime scene, out of scope for this phase)
- UI interaction tests (UGUI requires PlayMode or UI Toolkit test utilities)
- E2E integration tests (requires full race simulation)
- Camera system tests (Transform/Camera dependencies, LOW ROI)
- WebGL-specific tests (platform-dependent)
- Editor tool tests (excluded from coverage target by design)
- Performance/load tests

---

## Step-by-Step Tasks

### Task 1: Install Code Coverage Package

- **ACTION**: Add `com.unity.testtools.codecoverage` to the project
- **IMPLEMENT**: Add `"com.unity.testtools.codecoverage": "1.2.6"` to `Packages/manifest.json` under `dependencies`
- **MIRROR**: Existing package entries in manifest.json
- **IMPORTS**: N/A (Unity Editor auto-imports)
- **GOTCHA**: Version must be compatible with Unity 6.3 LTS. Check Package Manager for latest compatible version. The package generates HTML coverage reports under `CodeCoverage/` — add this to `.gitignore`.
- **VALIDATE**: Open Unity → Window → Analysis → Code Coverage. Verify panel opens without errors.

### Task 2: Run Baseline Coverage Measurement

- **ACTION**: Execute all existing tests with coverage enabled and record baseline
- **IMPLEMENT**:
  1. Open Code Coverage window in Unity Editor
  2. Enable "Generate HTML Report" and "Generate Badges"
  3. Set assembly filter to `EDIRacing.Runtime` only (exclude Editor, WebGL, Tests)
  4. Run all EditMode tests via Test Runner
  5. Record baseline metrics: line coverage %, branch coverage %, per-file breakdown
- **MIRROR**: N/A (manual Unity Editor operation)
- **IMPORTS**: N/A
- **GOTCHA**: First run may be slow as it instruments all assemblies. Ensure only `EDIRacing.Runtime` is included to avoid inflating numbers with untestable Editor code.
- **VALIDATE**: HTML report generated at `CodeCoverage/Report/index.html`. Baseline number recorded.

### Task 3: Create SessionDataTests.cs

- **ACTION**: Test all data structs in SessionData.cs (RaceResults, CarResult, EventLogEntry, SavedRuleCondition)
- **IMPLEMENT**:
  ```csharp
  [TestFixture]
  public class SessionDataTests
  {
      // CarResult.ColorIndex property (computed from Attributes)
      [Test] public void CarResult_ColorIndex_ReturnsZero_WhenNoAttributes()
      [Test] public void CarResult_ColorIndex_ReturnsValue_WhenColorIndexPresent()
      [Test] public void CarResult_ColorIndex_CaseInsensitive()
      [Test] public void CarResult_ColorIndex_ReturnsZero_WhenNotParseable()

      // RaceResults defaults
      [Test] public void RaceResults_DefaultRankings_IsEmpty()
      [Test] public void RaceResults_DefaultEventLog_IsEmpty()

      // SessionData defaults
      [Test] public void SessionData_DefaultCars_IsEmpty()
      [Test] public void SessionData_DefaultEvents_IsEmpty()
  }
  ```
- **MIRROR**: PURE_DATA_TEST, NAMING_CONVENTION
- **IMPORTS**: `using NUnit.Framework; using System;`
- **GOTCHA**: `CarResult.ColorIndex` uses `StringComparison.OrdinalIgnoreCase` — test with mixed case keys.
- **VALIDATE**: All tests pass. Coverage report shows SessionData.cs lines covered.

### Task 4: Create NetworkMessagesTests.cs

- **ACTION**: Test JSON serialization round-trip for all message types
- **IMPLEMENT**: For each of the 20+ message types, test:
  1. Default `type` field is correctly set
  2. JsonUtility.ToJson → JsonUtility.FromJson round-trip preserves data
  3. Empty/null array fields handled correctly
  Focus on: `RaceStartMessage`, `StateUpdateMessage`, `LeaderboardMessage`, `SurveyImportMessage`, `ConfigExportMessage`, `ReconnectStateMessage`
- **MIRROR**: SERIALIZATION_ROUNDTRIP, NAMING_CONVENTION
- **IMPORTS**: `using NUnit.Framework; using UnityEngine; using System;`
- **GOTCHA**: `JsonUtility` requires concrete types — cannot round-trip through `NetworkMessage` base class. Parse `type` field manually first.
- **VALIDATE**: All message types have at least 1 round-trip test. NetworkMessages.cs coverage > 60%.

### Task 5: Create SessionManagerTests.cs

- **ACTION**: Test save/load/export logic using temp directory
- **IMPLEMENT**:
  ```csharp
  [TestFixture]
  public class SessionManagerTests
  {
      private GameObject obj;
      private SessionManager manager;
      private string tempDir;

      [SetUp]
      public void SetUp()
      {
          obj = new GameObject();
          manager = obj.AddComponent<SessionManager>();
          tempDir = Path.Combine(Path.GetTempPath(), "EDIRacingTest_" + Guid.NewGuid());
          // Override SaveFolder to use temp dir
          manager.SaveFolder = tempDir;
      }

      [TearDown]
      public void TearDown()
      {
          Object.DestroyImmediate(obj);
          if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
      }

      [Test] public void SaveSession_CreatesJsonFile()
      [Test] public void LoadSession_ReturnsNull_WhenFileNotFound()
      [Test] public void SaveAndLoad_RoundTrip_PreservesData()
      [Test] public void FindLatestSession_ReturnsNull_WhenNoFiles()
      [Test] public void FindLatestSession_ReturnsMostRecent()
      [Test] public void GetSavedSessionPaths_ReturnsEmpty_WhenNoDir()
  }
  ```
- **MIRROR**: TEST_SETUP, NAMING_CONVENTION
- **IMPORTS**: `using NUnit.Framework; using UnityEngine; using System; using System.IO;`
- **GOTCHA**: `SessionManager.GetSaveDirectory()` is private and uses `Application.persistentDataPath`. Override `SaveFolder` to a temp directory. The `ExportResults` method has `#if UNITY_WEBGL` branching — in Editor, it writes to file.
- **VALIDATE**: All tests pass. No temp files left behind after TearDown.

### Task 6: Create WaypointPathTests.cs

- **ACTION**: Test waypoint path logic (distance, indexing)
- **IMPLEMENT**: Create WaypointPath with mock child transforms, test `GetClosestWaypoint`, path length calculations, and lap detection logic
- **MIRROR**: TEST_SETUP (create GameObjects with child transforms)
- **IMPORTS**: `using NUnit.Framework; using UnityEngine; using System.Collections.Generic;`
- **GOTCHA**: WaypointPath uses `transform.childCount` and child transforms — create actual child GameObjects in SetUp.
- **VALIDATE**: Tests pass. WaypointPath.cs coverage > 60%.

### Task 7: Create EventRuleTests.cs

- **ACTION**: Test EventRule struct fields and LogicOperator enum
- **IMPLEMENT**: Test EventRule field initialization, RuleCondition struct, and LogicOperator.And / LogicOperator.Or enum values
- **MIRROR**: PURE_DATA_TEST
- **IMPORTS**: `using NUnit.Framework;`
- **GOTCHA**: EventRule has `HasBeenTriggered` field — verify it's false by default.
- **VALIDATE**: Tests pass.

### Task 8: Deepen SavedRaceConfigTests.cs

- **ACTION**: Expand from 3 tests to comprehensive coverage
- **IMPLEMENT**: Add tests for:
  1. `FromScriptableObject` copies all fields correctly
  2. `ApplyTo` writes all fields back to ScriptableObject
  3. Round-trip `FromScriptableObject` → `ApplyTo` preserves values
  4. Edge case: zero values, negative values, max laps
- **MIRROR**: SERIALIZATION_ROUNDTRIP
- **IMPORTS**: `using NUnit.Framework; using UnityEngine;`
- **GOTCHA**: `RaceConfig` is a ScriptableObject — create with `ScriptableObject.CreateInstance<RaceConfig>()`, not `new`.
- **VALIDATE**: Tests pass. SavedRaceConfig coverage = 100%.

### Task 9: Deepen EventScheduleTests.cs

- **ACTION**: Expand from 3 tests to cover scheduling logic
- **IMPLEMENT**: Add tests for event timing, ordering, duplicate handling, empty schedule edge case
- **MIRROR**: NAMING_CONVENTION, PURE_DATA_TEST
- **IMPORTS**: `using NUnit.Framework; using UnityEngine;`
- **GOTCHA**: EventSchedule is a ScriptableObject — use `ScriptableObject.CreateInstance<>()`.
- **VALIDATE**: Tests pass. EventSchedule.cs coverage > 80%.

### Task 10: Deepen Existing Test Files

- **ACTION**: Add edge cases to existing test files
- **IMPLEMENT**:
  - `SurveyTemplatesTests.cs`: Add per-template question count assertions, validate all 4 templates have non-empty questions
  - `NetCarDataTests.cs`: Add null attrs, empty teamName, empty array round-trip
  - `EventManagerTests.cs`: Add tests for event with zero duration, event affecting no cars, AllowRepeat=true re-trigger
  - `LapTrackerTests.cs`: Add multi-lap completion, reset between races
- **MIRROR**: Existing patterns in each file
- **IMPORTS**: As per existing files
- **GOTCHA**: Don't break existing tests when adding new ones.
- **VALIDATE**: All old + new tests pass. No regression.

### Task 11: Create CI Test Pipeline

- **ACTION**: Add GitHub Actions workflow for automated test execution
- **IMPLEMENT**: Create `.github/workflows/test.yml` using `game-ci/unity-test-runner@v4` action
  ```yaml
  name: Tests
  on: [push, pull_request]
  jobs:
    test:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: game-ci/unity-test-runner@v4
          with:
            testMode: editmode
            projectPath: .
            coverageOptions: 'generateAdditionalMetrics;assemblyFilters:+EDIRacing.Runtime'
        - uses: actions/upload-artifact@v4
          with:
            name: coverage-report
            path: CodeCoverage
  ```
- **MIRROR**: CI/CD standards in `.claude/docs/coding-standards.md`
- **IMPORTS**: N/A
- **GOTCHA**: Requires Unity license secret (`UNITY_LICENSE`) in GitHub repo settings. `game-ci` requires Docker image with correct Unity version (6.3 LTS). Coverage report upload requires `actions/upload-artifact`.
- **VALIDATE**: Workflow file passes YAML lint. Push to branch triggers CI run.

### Task 12: Re-measure Coverage and Report

- **ACTION**: Run full test suite with coverage after all new tests are added
- **IMPLEMENT**:
  1. Run all EditMode tests with Code Coverage enabled
  2. Generate HTML report
  3. Compare against baseline from Task 2
  4. Document coverage per file in a report
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: Focus on `EDIRacing.Runtime` assembly only. Exclude editor code from metrics.
- **VALIDATE**: Coverage report shows improvement. Document delta from baseline.

---

## Testing Strategy

### Coverage Targets by System

| System | Files | LOC | Current | Target | Gap |
|---|---|---|---|---|---|
| Data | 12 | 1,271 | ~60% | 90% | +30% |
| Events/Rules | 7 | 659 | ~70% | 90% | +20% |
| Race Logic | 7 | 1,046 | ~30% | 70% | +40% |
| Network | 3 | 1,067 | ~10% | 50% | +40% |
| Car | 2 | 536 | ~15% | 40% | +25% |
| UI | 11 | 1,088 | 0% | 20% | +20% |
| Camera | 4 | 304 | 0% | 0% | — |
| Core | 1 | 462 | 0% | 0% | — |

### Expected Coverage After Plan Execution

```
Phase 1 (HIGH testability):     +15-20% → ~36-41%
Phase 2 (logic extraction):     +10-15% → ~46-56%
Phase 3 (deepen existing):      +5-8%   → ~51-64%
───────────────────────────────────────────
Cumulative estimate:             ~55-65%
```

**Note**: Reaching 80% will require a follow-up plan that addresses:
- PlayMode tests for physics and UI interaction
- Logic extraction from MonoBehaviour-heavy files (CarController, RaceManager, NetworkManager)
- Integration tests for multi-system interactions

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| CarResult_ColorIndex | No attributes | 0 | Yes |
| CarResult_ColorIndex | attrs with "colorIndex"="3" | 3 | No |
| CarResult_ColorIndex | attrs with "COLORINDEX"="5" | 5 | Yes (case) |
| SaveSession_RoundTrip | SessionData with 3 cars | Same data restored | No |
| LoadSession_Missing | Non-existent path | null | Yes |
| NetworkMessage_RoundTrip | RaceStartMessage with cars | Same cars restored | No |
| NetworkMessage_Empty | RaceStartMessage, 0 cars | Empty array, not null | Yes |

### Edge Cases Checklist

- [x] Empty input (empty arrays, empty strings)
- [x] Null handling (null attributes, null conditions)
- [x] Case sensitivity (CarResult.ColorIndex)
- [ ] Maximum size input (not applicable for current scope)
- [ ] Concurrent access (not applicable — Unity single-threaded)
- [ ] Network failure (deferred to Phase 2)
- [ ] File I/O failure (test with invalid paths)

---

## Validation Commands

### Run All Tests
```bash
# Via Unity CLI (headless)
/Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testResults TestResults.xml
```
EXPECT: All tests pass. Zero failures.

### Coverage Report
```bash
# Via Unity CLI with coverage
/Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -enableCodeCoverage \
  -coverageResultsPath CodeCoverage \
  -coverageOptions "generateHtmlReport;assemblyFilters:+EDIRacing.Runtime"
```
EXPECT: HTML report at `CodeCoverage/Report/index.html`. Coverage > baseline.

### Static Analysis
```bash
# Verify no compile errors (Unity batch mode)
/Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -quit -logFile -
```
EXPECT: No compilation errors in Unity log.

### Manual Validation

- [ ] Open Unity Editor, open Test Runner (Window → General → Test Runner)
- [ ] Run all EditMode tests — all green
- [ ] Open Code Coverage (Window → Analysis → Code Coverage)
- [ ] Enable coverage, re-run tests
- [ ] Check HTML report for per-file coverage breakdown
- [ ] Verify `EDIRacing.Runtime` assembly coverage percentage

---

## Acceptance Criteria

- [ ] `com.unity.testtools.codecoverage` installed and functional
- [ ] Baseline coverage measured and documented
- [ ] All Phase 1 test files created and passing (Tasks 3-9)
- [ ] Existing test files deepened (Task 10)
- [ ] Coverage increased by at least 15% from baseline
- [ ] No regressions in existing 171 tests
- [ ] CI workflow created (Task 11) — ready for activation when Unity license available
- [ ] Coverage report generated with per-file breakdown

## Completion Checklist

- [ ] All new tests follow `MethodUnderTest_Scenario_ExpectedResult` naming
- [ ] All tests use Arrange/Act/Assert structure
- [ ] All tests clean up GameObjects in TearDown
- [ ] No test depends on execution order or external state
- [ ] Tests use temp directories for file I/O (not real paths)
- [ ] Test assembly definition unchanged (all new tests in `Assets/Tests/EditMode/`)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Code Coverage package incompatible with Unity 6.3 LTS | LOW | HIGH | Check Unity Package Manager compatibility before installing |
| SessionManager tests fail due to Application.persistentDataPath | MEDIUM | LOW | Override SaveFolder to temp directory |
| 80% target unreachable without PlayMode tests | HIGH | MEDIUM | Accept ~55-65% from EditMode-only; plan Phase 2 for PlayMode |
| CI pipeline blocked by missing Unity license | HIGH | LOW | Create workflow file now, activate when license available |
| Extracting logic from MonoBehaviours breaks runtime | LOW | HIGH | Extract read-only computation first; test both old + new paths |

## Notes

### Coverage Calculation Methodology

File-level coverage (33%) overestimates because having a test file doesn't mean full coverage of the production file. Line-level coverage (~21%) is the more accurate metric and should be the primary measure.

### 80% Target Realism

Reaching 80% **line coverage** across `EDIRacing.Runtime` will likely require:

1. **This plan** (Phase 1-3): ~55-65% estimated
2. **Follow-up plan**: Logic extraction from MonoBehaviour-heavy files (CarController, RaceManager, NetworkManager) + PlayMode tests for UI interaction
3. **Final polish**: Edge cases, error paths, remaining branches

The 80% target is achievable but requires 2-3 implementation phases. This plan focuses on the highest-ROI work first.

### Priority Order

If time-constrained, execute tasks in this order:
1. Task 1-2 (tooling + baseline) — foundation
2. Task 3 (SessionDataTests) — most testable gap
3. Task 4 (NetworkMessagesTests) — largest untested file
4. Task 8-9 (deepen shallow tests) — quick wins
5. Task 5-7 (remaining new tests) — moderate effort
6. Task 10 (deepen all) — polish
7. Task 11 (CI) — infrastructure
8. Task 12 (re-measure) — validation
