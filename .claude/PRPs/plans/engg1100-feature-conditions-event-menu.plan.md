# Plan: ENGG*1100 Feature Conditions + Parameterized Event Menu

## Summary
Two connected changes. **(A) Feature conditions** — update the web-app survey post-processing so each team's car earns function tags from stricter rules: `facerecog / glasses / language / password / distance` are granted when the team's count is **strictly greater than the cohort average** (Password flips from its old `≤ average` logic), and `male` is granted when **non-male members (member_count − male_count) < 2** (replacing the old `male_count > 2`). **(B) Event menu** — replace Unity's direct digit-key event triggering with a professor-facing **parameterized control menu**: six on-screen buttons (also openable via digit keys 1–6) that fade in secondary menus for Name-Length (input box), Male (accelerate/decelerate), Color Boost/Penalty (color picker), and Function Boost/Penalty (function picker). Acceleration is fixed at **+20**, deceleration at **−15**, each lasting **10 s**. Snow and Night weather move to digit keys **9** and **0**.

## User Story
As a professor running the EDI Racing game, I want cars to gain features from clearer survey-driven rules and I want to trigger boosts/penalties live by picking a color, a function, a name-length threshold, or accelerate-vs-decelerate from an on-screen menu, so that I can drive the classroom demonstration interactively instead of memorizing which number key fires which fixed effect.

## Problem → Solution
- **Current (A)**: `facial/glasses/language/distance` use `≥ average`, `password` uses `≤ average` (reversed), `male` uses `male_count > 2`. `average_threshold` has no strict `>` branch and there is no way to compare `member_count − male_count`.
  → **Desired (A)**: all five count-features use strict `> average`; `male` uses `(member_count − male_count) < 2`. Add a `gt`/`lt` branch to `average_threshold` and a new `difference_threshold` rule type; map `member_count`.
- **Current (B)**: `EventManager.Update()` polls each `Schedule.Events[i].TriggerKey` (Digit1–8) and fires a fixed pre-baked `EventRule`. `EventPanel` renders one button per rule labeled `[n] DisplayName`.
  → **Desired (B)**: a purpose-built menu constructs `EventRule`s at trigger time from the professor's live choices and calls a new `EventManager.TriggerRule(rule)`. Digit-key rule polling is removed; digit keys 1–6 open menus, 9/0 fire weather.

## Metadata
- **Complexity**: Large
- **Source PRD**: N/A (free-form user request)
- **PRD Phase**: N/A
- **Predecessor**: `.claude/PRPs/plans/completed/engg1100-survey-template.plan.md` (this builds on that pipeline)
- **Estimated Files**: ~16 (5 web-app, ~8 Unity, 3 test)

---

## UX Design

### Before
```
Professor, mid-race (Unity EventPanel):
  ┌─ Events ──────────────────────────┐
  │ [1] Name Length Penalty  (key 1)  │   Pressing a digit key fires ONE
  │ [2] Color Boost (Blue)   (key 2)  │   fixed rule with baked-in color/
  │ [3] Color Penalty (Red)  (key 3)  │   function/threshold/delta.
  │ [4] Function Boost (Pwd) (key 4)  │   No choice of which color, which
  │ [5] Function Penalty ... (key 5)  │   function, or the name-length N.
  │ [6] Snow  [7] Night  [8] Sunset   │
  └───────────────────────────────────┘
```

