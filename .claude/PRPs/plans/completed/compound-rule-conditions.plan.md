# Plan: Compound Rule Conditions (AND/OR)

## Summary
Add AND/OR compound condition support to the event rule system. Currently each rule evaluates a single condition (one attribute + one operator + one value). This plan adds the ability to group multiple sub-conditions with AND/OR logic, enabling rules like "if color IS blue AND team size > 3, apply speed penalty." Changes span Unity C# (data model + engine), web-app React UI, and the JSON serialization pipeline.

## User Story
As a professor,
I want to create event rules with multiple conditions combined by AND/OR logic,
So that I can design more nuanced EDI demonstrations that reflect real-world intersectional dynamics.

## Problem → Solution
**Current:** Each EventRule has exactly one condition (`AttributeName` + `Operator` + `CompareValue`). To simulate "blue cars with large teams," the professor must create two separate rules with overlapping effects — imprecise and confusing.

**Desired:** A rule can contain a flat list of sub-conditions joined by AND or OR. The rule fires only when the compound expression evaluates to true.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md`
- **PRD Phase**: Could — Compound rule conditions
- **Estimated Files**: 10

---

## UX Design

### Before
```
┌─────────────────────────────────────────────┐
│  Rule: "Blue Car Penalty"                   │
│  ┌────────────┬──────────┬───────────────┐  │
│  │ colorIndex │ Equals   │ 3             │  │
│  └────────────┴──────────┴───────────────┘  │
│  Speed: -10  Duration: 8s  Weather: None    │
└─────────────────────────────────────────────┘
One condition per rule. No way to combine.
```

### After
```
┌─────────────────────────────────────────────┐
│  Rule: "Blue Large Team Penalty"            │
│  Logic: [AND ▼]                             │
│  ┌────────────┬──────────┬───────────────┐  │
│  │ colorIndex │ Equals   │ 3             │  │
│  └────────────┴──────────┴───────────────┘  │
│  ┌────────────┬──────────┬───────────────┐  │
│  │ member_cnt │ Greater  │ 3             │  │
│  └────────────┴──────────┴───────────────┘  │
│  [+ Add Condition]                          │
│  Speed: -10  Duration: 8s  Weather: None    │
└─────────────────────────────────────────────┘
Multiple conditions with AND/OR toggle.
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Rule condition area | Single row: attribute/operator/value | Multiple rows + AND/OR toggle + "Add Condition" button | Only appears when >1 condition |
| Operator dropdown | 9 operators (Equals…All) | Same 9 operators per sub-condition | AND/OR is a separate toggle, not in operator list |
| JSON export | Flat fields on rule | `Conditions[]` array + `LogicOperator` field | Backward-compatible: empty Conditions = use legacy fields |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Events/EventRule.cs` | all | Current rule struct — must extend |
| P0 | `Assets/Scripts/Events/RuleEngine.cs` | all | Core evaluation logic — must modify |
| P0 | `Assets/Scripts/Data/SessionData.cs` | 66-109 | SavedEventRule — serialization mirror |
| P1 | `Assets/Scripts/Events/ComparisonOperator.cs` | all | Operator enum — no changes needed |
| P1 | `Assets/Scripts/Events/EventManager.cs` | 60-91 | TriggerEvent flow — no changes needed |
| P1 | `web-app/client/src/components/RulesTab.jsx` | all | Web rule editor container |
| P1 | `web-app/client/src/components/RuleRow.jsx` | all | Web rule row UI |
| P1 | `web-app/client/src/constants.js` | all | Operator constants |
| P2 | `Assets/Tests/EditMode/RuleEngineTests.cs` | all | Test patterns to extend |
| P2 | `Assets/Tests/EditMode/SavedEventRuleTests.cs` | all | Serialization test patterns |

## External Documentation

No external research needed — feature uses established internal patterns.

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Events/EventRule.cs:10-22
[Serializable]
public struct EventRule
{
    public string DisplayName;
    public string AttributeName;
    public ComparisonOperator Operator;
    // PascalCase public fields, [Serializable] struct
}
```

### SERIALIZATION_PATTERN
```csharp
// SOURCE: Assets/Scripts/Data/SessionData.cs:66-77
[Serializable]
public struct SavedEventRule
{
    public string DisplayName;
    public string AttributeName;
    public int Operator;      // Enum stored as int for JSON
    public string CompareValue;
    // int for enums, string for text, float for numbers
}
```

### RULE_EVALUATION
```csharp
// SOURCE: Assets/Scripts/Events/RuleEngine.cs:11-47
public static bool IsAffected(EventRule rule, CarIdentity car)
{
    if (rule.Operator == ComparisonOperator.All) return true;
    string value = ResolveAttributeValue(rule.AttributeName, car);
    switch (rule.Operator)
    {
        case ComparisonOperator.Equals:
            return string.Equals(value, rule.CompareValue, ...);
        // ...
    }
}
```

