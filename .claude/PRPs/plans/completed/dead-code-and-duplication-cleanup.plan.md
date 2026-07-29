# Plan: Dead Code & Duplication Cleanup (Production Prep)

## Summary
Remove verified dead Unity/web-app code and consolidate two clusters of duplicated
web-app client code. Six original targets; **one is already done** (Unity survey
files no longer exist). Net effect: delete ~642 lines of dead Unity C#, delete one
authenticated-but-unused write endpoint (front + back), and de-duplicate two React
component pairs — fixing a latent CSV-corruption bug in the process.

## User Story
As a developer preparing this project for production,
I want dead code removed and duplicated logic consolidated into shared modules,
So that the codebase has less attack surface, no misleading dead paths, and a single
correct copy of the CSV export and room-status logic.

## Problem → Solution
- Dead Unity `MonoBehaviour`/`MenuItem` scripts with zero scene/code references still
  ship in the build → delete them.
- An authenticated `POST /api/surveys/import-config` endpoint + its client wrapper are
  never called anywhere → delete both ends (removes a writable, unused surface).
- Two modal components duplicate ~90 lines of room-code polling/debounce/JSX → extract
  a `useRoomStatus` hook + `RoomCodeModal` shell.
- Two CSV-export code paths duplicate four functions, and the copies have **diverged**:
  `ResultsTab`'s `escapeCsv` omits the `\n`/`\r` check that `HistoryPage`'s has, so any
  field containing a newline produces corrupt CSV → extract `utils/csvExport.js` with
  the correct implementation and point both files at it.

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A (free-form cleanup brief; item 2 traces to `PRP-Gap-Analysis-Report.md` GAP 1, already resolved by `completed/unity-survey-cleanup.plan.md`)
- **PRD Phase**: N/A
- **Estimated Files**: ~11 (3 deletes, 1 comment cleanup, 2 endpoint deletes, 3 new/changed for hook+shell, 3 changed for CSV) — Unity survey task is a no-op

---

## ⚠️ Verified Reality vs. Original Brief (read first)

The brief's line numbers/paths were checked against the tree. Corrections:

| Brief claim | Verified reality |
|---|---|
| `RuntimeSetup.cs` 462 lines, zero scene refs | ✅ Confirmed. GUID `6bd588b065ed04ba7a0330e01b74b545` has **zero** matches in any `.unity`/`.prefab`/`.asset`; no `AddComponent<RuntimeSetup>`/`typeof`/`nameof`. Only ref is a **stale doc comment** at `Assets/Scripts/UI/RaceFinishPanel.cs:7`. |
| Survey 3 files (~1160 lines) to delete | ⚠️ **Already deleted** — `SurveyBuilderPanel.cs`, `StudentSurveyPanel.cs`, `Network/SurveyCollector.cs` **do not exist**; class names have zero matches (see `completed/unity-survey-cleanup.plan.md`). This task is a **no-op**. **DO NOT** touch the *active* `Assets/Scripts/Data/Survey*.cs` (`SurveyTemplates`, `SurveyQuestion`, `SurveyResponseMapper`, `SurveyConfigManager`, `SurveyConfig`) or `Assets/Tests/EditMode/Survey*Tests.cs`. |
| Endpoint at `export.js:337-379` + `api.js:183-188` | ✅ Confirmed dead. Real paths: backend `web-app/src/routes/export.js:337-379` (**not** `server/routes/`), frontend `web-app/client/src/api.js:183-188` (**not** `services/api.js`). `requireAuth`-protected, does a DB `INSERT` scoped to `req.user.userId`. Only string `'import-config'` hits are the route def + the wrapper; `importConfigFromGame` has **only its own definition** as a hit. |
| `CreateImportUI.cs` "run once then delete", 462 lines | ✅ Safe to delete, but it's **180 lines** (not 462). It's a `[MenuItem("EDI Racing/Create Import JSON UI")]` static class. Replaced by active `Assets/Scripts/Editor/SceneWiring.cs:140-221`. GUID `dc3479dbe8063484595ca38d919524a8` has zero external matches. |
| Modals: `SendToGameModal` vs `SendConfigModal` | ✅ `SendToGameModal.jsx` 171 lines (used in `DashboardPage` + `EditorPage`), `SendConfigModal.jsx` 122 lines (used in `EditorPage` only). `SendToGameModal` has **extra** logic (5s polling, auto-save on `Finished`, "Watch Live Race" link) the shell must accommodate. **No** existing room-status hook. |
| CSV dup + `escapeCsv` bug | ✅ Confirmed. `ResultsTab.jsx` (`escapeCsv:142`, `downloadCsv:20`, `downloadJson:47`, `downloadBlob:149`) vs `HistoryPage.jsx` (`escapeCsv:179`, `downloadCsv:33`, `downloadJson:60`, `downloadBlob:187`). `ResultsTab.escapeCsv` omits the `\n`/`\r` guard. |