### After
```
Professor, mid-race (Unity control menu):
  ┌─ Events ─────────────┐        click [3] Color Boost  →  fade-in:
  │ [1] Name Length      │        ┌─ Boost which colour? ─────────┐
  │ [2] Male             │        │ (Blue) (Red) (Black) (White)  │
  │ [3] Color Boost      │        │ (Green)          [Cancel]     │
  │ [4] Color Penalty    │        └───────────────────────────────┘
  │ [5] Function Boost   │        → cars of that colour get +20 for 10s
  │ [6] Function Penalty │
  └──────────────────────┘        [1] → number input box (name length N)
   digit 1–6 open the same menus    "cars with name length > N  →  −15"
   9 = Snow   0 = Night  (direct)  [2] Male → (Accelerate)(Decelerate)
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Digit keys 1–7 | Each fires one fixed rule | Removed as direct triggers; 1–6 open menus | `EventManager.Update()` polling deleted |
| Name-length event | Fixed N=10 baked in rule | Button/key 1 → number input box → `teamName.Length > N` → −15 | Live threshold |
| Color events | Two fixed rules (Blue boost, Red penalty) | Color Boost / Color Penalty buttons → pick any of 5 colors | +20 / −15 |
| Function events | Two fixed rules (Password boost, FaceRecog penalty) | Function Boost / Penalty buttons → pick any of 5 functions | +20 / −15 |
| Male event | (none) | Male button → Accelerate / Decelerate → cars with `male` tag | +20 / −15 |
| Snow / Night | Digit keys 6/7 (schedule order) | Digit keys **9 / 0** (direct) | Values unchanged (−8/12s, −5/15s) |
| Sunset manual trigger | Digit key 8 | Removed as a manual trigger | Day-cycle auto-sunset in `WeatherEffect` still runs |
| `male` tag condition | `male_count > 2` | `(member_count − male_count) < 2` | Web-app post-processing |
| count-feature condition | `≥ avg` (pwd `≤ avg`) | strict `> avg` for all five | Web-app post-processing |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/export.js` | 93–166 | `applyPostProcessing` + `buildCarData` — the algorithm to extend |
| P0 | `web-app/src/seed-templates.js` | 26–48 | ENGG mappings + `postProcessing` array to edit |
| P0 | `Assets/Scripts/Events/EventManager.cs` | 47–107 | `Update()` key polling + `TriggerEvent` — refactor target |
| P0 | `Assets/Scripts/Events/RuleEngine.cs` | 42–121 | Operators the ad-hoc rules must reuse (no change) |
| P0 | `Assets/Scripts/Data/DefaultEventRules.cs` | 15–28 | Canonical operator/weather/delta values to mirror |
| P1 | `Assets/Scripts/UI/EventPanel.cs` | all | Panel it replaces; runtime-build + `OnEventTriggered` pattern |
| P1 | `Assets/Scripts/Race/RaceManager.cs` | 241–264 | `OnEventTriggered` → weather VFX switch (must keep firing) |
| P1 | `Assets/Scripts/Network/NetworkSync.cs` | 180–201 | `OnEventTriggered` → student broadcast (must keep firing) |
| P1 | `Assets/Scripts/UI/RaceUI.cs` | 28–52, 138–164 | Panel wiring + role/visibility toggling |
| P1 | `Assets/Scripts/Editor/TrackSetupEditor.cs` | 832–875 | Procedural UI-panel construction pattern |
| P1 | `Assets/Scripts/Events/EventRule.cs` | 33–76 | `EventRule` fields the builder populates |
| P1 | `Assets/Scripts/Events/ComparisonOperator.cs` | all | Operator enum (`Equals`, `Contains`, `LengthGreaterThan`, `All`) |
| P2 | `Assets/Scripts/Car/CarIdentity.cs` | 70–99 | `GetAttribute`, `ColorIndex`, `Functions` accessors |
| P2 | `Assets/Scripts/Car/CarController.cs` | 433–451 | `ApplySpeedModifier(delta, duration)` — the effect sink |
| P2 | `Assets/Scripts/Data/SurveyTemplates.cs` | 282–318 | ENGG template to mirror `member_count` mapping |
| P2 | `Assets/Tests/EditMode/EventManagerTests.cs` | all | Test contract to preserve for `TriggerEvent` |
| P2 | `Assets/Tests/EditMode/SurveyTemplatesTests.cs` | 55–65 | Mapping-count assertion to update (7 → 8) |

## External Documentation
No external research needed — feature uses established internal patterns (UGUI runtime construction, Input System `Keyboard.current`, existing rule engine, vitest, NUnit EditMode).

---

## Patterns to Mirror

### POST_PROCESSING_ALGORITHM
```javascript
// SOURCE: web-app/src/routes/export.js:110-144
// applyPostProcessing computes per-source averages, then tags each car.
// average_threshold currently ONLY handles 'gte'/'lte' — 'gt' branch is MISSING.
if (rule.type === 'average_threshold') {
  const avg = averages[rule.sourceAttribute] || 0;
  if (rule.direction === 'gte') passes = value >= avg;
  else if (rule.direction === 'lte') passes = value <= avg;
} else if (rule.type === 'fixed_threshold') {
  const threshold = parseFloat(rule.threshold) || 0;
  if (rule.direction === 'gt') passes = value > threshold;
  // ... gte / lt / lte
}
// Tags merged as slash-joined string into car.attributes (e.g. "facerecog/male").
```

### SEED_POSTPROCESSING_SHAPE
```javascript
// SOURCE: web-app/src/seed-templates.js:35-41 (current)
postProcessing: [
  { type: 'average_threshold', sourceAttribute: 'facial_count', direction: 'gte', tagName: 'facerecog', targetAttribute: 'functions' },
  { type: 'average_threshold', sourceAttribute: 'pwd_count',    direction: 'lte', tagName: 'password',  targetAttribute: 'functions' },
  { type: 'fixed_threshold',   sourceAttribute: 'male_count', threshold: 2, direction: 'gt', tagName: 'male', targetAttribute: 'functions' },
],
```

### EVENT_TRIGGER_CORE
```csharp
// SOURCE: Assets/Scripts/Events/EventManager.cs:60-91
// TriggerEvent(index) reads a pre-baked rule from Schedule, loops registered cars,
// applies RuleEngine.IsAffected → CarController.ApplySpeedModifier, fires OnEventTriggered.
// The car-loop body is what TriggerRule(EventRule) must extract & reuse.
foreach (var car in registeredCars)
    if (RuleEngine.IsAffected(rule, car)) {
        var controller = car.GetComponent<CarController>();
        if (controller != null) {
            controller.ApplySpeedModifier(rule.SpeedDelta, rule.Duration);
            affectedCount++;
        }
    }
OnEventTriggered?.Invoke(rule, affectedCount); // RaceManager weather + NetworkSync hang off this
```

