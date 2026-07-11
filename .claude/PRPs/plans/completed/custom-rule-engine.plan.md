# Plan: Custom Rule Engine

## Summary
Replace the hardcoded `EventMatcher` switch statement and `RaceEventType` enum with a configurable rule engine. Professors will be able to define rules like "if attribute 'language' contains 'english', apply +10 speed for 6s" using any dynamic attribute from Phase 1. All 7 v1 event types are reproduced as default rules in the new system.

## User Story
As a professor teaching any EDI-related course,
I want to define custom event rules that match on any car attribute,
So that I can create race events tailored to my course's EDI themes without modifying code.

## Problem → Solution
EventMatcher uses a switch statement over 7 hardcoded RaceEventType enum values with type-specific targeting fields → RuleEngine evaluates configurable EventRule structs using generic comparison operators against any attribute on CarIdentity.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md`
- **PRD Phase**: Phase 2 — Custom Rule Engine
- **Estimated Files**: 4 created, 6 modified, 3 deleted

---

## UX Design

Internal change — no user-facing UX transformation.

The professor's experience during the race is identical: keyboard shortcuts 1-7 trigger events, event panel buttons work the same, weather VFX activate as before. The difference is invisible to the user — the matching logic is now configurable rather than hardcoded. Phase 4 (Professor Builder UI) will expose this configurability.

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Event triggering | Keyboard 1-7 or panel buttons | Identical | Same input, same output |
| Event matching | Hardcoded per-type logic | Rule engine evaluation | Invisible to user |
| Weather VFX | Triggered by RaceEventType enum check | Triggered by WeatherType field on EventRule | Same visual result |
| Session save/load | SavedEventConfig with EventType enum | SavedEventRule with operator/attribute | Breaking change for saved sessions |
| Default events | 7 v1 events | Same 7 events as default EventRules | Identical race behavior |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Events/EventMatcher.cs` | all (36 lines) | Being replaced — understand all matching logic |
| P0 | `Assets/Scripts/Events/RaceEventConfig.cs` | all (45 lines) | Being replaced — understand all fields |
| P0 | `Assets/Scripts/Events/RaceEventType.cs` | all (14 lines) | Being removed — 7 enum values |
| P0 | `Assets/Scripts/Events/EventManager.cs` | all (117 lines) | Primary consumer — Schedule, TriggerEvent, TriggerEventByType |
| P0 | `Assets/Scripts/Events/EventSchedule.cs` | all (96 lines) | Holds event array + defaults |
| P1 | `Assets/Scripts/Car/CarIdentity.cs` | 35-58 | Attribute accessors used by RuleEngine |
| P1 | `Assets/Scripts/Race/RaceManager.cs` | 172-187, 205-239 | OnEventTriggered (weather dispatch), BuildSessionData (event serialization) |
| P1 | `Assets/Scripts/Data/SessionData.cs` | 60-107 | SavedEventConfig serialization |
| P1 | `Assets/Scripts/UI/EventPanel.cs` | all (95 lines) | UI consumer of event config |
| P2 | `Assets/Scripts/Network/NetworkSync.cs` | 160-171 | OnEventTriggered broadcasts event name |
| P2 | `Assets/Scripts/Data/CarData.cs` | 43-73 | GetAttribute accessors for rule evaluation |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| No external research needed | N/A | Feature uses established internal patterns — CarIdentity attribute accessors, event dispatch via C# Actions, ScriptableObject for configuration |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Data/CarData.cs:10-14
// PascalCase for public fields and types, no namespaces
[Serializable]
public struct AttributeEntry
{
    public string Key;
    public string Value;
}
```

### ERROR_HANDLING
```csharp
// SOURCE: Assets/Scripts/Events/EventMatcher.cs:23-26
// Null checks, safe defaults, no exceptions thrown
if (string.IsNullOrEmpty(config.TargetFunction)) return false;
string target = config.TargetFunction.Trim().ToLower();
return car.Functions != null
    && car.Functions.Any(f => f.Equals(target, StringComparison.OrdinalIgnoreCase));
```

### LOGGING_PATTERN
```csharp
// SOURCE: Assets/Scripts/Events/EventManager.cs:39,88
Debug.Log($"[EventManager] Activated with {Schedule.Events.Length} events configured");
Debug.Log($"[EventManager] '{config.DisplayName}' triggered: {affectedCount}/{registeredCars.Count} cars affected (speed {config.SpeedDelta:+#;-#;0} for {config.Duration}s)");
```

### SERIALIZATION_PATTERN
```csharp
// SOURCE: Assets/Scripts/Data/SessionData.cs:76-89
// Static factory + conversion methods, plain [Serializable] structs
[Serializable]
public struct SavedEventConfig
{
    public int EventType;
    public string DisplayName;
    public static SavedEventConfig FromConfig(RaceEventConfig config) { ... }
    public RaceEventConfig ToConfig(Key triggerKey) { ... }
}
```

### SCRIPTABLEOBJECT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Events/EventSchedule.cs:9-11
// [CreateAssetMenu] under "EDI Racing" menu, default values in field initializer
[CreateAssetMenu(fileName = "EventSchedule", menuName = "EDI Racing/Event Schedule")]
public class EventSchedule : ScriptableObject
{
    public RaceEventConfig[] Events = new RaceEventConfig[] { /* defaults */ };
}
```

