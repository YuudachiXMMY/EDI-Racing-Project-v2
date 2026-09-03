# Implementation Report: ENGG*1100 Feature Conditions + Parameterized Event Menu

## Summary
Implemented both parts of the plan. **Part A (web-app feature conditions)** — the five count-features now tag on strict `> average`, Password flipped from its old `≤ average`, and `male` now uses `(member_count − male_count) < 2`. **Part B (Unity event menu)** — direct digit-key rule triggering is replaced by a parameterized professor control menu (six buttons + digit keys 1-6 open fade-in secondary menus; Snow/Night on keys 9/0), backed by a new `EventManager.TriggerRule` + `EventActionBuilder`.

## Assessment vs Reality
| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Large | Large |
| Confidence | 8/10 | Part A verified; Part B implemented, CI-verification pending |
| Files Changed | ~16 | 12 code (5 web-app, 7 Unity incl. 3 test) + 2 docs |

## Tasks Completed
| # | Task | Status | Notes |
|---|---|---|---|
| A1 | `applyPostProcessing` gt/lt + difference_threshold | Complete | exported for tests |
| A2 | Seed template: 5× gt, male difference, member_count mapping | Complete | |
| A3 | Idempotent template-row migration | Complete | **Deviated** — added `refreshTemplateContent()` in seed-templates.js (plan referenced a non-existent `TEMPLATES` export) |
| A4 | web-app post-processing unit tests | Complete | 6 tests, all pass |
| A5 | Unity mirror member_count + fix mapping count test | Complete | 7→8 |
| B1 | `EventManager.TriggerRule` + remove digit polling | Complete | shared `ApplyRule`; `TriggerEvent` behavior unchanged |
| B2 | `EventActionBuilder` | Complete | all fixed constants centralized |
| B3 | Menu UI + digit-key input | Complete | **Deviated** — rewrote `EventPanel` (kept class name) instead of a new `EventMenuController` |
| B4 | Wire menu into RaceUI + editor | Complete (no-op) | **Deviated** — repurposing `EventPanel` means RaceUI/TrackSetupEditor/SceneWiring need no change; only a stale RaceUI hint string updated |
| B5 | Unity EditMode tests | Complete | `EventActionBuilderTests` (11) + 3 `EventManagerTests` |

## Validation Results
| Level | Status | Notes |
|---|---|---|
| Static Analysis (web-app) | Pass | Node ESM loads; full vitest suite imports the modules with no syntax errors |
| Unit Tests (web-app) | Pass | `postProcessing.test.js` 6/6; full suite 123 passed / 23 skipped / **16 of 17 files** |
| Unit Tests (Unity) | Deferred | EditMode tests written; cannot run here — the live UnitySkills/Unity instance is bound to the main checkout, not this isolated worktree. Runs via PR CI (game-ci/unity-test-runner). |
| Build (Unity) | Deferred | Compile verified by inspection (types/namespaces/enums/tuples/UnityAction valid); CI compiles on PR |
| Integration | N/A | |
| Edge Cases (web-app) | Pass | strict-gt boundary, single-response, password flip, male difference all covered |

### Known pre-existing failure (not caused by this change)
`__tests__/adversarial-ws.test.js` fails because it spawns a separate `../Server/server.js` whose deps are not installed ("Server deps missing — run `npm install` in Server/"). Unrelated to export/seed/db changes.

## Files Changed
| File | Action | Notes |
|---|---|---|
| `web-app/src/routes/export.js` | UPDATE | gt/lt on average_threshold; difference_threshold; export applyPostProcessing |
| `web-app/src/seed-templates.js` | UPDATE | member_count mapping; 5× gt; difference male; new `refreshTemplateContent()` |
| `web-app/src/db.js` | UPDATE | call refreshTemplateContent in applyMigrations |
| `web-app/__tests__/postProcessing.test.js` | CREATE | 6 tests |
| `Assets/Scripts/Data/SurveyTemplates.cs` | UPDATE | member_count mapping (parity) |
| `Assets/Tests/EditMode/SurveyTemplatesTests.cs` | UPDATE | mapping count 7→8 |
| `Assets/Scripts/Events/EventManager.cs` | UPDATE | TriggerRule + ApplyRule; removed Update() key polling |
| `Assets/Scripts/Events/EventActionBuilder.cs` | CREATE | rule factory |
| `Assets/Scripts/UI/EventPanel.cs` | REWRITE | parameterized menu (kept class name + fields) |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | camera-hint text (1-6 menus, 9/0 weather) |
| `Assets/Tests/EditMode/EventActionBuilderTests.cs` | CREATE | 11 tests |
| `Assets/Tests/EditMode/EventManagerTests.cs` | UPDATE | 3 TriggerRule tests |

## Deviations from Plan
1. **A3 migration** — plan referenced a `TEMPLATES` export that does not exist (seed array is module-local `templates`). Implemented an exported `refreshTemplateContent(db)` in seed-templates.js, called from `applyMigrations`. Same effect, cleaner co-location.
2. **B3/B4 UI** — plan proposed a new `EventMenuController` + rewiring RaceUI/TrackSetupEditor/SceneWiring. Instead **rewrote `EventPanel` in place** (kept the class name and its `EventManager`/`ContentParent`/`EventRowPrefab` fields). This preserves all existing wiring and the HUD visibility/auto-wire tests unchanged, reducing churn and merge risk — consistent with the plan's own "keep file compiling / avoid scene-wiring churn" guidance. Net: no changes needed to RaceUI wiring, TrackSetupEditor, or SceneWiring (only a stale hint string updated).

## Issues Encountered
- **Unity validation environment**: the running Unity instance (UnitySkills API) is attached to the main checkout, so it cannot compile/run this worktree's changes without polluting the user's working copy. Unity compile + EditMode tests are therefore deferred to PR CI. The C# was written to mirror existing, verified patterns exactly (EventRule construction, RaceUI.CreateTouchButton runtime UGUI, LegacyRuntime font).

## Tests Written
| Test File | Tests | Coverage |
|---|---|---|
| `web-app/__tests__/postProcessing.test.js` | 6 | strict-gt, password flip, male difference, combine, empty |
| `Assets/Tests/EditMode/EventActionBuilderTests.cs` | 11 | every builder's operator/attribute/value/delta/duration/weather + constants |
| `Assets/Tests/EditMode/EventManagerTests.cs` (added) | 3 | TriggerRule active/inactive/non-matching |

## Next Steps
- [ ] PR CI: Unity compile + EditMode tests (game-ci/unity-test-runner) — the deferred validation
- [ ] In Unity: run scene wiring, play-mode verify the menu (buttons + keys 1-6, colour/function pickers, name-length input, 9/0 weather) per plan Validation section
- [ ] Existing web-app deployments: create a new survey from the refreshed ENGG template to pick up the new conditions (existing surveys keep their copied config)