### WEATHER_HANGS_OFF_ONEVENTTRIGGERED
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:251-263
// Weather VFX is driven by rule.Weather when OnEventTriggered fires — so an ad-hoc
// weather rule built by the menu (Weather = Snow/Night) activates VFX for free.
switch (rule.Weather) {
    case WeatherType.Snow:  WeatherEffect.ActivateSnow(rule.Duration);  break;
    case WeatherType.Night: WeatherEffect.ActivateNight(rule.Duration); break;
    case WeatherType.Sunset:WeatherEffect.ActivateSunset(rule.Duration);break;
}
```

### CANONICAL_RULE_VALUES
```csharp
// SOURCE: Assets/Scripts/Data/DefaultEventRules.cs:19-25
// Reuse these exact operator/weather/delta values in EventActionBuilder:
// Name length : teamName, LengthGreaterThan, delta −(decel), Weather None
// Color       : colorIndex, Equals, "<index>", Weather None
// Function/Male: functions, Contains, "<tag>", Weather None
// Snow        : "", All, SpeedDelta −8f, Duration 12f, Weather Snow, AllowRepeat true
// Night       : "", All, SpeedDelta −5f, Duration 15f, Weather Night, AllowRepeat true
```

### RUNTIME_PANEL_CONSTRUCTION
```csharp
// SOURCE: Assets/Scripts/Editor/TrackSetupEditor.cs:837-874 (editor) + EventPanel.cs:55-83 (runtime)
// EventPanel builds its rows at runtime (Instantiate prefab, GetComponentInChildren<Button/Text>,
// AddListener with an int captured for the closure). EventMenuController follows the same runtime
// build approach so the panel needs only ONE serialized ref (EventManager) — sidestepping the
// known "scene wiring lags merged scripts" serialization gap for nested UI.
int index = i; button.onClick.AddListener(() => TriggerEvent(index));
```

### RULE_KEY_ASSIGNMENT (context — being retired for live triggers)
```csharp
// SOURCE: Assets/Scripts/Data/SurveyConfigManager.cs:93-108 + EventRuleKeys.cs:22-31
// ApplyRulesToSchedule maps template SavedEventRule[] → EventRule[] with Digit1..9.
// After this change the professor's live control no longer depends on these key bindings;
// the schedule remains for data/back-compat but is not key-polled.
```

---

## Files to Change

### Part A — Web-app feature conditions
| File | Action | Justification |
|---|---|---|
| `web-app/src/routes/export.js` | UPDATE | Add `gt`/`lt` to `average_threshold`; add `difference_threshold` rule type |
| `web-app/src/seed-templates.js` | UPDATE | Flip five features to `gt`, replace male rule with `difference_threshold`, add `member_count` mapping |
| `web-app/src/db.js` | UPDATE | Idempotent migration: update existing `ENGG*1100 Survey` template row's `mappings_json` + `post_processing_json` |
| `web-app/__tests__/postProcessing.test.js` | CREATE | Unit-test strict-gt, password flip, male difference, gt-branch |
| `Assets/Scripts/Data/SurveyTemplates.cs` | UPDATE | Mirror `member_count` mapping (parity; non-functional in Unity) |
| `Assets/Tests/EditMode/SurveyTemplatesTests.cs` | UPDATE | ENGG mapping count 7 → 8 |

### Part B — Unity parameterized event menu
| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Events/EventManager.cs` | UPDATE | Add `TriggerRule(EventRule)`; refactor `TriggerEvent`; delete digit-key polling in `Update()` |
| `Assets/Scripts/Events/EventActionBuilder.cs` | CREATE | Pure static factory for the 8 action rules (fixed +20/−15, 10 s, colors, functions, weather) |
| `Assets/Scripts/UI/EventMenuController.cs` | CREATE | Runtime menu UI + digit-key input (1–6 menus, 9/0 weather) → `EventManager.TriggerRule` |
| `Assets/Scripts/UI/EventPanel.cs` | UPDATE/RETIRE | Row-based trigger panel replaced by the menu (keep file compiling or remove refs) |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | Reference & toggle `EventMenuController` in place of `EventPanel` |
| `Assets/Scripts/Editor/TrackSetupEditor.cs` | UPDATE | Build the menu panel + attach `EventMenuController` |
| `Assets/Scripts/Editor/SceneWiring.cs` | UPDATE | Wire `EventMenuController.EventManager` reference |
| `Assets/Tests/EditMode/EventActionBuilderTests.cs` | CREATE | Assert each builder's operator/attribute/value/delta/duration/weather |
| `Assets/Tests/EditMode/EventManagerTests.cs` | UPDATE | Add `TriggerRule` coverage |

## NOT Building
- **A generic in-game rule editor.** The menu is hard-wired to the ENGG*1100 semantics (5 colors, 5 function tags, male, name-length). Arbitrary attribute rules are not authorable at runtime.
- **Per-template digit-key triggering for the other templates.** Removing `EventManager.Update()` polling means Accessibility/Diversity/V1 custom rules are no longer key-fired live; those templates remain usable for data/export, but the live control surface is the fixed ENGG menu. (Flagged in Risks.)
- **Sunset as a manual trigger.** The automatic day→sunset cycle in `WeatherEffect.StartCycle()` is untouched; only the manual key trigger is dropped.
- **Changing the game-data CSV/JSON shape.** `teamName,colorIndex,functions` is unchanged; `member_count` is not added to the game export, only used server-side for the male rule.
- **Unity-side average computation.** Averages/tags are still computed only in the web-app; Unity consumes the precomputed `functions` string.
- **Network replication of the menu UI itself.** Only the resulting `OnEventTriggered` (already broadcast) reaches students; the menu is professor-local.

---

## Step-by-Step Tasks