### EVENT_DISPATCH_PATTERN
```csharp
// SOURCE: Assets/Scripts/Events/EventManager.cs:18,90
// C# Action<T> delegates, not UnityEvents
public event Action<RaceEventConfig, int> OnEventTriggered;
OnEventTriggered?.Invoke(config, affectedCount);
```

### ATTRIBUTE_ACCESS_PATTERN
```csharp
// SOURCE: Assets/Scripts/Car/CarIdentity.cs:35-58
// Case-insensitive key lookup with default values
public string GetAttribute(string key, string defaultValue = "")
{
    if (Attributes == null) return defaultValue;
    for (int i = 0; i < Attributes.Length; i++)
        if (string.Equals(Attributes[i].Key, key, StringComparison.OrdinalIgnoreCase))
            return Attributes[i].Value;
    return defaultValue;
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Events/ComparisonOperator.cs` | CREATE | New enum defining all comparison operators |
| `Assets/Scripts/Events/WeatherType.cs` | CREATE | New enum for weather VFX hooks (None, Snow, Night) |
| `Assets/Scripts/Events/EventRule.cs` | CREATE | New configurable event rule struct replacing RaceEventConfig |
| `Assets/Scripts/Events/RuleEngine.cs` | CREATE | New static evaluator replacing EventMatcher |
| `Assets/Scripts/Events/EventMatcher.cs` | DELETE | Replaced by RuleEngine |
| `Assets/Scripts/Events/RaceEventConfig.cs` | DELETE | Replaced by EventRule |
| `Assets/Scripts/Events/RaceEventType.cs` | DELETE | Replaced by ComparisonOperator + WeatherType |
| `Assets/Scripts/Events/EventSchedule.cs` | UPDATE | Change array type from RaceEventConfig[] to EventRule[] |
| `Assets/Scripts/Events/EventManager.cs` | UPDATE | Use EventRule + RuleEngine instead of RaceEventConfig + EventMatcher |
| `Assets/Scripts/Data/SessionData.cs` | UPDATE | Replace SavedEventConfig with SavedEventRule |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Update OnEventTriggered signature and weather dispatch |
| `Assets/Scripts/UI/EventPanel.cs` | UPDATE | Use EventRule instead of RaceEventConfig |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Update OnEventTriggered signature |
| `Assets/Scripts/RuntimeSetup.cs` | UPDATE | Update OnEventTriggered handler signature (line 328) |

## NOT Building

- Professor-facing rule builder UI (Phase 4)
- JSON config file import/export for rules (Phase 3)
- Survey questions or student survey system (Phases 3, 5)
- Compound rule conditions (AND/OR) — Phase 2 is single-condition rules only
- New weather effect types beyond Snow/Night
- Any new UI panels or screens

---

## Step-by-Step Tasks

### Task 1: Create ComparisonOperator enum
- **ACTION**: Create new file `Assets/Scripts/Events/ComparisonOperator.cs`
- **IMPLEMENT**:
```csharp
/// <summary>
/// Comparison operators for the configurable rule engine.
/// Used in EventRule to match car attributes against values.
/// </summary>
public enum ComparisonOperator
{
    Equals,             // String or numeric equality (case-insensitive)
    NotEquals,          // Inverse of Equals
    Contains,           // Attribute value contains the comparison string (for slash-separated lists like functions)
    NotContains,        // Inverse of Contains
    GreaterThan,        // Numeric comparison: attribute value > compare value
    LessThan,           // Numeric comparison: attribute value < compare value
    LengthGreaterThan,  // String length comparison: attribute.Length > compare value (for team name length)
    LengthLessThan,     // String length comparison: attribute.Length < compare value
    All                 // Matches all cars regardless of attributes (for global/weather events)
}
```
- **MIRROR**: NAMING_CONVENTION (PascalCase enum values, XML summary)
- **IMPORTS**: None
- **GOTCHA**: No `using` statements needed — this is a plain enum in the global namespace
- **VALIDATE**: File compiles without errors

### Task 2: Create WeatherType enum
- **ACTION**: Create new file `Assets/Scripts/Events/WeatherType.cs`
- **IMPLEMENT**:
```csharp
/// <summary>
/// Optional weather VFX associated with an event rule.
/// When set to Snow or Night, the WeatherEffect component is activated.
/// </summary>
public enum WeatherType
{
    None,   // No weather VFX
    Snow,   // Activates snow particle system
    Night   // Activates night lighting transition
}
```
- **MIRROR**: NAMING_CONVENTION
- **IMPORTS**: None
- **GOTCHA**: Must remain serializable by JsonUtility (enums serialize as int by default — this is fine)
- **VALIDATE**: File compiles without errors

