# Plan: Align web-app "ENGG*1100 Survey" template rules with the new Unity live-event model (reduce to Snow/Night)

## Summary
The new Unity build drives race events from a live, professor-operated menu (`EventPanel` + `EventActionBuilder`) that **builds every rule at trigger time and ignores the imported `eventRules`**. The web-app's `ENGG*1100 Survey` template still ships 7 stale pre-baked rules (hardcoded Blue/Red, Password/Facerecog, name-length 10, old deltas). This plan reduces that template's `rules` array to **only the two weather events (Snow, Night)** — the sole events that still have a fixed, pre-baked identity — and makes the change propagate to already-seeded databases. **`mappings` and `postProcessing` are already correct and are NOT touched.** Scope is **web-app only**; Unity template sources are intentionally left unchanged.

## User Story
As a professor configuring an ENGG*1100 race, I want the web-app template's event rules to reflect what the game actually does (weather events + live-selected boosts/penalties), so that the rules editor and race records don't show obsolete color/function/name rules that the game no longer runs.

## Problem → Solution
`ENGG*1100 Survey` template `rules` = 7 legacy pre-baked rules that Unity's live `EventPanel` ignores → template `rules` = `[Snow Weather, Night Weather]` only, and existing DBs converge via an extended `refreshTemplateContent`.

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A (free-form request via `/ecc:prp-plan`)
- **PRD Phase**: N/A
- **Estimated Files**: 1 source file (`web-app/src/seed-templates.js`); 1 optional test hardening (`web-app/__tests__/db.test.js`)

---

## Key Investigation Findings (why this is the right change)

1. **Unity already migrated to the live-event model.** `Assets/Scripts/Events/EventManager.cs:47-50` — the `Update()` digit-key polling was removed. Live control is now `Assets/Scripts/UI/EventPanel.cs`, which builds rules on the fly via `EventActionBuilder` and calls `EventManager.TriggerRule`. Digit keys 1-6 open secondary menus; keys 9/0 fire Snow/Night directly (`EventPanel.cs:66-76`).
2. **Fixed magnitudes match the new spec.** `Assets/Scripts/Events/EventActionBuilder.cs:15-18` — `BoostDelta = +20`, `PenaltyDelta = -15`. Name Length penalty = `-15`; Color/Function/Male Boost = `+20`, Penalty = `-15`. Snow `-8/12s`, Night `-5/15s` (`EventActionBuilder.cs:24-29`).
3. **Imported `eventRules` are inert for live play.** Web-app `send-to-game` sends `eventRules` from `rules_json` (`web-app/src/routes/export.js:362,372`). Unity parses them (`JsonImporter`) and `RaceManager.LoadAndStartRaceWithRules` sets `EventManager.Schedule.Events` — but `EventPanel` never reads `Schedule.Events`. They survive only in `RaceManager.BuildSessionData` (`Assets/Scripts/Race/RaceManager.cs:306-312`, session-save record) and the dead `TriggerEvent(index)` path (tests only).
4. **The web-app does no computation with `rules`.** `grep` shows `rules_json` is only stored/listed/passed through (`surveys.js`, `templates.js`) and forwarded to Unity (`export.js`). Leaderboard/analysis never read it.
5. **`mappings` + `postProcessing` already match the new feature spec.** `web-app/src/seed-templates.js:26-45`: colorIndex lookup (Green=0/Black=1/Red=2/Blue=3/White=4) matches `EventActionBuilder.Colors`; `average_threshold … direction:'gt'` → facerecog/glasses/language/password/distance (strictly above cohort average); `difference_threshold member_count - male_count < 2` → male. No change required.

**Conclusion:** Only the `rules` array is out of date. Per decision, reduce it to Snow/Night; keep everything else.

---

## UX Design

### Before
```
Professor opens ENGG*1100 template rules editor (web-app):
  Name Length Penalty (-10)   Color Boost Blue (+15)   Color Penalty Red (-12)
  Function Boost Password (+10)   Function Penalty FaceRecog (-10)
  Snow (-8)   Night (-5)
  → 5 of these do NOTHING in the current game (EventPanel builds its own).
```