### TEST_STRUCTURE
```csharp
// SOURCE: Assets/Tests/EditMode/RuleEngineTests.cs:11-30
[SetUp]
public void Setup()
{
    var go = new GameObject("TestCar");
    testCar = go.AddComponent<CarIdentity>();
    testCar.Initialize(new CarData("TestTeam", new AttributeEntry[] { ... }));
}

[TearDown]
public void TearDown() { Object.DestroyImmediate(testCar.gameObject); }

[Test]
public void IsAffected_EqualsOperator_MatchesExact()
{
    var rule = new EventRule { AttributeName = "colorIndex", Operator = ComparisonOperator.Equals, CompareValue = "2" };
    Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
}
```

### REACT_COMPONENT
```jsx
// SOURCE: web-app/client/src/components/RuleRow.jsx:1-5
import React from 'react';
import { ComparisonOperator, ComparisonOperatorLabels, WeatherType, WeatherLabels } from '../constants';
// Functional component, props-driven, immutable onChange callbacks
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Events/EventRule.cs` | UPDATE | Add `RuleCondition[]` + `LogicOperator` fields |
| `Assets/Scripts/Events/RuleEngine.cs` | UPDATE | Add compound evaluation via `EvaluateConditions()` |
| `Assets/Scripts/Data/SessionData.cs` | UPDATE | Add `SavedRuleCondition[]` + `LogicOperator` to SavedEventRule |
| `web-app/client/src/components/RuleRow.jsx` | UPDATE | Render multiple condition rows + AND/OR toggle |
| `web-app/client/src/components/RulesTab.jsx` | UPDATE | Pass conditions to RuleRow |
| `web-app/client/src/constants.js` | UPDATE | Add LogicOperator constant |
| `Assets/Tests/EditMode/RuleEngineTests.cs` | UPDATE | Add compound condition tests |
| `Assets/Tests/EditMode/SavedEventRuleTests.cs` | UPDATE | Add compound serialization tests |
| `web-app/src/seed-templates.js` | UPDATE | Add one compound-rule example to a template |
| `web-app/client/src/styles/App.css` | UPDATE | Style for condition rows |

## NOT Building

- Nested compound groups (AND within OR) — flat list only, one logic operator per rule
- Visual condition tree builder — simple list with toggle is sufficient
- Migration tool for existing configs — backward-compatible design handles old format
- Changes to EventManager or CarController — rule evaluation API unchanged

---

## Step-by-Step Tasks

### Task 1: Add RuleCondition struct (Unity)
- **ACTION**: Create a new `RuleCondition` serializable struct in `EventRule.cs`
- **IMPLEMENT**:
  ```csharp
  [Serializable]
  public struct RuleCondition
  {
      public string AttributeName;
      public ComparisonOperator Operator;
      public string CompareValue;
  }

  public enum LogicOperator { And, Or }
  ```
  Add to `EventRule`:
  ```csharp
  public LogicOperator Logic;
  public RuleCondition[] Conditions;
  ```
- **MIRROR**: NAMING_CONVENTION — PascalCase, [Serializable]
- **GOTCHA**: Keep existing `AttributeName`, `Operator`, `CompareValue` fields for backward compatibility. When `Conditions` is null/empty, fall back to legacy single-condition fields.
- **VALIDATE**: File compiles. No runtime changes yet.

### Task 2: Update RuleEngine evaluation (Unity)
- **ACTION**: Modify `RuleEngine.IsAffected()` to handle compound conditions
- **IMPLEMENT**:
  ```csharp
  public static bool IsAffected(EventRule rule, CarIdentity car)
  {
      if (rule.Operator == ComparisonOperator.All) return true;
      
      // Compound: evaluate Conditions array
      if (rule.Conditions != null && rule.Conditions.Length > 0)
          return EvaluateConditions(rule.Conditions, rule.Logic, car);
      
      // Legacy: single condition (backward compat)
      return EvaluateSingleCondition(rule.AttributeName, rule.Operator, rule.CompareValue, car);
  }

  private static bool EvaluateConditions(RuleCondition[] conditions, LogicOperator logic, CarIdentity car)
  {
      if (logic == LogicOperator.And)
      {
          foreach (var c in conditions)
              if (!EvaluateSingleCondition(c.AttributeName, c.Operator, c.CompareValue, car))
                  return false;
          return true;
      }
      else // Or
      {
          foreach (var c in conditions)
              if (EvaluateSingleCondition(c.AttributeName, c.Operator, c.CompareValue, car))
                  return true;
          return false;
      }
  }

  private static bool EvaluateSingleCondition(string attrName, ComparisonOperator op, string compareValue, CarIdentity car)
  {
      // Extract existing switch logic into this method
  }
  ```
