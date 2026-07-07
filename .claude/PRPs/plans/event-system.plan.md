# Plan: Event System

## Summary
Implement the professor event system: 7 pre-configurable event types (v1 parity) that the professor sets up before the race and triggers during the race via keyboard shortcuts. Events modify car speeds based on attributes (team name length, color, functions) or apply global weather effects (snow, night). This is Phase 2 of the EDI Racing Game v2, building on the Phase 1 core racing loop.

## User Story
As a professor, I want to pre-configure events before the race and trigger them during the race, so that I can demonstrate how different factors (name length, color, functions, weather) create unequal outcomes for different groups.

## Problem → Solution
Cars race with uniform speed regardless of attributes → Events dynamically modify car speeds based on survey-derived attributes, making systemic inequality visible through gameplay.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 2 — Event System
- **Estimated Files**: 6 new C# scripts + updates to 2 existing files

---

## UX Design

### Before
Cars race uniformly. No way to influence the race based on car attributes. No demonstration of systemic inequality.

### After
```
Pre-Race:
1. Professor opens RaceConfig or EventSchedule asset in Inspector
2. Configures events: e.g. "Color Penalty: Red cars, -15 speed, 8s duration"
3. Assigns keyboard shortcuts (1-7) to each event

During Race:
1. Professor presses keyboard shortcut (e.g. "1" for name-length penalty)
2. Console/Debug log shows: "[Event] Name-Length Penalty triggered: 12 cars affected"
3. Affected cars visibly slow down for configured duration
4. Professor presses "2" for color boost — blue cars speed up
5. Class observes how different attributes create different outcomes
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Pre-race config | Only RaceConfig (speeds, laps) | RaceConfig + EventSchedule (7 event slots) | ScriptableObject in Inspector |
| During race | F1 = debug scoreboard only | 1-7 = trigger events, F1 = scoreboard | Keyboard shortcuts |
| Console output | Spawn + lap messages only | + Event trigger messages with affected count | Debug.Log |

---

## Mandatory Reading

| Priority | File | Why |
|---|---|---|
| P0 | `Assets/Scripts/Car/CarController.cs:219-229` | ApplySpeedModifier API — the integration hook |
| P0 | `Assets/Scripts/Car/CarIdentity.cs` | TeamName, ColorIndex, Functions — event matching data |
| P0 | `Assets/Scripts/Race/RaceManager.cs` | Orchestrator — events integrate here |
| P0 | `Assets/Scripts/Race/RaceConfig.cs` | ScriptableObject pattern to follow |
| P1 | `Assets/Scripts/Data/CarData.cs` | Immutable struct pattern |
| P1 | `Assets/Scripts/Race/CarSpawner.cs` | How spawned cars are referenced |
| P2 | `Assets/Data/vehicleGroupData.csv` | Real data to verify event matching logic |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| None needed | — | Feature uses established internal patterns only |

---

## Patterns to Mirror

### SCRIPTABLE_OBJECT_CONFIG
```csharp
// SOURCE: Assets/Scripts/Race/RaceConfig.cs:1-8
[CreateAssetMenu(fileName = "RaceConfig", menuName = "EDI Racing/Race Config")]
public class RaceConfig : ScriptableObject
{
    [Header("Car Settings")]
    public float DefaultSpeed = 40f;
    // ...
}
```

### SPEED_MODIFIER_API
```csharp
// SOURCE: Assets/Scripts/Car/CarController.cs:219-229
public void ApplySpeedModifier(float delta, float duration)
{
    StartCoroutine(SpeedModifierCoroutine(delta, duration));
}

private IEnumerator SpeedModifierCoroutine(float delta, float duration)
{
    agent.speed += delta;
    yield return new WaitForSeconds(duration);
    agent.speed = baseSpeed;
}
```

### CAR_IDENTITY_DATA
```csharp
// SOURCE: Assets/Scripts/Car/CarIdentity.cs:9-12
[Header("Identity")]
public string TeamName;
public int ColorIndex;
public string[] Functions;
```

### DEBUG_LOGGING
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:33
Debug.Log($"[RaceManager] Parsed {carDataList.Count} cars from CSV");
// Pattern: [ComponentName] message with interpolated data
```

