# Plan: Infinite Races + Latest-Leaderboard CSV to Web-App

## Summary
Make every race run **infinitely** (remove the hard-coded 3-lap auto-finish) and, when the
professor **ends / stops / saves** a race, capture the current leaderboard (rank, laps,
checkpoints, and millisecond-precision per-car total time, best lap, and average lap) and
deliver it to the web-app so it appears under the survey's **Results** section and is bundled
into the existing **Download Data** ZIP.

## User Story
As a **professor running a classroom race session**,
I want **races to run indefinitely and the final standings (with precise timings) to land in
my survey's Results tab and Download Data bundle**,
so that **I can run a race as long as I like and then hand students a complete, downloadable
results sheet without any manual export step.**

## Problem → Solution
- **Now:** Race auto-finishes when the first car hits `RaceConfig.TotalLaps` (=3). Results
  leave Unity as a `race_results` WS message but only land in the `game_sessions` table on
  room close; the survey **Results** tab reads the `race_results` table, which is **never
  written**, so it always shows "No race results yet." Per-car "time" (`CarResult.TotalTime`)
  is the checkpoint-segment timer — not a meaningful race time. Download Data bundles
  responses + Unity CSV + analysis, but **no leaderboard**.
- **Desired:** `TotalLaps <= 0` ⇒ infinite (no auto-finish). Professor ends the race via a new
  **End Race** button (and Save/Export, and room-close as a fallback), which snapshots the
  leaderboard with real millisecond timings and sends it to the web-app. The WS server writes
  it to `race_results` (survey-linked), so the Results tab renders it live and `export-bundle`
  adds a `leaderboard.csv`.