- **MIRROR**: RULE_EVALUATION pattern
- **GOTCHA**: `All` operator check must remain at the top level, before compound evaluation. Extract existing switch body into `EvaluateSingleCondition` — do NOT duplicate.
- **VALIDATE**: Existing tests still pass (backward compat via legacy path).

### Task 3: Update SavedEventRule serialization (Unity)
- **ACTION**: Add `SavedRuleCondition` struct and update `SavedEventRule`
- **IMPLEMENT**:
  ```csharp
  [Serializable]
  public struct SavedRuleCondition
  {
      public string AttributeName;
      public int Operator;
      public string CompareValue;
  }
  ```
  Add to `SavedEventRule`:
  ```csharp
  public int Logic;  // LogicOperator as int
  public SavedRuleCondition[] Conditions;
  ```
  Update `FromRule()` and `ToRule()` to convert `Conditions` array.
- **MIRROR**: SERIALIZATION_PATTERN — enums as int
- **GOTCHA**: Handle null `Conditions` in `ToRule()` — return empty array, not null. Null check in `FromRule()` too.
- **VALIDATE**: Round-trip test: EventRule → SavedEventRule → EventRule preserves compound conditions.

### Task 4: Add compound condition tests (Unity)
- **ACTION**: Add test cases to `RuleEngineTests.cs` and `SavedEventRuleTests.cs`
- **IMPLEMENT**:
  Tests for RuleEngine:
  - `IsAffected_AndConditions_AllMatch_ReturnsTrue`
  - `IsAffected_AndConditions_OneFails_ReturnsFalse`
  - `IsAffected_OrConditions_OneMatches_ReturnsTrue`
  - `IsAffected_OrConditions_NoneMatch_ReturnsFalse`
  - `IsAffected_EmptyConditions_FallsBackToLegacy`
  - `IsAffected_SingleConditionInArray_WorksLikeSimple`
  
  Tests for SavedEventRule:
  - `FromRule_WithConditions_PreservesAll`
  - `ToRule_WithConditions_RestoresCorrectly`
  - `RoundTrip_CompoundRule_Preserves`
- **MIRROR**: TEST_STRUCTURE pattern
- **VALIDATE**: All new + existing tests pass.

### Task 5: Update web-app constants
- **ACTION**: Add `LogicOperator` to `constants.js`
- **IMPLEMENT**:
  ```javascript
  export const LogicOperator = { And: 0, Or: 1 };
  export const LogicOperatorLabels = ['AND (all must match)', 'OR (any can match)'];
  ```
- **VALIDATE**: No runtime errors.

### Task 6: Update RuleRow component
- **ACTION**: Modify `RuleRow.jsx` to support multiple condition rows + AND/OR toggle
- **IMPLEMENT**:
  - When rule has `Conditions` array with >0 items, render each as a condition row
  - When rule has no Conditions, render legacy single-condition row (backward compat)
  - Add "AND/OR" toggle dropdown (only visible when >1 condition)
  - Add "+ Add Condition" button below condition list
  - Each condition row: AttributeName | Operator | CompareValue | [x Remove]
  - When Operator is `All`, disable condition rows (same as current behavior)
- **MIRROR**: REACT_COMPONENT pattern — functional, immutable onChange
- **GOTCHA**: When adding first condition via "+ Add Condition", migrate legacy fields into `Conditions[0]` so data isn't lost. When removing last condition, migrate `Conditions[0]` back to legacy fields.
- **VALIDATE**: Can add/remove conditions in browser. AND/OR toggle works.

### Task 7: Update RulesTab for conditions data flow
- **ACTION**: Ensure `RulesTab.jsx` default rule object includes `Conditions` and `Logic` fields
- **IMPLEMENT**:
  Update default new rule:
  ```javascript
  {
    DisplayName: '',
    Logic: LogicOperator.And,
    Conditions: [],  // Empty = legacy single-condition mode
    AttributeName: '',
    Operator: ComparisonOperator.Equals,
    CompareValue: '',
    SpeedDelta: -10,
    Duration: 8,
    Weather: WeatherType.None,
    AllowRepeat: false,
  }
  ```
- **VALIDATE**: New rules created in web-app include Logic/Conditions fields.

### Task 8: Add compound rule to seed template
- **ACTION**: Add one AND-compound rule example to an existing template in `seed-templates.js`
- **IMPLEMENT**: Add to "Accessibility" template:
  ```javascript
  {
    DisplayName: 'Intersectional Barrier',
    Logic: 0,  // AND
    Conditions: [
      { AttributeName: 'disability', Operator: 1, CompareValue: 'none' },
      { AttributeName: 'assistive_tech', Operator: 0, CompareValue: 'no' }
    ],
    AttributeName: '',
    Operator: 0,
    CompareValue: '',
    SpeedDelta: -15,
    Duration: 10,
    Weather: 0,
    AllowRepeat: false,
  }
  ```