### Task A1: Extend `applyPostProcessing` with `gt`/`lt` and `difference_threshold`
- **ACTION**: In `web-app/src/routes/export.js`, extend the `average_threshold` branch and add a new rule type.
- **IMPLEMENT**:
  - In the `average_threshold` block (≈ lines 118–121) add:
    ```js
    else if (rule.direction === 'gt') passes = value > avg;
    else if (rule.direction === 'lt') passes = value < avg;
    ```
  - Add a new branch after `fixed_threshold`:
    ```js
    else if (rule.type === 'difference_threshold') {
      const a = car.attributes.find(x => x.key === rule.sourceMinuend);
      const b = car.attributes.find(x => x.key === rule.sourceSubtrahend);
      const value = (a ? parseFloat(a.value) : 0) - (b ? parseFloat(b.value) : 0);
      const threshold = parseFloat(rule.threshold) || 0;
      if (rule.direction === 'lt') passes = value < threshold;
      else if (rule.direction === 'lte') passes = value <= threshold;
      else if (rule.direction === 'gt') passes = value > threshold;
      else if (rule.direction === 'gte') passes = value >= threshold;
    }
    ```
  - The averages loop (lines 98–107) already `continue`s on non-`average_threshold` types, so `difference_threshold` is correctly excluded from average computation.
- **MIRROR**: POST_PROCESSING_ALGORITHM
- **IMPORTS**: none
- **GOTCHA**: `difference_threshold` reads two mapped attributes; if either is unmapped, `find` returns undefined → treated as 0. `member_count` MUST be mapped (Task A2) or every team reads `0 − male_count`, tagging everyone `male` when `male_count ≥ 0` and `< 2`… ensure mapping exists.
- **VALIDATE**: `npm test` (Task A4) passes new cases.

### Task A2: Update the seed template (post-processing + `member_count` mapping)
- **ACTION**: In `web-app/src/seed-templates.js` ENGG template, add the `member_count` mapping and rewrite `postProcessing`.
- **IMPLEMENT**:
  - Add to `mappings` (near the other numeric mappings):
    ```js
    { QuestionId: 'member_count', AttributeName: 'member_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
    ```
  - Replace `postProcessing` (lines 35–41) with:
    ```js
    postProcessing: [
      { type: 'average_threshold', sourceAttribute: 'facial_count',   direction: 'gt', tagName: 'facerecog', targetAttribute: 'functions' },
      { type: 'average_threshold', sourceAttribute: 'glasses_count',  direction: 'gt', tagName: 'glasses',   targetAttribute: 'functions' },
      { type: 'average_threshold', sourceAttribute: 'language_count', direction: 'gt', tagName: 'language',  targetAttribute: 'functions' },
      { type: 'average_threshold', sourceAttribute: 'pwd_count',      direction: 'gt', tagName: 'password',  targetAttribute: 'functions' },
      { type: 'average_threshold', sourceAttribute: 'distance_km',    direction: 'gt', tagName: 'distance',  targetAttribute: 'functions' },
      { type: 'difference_threshold', sourceMinuend: 'member_count', sourceSubtrahend: 'male_count', threshold: 2, direction: 'lt', tagName: 'male', targetAttribute: 'functions' },
    ],
    ```
- **MIRROR**: SEED_POSTPROCESSING_SHAPE
- **IMPORTS**: none
- **GOTCHA**: This grows ENGG mappings from 7 → 8 (matters for the Unity parity test, Task A5). Web-app has no mapping-count assertion.
- **VALIDATE**: `curl .../api/templates` on a fresh DB shows the ENGG template with 8 mappings and 6 postProcessing rules, male rule of type `difference_threshold`.

### Task A3: Migrate the existing ENGG template row (existing DBs)
- **ACTION**: In `web-app/src/db.js`, after seeding guard, add an idempotent UPDATE so already-seeded deployments pick up the new mapping + post-processing.
- **IMPLEMENT**:
  ```js
  // One-shot content refresh for the ENGG template (seeding only runs on empty DB).
  try {
    const engg = TEMPLATES.find(t => t.name === 'ENGG*1100 Survey');
    if (engg) {
      db.prepare(`UPDATE templates SET mappings_json = ?, post_processing_json = ?
                  WHERE name = 'ENGG*1100 Survey'`)
        .run(JSON.stringify(engg.mappings), JSON.stringify(engg.postProcessing || []));
    }
  } catch { /* templates table not migrated yet — first-run seed handles it */ }
  ```
- **MIRROR**: DB migration try/catch pattern already used for `ALTER TABLE` in `db.js`.
- **IMPORTS**: reuse the `TEMPLATES`/seed export already imported by `db.js`.
- **GOTCHA**: This does NOT touch **surveys** already created from the template (their `post_processing_json` is a copy). Document that professors must create a new survey from the refreshed template to get the new behavior, OR extend the UPDATE to surveys linked to this template if in-flight surveys must change (out of scope unless requested).
- **VALIDATE**: On a pre-existing dev DB, restart web-app, `curl .../api/templates` shows the male rule as `difference_threshold` and five `gt` rules.

### Task A4: Web-app unit tests for the new post-processing
- **ACTION**: Create `web-app/__tests__/postProcessing.test.js` (vitest).
- **IMPLEMENT**: Export/import `applyPostProcessing` (extract it if not already exported, or test through `buildVehicleGroupCsv` with an in-memory DB like `export-bundle.test.js`). Cases:
  - Strict gt: counts `[3,4,5]` (avg 4) → only the `5` car gets the tag (the `4` car does **not**, proving `>` not `≥`).
  - Password flip: `pwd_count [2,4,6]` (avg 4) → only `6` gets `password` (old `≤` would have tagged `2` and `4`).
  - Male difference: `member_count/male_count` = `(5,4)→non_male 1 <2 → male`, `(5,3)→2 not <2 → no male`, `(3,3)→0 <2 → male`.
  - Missing `member_count` mapping guard: with mapping present, `(4, male 3)` → non_male 1 → male.