### COMPONENT_REFERENCES
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:8-14
[Header("References")]
public CarSpawner CarSpawner;
public LapTracker LapTracker;
public ScoreManager ScoreManager;
public RaceConfig Config;
```

### FIND_OBJECTS_PATTERN
```csharp
// SOURCE: Assets/Scripts/Race/LapTracker.cs:18
var checkpoints = FindObjectsByType<CheckpointTrigger>(FindObjectsSortMode.None);
```

---

## Files to Change

| File | Action | Description |
|---|---|---|
| `Assets/Scripts/Events/RaceEventType.cs` | CREATE | Enum for 7 event types |
| `Assets/Scripts/Events/RaceEventConfig.cs` | CREATE | Serializable struct for a single event configuration |
| `Assets/Scripts/Events/EventSchedule.cs` | CREATE | ScriptableObject holding pre-configured event list |
| `Assets/Scripts/Events/EventMatcher.cs` | CREATE | Static utility: given event type + car identity, returns whether car is affected |
| `Assets/Scripts/Events/EventManager.cs` | CREATE | MonoBehaviour: triggers events, applies speed modifiers to matched cars |
| `Assets/Scripts/Events/WeatherEffect.cs` | CREATE | Placeholder for weather visual state (Phase 7 fills in visuals) |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Add EventManager reference, wire up event system |
| `Assets/Scripts/Car/CarController.cs` | UPDATE | Fix speed modifier stacking (multiple events can overlap) |

## NOT Building
- Event configuration UI (Phase 4 — currently Inspector-only)
- Weather visual effects: snow particles, night skybox (Phase 7)
- Sound effects for events (Phase 7)
- Network sync of events (Phase 5)
- In-game event timeline visualization

---

## Step-by-Step Tasks

### Task 1: Project Structure
- **ACTION**: Create `Assets/Scripts/Events/` directory
- **VALIDATE**: Directory exists

### Task 2: RaceEventType Enum
- **ACTION**: Define enum for all 7 v1 event types
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Events/RaceEventType.cs

/// <summary>
/// The 7 event types matching v1 parity.
/// Each event modifies car speeds based on different criteria.
/// </summary>
public enum RaceEventType
{
    NameLengthPenalty,   // Cars with team name > threshold chars get penalized
    ColorBoost,          // Cars matching target colorIndex get speed boost
    ColorPenalty,        // Cars matching target colorIndex get speed penalty
    FunctionBoost,       // Cars with target function tag get speed boost
    FunctionPenalty,     // Cars with target function tag get speed penalty
    SnowWeather,         // All cars slow down (global weather)
    NightWeather         // All cars slow down (global weather, different magnitude)
}
```
- **MIRROR**: Enum pattern (simple, no attributes needed)
- **GOTCHA**: Keep names PascalCase per C# convention
- **VALIDATE**: Compiles

### Task 3: RaceEventConfig Serializable Struct
- **ACTION**: Define serializable configuration for a single event
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Events/RaceEventConfig.cs
using System;
using UnityEngine;

/// <summary>
/// Configuration for a single race event.
/// Stored in EventSchedule ScriptableObject.
/// </summary>
[Serializable]
public struct RaceEventConfig
{
    [Tooltip("Type of event to trigger")]
    public RaceEventType EventType;

    [Tooltip("Display name shown in logs and future UI")]
    public string DisplayName;

    [Header("Speed Modification")]
    [Tooltip("Speed change applied to affected cars (negative = penalty, positive = boost)")]
    public float SpeedDelta;

    [Tooltip("Duration in seconds the speed change lasts")]
    public float Duration;