### Task 3: Create EventRule struct
- **ACTION**: Create new file `Assets/Scripts/Events/EventRule.cs` replacing `RaceEventConfig.cs`
- **IMPLEMENT**:
```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Configuration for a single event rule.
/// Replaces the hardcoded RaceEventConfig with a generic attribute-based matching system.
/// Stored in EventSchedule ScriptableObject.
/// </summary>
[Serializable]
public struct EventRule
{
    [Tooltip("Display name shown in event panel and logs")]
    public string DisplayName;

    [Header("Rule Condition")]
    [Tooltip("Attribute name to check on the car (e.g., 'colorIndex', 'functions', 'teamName', or any custom attribute)")]
    public string AttributeName;

    [Tooltip("How to compare the attribute value")]
    public ComparisonOperator Operator;

    [Tooltip("Value to compare against (interpretation depends on operator)")]
    public string CompareValue;

    [Header("Effect")]
    [Tooltip("Speed change applied to matched cars (negative = penalty, positive = boost)")]
    public float SpeedDelta;

    [Tooltip("Duration in seconds the speed change lasts")]
    public float Duration;

    [Header("Weather (Optional)")]
    [Tooltip("Weather VFX to activate when this rule triggers (None for no VFX)")]
    public WeatherType Weather;

    [Header("Input")]
    [Tooltip("Keyboard shortcut to trigger this event (Alpha1-Alpha9)")]
    public Key TriggerKey;

    [Tooltip("Can this event be triggered multiple times?")]
    public bool AllowRepeat;

    [HideInInspector]
    public bool HasBeenTriggered;
}
```
- **MIRROR**: NAMING_CONVENTION (PascalCase fields), SERIALIZATION_PATTERN (struct with [Serializable])
- **IMPORTS**: `System`, `UnityEngine`, `UnityEngine.InputSystem`
- **GOTCHA**: `AttributeName` is ignored when `Operator == All` (matches all cars). `CompareValue` is a string — numeric comparisons parse it at evaluation time. `HasBeenTriggered` is runtime state, not serialized to JSON (marked `[HideInInspector]`).
- **VALIDATE**: File compiles; struct is serializable and appears in Unity Inspector when used in ScriptableObject

### Task 4: Create RuleEngine static class
- **ACTION**: Create new file `Assets/Scripts/Events/RuleEngine.cs` replacing `EventMatcher.cs`
- **IMPLEMENT**:
```csharp
using System;
using System.Linq;

/// <summary>
/// Evaluates EventRule conditions against car attributes.
/// Pure static utility — no MonoBehaviour, no state.
/// Replaces the hardcoded EventMatcher switch statement.
/// </summary>
public static class RuleEngine
{
    /// <summary>
    /// Returns true if the car matches the rule's condition.
    /// </summary>
    public static bool IsAffected(EventRule rule, CarIdentity car)
    {
        if (rule.Operator == ComparisonOperator.All)
            return true;

        string attributeValue = ResolveAttributeValue(rule.AttributeName, car);

        switch (rule.Operator)
        {
            case ComparisonOperator.Equals:
                return string.Equals(attributeValue, rule.CompareValue, StringComparison.OrdinalIgnoreCase);

            case ComparisonOperator.NotEquals:
                return !string.Equals(attributeValue, rule.CompareValue, StringComparison.OrdinalIgnoreCase);

            case ComparisonOperator.Contains:
                return ContainsValue(attributeValue, rule.CompareValue);

            case ComparisonOperator.NotContains:
                return !ContainsValue(attributeValue, rule.CompareValue);

            case ComparisonOperator.GreaterThan:
                return CompareNumeric(attributeValue, rule.CompareValue) > 0;

            case ComparisonOperator.LessThan:
                return CompareNumeric(attributeValue, rule.CompareValue) < 0;

            case ComparisonOperator.LengthGreaterThan:
                return CompareLengthNumeric(attributeValue, rule.CompareValue) > 0;

            case ComparisonOperator.LengthLessThan:
                return CompareLengthNumeric(attributeValue, rule.CompareValue) < 0;

            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves the attribute value from CarIdentity.
    /// Handles the special case of "teamName" which is a first-class field.
    /// </summary>
    private static string ResolveAttributeValue(string attributeName, CarIdentity car)
    {
        if (string.IsNullOrEmpty(attributeName))
            return "";

        // Special case: teamName is a first-class field, not in the attributes dictionary
        if (string.Equals(attributeName, "teamName", StringComparison.OrdinalIgnoreCase))
            return car.TeamName ?? "";

        return car.GetAttribute(attributeName, "");
    }

    /// <summary>
    /// Checks if the attribute value contains the target.
    /// Supports slash-separated lists (like functions: "facerecog/glasses/password").
    /// </summary>
    private static bool ContainsValue(string attributeValue, string target)
    {
        if (string.IsNullOrEmpty(attributeValue) || string.IsNullOrEmpty(target))
            return false;

        string trimmedTarget = target.Trim().ToLower();

        // Check slash-separated values (e.g., functions: "facerecog/glasses/password")
        if (attributeValue.Contains("/"))
        {
            return attributeValue.Split('/')
                .Select(v => v.Trim().ToLower())
                .Any(v => v.Equals(trimmedTarget, StringComparison.OrdinalIgnoreCase));
        }

        // Plain substring contains (case-insensitive)
        return attributeValue.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Compares two values as floats. Returns positive if attr > compare,
    /// negative if attr < compare, zero if equal or unparseable.
    /// </summary>
    private static int CompareNumeric(string attributeValue, string compareValue)
    {
        if (float.TryParse(attributeValue, out float attrNum) &&
            float.TryParse(compareValue, out float compNum))
        {
            return attrNum.CompareTo(compNum);
        }
        return 0; // unparseable values are treated as not matching
    }

    /// <summary>
    /// Compares the length of the attribute value against a numeric threshold.
    /// Returns positive if length > threshold, negative if length < threshold.
    /// </summary>
    private static int CompareLengthNumeric(string attributeValue, string compareValue)
    {
        int length = (attributeValue ?? "").Length;
        if (int.TryParse(compareValue, out int threshold))
            return length.CompareTo(threshold);
        return 0;
    }
}
```
- **MIRROR**: ERROR_HANDLING (null checks, safe defaults), ATTRIBUTE_ACCESS_PATTERN (case-insensitive)
- **IMPORTS**: `System`, `System.Linq`
- **GOTCHA**: `teamName` is resolved from `car.TeamName` directly since it's not in the attributes dictionary. `Contains` handles slash-separated values (like the `functions` attribute from v1) by splitting on `/` first. Numeric comparisons fail safely — unparseable values are treated as non-matching (returns 0 from CompareNumeric → neither GreaterThan nor LessThan is true). The `Operator == All` short-circuit avoids unnecessary attribute lookups for global events.
- **VALIDATE**: Verify each operator works correctly:
  - `Equals("colorIndex", "3")` matches car with colorIndex=3
  - `Contains("functions", "password")` matches car with functions containing "password"
  - `LengthGreaterThan("teamName", "10")` matches car with name length > 10
  - `All` matches all cars
  - `NotEquals`, `NotContains`, `GreaterThan`, `LessThan`, `LengthLessThan` work correctly

