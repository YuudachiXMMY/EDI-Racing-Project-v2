# Event System

> **Status**: Accepted — reverse-engineered from codebase (v2)
> **Last Updated**: 2026-07-23
> **Source**: `EventManager.cs`, `EventRule.cs`, `RuleEngine.cs`,
> `EventSchedule.cs`, `ComparisonOperator.cs`
> **ADR**: ADR-0006 (Interpreted Rule Engine)

---

## 1. Overview

The event system is the core educational mechanic. Professors trigger events
during the race (via keyboard 1-9 or programmatic API), each event evaluates a
set of conditions against every car's attributes, and applies speed modifiers
(penalties or boosts) to matching cars. This creates visible, real-time
inequality tied directly to student survey data.

Events support single-condition (legacy) and compound multi-condition rules with
AND/OR logic. Each event can optionally trigger a weather visual effect.

---

## 2. Player Fantasy

"I press '3' and announce 'Language Barrier!' — every team whose primary language
isn't English slows down for 8 seconds while snow falls. The class watches the
gap widen. Then I press '4' — 'Mentorship Program' — and first-generation
students get a speed boost. The conversation about systemic equity writes
itself."

---

## 3. Detailed Rules

### Event Rule Structure

```
EventRule {
    DisplayName         // "Language Barrier", "Color Boost (Blue)"
    // Compound conditions (v2)
    Logic               // AND or OR
    Conditions[]        // array of RuleCondition
    // Legacy single condition (v1 compat)
    AttributeName       // "colorIndex", "language", "teamName"
    Operator            // ComparisonOperator enum
    CompareValue        // "3", "English", "10"
    // Effect
    SpeedDelta          // m/s change (negative = penalty, positive = boost)
    Duration            // seconds
    Weather             // None, Snow, Night, Sunset
    // Control
    TriggerKey           // Key.Digit1 through Key.Digit9
    AllowRepeat          // can trigger more than once?
    HasBeenTriggered     // runtime state
}
```

### Condition Resolution Priority

```
1. If Operator == All → match all cars (global/weather events)
2. If Conditions[] is non-empty → evaluate compound conditions with Logic
3. Otherwise → evaluate legacy single condition (AttributeName/Operator/CompareValue)
```

### Compound Conditions

```
Logic = AND: ALL conditions must match
Logic = OR:  ANY condition can match

RuleCondition {
    AttributeName       // attribute to check
    Operator            // ComparisonOperator
    CompareValue        // value to compare against
}
```

### Comparison Operators

| Operator | Behavior |
|----------|----------|
| `Equals` | Case-insensitive string equality |
| `NotEquals` | Inverse of Equals |
| `Contains` | Attribute contains value; supports `/`-delimited lists |
| `NotContains` | Inverse of Contains |
| `GreaterThan` | Numeric: attribute > compare value |
| `LessThan` | Numeric: attribute < compare value |
| `LengthGreaterThan` | String length: attribute.Length > compare value |
| `LengthLessThan` | String length: attribute.Length < compare value |
| `All` | Always matches — used for global weather events |

### Attribute Resolution

```
if attributeName == "teamName" → car.TeamName
otherwise → car.GetAttribute(attributeName, "")
```

### Contains with Slash-Delimited Values

When the attribute value contains `/`, each `/`-separated segment is checked
independently (case-insensitive, trimmed). Example:
- Attribute: `"password/facerecog/fingerprint"`
- Contains `"facerecog"` → `true`

### Event Trigger Flow

```
1. Professor presses Digit key (or calls TriggerEventByName)
2. Check AllowRepeat — if already triggered and !AllowRepeat, skip
3. For each registered car:
   a. RuleEngine.IsAffected(rule, car) → bool
   b. If affected: car.ApplySpeedModifier(SpeedDelta, Duration)
   c. Count affected
4. Mark HasBeenTriggered = true
5. Log to EventLog (timestamp, name, affected/total)
6. Fire OnEventTriggered event (for weather VFX and UI)
```

### Default Event Rules (V1 Parity Template)