    [Header("Targeting (type-specific)")]
    [Tooltip("For ColorBoost/ColorPenalty: target color index (0=green,1=black,2=red,3=blue,4=white)")]
    public int TargetColorIndex;

    [Tooltip("For FunctionBoost/FunctionPenalty: target function name (e.g. 'facerecog')")]
    public string TargetFunction;

    [Tooltip("For NameLengthPenalty: team names longer than this get penalized")]
    public int NameLengthThreshold;

    [Header("Input")]
    [Tooltip("Keyboard shortcut to trigger this event (Alpha1-Alpha7)")]
    public KeyCode TriggerKey;

    [Tooltip("Can this event be triggered multiple times?")]
    public bool AllowRepeat;

    [HideInInspector]
    public bool HasBeenTriggered;
}
```
- **MIRROR**: CarData struct pattern (Serializable, public fields for Inspector)
- **GOTCHA**: `HasBeenTriggered` is runtime state — `HideInInspector` prevents save confusion. AllowRepeat defaults to false to prevent accidental double-triggers.
- **VALIDATE**: Appears correctly in Inspector when part of a list

### Task 4: EventSchedule ScriptableObject
- **ACTION**: Create ScriptableObject that holds the pre-configured event list
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Events/EventSchedule.cs
using UnityEngine;

/// <summary>
/// Pre-configured list of events for a race session.
/// Create via Assets > Create > EDI Racing > Event Schedule.
/// Professor sets up events here before starting the race.
/// </summary>
[CreateAssetMenu(fileName = "EventSchedule", menuName = "EDI Racing/Event Schedule")]
public class EventSchedule : ScriptableObject
{
    [Tooltip("List of events configured for this race session")]
    public RaceEventConfig[] Events = new RaceEventConfig[]
    {
        new RaceEventConfig
        {
            EventType = RaceEventType.NameLengthPenalty,
            DisplayName = "Name Length Penalty",
            SpeedDelta = -10f,
            Duration = 8f,
            NameLengthThreshold = 10,
            TriggerKey = KeyCode.Alpha1,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.ColorBoost,
            DisplayName = "Color Boost (Blue)",
            SpeedDelta = 15f,
            Duration = 6f,
            TargetColorIndex = 3,
            TriggerKey = KeyCode.Alpha2,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.ColorPenalty,
            DisplayName = "Color Penalty (Red)",
            SpeedDelta = -12f,
            Duration = 8f,
            TargetColorIndex = 2,
            TriggerKey = KeyCode.Alpha3,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.FunctionBoost,
            DisplayName = "Function Boost (Password)",
            SpeedDelta = 10f,
            Duration = 6f,
            TargetFunction = "password",
            TriggerKey = KeyCode.Alpha4,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.FunctionPenalty,
            DisplayName = "Function Penalty (Face Recog)",
            SpeedDelta = -10f,
            Duration = 8f,
            TargetFunction = "facerecog",
            TriggerKey = KeyCode.Alpha5,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.SnowWeather,
            DisplayName = "Snow Weather",
            SpeedDelta = -8f,
            Duration = 12f,
            TriggerKey = KeyCode.Alpha6,
            AllowRepeat = true
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.NightWeather,
            DisplayName = "Night Weather",
            SpeedDelta = -5f,
            Duration = 15f,
            TriggerKey = KeyCode.Alpha7,
            AllowRepeat = true
        }
    };

    /// <summary>
    /// Reset all runtime state (HasBeenTriggered flags).
    /// Call at race start.
    /// </summary>
    public void ResetRuntimeState()
    {
        for (int i = 0; i < Events.Length; i++)
        {
            Events[i].HasBeenTriggered = false;
        }
    }
}
```
- **MIRROR**: SCRIPTABLE_OBJECT_CONFIG (RaceConfig pattern)
- **GOTCHA**: Default array provides v1 parity out-of-the-box. ResetRuntimeState needed because ScriptableObject persists between plays in editor.
- **VALIDATE**: Create asset via Assets > Create > EDI Racing > Event Schedule; 7 default events visible