- **MIRROR**: `web-app/__tests__/export-bundle.test.js` in-memory DB setup.
- **IMPORTS**: `vitest`, the module under test.
- **GOTCHA**: If `applyPostProcessing` is module-private, add a named export (it is currently an internal function in `export.js`) — prefer exporting it over duplicating logic in the test.
- **VALIDATE**: `cd web-app && npm test` green.

### Task A5: Mirror `member_count` mapping in Unity + fix the count test
- **ACTION**: Add the `member_count` mapping to `SurveyTemplates.ENGG1100Survey()` and update the assertion.
- **IMPLEMENT**:
  - In `Assets/Scripts/Data/SurveyTemplates.cs` add to the ENGG `Mappings` array:
    ```csharp
    new AttributeMapping { QuestionId = "member_count", AttributeName = "member_count", DefaultValue = "0", TransformType = "numeric", LookupEntries = Array.Empty<AttributeEntry>() },
    ```
  - In `Assets/Tests/EditMode/SurveyTemplatesTests.cs:63` change `Assert.AreEqual(7, config.Mappings.Length);` → `8`.
- **MIRROR**: existing numeric mappings in the same method.
- **IMPORTS**: none.
- **GOTCHA**: Unity does not post-process, so `member_count` is a passive attribute here; keep it only for web-app/Unity mapping parity. Rules count stays 8 (unchanged) — do not touch Unity template rules.
- **VALIDATE**: EditMode `SurveyTemplatesTests` pass.

### Task B1: Add `EventManager.TriggerRule` and remove digit-key polling
- **ACTION**: Refactor `Assets/Scripts/Events/EventManager.cs`.
- **IMPLEMENT**:
  - Extract the car-loop + `OnEventTriggered` invoke into a private `ApplyRule(EventRule rule)` returning `affectedCount` and firing `OnEventTriggered`.
  - Add public `TriggerRule(EventRule rule)`:
    ```csharp
    /// <summary>Apply an ad-hoc rule (built at trigger time) to all registered cars and fire
    /// OnEventTriggered. Used by the parameterized event menu. Ignores HasBeenTriggered (no
    /// schedule slot). Respects isActive.</summary>
    public int TriggerRule(EventRule rule)
    {
        if (!isActive) return 0;
        return ApplyRule(rule);
    }
    ```
  - Rewrite `TriggerEvent(index)` to keep its HasBeenTriggered/AllowRepeat guard, then call `ApplyRule(Schedule.Events[index])` and set the flag. (Preserves every `EventManagerTests` behavior — those tests call `TriggerEvent` WITHOUT `Activate()`, so keep that path un-gated by isActive.)
  - **Delete** the `for` loop in `Update()` that polls `Schedule.Events[i].TriggerKey` (lines 51–57). Leave `Update()` empty or remove it.
- **MIRROR**: EVENT_TRIGGER_CORE
- **IMPORTS**: none new.
- **GOTCHA**: `TriggerEvent` currently does NOT check `isActive`; only the deleted `Update` loop did. Keep `TriggerEvent` un-gated so `EventManagerTests` (which trigger without `Activate`) still pass. Only `TriggerRule` applies the isActive gate.
- **VALIDATE**: All existing `EventManagerTests` pass unchanged; new `TriggerRule` tests (Task B5) pass.

### Task B2: Create `EventActionBuilder` (pure static rule factory)
- **ACTION**: New file `Assets/Scripts/Events/EventActionBuilder.cs`.
- **IMPLEMENT**: Centralize every fixed constant so nothing is hardcoded in UI:
  ```csharp
  public static class EventActionBuilder
  {
      public const float BoostDelta = 20f;
      public const float PenaltyDelta = -15f;
      public const float EffectDuration = 10f;

      // Display label → car function tag (as computed by the web-app).
      public static readonly (string Label, string Tag)[] Functions =
      {
          ("Facial", "facerecog"), ("Glasses", "glasses"), ("Language", "language"),
          ("Password", "password"), ("Distance", "distance")
      };
      // Display label → colorIndex (Green=0, Black=1, Red=2, Blue=3, White=4).
      public static readonly (string Label, int Index)[] Colors =
      {
          ("Blue", 3), ("Red", 2), ("Black", 1), ("White", 4), ("Green", 0)
      };

      public static EventRule NameLengthPenalty(int threshold) => new EventRule {
          DisplayName = $"Name Length > {threshold}", AttributeName = "teamName",
          Operator = ComparisonOperator.LengthGreaterThan, CompareValue = threshold.ToString(),
          SpeedDelta = PenaltyDelta, Duration = EffectDuration, Weather = WeatherType.None, AllowRepeat = true };

      public static EventRule Male(bool accelerate) => FunctionTag("male", accelerate, "Male");
      public static EventRule Function(string tag, bool boost) => FunctionTag(tag, boost, $"Function {(boost ? "Boost" : "Penalty")} ({tag})");
      private static EventRule FunctionTag(string tag, bool boost, string name) => new EventRule {
          DisplayName = name, AttributeName = "functions", Operator = ComparisonOperator.Contains,
          CompareValue = tag, SpeedDelta = boost ? BoostDelta : PenaltyDelta,
          Duration = EffectDuration, Weather = WeatherType.None, AllowRepeat = true };

      public static EventRule Color(int colorIndex, bool boost) => new EventRule {
          DisplayName = $"Color {(boost ? "Boost" : "Penalty")} ({colorIndex})", AttributeName = "colorIndex",
          Operator = ComparisonOperator.Equals, CompareValue = colorIndex.ToString(),
          SpeedDelta = boost ? BoostDelta : PenaltyDelta, Duration = EffectDuration,
          Weather = WeatherType.None, AllowRepeat = true };

      public static EventRule Snow()  => new EventRule { DisplayName = "Snow Weather",  AttributeName = "", Operator = ComparisonOperator.All, CompareValue = "", SpeedDelta = -8f, Duration = 12f, Weather = WeatherType.Snow,  AllowRepeat = true };
      public static EventRule Night() => new EventRule { DisplayName = "Night Weather", AttributeName = "", Operator = ComparisonOperator.All, CompareValue = "", SpeedDelta = -5f, Duration = 15f, Weather = WeatherType.Night, AllowRepeat = true };
  }
  ```