## Metadata
- **Complexity**: Large
- **Source PRD**: N/A (free-form feature request via `/ecc:prp-plan`)
- **PRD Phase**: N/A
- **Estimated Files**: ~16 (7 Unity C#, 3 WS/back-end, 4 React/client, + tests)

---

## Decisions Locked (from user)
1. **Infinite mode** = configurable sentinel: `RaceConfig.TotalLaps <= 0` means infinite;
   positive values keep the lap-limited behavior. Back-compatible with saved sessions.
2. **End triggers** (ALL of them send the latest leaderboard to the web-app):
   - New **End Race** button in `RaceControlPanel`.
   - Existing **Save Session** button.
   - Existing **Export Results** button.
   - **Room close** on the WS server, as a fallback (uses last received results / leaderboard).
3. **Millisecond timings** — add per-car, all F3 (ms) precision:
   - **Total time** since race start (`ElapsedTime`).
   - **Best lap** (`BestLapTime`).
   - **Average lap** (`AverageLapTime`).
4. **Results landing** = WS server writes the `race_results` table (survey-linked by
   `linked_room_code`); the Results tab's existing read path is unchanged.

---

## UX Design

### Before
```
Unity (professor host)                    Web-App (survey editor)
┌──────────────────────────┐             ┌───────────────────────────────┐
│ Race auto-ends at lap 3   │             │ Results tab: "No race results │
│ Controls: Pause Save Exp  │  race_      │  yet."  (race_results empty)  │
│ Export → local file only  │  results ─► │ Download Data: responses +    │
│                           │  (WS→game_  │   vehicleGroupData + analysis │
└──────────────────────────┘   sessions) └───────────────────────────────┘
```

### After
```
Unity (professor host)                    Web-App (survey editor)
┌──────────────────────────┐             ┌───────────────────────────────┐
│ Race runs forever         │             │ Results tab shows latest      │
│ Controls: Pause Save Exp  │  race_      │  standings: Rank Team Laps CP │
│   + [End Race] (new)      │  results ─► │  Total(ms) Best(ms) Avg(ms)   │
│ End/Save/Export → snapshot│  (WS writes │ Per-session Download CSV      │
│  leaderboard w/ ms times  │  race_      │ Download Data ZIP now also    │
│  → send to web-app        │  results)   │  contains leaderboard.csv     │
└──────────────────────────┘             └───────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Race end | Auto at lap 3 | Runs until professor acts (infinite) | Only when `TotalLaps <= 0` |
| RaceControlPanel | Pause / Save / Export | + **End Race** | New button; snapshots + finishes |
| Save / Export buttons | Local session/export only | Also send leaderboard to web-app | Reuses `race_results` WS message |
| Survey Results tab | Always empty | Shows latest standings + times | `race_results` now written by WS server |
| ResultsTable columns | Rank/Team/Laps/CP/Time | + Total / Best Lap / Avg Lap (ms) | New columns |
| Download Data ZIP | 3 files | + `leaderboard.csv` | Latest `race_results` row for survey |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Race/RaceManager.cs` | 65-237, 314-352 | `OnCarCompletedLap` finish check; `LoadAndStartRace`; Save/Export API |
| P0 | `Assets/Scripts/Race/RaceConfig.cs` | 72-73 | `TotalLaps = 3` — the sentinel field |
| P0 | `Assets/Scripts/Race/LapTracker.cs` | 26-42 | Where lap completion fires (`OnLapCompleted`) |
| P0 | `Assets/Scripts/Car/CarIdentity.cs` | 9-72 | Add lap-timing state + `Initialize`/`Update` |
| P0 | `Assets/Scripts/Race/ScoreManager.cs` | 47-72 | `CollectResults` builds `CarResult[]` |
| P0 | `Assets/Scripts/Data/SessionData.cs` | 161-191 | `RaceResults` / `CarResult` struct (add time fields) |
| P0 | `Assets/Scripts/Data/ResultsExporter.cs` | 26-61 | CSV format to mirror (add columns + F3) |
| P0 | `Assets/Scripts/Network/NetworkSync.cs` | 135-209 | `race_results` broadcast (extract reusable method) |
| P0 | `Assets/Scripts/UI/RaceControlPanel.cs` | 20-118 | Add **End Race** button + wire triggers |
| P0 | `Server/server.js` | 128-182, 286-298, 621-640 | Room close archive; `race_results` message handling |
| P0 | `web-app/src/routes/results.js` | 10-33, 88-130 | `race_results` write/read; shared-secret pattern |
| P0 | `web-app/src/routes/export.js` | 264-286 | `export-bundle` — add `leaderboard.csv` |
| P0 | `web-app/client/src/utils/csvExport.js` | 42-67 | `buildResultsCsv` (add time columns) |
| P1 | `web-app/client/src/components/ResultsTable.jsx` | all | Add Total/Best/Avg columns |
| P1 | `web-app/client/src/components/ResultsTab.jsx` | all | Read path already correct; no data change needed |
| P1 | `web-app/src/config.js` | 8-11 | `WS_GAME_URL` / `API_URL` wiring |
| P2 | `web-app/__tests__/export-bundle.test.js` | all | Test pattern for bundle |
| P2 | `web-app/__tests__/results-archive.test.js` | all | Test pattern for results write |
| P2 | `Assets/Tests/EditMode/ResultsExporterTests.cs` | all | Test pattern for CSV export |
| P2 | `Assets/Tests/EditMode/ScoreManagerTests.cs` | all | Test pattern for CollectResults |

## External Documentation
No external research needed — feature uses established internal patterns (Unity MonoBehaviour +
JsonUtility, Express routes + better-sqlite3, React presentational components, Vitest/NUnit).

---

## Patterns to Mirror

### NAMING_CONVENTION (Unity C# — PascalCase public fields, `On`-prefixed events)
```csharp
// SOURCE: Assets/Scripts/Data/SessionData.cs:172-191
[Serializable]
public struct CarResult
{
    public int Rank;
    public string TeamName;
    public AttributeEntry[] Attributes;
    public int LapsCompleted;
    public int CheckpointsPassed;
    public float TotalTime;
    // ColorIndex computed property ...
}
```

### CSV_EXPORT (Unity — string builder, dynamic attribute columns, EscapeCsv)
```csharp
// SOURCE: Assets/Scripts/Data/ResultsExporter.cs:44-59
sb.Append("Rank,TeamName");
foreach (var key in allKeys) sb.Append($",{EscapeCsv(key)}");
sb.AppendLine(",LapsCompleted,CheckpointsPassed,Time");
foreach (var car in results.Rankings)
{
    sb.Append($"{car.Rank},{EscapeCsv(car.TeamName)}");
    foreach (var key in allKeys) sb.Append($",{EscapeCsv(car.Attributes.Get(key, ""))}");
    sb.AppendLine($",{car.LapsCompleted},{car.CheckpointsPassed},{car.TotalTime:F2}");
}
```

### CSV_EXPORT (JS — mirror of the Unity exporter, RFC-4180 escaping)
```javascript
// SOURCE: web-app/client/src/utils/csvExport.js:53-64
let csv = 'Rank,TeamName';
for (const key of allKeys) csv += `,${escapeCsv(key)}`;
csv += ',LapsCompleted,CheckpointsPassed,Time\n';
for (const car of rankings) {
  csv += `${car.Rank},${escapeCsv(car.TeamName)}`;
  for (const key of allKeys) { const a = (car.Attributes||[]).find(x=>x.Key===key); csv += `,${escapeCsv(a?a.Value:'')}`; }
  csv += `,${car.LapsCompleted},${car.CheckpointsPassed},${(car.TotalTime||0).toFixed(2)}\n`;
}
```

### WS_RESULTS_BROADCAST (Unity — build message, JsonUtility, Send)
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:185-208
private void OnRaceFinishedHandler(CarIdentity winner)
{
    if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
    if (ScoreManager == null) return;
    var results = ScoreManager.CollectResults(RaceManager.GetEventLog(), Time.time - RaceManager.RaceStartTime);
    string configName = RaceManager.SurveyConfigManager?.ActiveConfig?.ConfigName ?? "";
    var msg = new RaceResultsMessage { configName = configName, resultsJson = JsonUtility.ToJson(results) };
    NetworkManager.Send(JsonUtility.ToJson(msg));
}
```

### WS_SERVER_MESSAGE_HANDLING (server.js — capture message, mutate room, relay)
```javascript
// SOURCE: Server/server.js:627-632
} else if (msg.type === 'race_results') {
  room.raceResults = raw;
  room.gamePhase = 'Finished';
} else if (msg.type === 'race_end') {
  room.gamePhase = 'Finished';
}
```

### SHARED_SECRET_INTERNAL_WRITE (results.js — fail-closed shared-secret auth)
```javascript
// SOURCE: web-app/src/routes/results.js:88-115
if (!ARCHIVE_ENABLED) return res.status(503).json({ success:false, error:'... set INTERNAL_SECRET.' });
const provided = Buffer.from(req.headers['x-internal-secret'] || '');
const expected = Buffer.from(INTERNAL_SECRET);
if (provided.length !== expected.length || !timingSafeEqual(provided, expected))
  return res.status(403).json({ success:false, error:'Forbidden' });