- **GOTCHA**: Keep legacy fields present (empty) for backward compat. Don't remove existing rules from the template.
- **VALIDATE**: Template loads in web-app without errors.

### Task 9: Add CSS for condition rows
- **ACTION**: Add styles for compound condition UI in `App.css`
- **IMPLEMENT**:
  ```css
  .condition-list { display: flex; flex-direction: column; gap: 4px; }
  .condition-row { display: flex; gap: 8px; align-items: center; }
  .condition-row .remove-btn { /* match existing delete button style */ }
  .logic-toggle { margin-bottom: 4px; }
  .add-condition-btn { /* match existing add button style */ }
  ```
- **VALIDATE**: UI renders cleanly with 1, 2, and 3 conditions.

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| AND — all match | rule(AND, [color=2, lang=French]) + car(color=2, lang=French) | true | No |
| AND — one fails | rule(AND, [color=2, lang=English]) + car(color=2, lang=French) | false | No |
| OR — one matches | rule(OR, [color=2, lang=English]) + car(color=2, lang=French) | true | No |
| OR — none match | rule(OR, [color=5, lang=English]) + car(color=2, lang=French) | false | No |
| Empty Conditions | rule(Conditions=[]) + legacy fields set | evaluates legacy | Yes |
| Null Conditions | rule(Conditions=null) + legacy fields set | evaluates legacy | Yes |
| Single condition in array | rule(AND, [color=2]) | same as simple rule | Yes |
| All operator | rule(Operator=All, Conditions=[...]) | true (ignores conditions) | Yes |
| SavedEventRule round-trip | compound EventRule → SavedEventRule → EventRule | identical | No |

### Edge Cases Checklist
- [x] Empty Conditions array → falls back to legacy single-condition
- [x] Null Conditions array → falls back to legacy single-condition
- [x] Single condition in Conditions array → works like simple rule
- [x] All operator with conditions → All takes precedence
- [x] Mixed operator types in conditions (Equals + GreaterThan)
- [ ] Web-app: remove all conditions → reverts to legacy mode

---

## Validation Commands

### Unit Tests
```bash
# Unity EditMode tests (run via Unity Test Runner or CI)
# game-ci/unity-test-runner@v4 in GitHub Actions
```
EXPECT: All existing + new tests pass

### Web-app
```bash
cd web-app && npm test
```
EXPECT: No regressions

### Manual Validation
- [ ] Open web-app → create survey → go to Rules tab
- [ ] Create a rule → click "+ Add Condition" → second condition row appears
- [ ] Toggle AND/OR dropdown
- [ ] Export survey JSON → verify `Conditions` array present
- [ ] Import into Unity → trigger event → verify compound logic works
- [ ] Load old config (no Conditions field) → verify backward compat

---

## Acceptance Criteria
- [ ] AND conditions: rule fires only when ALL sub-conditions match
- [ ] OR conditions: rule fires when ANY sub-condition matches
- [ ] Legacy rules (no Conditions) still work unchanged
- [ ] Web-app UI supports add/remove conditions + AND/OR toggle
- [ ] JSON export includes Conditions array
- [ ] Unity import handles both old and new format
- [ ] 9+ new unit tests pass
- [ ] Existing 30+ tests still pass

## Completion Checklist
- [ ] Code follows discovered patterns (PascalCase, [Serializable], int-for-enum)
- [ ] Backward compatible — no existing config breaks
- [ ] Tests follow NUnit [Test] pattern with Setup/TearDown
- [ ] React components follow functional + immutable onChange pattern
- [ ] No hardcoded values (LogicOperator enum, not magic ints)
- [ ] Seed template updated with compound example

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| JsonUtility fails on nested arrays | LOW | HIGH | `SavedRuleCondition[]` is a simple serializable struct array — JsonUtility handles these. Test early. |
| Old configs crash on missing Conditions field | MEDIUM | HIGH | Null/empty check at top of IsAffected + in ToRule(). JsonUtility leaves missing fields as default (null for arrays). |
| UI complexity for professors | LOW | MEDIUM | Default is legacy single-condition mode. Compound is opt-in via "+ Add Condition" button. |

## Notes
- Design decision: **flat list, not nested tree**. One LogicOperator per rule (AND or OR), not nested groups. This covers 95%+ of use cases and keeps UI simple. Nested AND/OR (e.g., "(A AND B) OR (C AND D)") is explicitly out of scope.
- The 9-rule keyboard shortcut limit (Digit1-Digit9) remains unchanged — compound conditions add depth to each rule, not more rules.
- No changes needed to `EventManager.cs`, `CarController.cs`, or `EventSchedule.cs` — the `RuleEngine.IsAffected()` API contract (returns bool) is preserved.