---

## UX Design

### Before / After
Internal cleanup + refactor — **no intended user-facing change**. The one behavioral
*fix* is invisible in the happy path: CSV exports from the **Results tab** for teams/
attributes whose values contain a newline will stop producing corrupt (row-breaking)
CSV. All other changes must be behavior-preserving.

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Results tab → Download CSV | Newline in a field breaks CSV rows | Field is quoted, CSV valid | Bug fix, only path with behavior change |
| Send-to-Game / Send-Config modals | Two hand-maintained copies | Same UI, shared hook + shell | Behavior-preserving |
| `POST /api/surveys/import-config` | Exists, unused, auth-writable | 404 (route gone) | No caller exists |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/client/src/components/SendToGameModal.jsx` | 1-171 | Superset modal — the shell + hook must reproduce this exactly (polling, auto-save, live link) |
| P0 | `web-app/client/src/components/SendConfigModal.jsx` | 1-122 | Subset modal — proves which parts are shared vs. optional |
| P0 | `web-app/client/src/components/RoomStatusBadge.jsx` | 1-40 | Presentational badge consuming `{status, checking}` — hook feeds this |
| P0 | `web-app/client/src/hooks/useRaceWebSocket.js` | all | Only existing hook — mirror its file/naming/return-shape conventions |
| P0 | `web-app/client/src/components/ResultsTab.jsx` | 20-49, 142-157 | CSV funcs to replace; the **buggy** `escapeCsv` |
| P0 | `web-app/client/src/pages/HistoryPage.jsx` | 33-62, 179-195 | CSV funcs to replace; the **correct** `escapeCsv` (source of truth) |
| P0 | `web-app/src/routes/export.js` | 1-2, 337-379 | Endpoint to delete + the `randomBytes` import it orphans |
| P0 | `web-app/client/src/api.js` | 183-188 | `importConfigFromGame` wrapper to delete |
| P1 | `web-app/client/src/pages/EditorPage.jsx` | 10-11, 294-295 | Consumes both modals — must keep working after refactor |
| P1 | `web-app/client/src/pages/DashboardPage.jsx` | 5, 181 | Consumes `SendToGameModal` — must keep working after refactor |
| P1 | `Assets/Scripts/UI/RaceFinishPanel.cs` | 7 | Stale comment mentioning `RuntimeSetup` to clean up |
| P2 | `Assets/Scripts/Editor/SceneWiring.cs` | 140-221 | Proves `CreateImportUI.cs` is redundant (do not modify) |

## External Documentation
No external research needed — feature uses established internal patterns (React hooks,
Express routing, Unity asset/meta deletion). Standards below are all from the codebase.

---

## Patterns to Mirror

### HOOK_FILE_CONVENTION
// SOURCE: web-app/client/src/hooks/useRaceWebSocket.js (only existing hook)
- Location: `client/src/hooks/`, one hook per file, filename === hook name + `.js`.
- ESM default project (`client/package.json` has `"type": "module"`), named `export function useX(...)`.
- New hook goes at `client/src/hooks/useRoomStatus.js`.

### API_WRAPPER_PATTERN
// SOURCE: web-app/client/src/api.js:176-188
```js
export async function sendConfigToGame(id, roomCode) {
  return request(`/surveys/${id}/send-config-to-game`, {
    method: 'POST',
    body: JSON.stringify({ roomCode }),
  });
}