- **MIRROR**: CANONICAL_RULE_VALUES; operators from `ComparisonOperator.cs`; the `functions` `Contains` match confirmed by `RuleEngineTests.IsAffected_Contains_MatchesSlashSeparatedList`.
- **IMPORTS**: none (all types in the default assembly).
- **GOTCHA**: `colorIndex`/`functions` on the car are strings (`CarIdentity.GetAttribute`). `Equals` on `colorIndex` is a case-insensitive string compare in `RuleEngine` — compare against the integer index as a string (`"3"`), matching how the web-app writes `colorIndex`.
- **VALIDATE**: `EventActionBuilderTests` (Task B5) assert every field.

### Task B3: Create `EventMenuController` (runtime UI + input)
- **ACTION**: New file `Assets/Scripts/UI/EventMenuController.cs` (MonoBehaviour).
- **IMPLEMENT**:
  - Serialized: `public EventManager EventManager;` (single ref — auto-resolve in `Awake` like `EventPanel.cs:34-35`). Everything else built in code in `Start()`.
  - Build a primary vertical list of 6 buttons: `[1] Name Length`, `[2] Male`, `[3] Color Boost`, `[4] Color Penalty`, `[5] Function Boost`, `[6] Function Penalty`.
  - Build ONE reusable secondary overlay (`CanvasGroup` for fade) that is repopulated per action:
    - **Name Length** → a UGUI `InputField` (contentType Integer) + `Confirm` button → `EventManager.TriggerRule(EventActionBuilder.NameLengthPenalty(int.Parse(field.text)))`.
    - **Male** → two buttons `Accelerate` / `Decelerate` → `Male(true/false)`.
    - **Color Boost / Penalty** → 5 buttons from `EventActionBuilder.Colors` → `Color(index, boost)`.
    - **Function Boost / Penalty** → 5 buttons from `EventActionBuilder.Functions` → `Function(tag, boost)`.
    - A `Cancel`/back button that fades the overlay out.
  - **Fade-in**: coroutine lerping `CanvasGroup.alpha` 0→1 over ~0.2 s using `unscaledDeltaTime` (race can be paused; mirrors `RaceControlPanel.FadeStatus`).
  - **Input** (in `Update`, guarded by `EventManager != null && EventManager.IsActive`):
    - `Key.Digit1..Digit6` `wasPressedThisFrame` → open the matching secondary menu (same handler the button click calls).
    - `Key.Digit9` → `TriggerRule(EventActionBuilder.Snow())`; `Key.Digit0` → `TriggerRule(EventActionBuilder.Night())`.
    - Read via `Keyboard.current` (see original `EventManager.Update` / `CameraManager.Update`).
- **MIRROR**: RUNTIME_PANEL_CONSTRUCTION; `RaceControlPanel` button-wiring & status-fade; Input System usage in `CameraManager.cs:66-100`.
- **IMPORTS**: `UnityEngine`, `UnityEngine.UI`, `UnityEngine.InputSystem`, `System.Collections`.
- **GOTCHA**:
  - Digit-key handling must NOT also be done by `EventManager` (polling deleted in B1) — otherwise double fire.
  - Weather keys 9/0 fire directly (no submenu) per the approved design.
  - Guard against opening a submenu while typing in the Name-Length `InputField` — check `field.isFocused` (or "overlay already active") before treating 1–6 as menu-open.
  - Build UI under the same Canvas as the existing panels; anchor top-right like `WireOrCreateEventPanel` (`TrackSetupEditor.cs:837-839`).
- **VALIDATE**: Play mode — buttons + keys open menus; picking an option applies +20/−15 for 10 s; 9/0 trigger snow/night VFX. Use the UnitySkills API play-mode verification (see Notes).