### After
```
Professor opens ENGG*1100 template rules editor (web-app):
  Snow Weather (-8)   Night Weather (-5)
  → matches what the game runs; boosts/penalties are chosen live in-game.
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| web-app template rules editor | 7 rules (5 misleading) | 2 weather rules | Cosmetic/accuracy; no gameplay change |
| `send-to-game` ack `rulesCount` | 7 | 2 | `export.js:384` returns `eventRules.length` |
| Unity live events (EventPanel) | unaffected | unaffected | Was already ignoring imported rules |
| Existing surveys created from template | unchanged | unchanged | Surveys hold their own copy; not refreshed (by design) |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/seed-templates.js` | 6-56 | The `templates` array + ENGG rules to edit |
| P0 | `web-app/src/seed-templates.js` | 79-104 | `refreshTemplateContent` — extend to include `rules_json` |
| P1 | `web-app/src/db.js` | 54-70 | Migration order: delete-legacy → `refreshTemplateContent` → `seedTemplates` |
| P1 | `web-app/__tests__/db.test.js` | 101-131 | Existing template/rules assertions (must still pass) |
| P2 | `Assets/Scripts/Events/EventActionBuilder.cs` | 14-29,113-143 | Source of the fixed Snow/Night constants (parity reference) |
| P2 | `web-app/src/routes/export.js` | 353-388 | `send-to-game` — the only consumer of `rules_json` |

## External Documentation
_No external research needed — feature uses established internal patterns._

---

## Patterns to Mirror

### RULE_OBJECT_SHAPE (weather rules to keep, verbatim)
```js
// SOURCE: web-app/src/seed-templates.js:52-53
{ DisplayName: 'Snow Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -8, Duration: 12, Weather: 1, AllowRepeat: true },
{ DisplayName: 'Night Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -5, Duration: 15, Weather: 2, AllowRepeat: true },
```
`Operator: 8` = `ComparisonOperator.All`; `Weather: 1` = Snow, `2` = Night. These already match `EventActionBuilder.Snow()/Night()` — keep byte-for-byte.

### IDEMPOTENT_REFRESH_PATTERN (extend this to cover rules)
```js
// SOURCE: web-app/src/seed-templates.js:90-104
export function refreshTemplateContent(db) {
  const update = db.prepare(
    'UPDATE templates SET mappings_json = ?, post_processing_json = ? WHERE name = ?'
  );
  const updateMany = db.transaction((items) => {
    for (const t of items) {
      update.run(
        JSON.stringify(t.mappings),
        JSON.stringify(t.postProcessing || []),
        t.name
      );
    }
  });
  updateMany(templates);
}
```

### MIGRATION_CALL_SITE (already wired — no change needed here)
```js
// SOURCE: web-app/src/db.js:63-70
// refreshTemplateContent(db) runs on every startup, before seedTemplates().
try { refreshTemplateContent(db); } catch { /* templates table not present yet */ }
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/seed-templates.js` | UPDATE | Reduce ENGG `rules` to `[Snow, Night]`; extend `refreshTemplateContent` to also write `rules_json`; update both docstrings |
| `web-app/__tests__/db.test.js` | UPDATE (optional, advisory) | Harden the rules assertion to lock in the 2-rule Snow/Night set |

## NOT Building
- **No Unity changes.** `Assets/Scripts/Data/SurveyTemplates.cs` (`ENGG1100Survey`) and `Assets/Scripts/Data/DefaultEventRules.cs` (`BaseSaved`) keep their current 7/8-rule sets. Decision: web-app only. Accept that the two template sources are no longer byte-identical on `rules`.
- **No `mappings` change.** Already correct.
- **No `postProcessing` change.** Already correct.
- **No change to professors' existing surveys.** `refreshTemplateContent` touches only the `templates` table; surveys hold their own copy (documented behavior — preserved).
- **No new event types, no schema migration, no route changes.**

---

## Step-by-Step Tasks

### Task 1: Reduce the ENGG*1100 template `rules` to Snow + Night
- **ACTION**: In `web-app/src/seed-templates.js`, replace the 7-element `rules:` array (lines 46-54) with only the two weather rules.
- **IMPLEMENT**:
  ```js
  rules: [
    { DisplayName: 'Snow Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -8, Duration: 12, Weather: 1, AllowRepeat: true },
    { DisplayName: 'Night Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -5, Duration: 15, Weather: 2, AllowRepeat: true },
  ]
  ```
