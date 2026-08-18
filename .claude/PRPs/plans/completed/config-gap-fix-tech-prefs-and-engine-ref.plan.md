# Plan: technical-preferences 填写 + CLAUDE.md 引擎引用修正

## Summary
Fill in all 25 `[TO BE CONFIGURED]` placeholders in `.claude/docs/technical-preferences.md` based on actual codebase conventions discovered through exploration, and fix the incorrect Godot engine reference in `CLAUDE.md` to point to the Unity VERSION.md.

## User Story
As a Claude Code agent working on this project,
I want the technical-preferences.md to accurately reflect the project's actual conventions and the CLAUDE.md to reference the correct engine docs,
So that all agents operate consistently and don't receive misleading engine documentation.

## Problem → Solution
`technical-preferences.md` has 25 placeholder values despite the project being mature with established conventions → Fill them from observed codebase patterns.
`CLAUDE.md` references `@docs/engine-reference/godot/VERSION.md` for a Unity project → Change to `@docs/engine-reference/unity/VERSION.md`.

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A (Gap Analysis Report)
- **PRD Phase**: GAP 3 + GAP 6
- **Estimated Files**: 2

---

## UX Design

N/A — internal configuration change, no user-facing UX transformation.

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `.claude/docs/technical-preferences.md` | all | File being updated |
| P0 (critical) | `CLAUDE.md` | 25-26 | Line with wrong engine ref |
| P1 (important) | `docs/engine-reference/unity/VERSION.md` | all | Correct target for engine reference |
| P2 (reference) | `Assets/Scripts/Car/CarController.cs` | 1-55 | Naming convention evidence |
| P2 (reference) | `Assets/Scripts/Events/EventManager.cs` | 1-20 | Naming convention evidence |
| P2 (reference) | `Assets/Scripts/Data/CarData.cs` | 1-15 | Struct naming evidence |
| P2 (reference) | `Assets/Scripts/Race/RaceConfig.cs` | 1-10 | ScriptableObject naming |
| P2 (reference) | `Assets/Scripts/Editor/BuildScript.cs` | all | Build system evidence |
| P2 (reference) | `Packages/manifest.json` | all | Package dependencies |
| P2 (reference) | `ProjectSettings/ProjectVersion.txt` | 1 | Unity version |
| P2 (reference) | `Assets/Tests/EditMode/CsvParserTests.cs` | 1-10 | Test framework evidence |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| N/A | N/A | No external research needed — all values derived from codebase observation |

---

## Patterns to Mirror

Code patterns discovered in the codebase. These inform the values to fill in.

### NAMING_CONVENTION — Classes
// SOURCE: Assets/Scripts/Car/CarController.cs:12, Assets/Scripts/Data/CarData.cs:22
// PascalCase for classes and structs: `CarController`, `CarData`, `EventManager`, `RaceConfig`

### NAMING_CONVENTION — Variables
// SOURCE: Assets/Scripts/Car/CarController.cs:14-46
// camelCase for private fields: `agent`, `waypointPath`, `currentWaypointIndex`, `baseSpeed`
// PascalCase for public fields/properties: `Schedule`, `Config`, `DefaultCsvData`

### NAMING_CONVENTION — Events
// SOURCE: Assets/Scripts/Events/EventManager.cs:18, Assets/Scripts/Network/NetworkManager.cs:40-48
// C# event pattern with `Action<T>`: `event Action<EventRule, int> OnEventTriggered`
// `On` prefix for events: `OnConnected`, `OnDisconnected`, `OnRoomCreated`, `OnStateChanged`

### NAMING_CONVENTION — Files
// SOURCE: Assets/Scripts/ directory structure
// PascalCase matching class name: `CarController.cs`, `EventManager.cs`, `RaceConfig.cs`
// Feature-based folder organization: Car/, Race/, Events/, Data/, UI/, Network/, Camera/

### NAMING_CONVENTION — Scenes/Prefabs
// SOURCE: Assets/Scenes/, Assets/Prefabs/Cars/
// Scenes: snake_case (`complete_track_demo.unity`, `SampleScene.unity`)
// Prefabs: PascalCase with number suffix for variants (`Car1.prefab`, `Car_Red.prefab`)

### NAMING_CONVENTION — Constants
// SOURCE: Assets/Scripts/Car/CarController.cs:55
// PascalCase: `private const int MaxRecoveryAttempts = 3;`

### TEST_STRUCTURE
// SOURCE: Assets/Tests/EditMode/CsvParserTests.cs:1-6
```csharp
using NUnit.Framework;
[TestFixture]
public class CsvParserTests
{
    [Test]
    public void Parse_EmptyString_ReturnsEmptyList() { ... }
}
```
// Pattern: NUnit with Unity Test Framework, `[ClassName]_[Scenario]_[Expected]` method naming

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `.claude/docs/technical-preferences.md` | UPDATE | Fill in all 25 `[TO BE CONFIGURED]` placeholders |
| `CLAUDE.md` | UPDATE | Fix engine reference from godot to unity |

