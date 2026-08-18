# Plan: UI & Camera System

## Summary
Implement the complete UI layer and camera system for the EDI Racing Game v2. This includes professor controls (setup screen, event panel, free camera WASD+mouse, fixed positions 1-9, pause/resume), student spectator view (auto-follow camera, leaderboard overlay), team name labels on cars, and a real-time score dashboard. Currently the game only outputs to Debug.Log — this phase adds proper in-game UI using Unity's UGUI system.

## User Story
As a professor, I want visual controls and a camera system so that I can manage the race and present it to students without relying on keyboard shortcuts and console output.

As a student, I want to spectate the race from my browser with an automatic camera and leaderboard, so that I can follow the race progress and discuss outcomes.

## Problem → Solution
All interactions are keyboard shortcuts with Debug.Log output only → Full in-game UI with visual controls, leaderboard, event panel, and flexible camera system for both professor and student roles.

## Metadata
- **Complexity**: Large
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 4 — UI & Camera
- **Estimated Files**: 12 new C# scripts + 2 updated existing files

---

## UX Design

### Before
```
┌─────────────────────────────────────────────┐
│  Only Debug.Log output in Console           │
│  Keyboard shortcuts: 1-7 events, T=score,   │
│  P=save, L=load, X=export                   │
│  No visual leaderboard, no camera controls  │
│  Fixed default camera position              │
└─────────────────────────────────────────────┘
```

### After
```
┌─────────────────────────────────────────────────────┐
│ PROFESSOR VIEW                                      │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │                                             │    │
│  │         3D Race View                        │    │
│  │    (Free camera: WASD + Mouse)              │    │
│  │    (Fixed positions: F1-F9)                 │    │
│  │                                             │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
│  ┌──────────┐  ┌──────────────────────────────┐    │
│  │Leaderboard│  │ Event Panel                  │    │
│  │ 1. Team A │  │ [1] Name Penalty   [Trigger]│    │
│  │ 2. Team B │  │ [2] Color Boost    [Trigger]│    │
│  │ 3. Team C │  │ [3] Color Penalty  [Trigger]│    │
│  │ ...       │  │ [4] Func Boost     [Trigger]│    │
│  └──────────┘  │ [5] Func Penalty   [Trigger]│    │
│                 │ [6] Snow Weather   [Trigger]│    │
│  [Pause][Resume]│ [7] Night Weather  [Trigger]│    │
│  [Save][Export] └──────────────────────────────┘    │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ STUDENT VIEW                                        │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │                                             │    │
│  │         3D Race View                        │    │
│  │    (Auto-follow camera on leader)           │    │
│  │                                             │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
│  ┌──────────┐                                       │
│  │Leaderboard│  (Read-only, no controls)            │
│  │ 1. Team A │                                      │
│  │ 2. Team B │                                      │
│  │ 3. Team C │                                      │
│  └──────────┘                                       │
└─────────────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Scoreboard | T key → Debug.Log | Always-visible leaderboard panel | Updates every frame |
| Event triggers | 1-7 keys (hidden) | 1-7 keys + visual buttons in Event Panel | Both work simultaneously |
| Camera | Fixed default | Professor: WASD free + F1-F9 fixed; Student: auto-follow | Role-based |
| Race controls | None | Pause/Resume/Save/Export buttons | Professor only |
| Car labels | None | Floating team name labels above cars | World-space UI |
| Session I/O | P/L/X keys | Buttons + keyboard shortcuts (both work) | Professor only |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/Race/RaceManager.cs` | all | Central orchestrator — UI hooks into events/state here |
| P0 (critical) | `Assets/Scripts/Race/ScoreManager.cs` | all | GetRankedCars() drives leaderboard content |
| P0 (critical) | `Assets/Scripts/Events/EventManager.cs` | 60-91 | TriggerEvent(int) — UI buttons call this |
| P1 (important) | `Assets/Scripts/Events/EventSchedule.cs` | all | Events array — UI displays this list |
| P1 (important) | `Assets/Scripts/Car/CarIdentity.cs` | all | TeamName for labels, CurrentLap for leaderboard |
| P1 (important) | `Assets/Scripts/Race/RaceConfig.cs` | all | ScriptableObject pattern for config |
| P2 (reference) | `Assets/Scripts/Events/WeatherEffect.cs` | all | Weather state for UI indicator |
| P2 (reference) | `Assets/Scripts/Data/SessionManager.cs` | all | Save/Export methods for UI buttons |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| None needed | — | Unity UGUI (com.unity.ugui 2.0.0) is well-known; no external research required |