| # | Event | Attribute | Operator | Value | Delta | Duration |
|---|-------|-----------|----------|-------|-------|----------|
| 1 | Name Length Penalty | teamName | LengthGreaterThan | 10 | -10 m/s | 8s |
| 2 | Color Boost (Blue) | colorIndex | Equals | 3 | +15 m/s | 6s |
| 3 | Color Penalty (Red) | colorIndex | Equals | 2 | -12 m/s | 8s |
| 4 | Function Boost (Password) | functions | Contains | password | +10 m/s | 6s |
| 5 | Function Penalty (Face Recog) | functions | Contains | facerecog | -10 m/s | 8s |
| 6 | Snow Weather | — | All | — | -8 m/s | 12s |
| 7 | Night Weather | — | All | — | -5 m/s | 15s |

---

## 4. Formulas

### Condition Matching

```
IsAffected(rule, car):
    if rule.Operator == All → return true
    if rule.Conditions.Length > 0 → EvaluateConditions(conditions, logic, car)
    else → EvaluateSingleCondition(attributeName, operator, compareValue, car)

EvaluateConditions(conditions, AND, car):
    return conditions.All(c => EvaluateSingle(c))

EvaluateConditions(conditions, OR, car):
    return conditions.Any(c => EvaluateSingle(c))
```

### Numeric Comparison

```
CompareNumeric(attrValue, compareValue):
    parse both as float
    return attrNum.CompareTo(compNum)
    // returns 0 if either parse fails (treats as equal → no match)
```

### Length Comparison

```
CompareLengthNumeric(attrValue, compareValue):
    length = attrValue.Length
    threshold = int.Parse(compareValue)
    return length.CompareTo(threshold)
```

### Speed Modifier Application

```
agent.speed += SpeedDelta        // immediate
wait(Duration)
agent.speed -= SpeedDelta        // restored
// Multiple events stack: snow(-8) + barrier(-10) = net -18 m/s
```

---

## 5. Edge Cases

| Scenario | Handling |
|----------|----------|
| Attribute not found on car | Returns `""` — most operators treat as no match |
| Numeric comparison with non-numeric value | `CompareNumeric` returns 0 (treated as equal, no match for GT/LT) |
| AllowRepeat=false, triggered twice | Second press logged as "already triggered", no effect |
| Event index out of range | `TriggerEvent` returns early |
| No event named X | `TriggerEventByName` logs warning |
| Empty CompareValue with Equals | Matches cars with empty attribute value |
| Multiple events overlap | Speed deltas stack additively; each tracks its own timer |
| Event triggered with 0 registered cars | Logs "0/0 cars affected" |

---

## 6. Dependencies

| Dependency | Role |
|-----------|------|
| RuleEngine | Static condition evaluation |
| CarIdentity | Provides attribute values |
| CarController | Receives `ApplySpeedModifier()` |
| EventSchedule (SO) | Stores EventRule array |
| WeatherEffect | Triggered by `rule.Weather` |
| Keyboard (Input System) | Digit1-9 trigger keys |

---

## 7. Tuning Knobs

| Parameter | Type | Per-Rule | Effect |
|-----------|------|----------|--------|
| DisplayName | string | Yes | Shown in UI and logs |
| AttributeName | string | Yes | Car attribute to match |
| Operator | enum | Yes | Comparison type |
| CompareValue | string | Yes | Threshold or target value |
| SpeedDelta | float (m/s) | Yes | Speed change (+boost, -penalty) |
| Duration | float (s) | Yes | How long effect lasts |
| Weather | enum | Yes | VFX to activate (None/Snow/Night/Sunset) |
| AllowRepeat | bool | Yes | One-shot vs repeatable |
| Logic | enum | Yes | AND/OR for compound conditions |

---

## 8. Acceptance Criteria

- [ ] Events triggered by keyboard (1-9) apply speed modifiers to matching cars
- [ ] Events can be triggered programmatically via `TriggerEventByName()`
- [ ] `All` operator matches every car (for weather/global events)
- [ ] `Contains` correctly handles `/`-delimited attribute values
- [ ] Compound conditions with AND require all sub-conditions to match
- [ ] Compound conditions with OR require any sub-condition to match
- [ ] Non-repeatable events cannot fire twice
- [ ] Event log records timestamp, event name, affected count, total cars
- [ ] Weather VFX activates when rule.Weather != None
- [ ] Speed modifiers stack additively and expire independently