- **MIRROR**: RULE_OBJECT_SHAPE (keep the two existing weather entries verbatim; delete the other five).
- **IMPORTS**: none.
- **GOTCHA**: Keep the trailing comma style consistent with the file. Do NOT alter `mappings`, `postProcessing`, or `questions`. `Operator: 8` and `Weather: 1/2` must stay — they are enum ints, not display values.
- **VALIDATE**: `node --input-type=module -e "import('./web-app/src/seed-templates.js')"` parses; the ENGG entry's `rules.length === 2`.

### Task 2: Add a short "why only weather" comment above the reduced array
- **ACTION**: Add a one-line comment explaining the reduction, mirroring the explanatory-comment style already used for `postProcessing` (seed-templates.js:37,43).
- **IMPLEMENT**:
  ```js
  // Unity's live EventPanel builds Color/Function/Name/Male events on the fly (fixed +20/-15) and
  // ignores imported rules — only the fixed weather events keep a pre-baked identity here.
  rules: [ /* Snow, Night */ ]
  ```
- **MIRROR**: the inline-comment convention in the same file.
- **GOTCHA**: keep it concise; do not restate the whole investigation.
- **VALIDATE**: file still parses; comment reads clearly.

### Task 3: Extend `refreshTemplateContent` to also refresh `rules_json`
- **ACTION**: Update the `UPDATE` statement and its `update.run(...)` call in `refreshTemplateContent` (seed-templates.js:91-100) to include `rules_json`, so existing DBs seeded with the old 7-rule config converge to the new 2-rule set.
- **IMPLEMENT**:
  ```js
  const update = db.prepare(
    'UPDATE templates SET mappings_json = ?, rules_json = ?, post_processing_json = ? WHERE name = ?'
  );
  // ...
  update.run(
    JSON.stringify(t.mappings),
    JSON.stringify(t.rules),
    JSON.stringify(t.postProcessing || []),
    t.name
  );
  ```
- **MIRROR**: IDEMPOTENT_REFRESH_PATTERN.
- **IMPORTS**: none.
- **GOTCHA**: The current docstring (seed-templates.js:84-85) says rules are deliberately left untouched "to avoid clobbering any professor edits made directly on templates." This is now safe to change: the built-in `templates` table has no professor-facing edit endpoint (`templates.js` is list-only; professors edit their own `surveys` rows, which are separate copies and remain untouched). **Update the docstring** to state that `rules_json` is now also refreshed and why. Column order in the SQL must match the `run()` argument order exactly.
- **VALIDATE**: A DB pre-seeded with the old 7-rule ENGG row, after `refreshTemplateContent`, returns `rules.length === 2` for `ENGG*1100 Survey`.

### Task 4 (advisory): Harden the db test
- **ACTION**: In `web-app/__tests__/db.test.js` (the `templates have valid JSON in rules_json` test, ~line 123-131), optionally assert the ENGG template now has exactly the Snow/Night set.
- **IMPLEMENT**:
  ```js
  const engg = db.prepare("SELECT rules_json FROM templates WHERE name = 'ENGG*1100 Survey'").get();
  const enggRules = JSON.parse(engg.rules_json).map(r => r.DisplayName);
  expect(enggRules).toEqual(['Snow Weather', 'Night Weather']);
  ```
- **MIRROR**: existing assertion style in db.test.js.
- **GOTCHA**: Keep the existing `rules.length).toBeGreaterThan(0)` loop passing (2 > 0). This task is advisory — skip if minimizing surface area.
- **VALIDATE**: `npm test` in `web-app/` passes.

---

## Testing Strategy

### Unit / Integration Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `db.test.js` seeds 1 template | fresh DB | `['ENGG*1100 Survey']`, still passes | No |
| `db.test.js` rules_json valid | fresh DB | array, `length > 0` (now 2) | No |
| (advisory) ENGG rules content | fresh DB | `['Snow Weather','Night Weather']` | No |
| Refresh convergence | DB with old 7-rule ENGG row | after startup, ENGG `rules.length === 2` | Yes (existing-DB migration) |
| `adversarial-ws.test.js` | survey_import flow | unaffected (no rulesCount assertion) | No |