const linked = db.prepare('SELECT id, user_id FROM surveys WHERE linked_room_code = ? COLLATE NOCASE').get(roomCode);
```

### EXPORT_BUNDLE_FILE_LIST (export.js — array of {name, data} → createZip)
```javascript
// SOURCE: web-app/src/routes/export.js:267-282
const files = [
  { name: `${base}-responses.xlsx`, data: buildResponsesWorkbook(survey, db) },
  { name: 'vehicleGroupData.csv', data: buildVehicleGroupCsv(survey) },
  { name: `${base}-analysis.csv`, data: buildSurveyAnalysisCsv(survey, db) },
];
const zip = createZip(files);
```

### TEST_STRUCTURE (Vitest — describe/it, in-memory DB helper)
```javascript
// SOURCE: web-app/__tests__/results-archive.test.js (mirror its setup/teardown + supertest-style calls)
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Sentinel finish check; add `EndRace()`; route Save/Export to WS send |
| `Assets/Scripts/Car/CarIdentity.cs` | UPDATE | Add `BestLapTime`, `LastLapStartTime`, `AccumulatedLapTime`; `RecordLap()`; init |
| `Assets/Scripts/Race/ScoreManager.cs` | UPDATE | Populate new time fields in `CollectResults` |
| `Assets/Scripts/Data/SessionData.cs` | UPDATE | Add `ElapsedTime`, `BestLapTime`, `AverageLapTime` to `CarResult` |
| `Assets/Scripts/Data/ResultsExporter.cs` | UPDATE | New CSV columns; F3 (ms) for time fields |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Extract `BroadcastRaceResults()` reusable by Save/Export/EndRace |
| `Assets/Scripts/UI/RaceControlPanel.cs` | UPDATE | Add **End Race** button + handler |
| `Assets/Scenes/complete_track_demo.unity` | UPDATE | Add End Race button GameObject + wire ref (via UnitySkills API) |
| `Server/server.js` | UPDATE | On `race_results` msg → POST latest results to new internal endpoint |
| `web-app/src/routes/results.js` | UPDATE | Add shared-secret `POST /api/internal/race-results` (survey-linked write) |
| `web-app/src/routes/export.js` | UPDATE | Add `buildLeaderboardCsv` + `leaderboard.csv` to bundle |
| `web-app/client/src/utils/csvExport.js` | UPDATE | Add Total/Best/Avg columns (toFixed(3)) to `buildResultsCsv` |
| `web-app/client/src/components/ResultsTable.jsx` | UPDATE | Render new time columns |
| `Assets/Tests/EditMode/ResultsExporterTests.cs` | UPDATE | Cover new columns / ms formatting |
| `Assets/Tests/EditMode/ScoreManagerTests.cs` | UPDATE | Cover new time fields + infinite mode |
| `web-app/__tests__/export-bundle.test.js` | UPDATE | Assert `leaderboard.csv` present |
| `web-app/__tests__/results-archive.test.js` | UPDATE | Cover internal race-results write endpoint |