### Task 5: EventMatcher (Static Utility)
- **ACTION**: Create pure logic for matching events to cars
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Events/EventMatcher.cs
using System;
using System.Linq;

/// <summary>
/// Determines whether a car is affected by a given event.
/// Pure static utility — no MonoBehaviour, no state.
/// </summary>
public static class EventMatcher
{
    public static bool IsAffected(RaceEventConfig config, CarIdentity car)
    {
        switch (config.EventType)
        {
            case RaceEventType.NameLengthPenalty:
                return car.TeamName.Length > config.NameLengthThreshold;

            case RaceEventType.ColorBoost:
            case RaceEventType.ColorPenalty:
                return car.ColorIndex == config.TargetColorIndex;

            case RaceEventType.FunctionBoost:
            case RaceEventType.FunctionPenalty:
                if (string.IsNullOrEmpty(config.TargetFunction)) return false;
                string target = config.TargetFunction.Trim().ToLower();
                return car.Functions != null
                    && car.Functions.Any(f => f.Equals(target, StringComparison.OrdinalIgnoreCase));

            case RaceEventType.SnowWeather:
            case RaceEventType.NightWeather:
                return true; // Weather affects all cars

            default:
                return false;
        }
    }
}
```
- **MIRROR**: CsvParser static utility pattern (static class, no state)
- **GOTCHA**: Case-insensitive function matching since v1 CSV has mixed case. Null check on Functions array (envEmachine has empty functions).
- **VALIDATE**: NameLengthPenalty with threshold=10 matches "JesusTakeThisWheel" (19 chars) but not "Zoom" (4 chars)

### Task 6: Update CarController — Fix Speed Modifier Stacking
- **ACTION**: Update ApplySpeedModifier to handle overlapping events correctly
- **IMPLEMENT**: Replace the current coroutine approach with a tracked modifier system
```csharp
// In CarController.cs — replace ApplySpeedModifier and SpeedModifierCoroutine:

// New field:
private int activeModifierCount;

/// <summary>
/// Temporarily modify speed. Supports stacking: multiple concurrent modifiers
/// each apply their delta independently. Speed restores when each expires.
/// </summary>
public void ApplySpeedModifier(float delta, float duration)
{
    StartCoroutine(SpeedModifierCoroutine(delta, duration));
}