### Task 5: Update EventSchedule
- **ACTION**: Change `Events` array type from `RaceEventConfig[]` to `EventRule[]`. Update default values to match v1 parity.
- **IMPLEMENT**:
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pre-configured list of event rules for a race session.
/// Create via Assets > Create > EDI Racing > Event Schedule.
/// Professor sets up rules here before starting the race.
/// </summary>
[CreateAssetMenu(fileName = "EventSchedule", menuName = "EDI Racing/Event Schedule")]
public class EventSchedule : ScriptableObject
{
    [Tooltip("List of event rules configured for this race session")]
    public EventRule[] Events = new EventRule[]
    {
        new EventRule
        {
            DisplayName = "Name Length Penalty",
            AttributeName = "teamName",
            Operator = ComparisonOperator.LengthGreaterThan,
            CompareValue = "10",
            SpeedDelta = -10f,
            Duration = 8f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit1,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Color Boost (Blue)",
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = "3",
            SpeedDelta = 15f,
            Duration = 6f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit2,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Color Penalty (Red)",
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = "2",
            SpeedDelta = -12f,
            Duration = 8f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit3,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Function Boost (Password)",
            AttributeName = "functions",
            Operator = ComparisonOperator.Contains,
            CompareValue = "password",
            SpeedDelta = 10f,
            Duration = 6f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit4,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Function Penalty (Face Recog)",
            AttributeName = "functions",
            Operator = ComparisonOperator.Contains,
            CompareValue = "facerecog",
            SpeedDelta = -10f,
            Duration = 8f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit5,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Snow Weather",
            AttributeName = "",
            Operator = ComparisonOperator.All,
            CompareValue = "",
            SpeedDelta = -8f,
            Duration = 12f,
            Weather = WeatherType.Snow,
            TriggerKey = Key.Digit6,
            AllowRepeat = true
        },
        new EventRule
        {
            DisplayName = "Night Weather",
            AttributeName = "",
            Operator = ComparisonOperator.All,
            CompareValue = "",
            SpeedDelta = -5f,
            Duration = 15f,
            Weather = WeatherType.Night,
            TriggerKey = Key.Digit7,
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
- **MIRROR**: SCRIPTABLEOBJECT_PATTERN
- **IMPORTS**: `UnityEngine`, `UnityEngine.InputSystem`
- **GOTCHA**: The ScriptableObject asset in the scene (`Assets/ScriptableObjects/EventSchedule.asset` or wherever it is) will lose its serialized data when the struct type changes. The default values in the field initializer will take effect, which is correct — they match v1 parity. If the asset was manually configured in the Inspector, it will need re-configuration or deletion and re-creation. Check if the asset exists and note this in validation.
- **VALIDATE**: Create or reassign the EventSchedule asset. Verify all 7 default rules appear in Inspector with correct values.

### Task 6: Update EventManager
- **ACTION**: Replace `RaceEventConfig` references with `EventRule`. Replace `EventMatcher.IsAffected` with `RuleEngine.IsAffected`. Remove `TriggerEventByType` (no longer applicable — events are identified by index or name, not by enum type).
- **IMPLEMENT**: Full replacement of EventManager.cs:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages race events: listens for keyboard triggers,
/// evaluates rules against car attributes, applies speed modifiers.
/// </summary>
public class EventManager : MonoBehaviour
{
    [Header("Configuration")]
    public EventSchedule Schedule;

    private readonly List<CarIdentity> registeredCars = new List<CarIdentity>();
    private bool isActive;

    public event Action<EventRule, int> OnEventTriggered;

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
        Debug.Log($"[EventManager] Activated with {Schedule.Events.Length} event rules configured");
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
            if (Keyboard.current != null && Keyboard.current[Schedule.Events[i].TriggerKey].wasPressedThisFrame)
            {
                TriggerEvent(i);
            }
        }
    }

    public void TriggerEvent(int eventIndex)
    {
        if (eventIndex < 0 || eventIndex >= Schedule.Events.Length) return;

        var rule = Schedule.Events[eventIndex];

        if (rule.HasBeenTriggered && !rule.AllowRepeat)
        {
            Debug.Log($"[EventManager] '{rule.DisplayName}' already triggered (repeat disabled)");
            return;
        }

        int affectedCount = 0;
        foreach (var car in registeredCars)
        {
            if (RuleEngine.IsAffected(rule, car))
            {
                var controller = car.GetComponent<CarController>();
                if (controller != null)
                {
                    controller.ApplySpeedModifier(rule.SpeedDelta, rule.Duration);
                    affectedCount++;
                }
            }
        }

        Schedule.Events[eventIndex].HasBeenTriggered = true;

        Debug.Log($"[EventManager] '{rule.DisplayName}' triggered: {affectedCount}/{registeredCars.Count} cars affected (speed {rule.SpeedDelta:+#;-#;0} for {rule.Duration}s)");

        OnEventTriggered?.Invoke(rule, affectedCount);
    }

    /// <summary>
    /// Trigger event by display name (for programmatic access / future UI / network sync).
    /// </summary>
    public void TriggerEventByName(string displayName)
    {
        for (int i = 0; i < Schedule.Events.Length; i++)
        {
            if (string.Equals(Schedule.Events[i].DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
            {
                TriggerEvent(i);
                return;
            }
        }
        Debug.LogWarning($"[EventManager] No event named '{displayName}' found in schedule");
    }

    public void ClearRegisteredCars()
    {
        registeredCars.Clear();
        isActive = false;
    }

    public int RegisteredCarCount => registeredCars.Count;
    public bool IsActive => isActive;
}
```
- **MIRROR**: EVENT_DISPATCH_PATTERN, LOGGING_PATTERN
- **IMPORTS**: `System`, `System.Collections.Generic`, `UnityEngine`, `UnityEngine.InputSystem`
- **GOTCHA**: The `OnEventTriggered` event signature changes from `Action<RaceEventConfig, int>` to `Action<EventRule, int>`. All subscribers (RaceManager, EventPanel, NetworkSync) must update their handler signatures. `TriggerEventByType` is replaced by `TriggerEventByName` since events no longer have enum types — check if `TriggerEventByType` is called anywhere.
- **VALIDATE**: Enter Play Mode. Press 1-7. Verify same cars are affected as before with same speed deltas and durations.

### Task 7: Update RaceManager event handling
- **ACTION**: Update `OnEventTriggered` handler signature from `RaceEventConfig` to `EventRule`. Update weather dispatch from `config.EventType == RaceEventType.Snow/Night` to `rule.Weather == WeatherType.Snow/Night`. Update `BuildSessionData` to serialize `EventRule[]` as `SavedEventRule[]`.
- **IMPLEMENT**: Changes in RaceManager.cs:

  **Line 172-187** — Update `OnEventTriggered` handler:
```csharp
    private void OnEventTriggered(EventRule rule, int affectedCount)
    {
        eventLog.Add(new EventLogEntry
        {
            Timestamp = Time.time - raceStartTime,
            EventName = rule.DisplayName,
            AffectedCount = affectedCount,
            TotalCars = spawnedCars != null ? spawnedCars.Count : 0
        });

        if (WeatherEffect == null) return;
        if (rule.Weather == WeatherType.Snow)
            WeatherEffect.ActivateSnow(rule.Duration);
        else if (rule.Weather == WeatherType.Night)
            WeatherEffect.ActivateNight(rule.Duration);
    }
```

  **Lines 222-229** — Update `BuildSessionData` event serialization:
```csharp
        var events = Array.Empty<SavedEventRule>();
        if (EventManager != null && EventManager.Schedule != null)
        {
            events = new SavedEventRule[EventManager.Schedule.Events.Length];
            for (int i = 0; i < events.Length; i++)
                events[i] = SavedEventRule.FromRule(EventManager.Schedule.Events[i]);
        }
```

  **Lines 248-255** — Update `LoadSession` event restoration:
```csharp
        if (EventManager != null && EventManager.Schedule != null && session.Events.Length > 0)
        {
            int count = Mathf.Min(session.Events.Length, EventManager.Schedule.Events.Length);
            for (int i = 0; i < count; i++)
            {
                Key key = EventManager.Schedule.Events[i].TriggerKey;
                EventManager.Schedule.Events[i] = session.Events[i].ToRule(key);
            }
        }
```
- **MIRROR**: LOGGING_PATTERN, ERROR_HANDLING
- **IMPORTS**: No new imports needed
- **GOTCHA**: The `SessionData.Events` field type changes from `SavedEventConfig[]` to `SavedEventRule[]` — must update SessionData first (Task 8) or in parallel. The `LoadSession` method restores events by index, preserving the trigger key from the schedule — same pattern as before.
- **VALIDATE**: During a race: trigger snow (key 6) — verify snow VFX activates. Trigger night (key 7) — verify night VFX activates. Press P to save session — verify no errors. Press L to load — verify events restore correctly.

### Task 8: Update SessionData serialization
- **ACTION**: Replace `SavedEventConfig` with `SavedEventRule`. Update `SessionData.Events` field type.
- **IMPLEMENT**: Replace the `SavedEventConfig` struct (lines 60-107 of SessionData.cs) with:
```csharp
/// <summary>
/// Serializable copy of EventRule without runtime state (HasBeenTriggered)
/// and without Key binding (UI concern, not data).
/// </summary>
[Serializable]
public struct SavedEventRule
{
    public string DisplayName;
    public string AttributeName;
    public int Operator; // ComparisonOperator as int
    public string CompareValue;
    public float SpeedDelta;
    public float Duration;
    public int Weather; // WeatherType as int
    public bool AllowRepeat;

    public static SavedEventRule FromRule(EventRule rule)
    {
        return new SavedEventRule
        {
            DisplayName = rule.DisplayName ?? "",
            AttributeName = rule.AttributeName ?? "",
            Operator = (int)rule.Operator,
            CompareValue = rule.CompareValue ?? "",
            SpeedDelta = rule.SpeedDelta,
            Duration = rule.Duration,
            Weather = (int)rule.Weather,
            AllowRepeat = rule.AllowRepeat
        };
    }

    public EventRule ToRule(Key triggerKey)
    {
        return new EventRule
        {
            DisplayName = DisplayName,
            AttributeName = AttributeName,
            Operator = (ComparisonOperator)Operator,
            CompareValue = CompareValue,
            SpeedDelta = SpeedDelta,
            Duration = Duration,
            Weather = (WeatherType)Weather,
            TriggerKey = triggerKey,
            AllowRepeat = AllowRepeat,
            HasBeenTriggered = false
        };
    }
}
```

  And update SessionData class:
```csharp
public SavedEventRule[] Events = Array.Empty<SavedEventRule>();
```
- **MIRROR**: SERIALIZATION_PATTERN (static factory + conversion, plain structs, int for enums)
- **IMPORTS**: `System` (already imported)
- **GOTCHA**: Enums are stored as `int` for JsonUtility compatibility and forward compatibility (adding new operators won't break old saves). Old saved sessions with `SavedEventConfig` will fail to deserialize — this is an accepted breaking change per the PRD. Null-coalescing on strings prevents `JsonUtility` serialization issues.
- **VALIDATE**: Save session (P), load session (L). Verify events survive round-trip with correct operator, attribute, and compare values.

### Task 9: Update EventPanel
- **ACTION**: Replace `RaceEventConfig` references with `EventRule` in the UI panel.
- **IMPLEMENT**: Changes in EventPanel.cs:

  **Line 77** — Update handler signature:
```csharp
    private void OnEventTriggered(EventRule rule, int affectedCount)
```

  The rest of the method body (`events[i].HasBeenTriggered`, `events[i].AllowRepeat`) works identically since `EventRule` has the same field names.
- **MIRROR**: N/A — minimal change
- **IMPORTS**: No changes
- **GOTCHA**: `EventPanel.BuildEventRows` iterates `EventManager.Schedule.Events` which changes from `RaceEventConfig[]` to `EventRule[]`. The fields accessed (`DisplayName`, `HasBeenTriggered`, `AllowRepeat`) exist on both types, so the only change is the event handler signature.
- **VALIDATE**: Enter Play Mode. Verify event panel shows 7 buttons with correct labels. Click each button. Verify they trigger correctly and disable after triggering (except repeatable ones).

### Task 10: Update NetworkSync event handler
- **ACTION**: Update `OnEventTriggered` handler signature from `RaceEventConfig` to `EventRule`.
- **IMPLEMENT**: Change in NetworkSync.cs line 160:
```csharp
    private void OnEventTriggered(EventRule rule, int affectedCount)
    {
        if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
        var cars = RaceManager.SpawnedCars;
        var msg = new EventTriggeredMessage
        {
            name = rule.DisplayName,
            affected = affectedCount,
            total = cars != null ? cars.Count : 0
        };
        NetworkManager.Send(JsonUtility.ToJson(msg));
    }
```
- **MIRROR**: N/A — minimal change
- **IMPORTS**: No changes
- **GOTCHA**: The `EventTriggeredMessage` struct remains unchanged (it only carries `name`, `affected`, `total`). The network protocol is not affected by the rule engine change — students still receive the same message format.
- **VALIDATE**: Host a room, trigger events. Verify student client receives event messages correctly.

### Task 11: Update RuntimeSetup event handler
- **ACTION**: Update `OnEventTriggered` handler signature from `RaceEventConfig` to `EventRule` in `RuntimeSetup.cs`.
- **IMPLEMENT**: Change line 328 in RuntimeSetup.cs:
```csharp
    private void OnEventTriggered(EventRule rule, int affectedCount)
    {
        string entry = $"[{Time.time:F0}s] {rule.DisplayName} ({affectedCount} cars)";
        eventLogEntries.Add(entry);

        if (eventLogEntries.Count > 10)
            eventLogEntries.RemoveAt(0);

        if (eventLogText != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Events</b>");
            sb.AppendLine("─────────────────");
            foreach (var e in eventLogEntries)
                sb.AppendLine(e);
            eventLogText.text = sb.ToString();
        }
    }
```
- **MIRROR**: N/A — minimal change (parameter type only)
- **IMPORTS**: No changes
- **GOTCHA**: Only the parameter type changes from `RaceEventConfig config` to `EventRule rule`. The body references `config.DisplayName` which becomes `rule.DisplayName` — same field name exists on EventRule.
- **VALIDATE**: Enter Play Mode. Trigger events. Verify event log panel in top-right shows event entries.

### Task 12: Delete old files
- **ACTION**: Delete `EventMatcher.cs`, `RaceEventConfig.cs`, `RaceEventType.cs` and their .meta files
- **IMPLEMENT**: Delete these files:
  - `Assets/Scripts/Events/EventMatcher.cs`
  - `Assets/Scripts/Events/RaceEventConfig.cs`
  - `Assets/Scripts/Events/RaceEventType.cs`
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: Verify no other files reference `RaceEventType`, `RaceEventConfig`, or `EventMatcher` before deleting. Search the entire codebase. The only remaining reference should be in the new `RuleEngine.cs` (replacing EventMatcher) and the updated files from Tasks 5-10.
- **VALIDATE**: Full project compilation — zero errors. No "missing script" warnings in the console.

### Task 13: Verify EventSchedule ScriptableObject asset
- **ACTION**: Check if an EventSchedule `.asset` file exists in the project. If it does, it will have lost its data due to the type change and needs deletion and re-creation (or will auto-populate with new defaults).
- **IMPLEMENT**: Search for `.asset` files that reference EventSchedule. If found, the asset should either be deleted (Unity will use the field initializer defaults) or left as-is (Unity will create default values for unrecognized fields).
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: ScriptableObject assets store serialized data tied to field names and types. When `RaceEventConfig[] Events` becomes `EventRule[] Events`, the array element type changes. Unity will:
  1. Keep the field name `Events` (same)
  2. Fail to deserialize old elements into new struct (different fields)
  3. Initialize with default values (which happen to be zeros/empty — NOT the field initializer defaults)
  
  This means the asset will appear to have 7 events but with empty/zero values. **The asset must be deleted and re-created**, or the professor must reconfigure events in the Inspector. The code field initializer (Task 5) only applies to newly created assets.
- **VALIDATE**: In Unity Editor: select the EventSchedule asset. Verify all 7 rules display correctly in Inspector. If not, delete and re-create via Assets > Create > EDI Racing > Event Schedule.

---

## Testing Strategy

### Unit Tests

This is a Unity project without CLI test pipeline. Validation is done via Play Mode testing.

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| RuleEngine: Equals operator | colorIndex="3", car with colorIndex=3 | true | No |
| RuleEngine: Equals miss | colorIndex="3", car with colorIndex=2 | false | No |
| RuleEngine: NotEquals operator | colorIndex="3", car with colorIndex=2 | true | No |
| RuleEngine: Contains operator | functions has "facerecog/glasses", target="glasses" | true | No |
| RuleEngine: Contains with plain string | language="english", target="eng" | true | No |
| RuleEngine: NotContains operator | functions has "password", target="glasses" | true | No |
| RuleEngine: GreaterThan numeric | score="8", compare="5" | true | No |
| RuleEngine: LessThan numeric | score="3", compare="5" | true | No |
| RuleEngine: GreaterThan non-numeric | score="abc", compare="5" | false | Yes |
| RuleEngine: LengthGreaterThan | teamName="VeryLongTeamName" (15 chars), compare="10" | true | No |
| RuleEngine: LengthLessThan | teamName="Hi" (2 chars), compare="5" | true | No |
| RuleEngine: All operator | Any car | true | No |
| RuleEngine: empty attribute | non-existent attribute, Equals "test" | false | Yes |
| RuleEngine: null attribute name | attributeName=null | "" resolved, false for most ops | Yes |
| RuleEngine: case insensitive | colorIndex="3" vs key "ColorIndex" | true (resolves correctly) | Yes |
| v1 parity: NameLengthPenalty | teamName > 10 chars | speed -10 for 8s | No |
| v1 parity: ColorBoost Blue | colorIndex=3 | speed +15 for 6s | No |
| v1 parity: ColorPenalty Red | colorIndex=2 | speed -12 for 8s | No |
| v1 parity: FunctionBoost Password | functions contains "password" | speed +10 for 6s | No |
| v1 parity: FunctionPenalty FaceRecog | functions contains "facerecog" | speed -10 for 8s | No |
| v1 parity: Snow Weather | all cars | speed -8 for 12s + snow VFX | No |
| v1 parity: Night Weather | all cars | speed -5 for 15s + night VFX | No |
| Session round-trip | Save with EventRules, load | All rules restored correctly | No |
| Custom attribute rule | language="french", rule checks language Equals "french" | speed modified | No |

### Edge Cases Checklist
- [x] Empty attribute name with non-All operator → returns "" → won't match most comparisons
- [x] Non-numeric attribute with GreaterThan/LessThan → CompareNumeric returns 0 → false
- [x] Null CompareValue → null-safe via null coalescing in ContainsValue
- [x] Car with no attributes → GetAttribute returns "" → safe
- [x] Multiple events triggered simultaneously → each evaluated independently (existing behavior)
- [x] AllowRepeat=false + re-trigger → blocked (existing behavior preserved)
- [x] 50 cars × 7 rules → 350 evaluations per trigger → trivial performance
- [x] Slash-separated functions with Contains → splits on "/" and checks each token
- [x] Case sensitivity → all string comparisons are case-insensitive

---

## Validation Commands

### Build Verification
```
Unity Editor > File > Build Settings > Build (WebGL)
```
EXPECT: Zero compilation errors

### Play Mode Verification
```
Unity Editor > Play Mode in complete_track_demo scene
```
EXPECT: Cars spawn, race runs, all 7 events trigger correctly, weather VFX work

### Manual Validation
- [ ] Open Unity, verify zero compilation errors in Console
- [ ] Enter Play Mode in `complete_track_demo` scene
- [ ] Verify cars spawn correctly with default CSV
- [ ] Press 1 — verify Name Length Penalty affects teams with names > 10 chars
- [ ] Press 2 — verify Color Boost affects blue (colorIndex=3) cars only
- [ ] Press 3 — verify Color Penalty affects red (colorIndex=2) cars only
- [ ] Press 4 — verify Function Boost affects cars with "password" function
- [ ] Press 5 — verify Function Penalty affects cars with "facerecog" function
- [ ] Press 6 — verify Snow Weather affects ALL cars and snow VFX activates
- [ ] Press 7 — verify Night Weather affects ALL cars and night VFX activates
- [ ] Press T — verify scoreboard prints correctly
- [ ] Press P — verify session saves without error
- [ ] Press L — verify session loads and events restore correctly
- [ ] Press X — verify results CSV exports correctly
- [ ] Verify EventPanel buttons work (click each one)
- [ ] Verify buttons disable after triggering (except Snow/Night which are repeatable)
- [ ] Verify no console errors or warnings during all operations
- [ ] Select EventSchedule asset in Inspector — verify 7 rules display with correct values
- [ ] (If network is available) Host room, trigger events, verify student receives event messages

---

## Acceptance Criteria
- [ ] `RuleEngine.IsAffected()` evaluates all 9 comparison operators correctly
- [ ] All 7 v1 event types are reproducible as default EventRules
- [ ] Race behavior is identical to pre-refactor (same cars affected, same speed deltas)
- [ ] Weather VFX (snow, night) trigger via `WeatherType` field instead of enum check
- [ ] `EventSchedule` ScriptableObject holds `EventRule[]` with correct defaults
- [ ] Session save/load works with new `SavedEventRule` format
- [ ] EventPanel UI works identically (buttons, labels, disable-after-trigger)
- [ ] Network event broadcast works (same message format to students)
- [ ] Old files (`EventMatcher.cs`, `RaceEventConfig.cs`, `RaceEventType.cs`) are deleted
- [ ] Zero compilation errors
- [ ] Zero console errors during normal race flow

## Completion Checklist
- [ ] Code follows discovered patterns (PascalCase, no namespaces, `[Serializable]` structs)
- [ ] Error handling matches codebase style (null checks, safe defaults, no exceptions)
- [ ] Logging follows `[ClassName]` prefix convention
- [ ] No hardcoded values (operators are enum, attribute names are strings)
- [ ] No unnecessary scope additions (no builder UI, no JSON config, no compound rules)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| EventSchedule asset loses data on type change | HIGH (known) | MEDIUM | Delete and re-create the asset; new defaults match v1 parity exactly |
| Old saved sessions won't load | MEDIUM | LOW | Accepted breaking change per PRD; sessions from Phase 1 used old format |
| Contains operator behavior differs from v1 | LOW | MEDIUM | v1 used `Array.Any(f => f.Equals(...))` on split functions; new code also splits on "/" — behavior is equivalent |
| RuleEngine performance with many custom rules | LOW | LOW | Rules evaluated once per event trigger per car, not per frame; 50 cars × 20 rules = trivial |
| Other scripts reference deleted types | LOW | HIGH | Full codebase search before deletion (Task 11). All references updated in Tasks 5-10. |

## Notes

- **Operator design**: The `ComparisonOperator` enum includes `LengthGreaterThan` and `LengthLessThan` specifically for the v1 NameLengthPenalty pattern. These operate on string length rather than the string value itself. Phase 3 (Survey Config) may add more operators; the enum is easily extensible.

- **Weather VFX hook**: Weather effects are decoupled from the rule engine via the `WeatherType` field. RuleEngine doesn't know about weather — it just evaluates conditions. RaceManager checks `rule.Weather` after triggering and activates VFX. This keeps the rule engine pure and testable.

- **TriggerEventByType → TriggerEventByName**: The old `TriggerEventByType(RaceEventType)` method is replaced with `TriggerEventByName(string)` since events no longer have enum types. Verified via grep that `TriggerEventByType` is not called anywhere in the codebase (it was a future-use method).

- **Forward compatibility**: The `EventRule` struct is designed to be serialized in Phase 3's JSON config files. All fields are serializable by both `JsonUtility` and Newtonsoft.Json. The `ComparisonOperator` and `WeatherType` enums can be extended without breaking existing rules.

- **Task ordering**: Tasks 1-4 (new files) have no dependencies and can be implemented in any order. Tasks 5-11 (updates) depend on Tasks 1-4 but are independent of each other. Task 12 (deletion) must come last after all references are updated. Task 13 (asset verification) is done in Unity Editor after compilation.