export async function importConfigFromGame(configData) {   // ← DELETE this whole export (183-188)
  return request('/surveys/import-config', {
    method: 'POST',
    body: JSON.stringify(configData),
  });
}
```
Each API call is a thin `export async function` wrapping `request(path, opts)`. Deleting
one means removing the entire function + one surrounding blank line; leave neighbors intact.

### EXPRESS_ROUTE_PATTERN
// SOURCE: web-app/src/routes/export.js:337-379 (the route to delete) and :381 (the next route, keep)
```js
// POST /api/surveys/import-config — import a SurveyConfig from Unity format
router.post('/import-config', requireAuth, (req, res) => {
  ...
  const shareCode = randomBytes(4).toString('hex').toUpperCase();   // ← ONLY use of randomBytes in this file
  const result = db.prepare(`INSERT INTO surveys (...) VALUES (...)`).run(req.user.userId, ...);
  res.status(201).json({ success: true, data: {...} });
});

// POST /api/surveys/:id/send-config-to-game — send raw survey config to Unity   ← KEEP
router.post('/:id/send-config-to-game', requireAuth, (req, res) => { ... });
```

### CSV_ESCAPE_CORRECT (source of truth)
// SOURCE: web-app/client/src/pages/HistoryPage.jsx:179-185
```js
function escapeCsv(value) {
  if (!value) return '';
  const str = String(value);
  if (str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r'))
    return '"' + str.replace(/"/g, '""') + '"';
  return str;
}
```

### CSV_ESCAPE_BUGGY (to be eliminated)
// SOURCE: web-app/client/src/components/ResultsTab.jsx:142-147
```js
function escapeCsv(value) {
  if (!value) return '';
  const str = String(value);
  if (str.includes(',') || str.includes('"')) return '"' + str.replace(/"/g, '""') + '"';  // ← missing \n / \r
  return str;
}
```

### CSV_DOWNLOAD_BLOB (verbatim identical in both files)
// SOURCE: web-app/client/src/components/ResultsTab.jsx:149-157
```js
function downloadBlob(content, filename, mimeType) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
```

### CSV_BUILD_ROWS (identical except output filename)
// SOURCE: web-app/client/src/components/ResultsTab.jsx:20-45 (filename `race-results-${session.id}.csv`)
//         web-app/client/src/pages/HistoryPage.jsx:33-58   (filename `session-${session.roomCode}-${session.id}.csv`)
The entire `rankings → allKeys → header → rows` builder is identical; only the final
`downloadBlob(csv, <FILENAME>, ...)` differs. Extract as `buildResultsCsv(session)` (returns
the CSV string, no filename), leaving the filename decision at each call site.

### UNITY_ASSET_DELETION
Every `.cs` has a paired `.cs.meta`. Delete **both** the file and its `.meta`. Never leave
an orphan `.meta`. Editor scripts live in assembly `EDIRacing.Editor.asmdef`; runtime in
`EDIRacing.Runtime.asmdef` — deletions here don't cross assembly boundaries.

### TEST_STRUCTURE
- **web-app backend**: `vitest` (`web-app/package.json` → `npm test` = `vitest run`). Currently **zero** test files exist. No new backend test required for a pure deletion; run the suite to confirm no import breakage (there is none to run, but the command must still exit clean).
- **web-app client**: **no** test runner configured (`client/package.json` scripts: `dev`, `build`, `lint` = `oxlint`, `preview`). Validation = `npm run lint` + `npm run build` + manual click-through. Do **not** invent a test framework.
- **Unity**: `Assets/Tests/EditMode` (`Tests.asmdef`). Deleting `RuntimeSetup.cs`/`CreateImportUI.cs` needs no test; verify via editor compile (no errors) — done through UnitySkills API if available.

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/RuntimeSetup.cs` (+ `.meta`) | DELETE | 462-line dead `MonoBehaviour`, zero refs |
| `Assets/Scripts/UI/RaceFinishPanel.cs` | UPDATE | Remove stale `RuntimeSetup` mention in comment (line 7) |
| `Assets/Scripts/Editor/CreateImportUI.cs` (+ `.meta`) | DELETE | 180-line one-shot `[MenuItem]`, replaced by `SceneWiring.cs:140-221` |
| `web-app/src/routes/export.js` | UPDATE | Delete route 337-379 **and** orphaned `randomBytes` import (line 2) |
| `web-app/client/src/api.js` | UPDATE | Delete `importConfigFromGame` (183-188) |
| `web-app/client/src/hooks/useRoomStatus.js` | CREATE | Shared room-code debounce+poll+abort logic |
| `web-app/client/src/components/RoomCodeModal.jsx` | CREATE | Shared modal shell (overlay, room-code field, badge, actions) |
| `web-app/client/src/components/SendToGameModal.jsx` | UPDATE | Reduce to superset config over shell/hook (keeps polling + auto-save + live link) |
| `web-app/client/src/components/SendConfigModal.jsx` | UPDATE | Reduce to subset config over shell/hook |
| `web-app/client/src/utils/csvExport.js` | CREATE | `escapeCsv` (correct), `downloadBlob`, `buildResultsCsv`, `downloadJson` |
| `web-app/client/src/components/ResultsTab.jsx` | UPDATE | Import from `csvExport.js`; drop local copies (fixes bug) |
| `web-app/client/src/pages/HistoryPage.jsx` | UPDATE | Import from `csvExport.js`; drop local copies |

## NOT Building
- **No** deletion or edits to active `Assets/Scripts/Data/Survey*.cs` or `Assets/Tests/EditMode/Survey*Tests.cs` — these are live, unrelated to the dead survey panels.
- **No** new test framework for the client (none exists; don't add one).
- **No** behavioral change to the modals beyond de-duplication (keep polling/auto-save/live-link exactly as `SendToGameModal` has them).
- **No** change to `RoomStatusBadge.jsx`, `SceneWiring.cs`, `useRaceWebSocket.js`, or any Unity scene/prefab.
- **No** new abstraction for the JSON export beyond a shared `downloadJson` + `downloadBlob` (don't over-generalize filenames).
- **No** touching `sendToGame`/`sendConfigToGame`/`send-config-to-game` route — only `import-config` is dead.

---

## Step-by-Step Tasks

> Suggested order groups the trivially-safe deletions first, then the two refactors.
> Tasks 1-4 are independent of 5-6.

### Task 1: Delete `RuntimeSetup.cs` + clean stale comment
- **ACTION**: Delete `Assets/Scripts/RuntimeSetup.cs` and `Assets/Scripts/RuntimeSetup.cs.meta`. Edit `Assets/Scripts/UI/RaceFinishPanel.cs:7` to drop the `Auto-wired by RuntimeSetup or ...` phrasing (leave the "assignable via Inspector" intent).
- **IMPLEMENT**: `git rm Assets/Scripts/RuntimeSetup.cs Assets/Scripts/RuntimeSetup.cs.meta`; then a one-line comment edit in `RaceFinishPanel.cs`.
- **MIRROR**: UNITY_ASSET_DELETION.
- **IMPORTS**: none.
- **GOTCHA**: Delete the `.meta` too. Do not confuse with the *active* panels (`RaceFinishPanel`, `LeaderboardPanel`, etc.) — only `RuntimeSetup.cs` goes.
- **VALIDATE**: `grep -rn "RuntimeSetup" Assets/ --include=*.cs` returns **nothing** (comment now gone); Unity editor compiles with no errors.

### Task 2: Confirm survey no-op (do nothing destructive)
- **ACTION**: Verify the three brief-named files are absent; take **no** deletion action.
- **IMPLEMENT**: `ls Assets/Scripts/SurveyBuilderPanel.cs Assets/Scripts/StudentSurveyPanel.cs Assets/Scripts/Network/SurveyCollector.cs` → expect "No such file". `grep -rn "SurveyBuilderPanel\|StudentSurveyPanel\|class SurveyCollector" Assets/` → expect empty.
- **MIRROR**: n/a.
- **IMPORTS**: none.
- **GOTCHA**: `Assets/Scripts/Data/Survey*.cs` and `Assets/Tests/EditMode/Survey*Tests.cs` are **live** — must remain untouched.
- **VALIDATE**: The two greps confirm absence; no files changed for this task.

### Task 3: Delete `CreateImportUI.cs`
- **ACTION**: Delete `Assets/Scripts/Editor/CreateImportUI.cs` and its `.meta`.
- **IMPLEMENT**: `git rm Assets/Scripts/Editor/CreateImportUI.cs Assets/Scripts/Editor/CreateImportUI.cs.meta`.
- **MIRROR**: UNITY_ASSET_DELETION.
- **IMPORTS**: none.
- **GOTCHA**: It's a `[MenuItem]` static class in `EDIRacing.Editor.asmdef`; removing it removes the "EDI Racing > Create Import JSON UI" menu entry (intended — the panel is now built by `SceneWiring.cs`). Delete the `.meta`.
- **VALIDATE**: `grep -rn "CreateImportUI" Assets/ --include=*.cs` → empty; editor compiles clean; `SceneWiring.cs` unchanged.

### Task 4: Delete dead `import-config` endpoint (both ends)
- **ACTION**: In `web-app/src/routes/export.js`, delete the comment + route at lines 337-379 **and** the now-unused `import { randomBytes } from 'crypto';` at line 2. In `web-app/client/src/api.js`, delete the `importConfigFromGame` export at 183-188.
- **IMPLEMENT**: Remove the block from `// POST /api/surveys/import-config ...` through its closing `});` (keep the following `send-config-to-game` route). Remove line 2 import. Remove the client function + one adjacent blank line.
- **MIRROR**: EXPRESS_ROUTE_PATTERN, API_WRAPPER_PATTERN.
- **IMPORTS**: Removing an import, not adding one.
- **GOTCHA**: `randomBytes` is used **only** by this route (export.js:2 import, :359 use) — leaving the import triggers an unused-import lint/build flag. After deleting the route body, `grep -n randomBytes web-app/src/routes/export.js` must show **nothing** — remove both.
- **VALIDATE**: `grep -rn "import-config\|importConfigFromGame\|randomBytes" web-app/src web-app/client/src` → **zero** matches. Backend starts (`npm run dev` in `web-app/`) with no error; `npm test` (vitest) exits clean. Client `npm run lint && npm run build` succeed.

### Task 5: Extract `useRoomStatus` hook + `RoomCodeModal` shell; refactor both modals
- **ACTION**: Create `client/src/hooks/useRoomStatus.js` encapsulating the debounce + optional polling + abort room-status logic. Create `client/src/components/RoomCodeModal.jsx` as the shared shell. Rewrite `SendToGameModal.jsx` and `SendConfigModal.jsx` to consume them.
- **IMPLEMENT**:
  - `useRoomStatus({ poll = false, onFinished })` returns `{ roomCode, setRoomCode, roomStatus, checking }` and internally owns the constants `ROOM_CODE_KEY='edi-last-room-code'`, `DEBOUNCE_DELAY=800`, `MIN_CODE_LENGTH=4`, `POLL_INTERVAL=5000`, the `debounceRef`/`pollRef`/`abortRef`, the `fetchStatus` (trim/upper, min-length guard, `getRoomStatus`, abort check, `{ exists:false, error:'Failed to check' }` fallback), the `[roomCode]` effect, and the unmount cleanup. When `poll` is true it sets `setInterval(fetchStatus, POLL_INTERVAL)`; when `onFinished` is provided it calls it inside `fetchStatus` on `gamePhase === 'Finished'` (guarded once via an internal ref/state so it fires only once). Seed `roomCode` from `localStorage`.
  - `RoomCodeModal` renders the shared shell: `modal-overlay`/`modal-content send-to-game-modal`, `<h3>{title}</h3>`, `<p className="modal-hint">{hint}</p>`, the Room Code `<input>` (uppercasing, `maxLength={8}`, disabled while sending), `<RoomStatusBadge status={roomStatus} checking={checking} />`, an optional `{children}` slot (for the "Watch Live Race" link), `{message}` paragraph, and the actions block (primary Send button with `canSend`/label, secondary Cancel/Done). Accept props: `title, hint, sendLabel, roomCode, setRoomCode, roomStatus, checking, status, message, onSend, onClose` plus `children`.
  - `SendToGameModal`: `useRoomStatus({ poll:true, onFinished: (trimmed) => autoSaveResults(...) })`; keeps its `resultsSaved`/auto-save (`getRoomResults`+`saveRaceResults`) and passes the "Watch Live Race" `<a>` as `children`; owns its `handleSend` (`sendToGame`) + success message.
  - `SendConfigModal`: `useRoomStatus({ poll:false })`; no children; `handleSend` (`sendConfigToGame`) + its message. Distinct `title`/`hint`/`sendLabel`.
  - Keep `status`/`message`/`handleSend` in each modal (they differ per modal); only the room-status + shell are shared.
- **MIRROR**: HOOK_FILE_CONVENTION, and the exact bodies in `SendToGameModal.jsx:1-171` / `SendConfigModal.jsx:1-122`.
- **IMPORTS**: hook: `import { useState, useEffect, useRef } from 'react'; import { getRoomStatus } from '../api.js';`. Modals import `useRoomStatus` from `../hooks/useRoomStatus.js` and `RoomCodeModal` from `./RoomCodeModal.jsx`; `SendToGameModal` still imports `sendToGame, getRoomResults, saveRaceResults`; `SendConfigModal` still imports `sendConfigToGame`.
- **GOTCHA**: `SendToGameModal` is the **superset** — the shell/hook must not drop its polling, the `Finished` auto-save (call-once via `resultsSaved`), the "pause polling while sending" behavior, or the live-race link. `SendConfigModal` simply opts out (`poll:false`, no children, no `onFinished`). Both must keep the `send-to-game-modal` CSS class (shared styling) and identical `canSend` semantics (`status !== 'sending' && !checking && (!roomStatus || roomStatus.exists !== false)`). Both are consumed by `EditorPage.jsx:294-295` and `SendToGameModal` also by `DashboardPage.jsx:181` — their `{ surveyId, onClose }` prop contract must not change.
- **VALIDATE**: `npm run lint && npm run build` in `client/`. Manual: open Editor page → both modals render identically to before; enter a real room code → badge shows status; for Send-to-Game, verify polling still updates and (if a race finishes) results auto-save + "Watch Live Race" link appears. Dashboard's Send-to-Game still opens.

### Task 6: Extract `utils/csvExport.js`, fix newline escaping, repoint both consumers
- **ACTION**: Create `client/src/utils/csvExport.js` exporting `escapeCsv` (the **correct** version with `\n`/`\r`), `downloadBlob`, `buildResultsCsv(session)` (returns CSV string), and `downloadJson(session, filename)`. Update `ResultsTab.jsx` and `HistoryPage.jsx` to import these and delete their local copies.
- **IMPLEMENT**:
  - `escapeCsv`: use CSV_ESCAPE_CORRECT verbatim (from `HistoryPage.jsx:179-185`).
  - `downloadBlob`: use CSV_DOWNLOAD_BLOB verbatim.
  - `buildResultsCsv(session)`: the shared `rankings → allKeys → header → rows` builder (CSV_BUILD_ROWS), returning the CSV **string** (return `''` when `rankings.length === 0`, matching the current early `return`). No filename inside.
  - `downloadJson(session, filename)`: `downloadBlob(JSON.stringify(session, null, 2), filename, 'application/json')`.
  - `ResultsTab.jsx`: `import { buildResultsCsv, downloadBlob, downloadJson } from '../utils/csvExport.js';`. Its `downloadCsv(session)` becomes: `const csv = buildResultsCsv(session); if (!csv) return; downloadBlob(csv, \`race-results-${session.id}.csv\`, 'text/csv;charset=utf-8');`. Its `downloadJson(session)` → `downloadJson(session, \`race-results-${session.id}.json\`)`. Delete local `escapeCsv`/`downloadBlob`.
  - `HistoryPage.jsx`: same import; `downloadCsv` uses filename `session-${session.roomCode}-${session.id}.csv`; `downloadJson(session, \`session-${session.roomCode}-${session.id}.json\`)`. Delete local `escapeCsv`/`downloadBlob`.
- **MIRROR**: CSV_ESCAPE_CORRECT, CSV_DOWNLOAD_BLOB, CSV_BUILD_ROWS.
- **IMPORTS**: relative path from `components/` is `../utils/csvExport.js`; from `pages/` is `../utils/csvExport.js` (both one level down from `src/`).
- **GOTCHA**: The **whole point** is that `ResultsTab` now uses the correct `escapeCsv` — do **not** copy the buggy version. Preserve each file's distinct filename. Keep the `rankings.length === 0` early-return behavior (now driven by `buildResultsCsv` returning `''`). `utils/` does not exist yet — creating it is expected. Watch the local-scope name clash: both files currently define a `downloadJson` function — replace those definitions with the imported one (don't leave a shadowing local).
- **VALIDATE**: `grep -rn "function escapeCsv\|function downloadBlob" client/src/components/ResultsTab.jsx client/src/pages/HistoryPage.jsx` → **empty** (locals removed). `npm run lint && npm run build`. Manual: export a session whose `TeamName` contains `"a\nb"` from the Results tab → CSV field is quoted, row count intact.

---

## Testing Strategy

### Unit Tests
The client has no test runner; the backend has `vitest` but zero existing tests. The only
logic worth a focused test is the CSV escaping fix. If adding a test is desired, the
lowest-friction option is a backend-adjacent `vitest` test, but `csvExport.js` lives in the
client (no runner). **Recommended**: verify the fix manually (below) rather than bolt on a
client test harness for a single function. If a test is mandated by the reviewer, add a
minimal `vitest` config to the client and one spec — treat that as out-of-scope for this plan.

| Test (manual) | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Results-tab CSV escape | `TeamName = "Red\nTeam"` | Field emitted as `"Red\nTeam"` (quoted), rows unbroken | ✅ (the bug) |
| Results-tab CSV comma | `TeamName = "a,b"` | Quoted | yes |
| Empty rankings | `session.rankings = []` | No download (early return) | yes |
| Modal parity | Open both modals | Identical UI to pre-refactor | behavior-preserving |

### Edge Cases Checklist
- [x] Empty input — `rankings.length === 0` early return preserved; `escapeCsv('')` → `''`
- [x] Field with `\n`/`\r` — now quoted (the fix)
- [x] Field with `,`/`"` — quoted (unchanged)
- [ ] Concurrent access — n/a (client-side, single user)
- [x] Network failure — modal `fetchStatus` fallback `{ exists:false, error:'Failed to check' }` preserved in hook
- [x] Permission denied — deleted endpoint was `requireAuth`; removal reduces surface, no new path

---

## Validation Commands

### Static Analysis
```bash
cd web-app/client && npm run lint      # oxlint
```
EXPECT: Zero lint errors (esp. no unused imports after modal/CSV refactor).

### Build
```bash
cd web-app/client && npm run build     # vite build
```
EXPECT: Build succeeds, no unresolved imports.

### Backend
```bash
cd web-app && npm test                 # vitest run
cd web-app && npm run dev              # boots Express with --watch; Ctrl-C after "listening"
```
EXPECT: vitest exits clean; server boots with no `randomBytes`/route errors.

### Dead-reference sweeps (must all be empty)
```bash
grep -rn "RuntimeSetup" Assets/ --include=*.cs
grep -rn "CreateImportUI" Assets/ --include=*.cs
grep -rn "SurveyBuilderPanel\|StudentSurveyPanel\|class SurveyCollector" Assets/ --include=*.cs
grep -rn "import-config\|importConfigFromGame\|randomBytes" web-app/src web-app/client/src
grep -rn "function escapeCsv\|function downloadBlob" web-app/client/src/components/ResultsTab.jsx web-app/client/src/pages/HistoryPage.jsx
```
EXPECT: All empty.

### Unity Validation
Prefer UnitySkills API (`http://localhost:8090`) to trigger a domain reload / compile check.
EXPECT: No compile errors after deleting `RuntimeSetup.cs` / `CreateImportUI.cs`; no orphaned `.meta` warnings.

### Manual Validation
- [ ] Editor page: both Send modals render + function identically to before (incl. polling, auto-save, live link on Send-to-Game).
- [ ] Dashboard page: Send-to-Game opens and works.
- [ ] Results tab: export a session with a newline-containing field → valid CSV.
- [ ] History page: CSV/JSON export still works with correct filenames.
- [ ] `POST /api/surveys/import-config` now 404s (no caller exists, so nothing breaks).

---

## Acceptance Criteria
- [ ] All 6 tasks addressed (Task 2 confirmed no-op).
- [ ] `RuntimeSetup.cs` (+meta) and `CreateImportUI.cs` (+meta) deleted; Unity compiles.
- [ ] `RaceFinishPanel.cs:7` stale comment cleaned.
- [ ] `import-config` route + `randomBytes` import + `importConfigFromGame` all gone; server boots.
- [ ] `useRoomStatus` + `RoomCodeModal` created; both modals refactored, behavior identical.
- [ ] `utils/csvExport.js` created with correct `escapeCsv`; both consumers repointed; bug fixed.
- [ ] All validation commands pass; all dead-reference sweeps empty.

## Completion Checklist
- [ ] Code follows discovered patterns (hook convention, API/route/CSV patterns).
- [ ] No behavior change except the CSV newline fix.
- [ ] No unused imports (`randomBytes`, old CSV locals).
- [ ] `.meta` files deleted alongside `.cs`.
- [ ] No new test framework invented for the client.
- [ ] Active `Data/Survey*` and `Survey*Tests` untouched.
- [ ] Self-contained — no further codebase searching needed.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Modal refactor drops `SendToGameModal`'s polling/auto-save/live-link | Med | High (silent feature loss) | Treat `SendToGameModal` as the superset spec; diff behavior against the captured 1-171 source before/after |
| Removing `randomBytes` import missed → build/lint fail | Low | Low | Task 4 grep verifies zero remaining uses |
| Orphaned `.cs.meta` left behind → Unity warning | Med | Low | UNITY_ASSET_DELETION: always `git rm` both |
| CSV `buildResultsCsv` return-shape mismatch (string vs void) breaks early-return | Low | Med | Return `''` on empty rankings; call sites keep `if (!csv) return;` |
| `utils/` import path wrong from `components/` vs `pages/` | Low | Low | Both are one level under `src/` → `../utils/csvExport.js` for both |

## Notes
- Verification for this plan was done by two read-only exploration passes (Unity refs + web-app refs) plus direct reads of every file to be changed; all line numbers above are from the current tree, not the brief.
- Item 2 (Unity survey files) is retained in the plan only to record that it's **already complete** (`completed/unity-survey-cleanup.plan.md`) and to fence off the active `Data/Survey*` files from accidental deletion.
- The CSV bug is the only correctness defect in the set; everything else is dead-code removal or behavior-preserving de-duplication.
