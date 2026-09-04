# Implementation Report: Reduce web-app ENGG*1100 template rules to Snow/Night

## Summary
Reduced the web-app `ENGG*1100 Survey` template's `rules` array from 7 stale pre-baked rules to just the two weather events (Snow, Night), matching the new Unity live-event model where `EventPanel`/`EventActionBuilder` build Color/Function/Name/Male events on the fly (fixed +20/-15) and ignore imported rules. Extended `refreshTemplateContent` so already-seeded databases converge. `mappings` and `postProcessing` were verified already-correct and left untouched. Scope: web-app only (no Unity changes).

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Small | Small |
| Confidence | 9/10 | Implemented exactly as planned |
| Files Changed | 1 source (+1 optional test) | 2 (`seed-templates.js`, `db.test.js`) |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | Reduce ENGG `rules` to Snow+Night | Complete | Kept the two weather entries verbatim; removed the other 5 |
| 2 | Add "why only weather" comment | Complete | Mirrors the postProcessing inline-comment style |
| 3 | Extend `refreshTemplateContent` to refresh `rules_json` | Complete | SQL + run() args updated; docstring rewritten to explain the safety rationale |
| 4 | Harden db test (advisory) | Complete | New test asserts ENGG rules === `['Snow Weather','Night Weather']` |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis | Pass | ESM parse of `seed-templates.js` OK; exports intact. No lint/typecheck configured (plain ESM JS) |
| Unit Tests | Pass | `db.test.js` 14/14 (incl. new assertion) |
| Build | N/A | No build step for web-app |
| Integration | Pass | Full suite 18 files / 166 tests pass (incl. `adversarial-ws.test.js` WS relay 26/26) |
| Edge Cases | Pass | Fresh-DB seed = 2 rules; `refreshTemplateContent` now also converges existing DBs' `rules_json` |

## Files Changed

| File | Action | Lines |
|---|---|---|
| `web-app/src/seed-templates.js` | UPDATED | rules 7→2 (+comment); `refreshTemplateContent` +`rules_json`; docstring |
| `web-app/__tests__/db.test.js` | UPDATED | +7 (new Snow/Night assertion test) |

## Deviations from Plan
None — implemented exactly as planned.

## Issues Encountered
- The isolated worktree had no `node_modules` for `web-app/` or `Server/`. Symlinked both from the main checkout to run tests, then removed the symlinks before committing (they are not tracked; `node_modules` is gitignored). The initial `adversarial-ws.test.js` failure was purely this missing-`Server/`-deps environment gap, not a regression — it passes once deps are present.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `web-app/__tests__/db.test.js` | +1 | ENGG*1100 template `rules_json` reduced to exactly Snow/Night |

## Next Steps
- [ ] Code review
- [ ] Merge PR
- [ ] (Optional, out of scope) mirror the reduction into Unity `SurveyTemplates.cs` / `DefaultEventRules.cs` if byte-parity is ever wanted again