private IEnumerator SpeedModifierCoroutine(float delta, float duration)
{
    activeModifierCount++;
    agent.speed += delta;
    yield return new WaitForSeconds(duration);
    agent.speed -= delta;
    activeModifierCount--;

    // Safety: if all modifiers expired, snap back to base
    if (activeModifierCount <= 0)
    {
        activeModifierCount = 0;
        agent.speed = baseSpeed * speedMultiplier;
    }
}
```
- **MIRROR**: Existing SpeedModifierCoroutine pattern
- **GOTCHA**: Original code reset to `baseSpeed` after ANY modifier expired, which breaks when multiple events overlap. New approach subtracts the specific delta instead. Safety reset on zero modifiers handles float drift. Must account for collision `speedMultiplier` in the safety reset.
- **VALIDATE**: Two overlapping modifiers (+10 and -5) resolve correctly

### Task 7: EventManager MonoBehaviour
- **ACTION**: Create the main event manager that handles triggering and applying events
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Events/EventManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages race events: listens for keyboard triggers,
/// matches affected cars, applies speed modifiers.
/// </summary>
public class EventManager : MonoBehaviour
{
    [Header("Configuration")]
    public EventSchedule Schedule;

    private List<CarIdentity> registeredCars = new List<CarIdentity>();
    private bool isActive;

    public event Action<RaceEventConfig, int> OnEventTriggered; // config, affectedCount

    public void RegisterCar(CarIdentity car)
    {
        registeredCars.Add(car);
    }

    public void RegisterCars(List<GameObject> cars)
    {
        foreach (var car in cars)
        {
            var identity = car.GetComponent<CarIdentity>();
            if (identity != null)
                registeredCars.Add(identity);
        }
    }

    public void Activate()
    {
        isActive = true;
        Schedule.ResetRuntimeState();
        Debug.Log($"[EventManager] Activated with {Schedule.Events.Length} events configured");
    }

    public void Deactivate()
    {
        isActive = false;
    }

    private void Update()
    {
        if (!isActive || Schedule == null) return;

        for (int i = 0; i < Schedule.Events.Length; i++)
        {
            if (Input.GetKeyDown(Schedule.Events[i].TriggerKey))
            {
                TriggerEvent(i);
            }
        }
    }

    public void TriggerEvent(int eventIndex)
    {
        if (eventIndex < 0 || eventIndex >= Schedule.Events.Length) return;

        var config = Schedule.Events[eventIndex];

        if (config.HasBeenTriggered && !config.AllowRepeat)
        {
            Debug.Log($"[EventManager] '{config.DisplayName}' already triggered (repeat disabled)");
            return;
        }

        int affectedCount = 0;
        foreach (var car in registeredCars)
        {
            if (EventMatcher.IsAffected(config, car))
            {
                var controller = car.GetComponent<CarController>();
                if (controller != null)
                {
                    controller.ApplySpeedModifier(config.SpeedDelta, config.Duration);
                    affectedCount++;
                }
            }
        }

        Schedule.Events[eventIndex].HasBeenTriggered = true;

        Debug.Log($"[EventManager] '{config.DisplayName}' triggered: {affectedCount}/{registeredCars.Count} cars affected (speed {config.SpeedDelta:+#;-#;0} for {config.Duration}s)");

        OnEventTriggered?.Invoke(config, affectedCount);
    }

    /// <summary>
    /// Trigger event by type (for programmatic access / future UI / network sync).
    /// </summary>
    public void TriggerEventByType(RaceEventType type)
    {
        for (int i = 0; i < Schedule.Events.Length; i++)
        {
            if (Schedule.Events[i].EventType == type)
            {
                TriggerEvent(i);
                return;
            }
        }
        Debug.LogWarning($"[EventManager] No event of type '{type}' found in schedule");
    }

    public int RegisteredCarCount => registeredCars.Count;
    public bool IsActive => isActive;
}
```
- **MIRROR**: COMPONENT_REFERENCES, DEBUG_LOGGING, event Action pattern from LapTracker
- **GOTCHA**: `Schedule.Events` is a struct array — modifying `HasBeenTriggered` requires writing back to the array index. `OnEventTriggered` event provides hook for Phase 4 UI.
- **VALIDATE**: Press 1-7 during race -> correct events fire with correct car counts

### Task 8: WeatherEffect Placeholder
- **ACTION**: Create marker component for weather state tracking (visuals added in Phase 7)
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Events/WeatherEffect.cs
using UnityEngine;
using System.Collections;

/// <summary>
/// Tracks active weather state. Phase 7 adds visual effects
/// (snow particles, night skybox) based on this state.
/// </summary>
public class WeatherEffect : MonoBehaviour
{
    public bool IsSnowActive { get; private set; }
    public bool IsNightActive { get; private set; }

    public void ActivateSnow(float duration)
    {
        IsSnowActive = true;
        Debug.Log("[Weather] Snow started");
        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsSnowActive = false;
            Debug.Log("[Weather] Snow ended");
        }));
    }

    public void ActivateNight(float duration)
    {
        IsNightActive = true;
        Debug.Log("[Weather] Night started");
        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsNightActive = false;
            Debug.Log("[Weather] Night ended");
        }));
    }

    private IEnumerator DeactivateAfter(float duration, System.Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        onComplete?.Invoke();
    }
}
```
- **MIRROR**: Coroutine pattern from CarController
- **GOTCHA**: Public bool properties allow Phase 7 visual systems to query weather state without coupling.
- **VALIDATE**: State toggles on/off correctly after duration

### Task 9: Update RaceManager — Integrate EventManager
- **ACTION**: Wire EventManager into the race lifecycle
- **IMPLEMENT**: Add EventManager and WeatherEffect references and integration
```csharp
// In RaceManager.cs — add to [Header("References")]:
public EventManager EventManager;
public WeatherEffect WeatherEffect;