### Edge Cases Checklist
- [ ] Existing DB seeded before this change → `refreshTemplateContent` reduces to 2 rules (Task 3).
- [ ] Fresh DB → seeded directly with 2 rules.
- [ ] `send-to-game` ack `rulesCount` becomes 2 — no test asserts a specific value (verified).
- [ ] A survey already created from the template keeps its own `rules_json` copy (unchanged by design).
- [ ] `mappings`/`postProcessing` byte-unchanged (regression guard).

---

## Validation Commands

### Static / Parse
```bash
cd web-app && node --input-type=module -e "import('./src/seed-templates.js').then(()=>console.log('ok'))"
```
EXPECT: prints `ok`, no syntax error.

### Unit Tests
```bash
cd web-app && npm test -- db.test.js
```
EXPECT: all db tests pass (template count 1, names `['ENGG*1100 Survey']`, rules_json valid).

### Full Test Suite
```bash
cd web-app && npm test
```
EXPECT: no regressions across all `__tests__/*`.

### Manual Validation (existing-DB migration)
- [ ] Copy a production-shaped DB with the old 7-rule ENGG template.
- [ ] Start the web-app (triggers `applyMigrations` → `refreshTemplateContent`).
- [ ] `SELECT rules_json FROM templates WHERE name='ENGG*1100 Survey'` → 2 rules (Snow, Night).
- [ ] Open the template in the professor UI → only Snow/Night shown.
- [ ] `send-to-game` to a live Unity room → race runs; live EventPanel (keys 1-6, 9/0) works exactly as before.

---

## Acceptance Criteria
- [ ] ENGG*1100 template `rules` = exactly `[Snow Weather, Night Weather]` in `seed-templates.js`.
- [ ] `mappings` and `postProcessing` unchanged (diff shows only `rules` + `refreshTemplateContent` + docstrings).
- [ ] `refreshTemplateContent` now also updates `rules_json`; docstring updated to match.
- [ ] Fresh-DB and existing-DB both end with the 2-rule ENGG template.
- [ ] `web-app` test suite passes.
- [ ] No Unity files modified.

## Completion Checklist
- [ ] Code follows the file's existing object/comment style.
- [ ] SQL column order matches `run()` argument order in `refreshTemplateContent`.
- [ ] No hardcoded values beyond the two weather rules (which mirror `EventActionBuilder`).
- [ ] Docstrings for `refreshTemplateContent` (and the template comment) reflect reality.
- [ ] Self-contained — no further codebase search needed to implement.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `refreshTemplateContent` clobbers an intentionally-edited built-in template row | Very Low | Low | No edit endpoint exists for the `templates` table; professors edit `surveys` copies (untouched). Documented in Task 3. |
| Some hidden consumer relies on the 5 removed rules | Very Low | Low | `grep` confirms `rules_json` is only stored/listed/forwarded-to-Unity; Unity's live path ignores it. |
| Two template sources (Unity vs web-app) drift on `rules` | Certain (accepted) | Low | Explicit decision: web-app only. Unity live events don't read the template rules; parity on `rules` is no longer meaningful. |
| Existing-DB refresh not triggered | Very Low | Medium | `refreshTemplateContent` already runs on every startup (`db.js:67`); Task 3 just widens what it updates. |

## Notes
- The genuinely load-bearing web-app config for the new model is `mappings` + `postProcessing` (they decide which car gets which feature tag), and both are already correct. The `rules` array is now essentially a cosmetic/record artifact on the web-app side because Unity's `EventPanel` authoritatively builds events live via `EventActionBuilder`.
- If cross-end parity ever becomes desirable again, a follow-up (out of scope here) would mirror the same reduction into `Assets/Scripts/Data/SurveyTemplates.cs` `ENGG1100Survey()` and reconcile `DefaultEventRules.BaseSaved()` + `SurveyTemplatesTests`/`DefaultEventRulesTests`.
