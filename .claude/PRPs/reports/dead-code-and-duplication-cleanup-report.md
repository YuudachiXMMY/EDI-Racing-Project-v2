# Implementation Report: Dead Code & Duplication Cleanup (Production Prep)

## Summary
Executed all six cleanup targets from the plan. Deleted ~642 lines of dead Unity C#
(`RuntimeSetup.cs`, `CreateImportUI.cs` + their `.meta`), removed the dead
`POST /api/surveys/import-config` endpoint on both ends (plus its now-orphaned
`randomBytes` import), and consolidated two duplicated web-app client clusters —
extracting `useRoomStatus` + `RoomCodeModal` for the send modals and
`utils/csvExport.js` for the CSV export, fixing the `ResultsTab` newline-escaping bug.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium |
| Confidence | 8/10 | Single-pass, no rework |
| Files Changed | ~11 | 14 (incl. 2 `.meta` deletes; survey trio was a confirmed no-op) |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | Delete `RuntimeSetup.cs` + clean `RaceFinishPanel.cs:7` comment | ✅ Complete | `.cs` + `.meta` removed; comment reworded |
| 2 | Unity survey trio | ✅ No-op | Already absent (as planned); active `Data/Survey*` left untouched |
| 3 | Delete `CreateImportUI.cs` | ✅ Complete | `.cs` + `.meta` removed |
| 4 | Delete `import-config` endpoint (both ends) | ✅ Complete | Route + `randomBytes` import + `importConfigFromGame` all gone |
| 5 | Extract `useRoomStatus` + `RoomCodeModal`; refactor both modals | ✅ Complete | Superset (polling/auto-save/live-link) preserved |
| 6 | Extract `utils/csvExport.js`; fix newline escaping | ✅ Complete | `ResultsTab` now uses the correct `escapeCsv` |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis (oxlint) | ✅ Pass | Warnings only; `useRoomStatus` exhaustive-deps warning matches the existing codebase baseline (ResultsTab/EditorPage/etc. all have the same) |
| Unit Tests (vitest) | ⚠️ Pass w/ caveat | 40 passed, 9 skipped; 1 file (`adversarial-ws.test.js`) fails on a **pre-existing, unrelated** `Cannot find module 'ws'` in the legacy repo-root `Server/server.js` child process — its 9 tests are skipped and it references none of the changed files |
| CSV edge-case check | ✅ Pass | 9/9 assertions (newline/CR/comma/quote/empty/early-skip) via a standalone node harness |
| Build (vite) | ✅ Pass | 59 modules, imports resolve |
| Server syntax (`node --check`) | ✅ Pass | `export.js` + `index.js` |
| Dead-reference sweeps | ✅ Pass | All target symbols return zero matches |

## Files Changed

| File | Action | Notes |
|---|---|---|
| `Assets/Scripts/RuntimeSetup.cs` (+`.meta`) | DELETED | −462 (dead MonoBehaviour) |
| `Assets/Scripts/UI/RaceFinishPanel.cs` | UPDATED | Comment reworded (no API change) |
| `Assets/Scripts/Editor/CreateImportUI.cs` (+`.meta`) | DELETED | −180 (one-shot MenuItem) |
| `web-app/src/routes/export.js` | UPDATED | −43 route lines, −1 `randomBytes` import |
| `web-app/client/src/api.js` | UPDATED | −6 (`importConfigFromGame`) |
| `web-app/client/src/hooks/useRoomStatus.js` | CREATED | Shared debounce+poll+abort |
| `web-app/client/src/components/RoomCodeModal.jsx` | CREATED | Shared modal shell |
| `web-app/client/src/components/SendToGameModal.jsx` | UPDATED | 171 → 96 lines |
| `web-app/client/src/components/SendConfigModal.jsx` | UPDATED | 122 → 53 lines |
| `web-app/client/src/utils/csvExport.js` | CREATED | Correct `escapeCsv` + shared helpers |
| `web-app/client/src/components/ResultsTab.jsx` | UPDATED | Repointed; local dup removed (bug fixed) |
| `web-app/client/src/pages/HistoryPage.jsx` | UPDATED | Repointed; local dup removed |

## Deviations from Plan
1. **util JSON helper named `downloadJsonFile` (not `downloadJson`)** — each consumer keeps a thin local `downloadJson(session)` wrapper so the JSX call sites stay unchanged; the util export was renamed to avoid shadowing that local. Behavior identical.
2. **Once-guard for auto-save moved to a `resultsSavedRef`** in `SendToGameModal` (was `resultsSaved` state). The hook fires `onFinished` on every `Finished` poll and the modal owns the guard — preserving the original retry-on-save-failure semantics (slightly more correct than a stale-state closure).
3. **Backend test count**: the plan predicted "zero backend tests"; there are actually 5 vitest files / 49 tests. 4 files (40 tests) pass; the 1 failure is pre-existing infra (see Validation), not introduced here.
4. **Unicode arrows**: `ResultsTab.jsx`/`HistoryPage.jsx` expand-icon rendered as literal arrows (identical codepoints U+25BC/U+25B6 to the original escaped forms) — cosmetic source-representation only.

## Issues Encountered
- The GateGuard fact-forcing hook intercepted each first file touch; facts were presented and each write retried successfully.
- A transient broken state occurred when the `randomBytes` import removal applied before the route-body removal (gate blocked the first attempt); corrected immediately by retrying the route deletion before running any validation.

## Tests Written
No new committed test files (client has no runner; the CSV fix was verified via a
throwaway node harness, not committed). The correctness-critical `escapeCsv` fix is
covered by that ad-hoc check; a committed client test suite is out of scope per the plan.

## Next Steps
- [ ] Code review via `/code-review`
- [ ] PR already open (#51) — update its scope from plan-only to plan+implementation