---

## Patterns to Mirror

### MONOBEHAVIOUR_COMPONENT
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:10-11
public class RaceManager : MonoBehaviour
{
    [Header("References")]
    public CarSpawner CarSpawner;
```

### SCRIPTABLE_OBJECT_CONFIG
```csharp
// SOURCE: Assets/Scripts/Race/RaceConfig.cs:7-8
[CreateAssetMenu(fileName = "RaceConfig", menuName = "EDI Racing/Race Config")]
public class RaceConfig : ScriptableObject
```

### DEBUG_LOGGING
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:44
Debug.Log($"[RaceManager] Parsed {carDataList.Count} cars from CSV");
// Pattern: [ClassName] message with context
```

### EVENT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Events/EventManager.cs:18
public event Action<RaceEventConfig, int> OnEventTriggered;
// Pattern: C# events for cross-component communication
```

### SERIALIZABLE_DATA
```csharp
// SOURCE: Assets/Scripts/Data/CarData.cs:7-9
[Serializable]
public struct CarData
{
```

### HEADER_TOOLTIP_ATTRIBUTES
```csharp
// SOURCE: Assets/Scripts/Race/CarSpawner.cs:12-13
[Tooltip("Car prefabs indexed by color: 0=green, 1=black, 2=red, 3=blue, 4=white")]
public GameObject[] CarPrefabs;
```

### SUMMARY_DOCSTRING
```csharp
// SOURCE: Assets/Scripts/Race/ScoreManager.cs:7-8
/// <summary>
/// Ranks cars by race progress: most checkpoints passed, then least time.
/// </summary>
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/UI/RaceUI.cs` | CREATE | Top-level UI controller — manages panels, role switching |
| `Assets/Scripts/UI/LeaderboardPanel.cs` | CREATE | Real-time leaderboard from ScoreManager.GetRankedCars() |
| `Assets/Scripts/UI/EventPanel.cs` | CREATE | Professor event trigger buttons + status display |
| `Assets/Scripts/UI/RaceControlPanel.cs` | CREATE | Pause/Resume/Save/Export buttons for professor |
| `Assets/Scripts/UI/CarLabel.cs` | CREATE | World-space floating team name label per car |
| `Assets/Scripts/UI/CarLabelSpawner.cs` | CREATE | Spawns CarLabel instances after cars are created |
| `Assets/Scripts/Camera/RaceCameraController.cs` | CREATE | Free camera (WASD+mouse) for professor |
| `Assets/Scripts/Camera/SpectatorCamera.cs` | CREATE | Auto-follow camera for student view |
| `Assets/Scripts/Camera/CameraManager.cs` | CREATE | Switches between free/fixed/spectator cameras |
| `Assets/Scripts/Camera/FixedCameraPoint.cs` | CREATE | Component placed at predefined camera positions |
| `Assets/Scripts/UI/SetupScreen.cs` | CREATE | Pre-race setup: CSV import button, session load |
| `Assets/Scripts/UI/GameState.cs` | CREATE | Enum + state machine: Setup → Racing → Finished |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Add pause/resume, expose events for UI, add GameState |
| `Assets/Scripts/Race/CarSpawner.cs` | UPDATE | Emit event after spawning for CarLabelSpawner |

## NOT Building

- Main Menu scene (single-scene for now; setup screen is an overlay)
- In-game survey UI (Phase 5 scope — requires networking)
- Settings/options screen
- Custom event editor UI (events configured via ScriptableObject in Inspector)
- Responsive mobile layout (desktop/WebGL primary)
- Sound effects (Phase 7 scope)

---

## Step-by-Step Tasks

### Task 1: GameState Enum and State Machine
- **ACTION**: Create a `GameState` enum and state-tracking component to manage UI transitions
- **IMPLEMENT**:
  ```csharp
  public enum GameState { Setup, Racing, Paused, Finished }
  ```
  Simple state enum in its own file. RaceManager gains `CurrentState` property + `OnStateChanged` event.
- **MIRROR**: SERIALIZABLE_DATA pattern (simple type in own file), EVENT_PATTERN for OnStateChanged
- **IMPORTS**: `System; UnityEngine`
- **GOTCHA**: Keep this minimal — it's a value type, not a MonoBehaviour. The state lives on RaceManager.
- **VALIDATE**: Project compiles; RaceManager.CurrentState reflects correct phase

### Task 2: Update RaceManager for UI Integration
- **ACTION**: Add pause/resume API, GameState tracking, and public events that UI can subscribe to
- **IMPLEMENT**:
  - Add `public GameState CurrentState` property
  - Add `public event Action<GameState> OnStateChanged`
  - Add `PauseRace()` / `ResumeRace()` methods (set `Time.timeScale`)
  - Fire `OnStateChanged` in `LoadAndStartRace`, `ResetRace`, pause/resume
  - Add `public List<GameObject> SpawnedCars => spawnedCars` (read-only accessor for UI)
- **MIRROR**: EVENT_PATTERN, DEBUG_LOGGING
- **IMPORTS**: Existing imports sufficient
- **GOTCHA**: `Time.timeScale = 0` pauses NavMeshAgent; coroutines that use `WaitForSeconds` also pause — this is desired behavior. UI animations should use `Time.unscaledDeltaTime`.
- **VALIDATE**: Pressing pause freezes all cars; resume continues; state event fires

### Task 3: CameraManager
- **ACTION**: Create camera manager that owns switching between free/fixed/spectator modes
- **IMPLEMENT**:
  ```csharp
  public class CameraManager : MonoBehaviour
  {
      public RaceCameraController FreeCamera;
      public SpectatorCamera SpectatorCam;
      public FixedCameraPoint[] FixedPoints;
      public enum CameraMode { Free, Fixed, Spectator }
      public void SetMode(CameraMode mode, int fixedIndex = 0);
  }
  ```
  F1-F9 switch to fixed positions; Escape returns to free camera; spectator mode for students.
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, HEADER_TOOLTIP_ATTRIBUTES
- **IMPORTS**: `UnityEngine; UnityEngine.InputSystem`
- **GOTCHA**: Only one camera active at a time. Disable/enable GameObjects or use Camera.enabled toggling.
- **VALIDATE**: F1-F9 snaps camera; Escape returns to free; spectator follows leader

### Task 4: RaceCameraController (Free Camera)
- **ACTION**: Create WASD + mouse look free camera for professor
- **IMPLEMENT**:
  - WASD for movement (scaled by `Time.unscaledDeltaTime` so it works while paused)
  - Mouse look with right-click hold (typical Unity free camera)
  - Q/E for altitude
  - Scroll wheel for speed adjustment
  - Configurable: `moveSpeed`, `lookSensitivity`, `minSpeed`, `maxSpeed`
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, HEADER_TOOLTIP_ATTRIBUTES
- **IMPORTS**: `UnityEngine; UnityEngine.InputSystem`
- **GOTCHA**: Use `unscaledDeltaTime` so camera works when game is paused. Lock/hide cursor only during right-click hold.
- **VALIDATE**: Camera moves freely; works when paused; doesn't clip through terrain at normal speeds

### Task 5: SpectatorCamera (Auto-Follow)
- **ACTION**: Create auto-follow camera that tracks the race leader (or cycles through top cars)
- **IMPLEMENT**:
  - Reference to `ScoreManager` to get the current leader
  - Smooth follow with offset (behind + above the target)
  - Periodically (every 5s) check if leader changed and transition smoothly
  - `followOffset`, `followDistance`, `followHeight`, `smoothTime` configurable
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, HEADER_TOOLTIP_ATTRIBUTES
- **IMPORTS**: `UnityEngine; System.Collections.Generic`
- **GOTCHA**: Leader can be null if no cars spawned yet. Cars can be destroyed (unlikely but guard against it). Use `SmoothDamp` for smooth transitions.
- **VALIDATE**: Camera smoothly follows the leading car; transitions when lead changes

### Task 6: FixedCameraPoint
- **ACTION**: Simple component placed on GameObjects at predefined camera positions
- **IMPLEMENT**:
  ```csharp
  public class FixedCameraPoint : MonoBehaviour
  {
      [Tooltip("Index 0-8, maps to F1-F9")]
      public int PointIndex;
  }
  ```
  CameraManager finds these at startup via `FindObjectsByType<FixedCameraPoint>`.
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, HEADER_TOOLTIP_ATTRIBUTES
- **IMPORTS**: `UnityEngine`
- **GOTCHA**: Minimal component — just marks a position. Don't over-engineer.
- **VALIDATE**: CameraManager discovers points; pressing F-key moves camera there

### Task 7: RaceUI (Top-Level Controller)
- **ACTION**: Create the root UI controller that manages all panels and role-based visibility
- **IMPLEMENT**:
  ```csharp
  public class RaceUI : MonoBehaviour
  {
      public enum UserRole { Professor, Student }
      [Header("Panels")]
      public LeaderboardPanel Leaderboard;
      public EventPanel Events;
      public RaceControlPanel Controls;
      public SetupScreen Setup;
      [Header("Configuration")]
      public UserRole Role = UserRole.Professor;
      