### Task B4: Wire the menu into RaceUI + editor scene setup
- **ACTION**: Route role/visibility + scene construction to the new controller.
- **IMPLEMENT**:
  - `Assets/Scripts/UI/RaceUI.cs`: add `public EventMenuController EventMenu;`; in `ApplyRole()` toggle it with the professor gate exactly like `Events` (line 142); include it in `ResolveMissingReferences` auto-wire. Simplest layout: put `EventMenuController` on the existing EventPanel GameObject and toggle that object. If `EventPanel` is fully retired, remove its `SetActive` line and the `Events` serialized ref.
  - `Assets/Scripts/Editor/TrackSetupEditor.cs`: in `WireOrCreateEventPanel` (or a renamed `WireOrCreateEventMenu`), create the panel shell and `AddComponent<EventMenuController>()`, set `EventManager`. Drop the `EventRow.prefab` load (menu builds its own children).
  - `Assets/Scripts/Editor/SceneWiring.cs`: add a `Wire(ref eventMenu.EventManager, eventManager, ...)` line in the wiring pass (mirror line 65).
- **MIRROR**: `RaceUI.ApplyRole` (142–145), `TrackSetupEditor.WireOrCreateEventPanel`, `SceneWiring.Wire`.
- **IMPORTS**: none new.
- **GOTCHA**: Per memory *scene-wiring-lags-merged-scripts*, the `.unity`/prefab serialized wiring won't exist until `TrackSetupEditor`/`SceneWiring` is re-run — after merge, run the scene-wiring editor action and verify the menu appears for the professor. The single `EventManager` ref + runtime-built children keep this exposure minimal.
- **VALIDATE**: Open the scene, run scene wiring, enter play mode as professor → menu visible while racing, hidden in Setup/Finished and for students (`RaceUI.ShouldShowEventPanel` semantics).

### Task B5: Unity EditMode tests
- **ACTION**: Create `EventActionBuilderTests.cs`; extend `EventManagerTests.cs`.
- **IMPLEMENT**:
  - `EventActionBuilderTests` (no scene needed — pure struct assertions):
    - `NameLengthPenalty(10)` → `teamName`, `LengthGreaterThan`, `"10"`, `-15`, `10`, `None`.
    - `Color(3, boost:true)` → `colorIndex`, `Equals`, `"3"`, `+20`, `10`.
    - `Function("facerecog", boost:false)` → `functions`, `Contains`, `"facerecog"`, `-15`.
    - `Male(accelerate:true)` → `functions`, `Contains`, `"male"`, `+20`.
    - `Snow()` → `All`, `Weather Snow`, `-8`, `12`; `Night()` → `All`, `Weather Night`, `-5`, `15`.
    - Constants: `BoostDelta==20 && PenaltyDelta==-15 && EffectDuration==10`.
  - `EventManagerTests` additions (reuse `CreateCar`/`RegisterCar` harness):
    - `TriggerRule_Active_AppliesToMatchingCars_FiresEvent`: register a car with `functions="male"`, `Activate()`, `TriggerRule(EventActionBuilder.Male(true))` → `OnEventTriggered` fires, affected 1.
    - `TriggerRule_Inactive_ReturnsZero`: without `Activate()` → returns 0, no event.
    - `TriggerRule_NonMatching_AffectsNone`: car without the tag → affected 0.
- **MIRROR**: existing `EventManagerTests` SetUp/CreateCar; `RuleEngineTests` construction of rules; test-standards.md arrange/act/assert + `test_[system]_[scenario]_[expected]` naming.
- **IMPORTS**: `NUnit.Framework`, `UnityEngine`, `UnityEngine.InputSystem`.
- **GOTCHA**: `TriggerRule` checks `isActive`; call `Activate()` first in the positive test (mirrors real flow where `RaceManager.LoadAndStartRace` calls `Activate`).
- **VALIDATE**: EditMode suite green.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected | Edge? |
|---|---|---|---|
| pp gt strict | facial `[3,4,5]` avg 4 | only `5`→`facerecog` (`4` excluded) | boundary |
| pp password flip | pwd `[2,4,6]` avg 4 | only `6`→`password` | reversed logic |
| pp male difference | (member,male) `(5,4)/(5,3)/(3,3)` | male: yes/no/yes | new type |
| pp gt single response | facial `[5]` avg 5 | none (`5 > 5` false) | single team |
| builder NameLength | `10` | teamName/LengthGreaterThan/"10"/−15/10s | — |
| builder Color boost | `(3,true)` | colorIndex/Equals/"3"/+20/10s | — |
| builder Function pen | `("facerecog",false)` | functions/Contains/"facerecog"/−15 | — |
| builder Snow/Night | — | All/Snow −8/12, All/Night −5/15 | weather |
| TriggerRule active | car `functions="male"` | affected 1, event fired | — |
| TriggerRule inactive | not Activated | returns 0, no event | guard |
| ENGG mapping count | template | 8 mappings | test update |

### Edge Cases Checklist
- [ ] Single team (average == its own value → strict `>` tags nobody)
- [ ] `member_count < male_count` (bad data) → non_male negative `< 2` → male tag (document/accept)
- [ ] Empty responses → export returns empty, no crash
- [ ] Name-length input non-numeric / empty → guard `int.TryParse`, ignore trigger
- [ ] Name-length input `0` → all named cars slowed (allowed)
- [ ] Rapid repeated menu triggers → `ApplySpeedModifier` stacks (existing behavior via `activeModifierCount`)
- [ ] InputField focused while pressing 1–6 → do not open menus / double-handle
- [ ] Weather 9/0 while a submenu is open → still fires (direct)
- [ ] Student client → menu hidden, still receives `EventTriggered`/weather broadcasts

---

## Validation Commands

### Web-app tests
```bash
cd web-app && npm test
```
EXPECT: all vitest suites pass incl. `postProcessing.test.js`