## NOT Building

- No new files created
- No code changes to C# scripts
- No Unity editor changes
- No test additions
- Not changing `docs/CLAUDE.md` (separate file in docs/ that also references godot — that's GAP 5 scope, not this plan)

---

## Step-by-Step Tasks

### Task 1: Fix CLAUDE.md engine reference (GAP 6)
- **ACTION**: Change the `@` reference from Godot to Unity VERSION.md
- **IMPLEMENT**: Replace `@docs/engine-reference/godot/VERSION.md` with `@docs/engine-reference/unity/VERSION.md` on line 26
- **MIRROR**: N/A — single line replacement
- **IMPORTS**: N/A
- **GOTCHA**: The file `docs/engine-reference/unity/VERSION.md` already exists and is correctly populated for Unity 6.3 LTS. Also note that `docs/CLAUDE.md` (a separate file) also references godot — that file is out of scope for this plan but should be noted for a future fix.
- **VALIDATE**: Read `CLAUDE.md` and verify the `@` reference now points to `unity/VERSION.md`

### Task 2: Fill Engine & Language section
- **ACTION**: Fill in Rendering and Physics values
- **IMPLEMENT**:
  - **Rendering**: `URP (Universal Render Pipeline) 17.3.0` — evidenced by `com.unity.render-pipelines.universal: 17.3.0` in manifest.json
  - **Physics**: `Unity Built-in Physics (PhysX) + AI Navigation 2.0.13` — evidenced by `com.unity.modules.physics` and `com.unity.ai.navigation: 2.0.13` in manifest.json; NavMeshAgent used in CarController
- **MIRROR**: Match existing bullet format in the section
- **IMPORTS**: N/A
- **GOTCHA**: The project uses NavMeshAgent for car movement, not traditional physics rigidbodies for locomotion. Physics is used for collision detection (trigger-based).
- **VALIDATE**: Values match package versions in manifest.json

### Task 3: Fill Input & Platform section
- **ACTION**: Fill in all 6 input/platform values
- **IMPLEMENT**:
  - **Target Platforms**: `WebGL (primary), Editor (development)`
  - **Input Methods**: `Keyboard`
  - **Primary Input**: `Keyboard — teacher-operated; students observe or join via web browser`
  - **Gamepad Support**: `None`
  - **Touch Support**: `None`
  - **Platform Notes**: `WebGL build target (BuildScript.cs). Custom WebGL template (EDIRacing). WebSocket-based real-time networking for student participation via web browser. Memory limit: 2048 MB max (webGLMaximumMemorySize).`
- **MIRROR**: Match existing bullet format
- **IMPORTS**: N/A
- **GOTCHA**: BuildScript.cs explicitly targets WebGL only. The Input System package (1.19.0) is installed but the project primarily uses `Keyboard.current` direct access pattern.
- **VALIDATE**: Cross-reference with BuildScript.cs target and ProjectSettings webGL values

### Task 4: Fill Naming Conventions section
- **ACTION**: Fill in all 6 naming convention values
- **IMPLEMENT**:
  - **Classes**: `PascalCase (e.g., CarController, EventManager, RaceConfig)`
  - **Variables**: `camelCase for private fields, PascalCase for public fields/properties`
  - **Signals/Events**: `C# events with Action<T> delegate, On prefix (e.g., OnEventTriggered, OnStateChanged)`
  - **Files**: `PascalCase matching class name (e.g., CarController.cs, RaceManager.cs)`
  - **Scenes/Prefabs**: `Scenes: snake_case (complete_track_demo.unity); Prefabs: PascalCase (Car1.prefab, Car_Red.prefab)`
  - **Constants**: `PascalCase (e.g., MaxRecoveryAttempts)`
- **MIRROR**: Pattern evidence in Patterns to Mirror section above
- **IMPORTS**: N/A
- **GOTCHA**: None
- **VALIDATE**: Spot-check 3 random .cs files to verify naming matches

### Task 5: Fill Performance Budgets section
- **ACTION**: Fill in all 4 performance budget values
- **IMPLEMENT**:
  - **Target Framerate**: `60 FPS (WebGL standard)`
  - **Frame Budget**: `16.67 ms`
  - **Draw Calls**: `Not specified — optimize as needed for WebGL`
  - **Memory Ceiling**: `2048 MB (webGLMaximumMemorySize in ProjectSettings)`
- **MIRROR**: Match existing bullet format
- **IMPORTS**: N/A
- **GOTCHA**: WebGL has stricter performance constraints than native builds. Memory growth mode is set to 2 (geometric) with 64 MB initial.
- **VALIDATE**: Cross-reference webGLMaximumMemorySize in ProjectSettings.asset

### Task 6: Fill Testing section
- **ACTION**: Fill in framework and coverage values
- **IMPLEMENT**:
  - **Framework**: `Unity Test Framework 1.6.0 + NUnit (EditMode tests)`
  - **Minimum Coverage**: `80% (per coding-standards.md)`
- **MIRROR**: Test structure pattern from CsvParserTests.cs
- **IMPORTS**: N/A
- **GOTCHA**: Currently only 5 EditMode tests exist — coverage is well below 80%. This is documented as GAP 4 in the gap analysis.
- **VALIDATE**: Verify test assembly definition references match

### Task 7: Fill Engine Specialists section
- **ACTION**: Fill in all 7 specialist values and the file extension routing table
- **IMPLEMENT**:
  - **Primary**: `unity-specialist`
  - **Language/Code Specialist**: `unity-specialist` (C# is the only language)
  - **Shader Specialist**: `unity-shader-specialist`
  - **UI Specialist**: `unity-ui-specialist`
  - **Additional Specialists**: `unity-addressables-specialist (if Addressables adopted), unity-dots-specialist (if DOTS adopted)`
  - **Routing Notes**: `Project uses URP + UGUI + NavMesh. No DOTS/ECS. Shader work is minimal (asset store materials). UI is legacy UGUI (Text, Button, InputField).`

  File Extension Routing table:
  | File Extension / Type | Specialist to Spawn |
  |-----------------------|---------------------|
  | Game code (`*.cs`) | unity-specialist |
  | Shader / material files (`*.shader`, `*.shadergraph`, `*.mat`) | unity-shader-specialist |
  | UI / screen files (UI-related `*.cs`, `*.uxml`, `*.uss`) | unity-ui-specialist |
  | Scene / prefab / level files (`*.unity`, `*.prefab`) | unity-specialist |
  | Native extension / plugin files (`*.jslib`) | unity-specialist |
  | General architecture review | Primary |
- **MIRROR**: Match existing table format
- **IMPORTS**: N/A
- **GOTCHA**: The project uses legacy UGUI (com.unity.ugui 2.0.0), not UI Toolkit. The unity-ui-specialist should be aware of this.
- **VALIDATE**: Verify agent names match available agent types in the system

---

## Testing Strategy

### Manual Validation
No automated tests needed — this is a configuration file update.

| Check | Method | Expected |
|---|---|---|
| No `[TO BE CONFIGURED]` remaining | `grep -c "TO BE CONFIGURED" .claude/docs/technical-preferences.md` | 0 |
| No Godot reference in CLAUDE.md | `grep "godot" CLAUDE.md` | No matches |
| Unity reference present | `grep "unity/VERSION.md" CLAUDE.md` | 1 match |
| File parseable | Read both files end-to-end | No formatting errors |

### Edge Cases Checklist
- [x] Empty input — N/A (editing existing files)
- [x] Concurrent access — N/A (config files, not runtime)
- [ ] Verify docs/CLAUDE.md also references godot (out of scope, flag for future)

---

## Validation Commands

### Grep Verification
```bash
# Verify no placeholders remain
grep -c "TO BE CONFIGURED" .claude/docs/technical-preferences.md
```
EXPECT: `0`

```bash
# Verify no Godot ref in main CLAUDE.md
grep -i "godot" CLAUDE.md
```
EXPECT: No output (0 matches)

```bash
# Verify Unity ref exists
grep "unity/VERSION.md" CLAUDE.md
```
EXPECT: 1 match

### Manual Validation
- [ ] Read `.claude/docs/technical-preferences.md` — all values filled, no `[TO BE CONFIGURED]`
- [ ] Read `CLAUDE.md` line ~26 — engine reference points to `unity/VERSION.md`
- [ ] Values in technical-preferences match observed codebase patterns

---

## Acceptance Criteria
- [ ] All 25 `[TO BE CONFIGURED]` placeholders in technical-preferences.md are replaced with actual values
- [ ] `CLAUDE.md` engine reference changed from `godot/VERSION.md` to `unity/VERSION.md`
- [ ] All filled values are accurate (verifiable from codebase evidence)
- [ ] File formatting is preserved (no broken markdown)
- [ ] No unrelated changes introduced

## Completion Checklist
- [ ] Values match discovered codebase patterns (cross-referenced with source files)
- [ ] Markdown formatting intact
- [ ] No scope creep (only the 2 specified files changed)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Incorrect convention inference | Low | Low | Each value has source evidence from actual code |
| docs/CLAUDE.md also has stale godot ref | Confirmed | Low | Noted as separate follow-up; out of scope for this plan |
| Performance budget guesses inaccurate | Medium | Low | Values are conservative defaults for WebGL; can be tuned later |

## Notes
- `docs/CLAUDE.md` (the one inside docs/) also references godot at its bottom. This is a separate file from the root `CLAUDE.md` and should be fixed in a follow-up. It's flagged here but not in scope.
- The "Forbidden Patterns", "Allowed Libraries / Addons", and "Architecture Decisions Log" sections are left with their current empty-state text since they are intentionally open-ended (not `[TO BE CONFIGURED]` placeholders).
- Unity version is `6000.3.19f1` (Unity 6.3 LTS), matching `docs/engine-reference/unity/VERSION.md`.