      // Subscribe to RaceManager.OnStateChanged to show/hide panels
  }
  ```
  Professor sees all panels; Student sees only Leaderboard.
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, HEADER_TOOLTIP_ATTRIBUTES, EVENT_PATTERN
- **IMPORTS**: `UnityEngine; UnityEngine.UI`
- **GOTCHA**: All panels are children of a Canvas. Use `gameObject.SetActive()` for show/hide. Setup screen hides when race starts.
- **VALIDATE**: Role switch hides/shows correct panels; state transitions trigger panel changes

### Task 8: LeaderboardPanel
- **ACTION**: Create real-time leaderboard that polls ScoreManager
- **IMPLEMENT**:
  - Update interval: every 0.5s (not every frame — avoid GC pressure)
  - Display: rank, team name, lap count, color indicator
  - Show top 10 by default, scrollable for full list
  - Highlight top 3 with gold/silver/bronze colors
  - Uses `ScoreManager.GetRankedCars()` to get current standings
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, SUMMARY_DOCSTRING
- **IMPORTS**: `UnityEngine; UnityEngine.UI; System.Collections.Generic`
- **GOTCHA**: Object pooling for leaderboard rows to avoid GC. Pre-instantiate enough rows (50 max cars). Use `SetActive(false)` for unused rows.
- **VALIDATE**: Leaderboard shows correct rankings; updates as cars pass checkpoints; handles 50 cars without frame drops

### Task 9: EventPanel
- **ACTION**: Create professor-only panel showing event buttons with status
- **IMPLEMENT**:
  - One row per event from `EventSchedule.Events[]`
  - Each row: event name, keyboard shortcut hint, [Trigger] button, status (ready/triggered/active)
  - Button click calls `EventManager.TriggerEvent(index)`
  - Visual feedback: button grays out after triggered (if !AllowRepeat)
  - Shows affected count after trigger
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, DEBUG_LOGGING
- **IMPORTS**: `UnityEngine; UnityEngine.UI`
- **GOTCHA**: Subscribe to `EventManager.OnEventTriggered` for status updates. Button interactable state reflects `HasBeenTriggered && !AllowRepeat`.
- **VALIDATE**: Clicking button triggers event; keyboard shortcut still works; button disables for non-repeatable events

### Task 10: RaceControlPanel
- **ACTION**: Create professor-only controls: Pause, Resume, Save Session, Export Results
- **IMPLEMENT**:
  - Four buttons mapping to existing RaceManager/SessionManager methods
  - Pause/Resume toggle (single button that changes label)
  - Save → calls `SessionManager.SaveSession(BuildSessionData())`
  - Export → calls `SessionManager.ExportResults(...)`
  - Status text showing feedback ("Session saved!", "Results exported!")
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, DEBUG_LOGGING
- **IMPORTS**: `UnityEngine; UnityEngine.UI; System.Collections`
- **GOTCHA**: `BuildSessionData()` is currently private on RaceManager — make it public or expose via method. Status text fades after 3s using coroutine with `WaitForSecondsRealtime` (works while paused).
- **VALIDATE**: Buttons work; status text appears and fades; pause/resume toggles correctly

### Task 11: SetupScreen
- **ACTION**: Create pre-race overlay for CSV import and session loading
- **IMPLEMENT**:
  - Shown in `GameState.Setup`; hides when race starts
  - "Load CSV" button (in WebGL: uses a hidden file input via JS interop; in Editor: uses default TextAsset)
  - "Load Session" button → shows list from `SessionManager.GetSavedSessionPaths()`
  - "Start Race" button (active only when data is loaded)
  - Displays car count after CSV load
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, DEBUG_LOGGING
- **IMPORTS**: `UnityEngine; UnityEngine.UI; System.Collections.Generic`
- **GOTCHA**: For now (pre-WebGL), keep it simple: use the DefaultCsvData TextAsset reference for setup. The full file-picker JS interop is Phase 6 scope. Provide a "Start with Default Data" button.
- **VALIDATE**: Setup screen appears on load; can start race with default CSV; hides when race begins

### Task 12: CarLabel (World-Space Floating Name)
- **ACTION**: Create floating team name label above each car
- **IMPLEMENT**:
  - World-space Canvas per car (or single shared world-space Canvas with pooled elements)
  - `Text` showing `CarIdentity.TeamName`
  - Always faces camera (billboard effect)
  - Fixed offset above car (configurable height)
  - Optional: color strip matching car color
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, HEADER_TOOLTIP_ATTRIBUTES
- **IMPORTS**: `UnityEngine; UnityEngine.UI`
- **GOTCHA**: World-space UI per car is simpler than screen-space projection for 50 cars. Keep text size small. Billboard via `transform.LookAt(camera)` with y-rotation only to prevent tilt.
- **VALIDATE**: Labels appear above all cars; face camera; readable at racing distance

### Task 13: CarLabelSpawner
- **ACTION**: Spawn CarLabel instances after RaceManager creates cars
- **IMPLEMENT**:
  - Subscribe to `RaceManager.OnStateChanged` — when Racing starts, iterate `SpawnedCars` and add labels
  - Creates a small world-space Canvas + Text as child of each car
  - Reads `CarIdentity.TeamName` for text content
  - Cleanup on `ResetRace`
- **MIRROR**: MONOBEHAVIOUR_COMPONENT, EVENT_PATTERN
- **IMPORTS**: `UnityEngine; UnityEngine.UI; System.Collections.Generic`
- **GOTCHA**: Instantiate labels AFTER cars are spawned (subscribe to state change, not Start). Clean up labels when race resets.
- **VALIDATE**: All 37 test cars get labels; labels destroyed on reset; no orphaned GameObjects

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| LeaderboardPanel with 0 cars | Empty ScoreManager | Empty leaderboard, no errors | Yes |
| LeaderboardPanel with 50 cars | 50 registered cars | All 50 shown, correct order | Boundary |
| EventPanel button triggers event | Button click on index 2 | EventManager.TriggerEvent(2) called | No |
| EventPanel non-repeatable disabled | Event with AllowRepeat=false triggered | Button interactable=false | No |
| GameState transitions | Setup→Racing→Paused→Racing→Finished | Correct panels shown/hidden | No |
| SpectatorCamera null leader | No cars spawned | Camera stays at default position | Yes |
| CarLabel billboard | Camera at various angles | Label always faces camera | No |

### Edge Cases Checklist
- [ ] Zero cars (empty CSV)
- [ ] Maximum 50 cars (performance test)
- [ ] Pause while event duration is active (coroutine pauses too — correct)
- [ ] Camera at extreme positions (very far, very close)
- [ ] Rapid event triggering (all 7 in quick succession)
- [ ] State transition from Paused directly to reset
- [ ] Multiple session saves in quick succession
- [ ] Student role cannot access professor panels (visual verification)

---

## Validation Commands

### Static Analysis
```bash
# Unity will validate C# on domain reload — no external CLI needed
# Open Unity → check Console for compilation errors
```
EXPECT: Zero compilation errors in Console

### Unit Tests
```bash
# Unity Test Runner (Edit Mode tests if applicable)
# For now: manual verification against test table above
```
EXPECT: All test scenarios pass

### Play Mode Verification
```bash
# Enter Play Mode in Unity Editor
# 1. Setup screen appears
# 2. Click "Start with Default Data" → race begins
# 3. Leaderboard shows ranked cars
# 4. Event panel buttons work (click + keyboard)
# 5. Pause/Resume works
# 6. F1-F9 switch camera positions
# 7. Free camera WASD+mouse works
```
EXPECT: All 7 steps verified visually

### Performance Validation
```bash
# In Unity: Window > Analysis > Profiler
# Start race with 50 cars
# Monitor: Frame time, GC allocations from UI updates
```
EXPECT: Stable 30+ FPS with 50 cars + UI; no per-frame GC allocations from leaderboard

---

## Acceptance Criteria
- [ ] All tasks completed (13 tasks)
- [ ] All validation commands pass
- [ ] Professor sees: leaderboard, event panel, controls, free camera
- [ ] Student sees: leaderboard only, spectator camera
- [ ] Event panel buttons trigger events correctly
- [ ] Pause/Resume stops/continues the race
- [ ] Camera system: free (WASD+mouse), fixed (F1-F9), spectator (auto-follow)
- [ ] Car labels show team names, face camera
- [ ] Leaderboard updates in real-time
- [ ] Setup screen allows starting race
- [ ] No type errors / compilation errors
- [ ] 50-car performance: 30+ FPS

## Completion Checklist
- [ ] Code follows discovered patterns (MonoBehaviour, [Header], event Action, Debug.Log format)
- [ ] Error handling: null checks for references not yet assigned
- [ ] Logging follows `[ClassName] message` pattern
- [ ] No hardcoded values (use [Header] serialized fields)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| World-space UI performance with 50 car labels | MEDIUM | Frame drops below 30 FPS | Use object pooling; test early at 50 cars; can reduce to top-N labels |
| Camera clipping through terrain/buildings | LOW | Visual glitch | Add near-clip plane adjustment; collision raycast for free camera |
| UI overlapping 3D view at different resolutions | MEDIUM | Usability issue | Use Canvas Scaler with "Scale With Screen Size"; anchor panels to edges |
| Pause via Time.timeScale breaks UI animations | LOW | Frozen UI elements | Use unscaledDeltaTime for all UI animations/transitions |

## Notes
- All UI uses Unity's built-in UGUI (com.unity.ugui 2.0.0) — already in the project manifest
- Input System package (1.19.0) is already installed — use new Input System for keyboard detection
- Role switching (Professor/Student) is a local toggle for now; Phase 5 (networking) will determine role from connection type
- The `RaceManager.Update()` keyboard shortcuts remain functional alongside UI buttons (both paths coexist)
- SetupScreen's file picker is simplified for Editor use; full WebGL JS interop file picker deferred to Phase 6
- Camera fixed points need to be placed manually in the scene (track-specific); provide a helper script or document positions