// In LoadAndStartRace(), after registering cars with ScoreManager:
if (EventManager != null)
{
    EventManager.RegisterCars(spawnedCars);
    EventManager.Activate();
    EventManager.OnEventTriggered += OnEventTriggered;
}

// New method:
private void OnEventTriggered(RaceEventConfig config, int affectedCount)
{
    if (WeatherEffect == null) return;
    if (config.EventType == RaceEventType.SnowWeather)
        WeatherEffect.ActivateSnow(config.Duration);
    else if (config.EventType == RaceEventType.NightWeather)
        WeatherEffect.ActivateNight(config.Duration);
}
```
- **MIRROR**: COMPONENT_REFERENCES, event subscription pattern from LapTracker.OnLapCompleted
- **GOTCHA**: Null check on EventManager allows Phase 1 scenes to continue working without events
- **VALIDATE**: Race starts -> EventManager activates -> keyboard triggers work

### Task 10: Create Default EventSchedule Asset
- **ACTION**: Create `Assets/Settings/EventSchedule.asset` via Unity, with the 7 default v1-parity events
- **VALIDATE**: Asset appears in Project window with 7 configured events

### Task 11: Scene Setup
- **ACTION**: Add EventManager and WeatherEffect to the race scene
- **IMPLEMENT**:
  1. Add EventManager component to the RaceManager GameObject
  2. Add WeatherEffect component to the RaceManager GameObject
  3. Assign EventSchedule asset to EventManager.Schedule
  4. Wire EventManager and WeatherEffect references in RaceManager
- **VALIDATE**: Play mode -> press 1-7 -> events trigger with console output

---

## Testing Strategy

### Manual Tests (Unity Play Mode)

| Test | Input | Expected | Edge Case? |
|---|---|---|---|
| NameLength threshold=10 | v1 CSV (37 cars) | ~8 cars with names > 10 chars affected | No |
| ColorBoost blue (idx 3) | v1 CSV | 11 cars (blue) sped up | No |
| ColorPenalty red (idx 2) | v1 CSV | 8 cars (red) slowed | No |
| FunctionBoost "password" | v1 CSV | 27 cars with password boosted | No |
| FunctionPenalty "facerecog" | v1 CSV | 19 cars with facerecog penalized | No |
| Snow weather | Any | All 37 cars slowed | No |
| Night weather | Any | All 37 cars slowed | No |
| Empty function list | "envEmachine" car | Not affected by function events | Yes |
| Duplicate team names | "Apollo5" appears twice | Both independently affected | Yes |
| No-repeat event | Trigger same event twice | Second trigger shows "already triggered" | Yes |
| Repeatable event | Trigger snow twice | Both triggers apply (stacking) | Yes |
| Multiple overlapping events | Trigger 2 events < 1s apart | Both modifiers apply, both revert correctly | Yes |
| No EventManager | Remove EventManager ref | Race works normally, no errors | Yes |
| No schedule assigned | EventManager.Schedule = null | No errors, no events | Yes |

### Edge Cases Checklist
- [ ] Empty schedule (zero events)
- [ ] Event with zero duration
- [ ] Event with zero speed delta
- [ ] Function match case sensitivity ("FaceRecog" vs "facerecog")
- [ ] Car with null/empty Functions array
- [ ] Event triggered before race starts
- [ ] Multiple weather events overlapping
- [ ] All 7 events triggered in rapid succession
- [ ] ColorIndex out of range in event config (e.g. 99)

---

## Validation Commands

### Compile Check
```bash
curl -s http://localhost:8090/skills/debug/compile-check | jq .
```
EXPECT: Zero compile errors

### Play Mode Test
```
1. Open Assets/Scenes/complete_track_demo.unity
2. Ensure RaceManager has EventManager and WeatherEffect wired
3. Press Play
4. Cars spawn and start racing (Phase 1 unchanged)
5. Press 1 -> Console: "[EventManager] 'Name Length Penalty' triggered: N/37 cars affected"
6. Affected cars visibly slow down
7. Press 2 -> Console: "[EventManager] 'Color Boost (Blue)' triggered: 11/37 cars affected"
8. Blue cars speed up
9. Press 6 -> Console: "[EventManager] 'Snow Weather' triggered: 37/37 cars affected"
10. All cars slow down
11. Wait for duration -> speeds restore
12. Press 1 again -> Console: "already triggered (repeat disabled)"
13. Press 6 again -> Snow applies again (repeatable)
```
EXPECT: All events fire correctly, speeds modify and restore

### Backward Compatibility
```
1. Remove EventManager from RaceManager references (set to None)
2. Press Play
3. Race runs normally without errors
```
EXPECT: Phase 1 functionality unbroken

---

## Acceptance Criteria
- [ ] 7 event types implemented matching v1 parity
- [ ] Events pre-configured via EventSchedule ScriptableObject
- [ ] Keyboard shortcuts 1-7 trigger events during race
- [ ] Speed modifiers apply to correct cars based on matching logic
- [ ] Name-length matching: team name > threshold
- [ ] Color matching: exact colorIndex match
- [ ] Function matching: case-insensitive, handles empty arrays
- [ ] Weather events affect all cars
- [ ] Speed modifiers stack correctly (multiple overlapping events)
- [ ] Speed restores after duration expires
- [ ] Non-repeatable events prevent double-triggering
- [ ] Weather state tracked (IsSnowActive, IsNightActive)
- [ ] Console logs show event name, affected count, speed delta
- [ ] Backward compatible: race works without EventManager
- [ ] No compile errors
- [ ] WebGL-compatible

## Completion Checklist
- [ ] Code follows discovered patterns (ScriptableObject, static utility, component references)
- [ ] Error handling matches codebase style (null checks, Debug.LogWarning)
- [ ] Logging follows pattern: `[ComponentName] message`
- [ ] No hardcoded values (all in EventSchedule/RaceEventConfig)
- [ ] No string-based FindObjectByName lookups
- [ ] Components decoupled (events/delegates for communication)
- [ ] Immutable where possible (EventMatcher is stateless)
- [ ] Self-contained — no questions needed

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Speed modifier float drift | LOW | MEDIUM | Safety reset when activeModifierCount reaches 0 |
| Struct mutation in array | MEDIUM | LOW | Document that Events[i].HasBeenTriggered must be written back by index |
| Keyboard conflict with Unity | LOW | LOW | Alpha1-7 don't conflict with common editor shortcuts in play mode |
| Event during pause (if Phase 4 adds pause) | LOW | LOW | EventManager.isActive check; Deactivate() on pause |
| ScriptableObject runtime mutation persists in editor | MEDIUM | MEDIUM | ResetRuntimeState() called at race start; HideInInspector on HasBeenTriggered |

## Notes
- v1 had 7 event types: this plan reproduces all 7
- Event types verified against v1 CSV data:
  - Name-length threshold=10 affects ~8 of 37 cars
  - ColorIndex 3 (blue) appears 11 times
  - ColorIndex 2 (red) appears 8 times
  - "password" function appears in 27 of 37 cars
  - "facerecog" function appears in 19 of 37 cars
- OnEventTriggered Action provides hook for Phase 4 UI (event log panel, visual feedback)
- WeatherEffect provides hook for Phase 7 visuals (snow particles, skybox changes)
- TriggerEventByType() provides hook for Phase 5 network sync (relay event triggers from server)
- Phase 4 will replace keyboard shortcuts with UI buttons while keeping shortcuts as secondary input