### Web-app template sanity (fresh DB)
```bash
cd web-app && rm -f data/*.db && npm run dev &   # then:
curl -s localhost:3001/api/templates | python3 -c "import sys,json;t=[x for x in json.load(sys.stdin)['data'] if x['name']=='ENGG*1100 Survey'][0];c=t['config'];print('mappings',len(c['mappings']));print('pp',[(p['type'],p.get('direction'),p.get('tagName')) for p in c['postProcessing']])"
```
EXPECT: `mappings 8`; five `('average_threshold','gt',...)` + one `('difference_threshold','lt','male')`

### Unity EditMode tests (CI runner)
```bash
# game-ci/unity-test-runner@v4 (EditMode) — locally via Unity CLI:
Unity -batchmode -runTests -testPlatform EditMode -projectPath . -logFile -
```
EXPECT: `EventActionBuilderTests`, `EventManagerTests`, `SurveyTemplatesTests`, `RuleEngineTests` all pass; zero compile errors

### Manual (play mode, professor)
- [ ] Send-to-game a survey with ≥3 teams; confirm `functions` reflects strict-gt + male-difference
- [ ] Buttons 1–6 and keys 1–6 open the correct fade-in submenu
- [ ] Color Boost(Blue) → only blue cars +20 for 10 s; Function Penalty(Facial) → only `facerecog` cars −15
- [ ] Name Length input 5 → cars with name length > 5 slow −15
- [ ] Key 9 → snow VFX + all cars −8/12 s; key 0 → night VFX + −5/15 s
- [ ] Old digit keys no longer fire a fixed rule directly

---

## Acceptance Criteria
- [ ] Five count-features tag on strict `> average`; Password no longer uses `≤`
- [ ] `male` tag = `(member_count − male_count) < 2`; `member_count` mapped (web-app + Unity)
- [ ] `average_threshold` supports `gt`/`lt`; `difference_threshold` type implemented + tested
- [ ] Existing seeded DBs updated via idempotent migration
- [ ] `EventManager.TriggerRule(EventRule)` applies ad-hoc rules and fires `OnEventTriggered`
- [ ] `EventManager.Update()` no longer polls digit keys for schedule rules
- [ ] Six-button menu with fade-in secondary menus (name-length input, male accel/decel, color picker ×2, function picker ×2); openable by click and keys 1–6
- [ ] Accelerate +20, decelerate −15, duration 10 s throughout
- [ ] Snow=key 9, Night=key 0; weather VFX + student broadcast still fire
- [ ] All existing tests pass; new tests added and passing

## Completion Checklist
- [ ] Follows discovered patterns (post-processing shape, event-trigger core, runtime panel build)
- [ ] No hardcoded magnitudes in UI — all constants in `EventActionBuilder`
- [ ] Weather + network keep working via `OnEventTriggered` (unchanged sinks)
- [ ] Web-app/Unity mapping parity maintained (`member_count`)
- [ ] Tests follow test-standards.md naming + arrange/act/assert
- [ ] Scene wiring regenerated via `TrackSetupEditor`/`SceneWiring`; menu verified in play mode
- [ ] Self-contained — no further codebase search needed to implement

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Removing `Update()` polling regresses other templates' key triggers | HIGH | MEDIUM | Documented in NOT Building; the ENGG menu is the live control surface; other templates still export/data-import |
| Existing surveys keep old post-processing (copy) | MEDIUM | MEDIUM | Migration refreshes the template row; document "create new survey"; extend to surveys only if requested |
| Strict `>` tags nobody with a single team / all-equal counts | MEDIUM | LOW | Intended per approved decision; covered by edge test |
| Scene serialization lag hides the new menu after merge | MEDIUM | MEDIUM | Runtime-built children + single serialized ref; re-run scene wiring; play-mode verify (memory: scene-wiring-lags-merged-scripts) |
| `difference_threshold` on unmapped `member_count` mis-tags everyone | LOW | HIGH | Task A2 adds the mapping; Task A4 test guards it |
| InputField focus vs digit-key menu open double-handling | MEDIUM | LOW | Guard on `isFocused`/overlay-active before treating 1–6 as menu-open |

## Notes
- **Why the menu builds UI at runtime**: the project has hit serialized-reference loss on nested UI before (memory *scene-wiring-lags-merged-scripts*, *ugui-worldspace-billboard-mirror*). Keeping `EventMenuController` to a single serialized `EventManager` ref and building buttons/inputs in code makes the merge robust — the panel can't ship "visible but empty."
- **Play-mode verification** (memory *unity-playmode-verification*): use the UnitySkills API — set `runInBackground`, pause, `camera_screenshot` the menu + affected cars, `event_invoke` where useful — to confirm VFX/speed changes without a human in the loop.
- **Color/index contract**: Green=0, Black=1, Red=2, Blue=3, White=4 (from the ENGG lookup in `SurveyTemplates.cs:308`). The menu compares `colorIndex` as a string against these indices, matching how the web-app writes the attribute.
- **Function label→tag**: UI shows "Facial" but matches the `facerecog` tag; the mapping lives once in `EventActionBuilder.Functions`.
- **Sunset**: only the *manual* trigger is removed; `WeatherEffect`'s automatic day→sunset cycle is untouched.
- **`TriggerEvent(index)` retained**: kept for `EventManagerTests` and any programmatic/network path; both index and ad-hoc paths funnel through the shared `ApplyRule` helper.