## NOT Building
- No new **student-facing** UI; students keep receiving the periodic `leaderboard` message.
- No schema migration on `race_results` (new time fields live inside `rankings_json`).
- No change to the `game_sessions` archive contract (kept as-is; it's the room-close fallback).
- No lap-by-lap history table — only best/average/total per car (aggregate).
- No CSV download button relocation — the per-session "Download CSV" and "Download Data"
  buttons stay where they are; only their contents/columns expand.
- No change to lap-limited races other than the sentinel guard (positive `TotalLaps` unchanged).

---

## Step-by-Step Tasks

### Task 1: Infinite-lap sentinel in RaceManager
- **ACTION**: Guard the auto-finish so `TotalLaps <= 0` never finishes.
- **IMPLEMENT**: In `OnCarCompletedLap` (`RaceManager.cs:223-237`), change
  `if (car.CurrentLap >= Config.TotalLaps)` to
  `if (Config.TotalLaps > 0 && car.CurrentLap >= Config.TotalLaps)`.
- **MIRROR**: existing method body (keep the `raceFinished`/`OnRaceFinished`/`SetState` block).
- **IMPORTS**: none.
- **GOTCHA**: Do NOT early-return before `Debug.Log($"[Race] {car.TeamName} completed lap …")` —
  lap logging must still happen every lap in infinite mode.
- **VALIDATE**: EditMode test: with `TotalLaps=0`, completing 5 laps never sets `Finished`.

### Task 2: Per-car lap timing state in CarIdentity
- **ACTION**: Track best lap, accumulated lap time, and current-lap start.
- **IMPLEMENT**: Add public fields `float BestLapTime;` (0 = none yet), `float LastLapStartTime;`,
  `float AccumulatedLapTime;`, `int CompletedLaps;`. Add method
  `public void RecordLap(float now) { float lap = now - LastLapStartTime; if (lap > 0) { AccumulatedLapTime += lap; if (BestLapTime <= 0f || lap < BestLapTime) BestLapTime = lap; } LastLapStartTime = now; CompletedLaps++; }`.
  In `Initialize` (line 24-35) add `BestLapTime = 0f; AccumulatedLapTime = 0f; CompletedLaps = 0; LastLapStartTime = 0f;`.
- **MIRROR**: CarIdentity's existing public-field + method style (`CarIdentity.cs:12-22, 24-35`).
- **IMPORTS**: none (uses `UnityEngine.Time` indirectly via caller).
- **GOTCHA**: `LastLapStartTime` must be set to the **race start time** when the race starts, not 0,
  or the first lap time = `Time.time` (huge). Set it in Task 3.
- **VALIDATE**: Unit test `RecordLap` twice with known `now` values → `BestLapTime` = min,
  `AccumulatedLapTime` = sum.

### Task 3: Initialize lap timing at race start + record on lap completion
- **ACTION**: Seed each car's `LastLapStartTime` and record laps as they complete.
- **IMPLEMENT**:
  (a) In `RaceManager.LoadAndStartRace` (`RaceManager.cs:88-115`), inside the
  `foreach (var car in spawnedCars)` loop, after getting `identity`, set
  `identity.LastLapStartTime = Time.time;` (do it right before/after `ScoreManager.RegisterCar`).
  Note `raceStartTime = Time.time` is set at line 111 — use `Time.time` (same frame).
  (b) In `OnCarCompletedLap` (`RaceManager.cs:223`), first line: `car.RecordLap(Time.time);`.
- **MIRROR**: existing loop at `RaceManager.cs:92-96`.
- **IMPORTS**: none.
- **GOTCHA**: Visual-only student cars (`LoadAndStartRaceVisualOnly`) don't run lap logic — skip;
  they never call `RecordLap`, and their times stay 0 (correct — students don't compute results).
- **VALIDATE**: Play-mode/host: after N laps, `car.CompletedLaps == car.CurrentLap` and
  `BestLapTime > 0`.

### Task 4: Add time fields to CarResult
- **ACTION**: Extend the serialized result struct with ms timings.
- **IMPLEMENT**: In `SessionData.cs` `CarResult` (line 172-191) add:
  `public float ElapsedTime;` (total time since race start), `public float BestLapTime;`,
  `public float AverageLapTime;`. Keep `TotalTime` for back-compat.
- **MIRROR**: `CarResult` field style (`SessionData.cs:175-180`).
- **IMPORTS**: none.
- **GOTCHA**: `JsonUtility` serializes public fields automatically → these flow through
  `resultsJson` to the WS server and into `rankings_json` with no extra wiring. Web-app reads
  them as `car.ElapsedTime` / `car.BestLapTime` / `car.AverageLapTime` (PascalCase preserved).
- **VALIDATE**: `JsonUtility.ToJson` of a populated `CarResult` contains the three new keys.

### Task 5: Populate time fields in ScoreManager.CollectResults
- **ACTION**: Fill the new fields per car.
- **IMPLEMENT**: In `CollectResults` (`ScoreManager.cs:47-72`), inside the ranking loop add:
  `ElapsedTime = raceTime,` (whole-race elapsed passed in — same wall-clock for all cars at snapshot),
  `BestLapTime = c.BestLapTime,`
  `AverageLapTime = c.CompletedLaps > 0 ? c.AccumulatedLapTime / c.CompletedLaps : 0f,`.
  Leave `TotalTime = c.CheckpointTime` unchanged (legacy tiebreaker column).
- **MIRROR**: existing `CarResult` initializer (`ScoreManager.cs:54-64`).
- **IMPORTS**: none.
- **GOTCHA**: `raceTime` is `Time.time - raceStartTime` from callers (`RaceManager.cs:176, 282, 347`,
  `NetworkSync.cs:192`). Do not recompute it inside ScoreManager.
- **VALIDATE**: EditMode `ScoreManagerTests`: register cars with known `BestLapTime`/`AccumulatedLapTime`,
  call `CollectResults(log, 90f)` → assert `AverageLapTime` and `ElapsedTime`.

### Task 6: New CSV columns (Unity ResultsExporter, ms precision)
- **ACTION**: Add Total/Best/Avg columns with F3.
- **IMPLEMENT**: In `ResultsExporter.ExportRankingsCsv(RaceResults)` (`ResultsExporter.cs:26-61`):
  header → `…,LapsCompleted,CheckpointsPassed,TotalTime,BestLap,AvgLap`;
  row → `…,{car.LapsCompleted},{car.CheckpointsPassed},{car.ElapsedTime:F3},{car.BestLapTime:F3},{car.AverageLapTime:F3}`.
  Update the empty-results header string (line 29) to match. (Rename the old `Time` column to
  `TotalTime` for clarity, or keep `Time` — see GOTCHA.)
- **MIRROR**: `ResultsExporter.cs:44-59`.
- **IMPORTS**: none.
- **GOTCHA**: `ResultsExporterTests.cs` asserts the exact header/row string — update the test in
  Task 14 in lockstep. Keep column order stable (attributes block stays between TeamName and the
  numeric tail).
- **VALIDATE**: `ResultsExporterTests` pass with new columns.

### Task 7: Extract reusable BroadcastRaceResults in NetworkSync
- **ACTION**: Make the results-send callable from Save/Export/EndRace, not only `OnRaceFinished`.
- **IMPLEMENT**: Refactor `OnRaceFinishedHandler` (`NetworkSync.cs:185-209`) to delegate to a new
  `public void BroadcastRaceResults()` that does the current body (collect results via
  `ScoreManager.CollectResults(RaceManager.GetEventLog(), Time.time - RaceManager.RaceStartTime)`,
  build `RaceResultsMessage`, `Send`). `OnRaceFinishedHandler(winner)` just calls it.
- **MIRROR**: current handler body (`NetworkSync.cs:190-208`).
- **IMPORTS**: none.
- **GOTCHA**: Keep the host/connected guards (`!NetworkManager.IsHost` early-return). If not
  hosting (Editor-only run), the method no-ops safely — Save/Export local paths still work.
- **VALIDATE**: Call `BroadcastRaceResults()` while hosting → one `race_results` message sent.

### Task 8: RaceManager.EndRace() + route Save/Export to web-app send
- **ACTION**: Add explicit end + make Save/Export also push results.
- **IMPLEMENT**:
  (a) New `public void EndRace()`: `if (!raceStarted || raceFinished) return; raceFinished = true;`
  pick leader `var ranked = ScoreManager.GetRankedCars(); var leader = ranked.Count > 0 ? ranked[0] : null;`
  then `if (leader != null) OnRaceFinished?.Invoke(leader);` and `SetState(GameState.Finished);`.
  (`OnRaceFinished` already triggers `NetworkSync`'s handler → `BroadcastRaceResults`.) If `leader`
  is null (no cars), still `SetState(Finished)` but also call `NetworkSync?.BroadcastRaceResults()`
  directly so an empty leaderboard is still sent.
- (b) In `SaveCurrentSession` (`RaceManager.cs:166-171`) and `ExportCurrentResults` (173-179), after
  the existing local save/export, add `if (NetworkSync != null) NetworkSync.BroadcastRaceResults();`.
- **MIRROR**: existing public API methods `PauseRace`/`ResumeRace` (`RaceManager.cs:150-164`).
- **IMPORTS**: index into `GetRankedCars()` list (no LINQ needed); `System.Linq` optional.
- **GOTCHA**: `RaceFinishPanel` subscribes to `OnRaceFinished` and needs a non-null winner for its
  overlay text (`winner.TeamName`, `RaceFinishPanel.cs:83-90`). Only invoke `OnRaceFinished` when a
  leader exists; otherwise send results directly via NetworkSync (see above).
- **VALIDATE**: EndRace while hosting → `Finished` state + `race_results` sent; Save/Export also send.

### Task 9: Add "End Race" button to RaceControlPanel
- **ACTION**: New button + handler.
- **IMPLEMENT**: Add `public Button EndRaceButton;` and in `Start()` wire
  `if (EndRaceButton != null) EndRaceButton.onClick.AddListener(EndRace);`. Handler:
  `private void EndRace() { if (RaceManager == null) return; RaceManager.EndRace(); ShowStatus("Race ended — results sent"); }`.
- **MIRROR**: `SaveButton`/`SaveSession` wiring (`RaceControlPanel.cs:22, 50-51, 108-112`).
- **IMPORTS**: none (already uses `UnityEngine.UI`).
- **GOTCHA**: Serialized reference can drop to `{fileID:0}` (see the defensive auto-wire comment at
  `RaceControlPanel.cs:36-44`). The button ref survives (same-object), but confirm the scene actually
  contains the button — do the scene wiring in Task 10.
- **VALIDATE**: EditMode: panel with a mock button → clicking invokes `RaceManager.EndRace`.

### Task 10: Wire End Race button in the scene (UnitySkills API)
- **ACTION**: Create the button GameObject and assign `RaceControlPanel.EndRaceButton`.
- **IMPLEMENT**: Per project rule (technical-preferences.md), use the **UnitySkills REST API**
  (`http://localhost:8090`) — clone the existing `SaveBtn`/`ExportBtn` under the control panel,
  rename to `EndRaceBtn`, set label "End Race", and set the `RaceControlPanel.EndRaceButton`
  serialized reference. Mirror the prior "clone AutoCamBtn → ToggleNamesBtn" approach recorded in
  memory `[[scene-wiring-lags-merged-scripts]]`.
- **MIRROR**: existing control buttons in `complete_track_demo.unity`.
- **IMPORTS**: N/A (editor automation).
- **GOTCHA**: `.unity`/`.prefab` serialized refs lag merged `.cs` — after merge, verify the
  inspector slot is populated (no empty `{fileID:0}`). See memory `[[scene-wiring-lags-merged-scripts]]`.
- **VALIDATE**: Enter Play mode (host), click End Race → status text + `Finished` state.

### Task 11: WS server posts latest results to the web-app on `race_results`
- **ACTION**: When a `race_results` message arrives, write it to the survey-linked `race_results` table.
- **IMPLEMENT**: In `server.js` where `msg.type === 'race_results'` (line 627-629), after
  `room.raceResults = raw`, fire-and-forget POST to the new internal endpoint with the parsed
  results + `roomCode`:
  ```js
  postRaceResults(roomCode, room.raceResults); // helper: parse resultsJson, POST /api/internal/race-results
  ```
  Add helper `postRaceResults(roomCode, rawResultsMsg)` near `destroyRoom` (mirrors the archive fetch
  at `server.js:159-166`): parse `configName`/`resultsJson`, POST `{ roomCode, configName, rankings,
  eventLog, totalRaceTime }` with `x-internal-secret: INTERNAL_SECRET`. Keep the room-close archive
  path unchanged (fallback).
- **MIRROR**: `destroyRoom`'s archive fetch (`server.js:135-166`).
- **IMPORTS**: none (uses global `fetch`, `API_URL`, `INTERNAL_SECRET` already in file).
- **GOTCHA**: Fire-and-forget with `.catch(()=>{})` — a web-app outage must not crash the relay.
  De-dupe: End/Save/Export can each send `race_results`; the endpoint inserts a row each time
  (acceptable — Results tab shows newest first; or upsert by room — see Task 12 note).
- **VALIDATE**: adversarial-ws test / manual: send `race_results` → row appears in `race_results`.

### Task 12: Internal race-results write endpoint (shared-secret, survey-linked)
- **ACTION**: Add `POST /api/internal/race-results` mirroring `/sessions/archive` auth.
- **IMPLEMENT**: In `web-app/src/routes/results.js`, add a route guarded by the same
  `ARCHIVE_ENABLED` + `timingSafeEqual` shared-secret check as `/sessions/archive` (lines 88-99).
  Look up `survey` by `linked_room_code` (line 103-105 pattern). If found, insert into `race_results`
  (`survey_id, room_code, config_name, rankings_json, event_log_json, total_race_time`) using the
  existing INSERT shape (lines 20-31). Return `{success:true}` (or `{success:true, skipped:true}` if
  no linked survey — don't error, room may be unlinked).
- **MIRROR**: `/sessions/archive` (`results.js:83-142`) for auth; `POST /surveys/:id/results` (10-33)
  for the INSERT columns.
- **IMPORTS**: already present (`getDb`, `timingSafeEqual`, `DEFAULT_INTERNAL_SECRET`).
- **GOTCHA**: This endpoint is **auth-boundary-independent** like `/sessions/archive` — it can write
  against any professor's survey, so it MUST fail-closed on the default/empty secret. Reuse
  `archiveSecretUsable`. Don't require `requireAuth` (the WS server has no user JWT).
- **VALIDATE**: results-archive-style test: POST with correct secret + linked room → row in
  `race_results`; wrong secret → 403; default secret → 503.

### Task 13: Add leaderboard.csv to the export bundle + expand client CSV columns
- **ACTION**: Server-side `leaderboard.csv` in the ZIP; new columns in client `buildResultsCsv`.
- **IMPLEMENT**:
  (a) In `export.js`, add `buildLeaderboardCsv(survey, db)`: `SELECT rankings_json FROM race_results
  WHERE survey_id = ? ORDER BY received_at DESC LIMIT 1`; parse rankings; reuse the same column
  logic as the client `buildResultsCsv` (Rank, TeamName, dynamic attrs, LapsCompleted,
  CheckpointsPassed, TotalTime(=ElapsedTime, F3), BestLap(F3), AvgLap(F3)). Return `''` if none.
  Add `{ name: 'leaderboard.csv', data: buildLeaderboardCsv(survey, db) }` to the `files` array
  (`export.js:267-282`) — only if non-empty (skip empty to avoid a confusing 0-byte file, or include
  a header-only file; prefer include-if-nonempty).
  (b) In client `csvExport.js` `buildResultsCsv` (42-67): header tail →
  `,LapsCompleted,CheckpointsPassed,TotalTime,BestLap,AvgLap`; row tail →
  `,${car.LapsCompleted},${car.CheckpointsPassed},${(car.ElapsedTime||car.TotalTime||0).toFixed(3)},${(car.BestLapTime||0).toFixed(3)},${(car.AverageLapTime||0).toFixed(3)}`.
- **MIRROR**: `export.js` `buildVehicleGroupCsv`/bundle (`export.js:230-282`); client `csvExport.js:53-64`.
- **IMPORTS**: none new.
- **GOTCHA**: Keep server and client CSV **column order identical** so both downloads match.
  `ElapsedTime||TotalTime` fallback keeps old rows (no new fields) from breaking.
- **VALIDATE**: `export-bundle.test.js`: bundle includes `leaderboard.csv` with expected header when a
  `race_results` row exists.

### Task 14: Update ResultsTable columns + tests
- **ACTION**: Show the new time columns; update Unity + JS tests.
- **IMPLEMENT**:
  (a) `ResultsTable.jsx`: add `<th>Total</th><th>Best Lap</th><th>Avg Lap</th>` and cells
  `{(car.ElapsedTime||car.TotalTime||0).toFixed(3)}s`, `{(car.BestLapTime||0).toFixed(3)}s`,
  `{(car.AverageLapTime||0).toFixed(3)}s`.
  (b) Update `ResultsExporterTests.cs` + `ScoreManagerTests.cs` (new columns/fields) and
  `export-bundle.test.js` + `results-archive.test.js` (new endpoint + leaderboard.csv).
- **MIRROR**: `ResultsTable.jsx` existing `<td>` cells; `ResultsExporterTests.cs` assertions.
- **IMPORTS**: none.
- **GOTCHA**: `ResultsTable` is shared by `ResultsTab` **and** `HistoryPage.jsx` — both benefit; verify
  HistoryPage rows (from `game_sessions`) also carry the new fields (they do, same `rankings_json`).
- **VALIDATE**: `npm test` (web-app) green; Unity EditMode green.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| RaceManager infinite mode | `TotalLaps=0`, car completes 5 laps | never `Finished`, no `OnRaceFinished` | Yes |
| RaceManager lap-limited unchanged | `TotalLaps=3`, car hits lap 3 | `Finished` fires once | No |
| CarIdentity.RecordLap | two laps at t=10, t=25 (start t=0) | Best=10, Accumulated=25, CompletedLaps=2 | No |
| ScoreManager.CollectResults | cars w/ known best/accum, raceTime=90 | ElapsedTime=90, AvgLap=accum/laps | No |
| CollectResults zero laps | car CompletedLaps=0 | AverageLapTime=0 (no divide-by-zero) | Yes |
| ResultsExporter CSV | RaceResults w/ 2 cars | header+rows incl. TotalTime/BestLap/AvgLap F3 | No |
| ResultsExporter empty | `Rankings=[]` | header-only string w/ new columns | Yes |
| internal race-results write | correct secret, linked room | 1 row in `race_results` | No |
| internal race-results auth | wrong / default secret | 403 / 503 | Yes |
| internal race-results unlinked | secret ok, no linked survey | `{success:true, skipped}`, no row | Yes |
| export-bundle w/ results | survey has `race_results` row | ZIP contains `leaderboard.csv` | No |
| export-bundle no results | survey has none | bundle omits `leaderboard.csv` (no crash) | Yes |
| buildResultsCsv (client) | rankings w/ time fields | columns + toFixed(3) match server | No |

### Edge Cases Checklist
- [x] Empty input (no cars / no responses / no results row)
- [x] Zero laps completed (average lap divide-by-zero)
- [x] Legacy rows without new time fields (fallback to `TotalTime`/0)
- [x] Invalid types — JsonUtility/JSON.parse guarded (existing `try/catch` in server + api)
- [x] Concurrent End/Save/Export sends — multiple `race_results` rows, newest shown first
- [x] Web-app unreachable from WS server — fire-and-forget `.catch`
- [x] Permission denied — shared-secret fail-closed (default/empty secret ⇒ 503)
- [x] Unlinked room (no `linked_room_code`) — skip write, no error

---

## Validation Commands

### Static Analysis
```bash
# Client lint (oxlint config present)
cd web-app/client && npx oxlint
```
EXPECT: Zero errors in changed files

### Unit Tests — Web-App
```bash
cd web-app && npm test
```
EXPECT: All Vitest suites pass (incl. export-bundle + results-archive)

### Unit Tests — Unity (EditMode)
```bash
# Via UnitySkills API (preferred) or Unity Test Runner
curl -s -X POST http://localhost:8090/tests/run -d '{"mode":"EditMode"}'
# CI fallback: game-ci/unity-test-runner@v4 (EditMode)
```
EXPECT: ResultsExporterTests, ScoreManagerTests, LapTrackerTests pass

### Build Verification (WebGL)
```bash
# Ensure the Unity project compiles for WebGL (BuildScript.cs)
curl -s -X POST http://localhost:8090/build -d '{"target":"WebGL"}'
```
EXPECT: Compile success, no errors from new fields/methods

### Manual Validation
- [ ] Set `RaceConfig.TotalLaps = 0`; start a host race; confirm it runs past lap 3 indefinitely.
- [ ] Click **End Race** → Unity `Finished`; web-app Results tab shows standings with Total/Best/Avg (ms).
- [ ] Click **Save** and **Export** → each also pushes a fresh results row to the Results tab.
- [ ] Close the room without ending → archive fallback still records the session.
- [ ] Editor "Download Data" → ZIP contains `leaderboard.csv` with matching columns.
- [ ] Per-session "Download CSV" in Results tab includes the new time columns.

---

## Acceptance Criteria
- [ ] `TotalLaps <= 0` runs infinitely; positive values keep lap-limited finish.
- [ ] End Race, Save, and Export each deliver the latest leaderboard to the web-app.
- [ ] Room-close archive still works as a fallback.
- [ ] Survey **Results** tab renders the latest standings with Rank, Team, Laps, Checkpoints,
      and millisecond Total / Best Lap / Avg Lap.
- [ ] **Download Data** ZIP contains `leaderboard.csv`.
- [ ] All validation commands pass; no type/lint errors.

## Completion Checklist
- [ ] Code follows discovered patterns (Unity PascalCase, Express shared-secret, JS RFC-4180 CSV)
- [ ] Error handling matches codebase style (fire-and-forget WS→web, fail-closed secret)
- [ ] Logging matches conventions (`[RaceManager]`/`[Room …]` prefixes)
- [ ] Tests follow existing NUnit/Vitest patterns
- [ ] No hardcoded values (sentinel via `TotalLaps`, secret via env)
- [ ] Scene wiring verified post-merge (no `{fileID:0}` on EndRaceButton)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no further codebase searching required

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scene serialized ref for EndRaceButton drops to `{fileID:0}` | Med | Med | Auto-wire fallback in `Start()`; verify inspector post-merge ([[scene-wiring-lags-merged-scripts]]) |
| Duplicate `race_results` rows from End+Save+Export | High | Low | Results tab shows newest-first; acceptable. Optional: upsert by room_code |
| WS server → web-app write fails silently | Low | Med | Fire-and-forget `.catch`; room-close archive remains as backstop |
| `AverageLapTime` divide-by-zero (0 laps) | Med | Low | Guard `CompletedLaps > 0` |
| CSV column drift between Unity/server/client | Med | Med | Single documented column order; tests assert header on all three |
| `RaceFinishPanel` NRE on null winner in EndRace | Med | Med | Only invoke `OnRaceFinished` when a leader exists / null-guard overlay |
| WebGL local Save/Export I/O differs from Editor | Low | Low | Send-to-web path is network, independent of local file I/O |

## Notes
- **Why WS server writes `race_results` (not Unity direct):** the Unity WebGL host has only a
  short-lived host token, not a professor JWT, and already relays everything through the WS server;
  the server already holds `INTERNAL_SECRET` and the `room_code → survey` link. This reuses the exact
  `/sessions/archive` trust boundary.
- **`ElapsedTime` is wall-clock-from-start** (equal across cars at a single snapshot). Per-car
  differentiation lives in **Best Lap** and **Avg Lap**. If future lap-limited races finish cars at
  different moments, `ElapsedTime` naturally diverges (snapshot per car), so the field is future-proof.
- **`TotalTime` (legacy `CheckpointTime`) is retained** as a hidden tiebreaker column for backward
  compatibility; the UI surfaces the new `ElapsedTime` as "Total".
- Related memory: [[scene-wiring-lags-merged-scripts]], [[unity-playmode-verification]].
