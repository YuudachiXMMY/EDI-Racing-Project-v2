# Plan: Student 2D View — Live Leaderboard + Clickable Car Status + Elliptical Minimap

## Summary
Enhance the existing **student 2D spectator view** (`web-app/client` React page `LiveRacePage`) so that: (1) the live **leaderboard** rows are **clickable**; (2) clicking a car (row or minimap dot) opens a **car-status panel** showing the car's **color, equipped functions, speed, ranking, laps finished** (plus checkpoints); (3) the `TrackMinimap` becomes a stable **elliptical map** that draws a **precise START marker** and **highlights** the selected car. Four of the five requested status fields (**color, equipped functions, ranking, laps**) already reach the browser today and only need web-side consumption; **speed** and **precise start / track geometry** are the only two that are not on the wire and get a small, additive upstream broadcast (with graceful client-side fallbacks so the web slice ships regardless).

## User Story
As a **student (or observer) watching the race in the browser 2D view**, I want to **click any car in the leaderboard or on the minimap and see its current status — color, equipped functions, speed, ranking, and laps finished — on an elliptical track map that shows the start line and highlights that car**, so that I can **follow a specific team and understand its situation without needing the Unity/professor screen**.

## Problem → Solution
- **Current**: `LiveRacePage` renders `LiveLeaderboard` (rank/name/lap/cp, read-only), `TrackMinimap` (a jittering scatter-plot of car dots colored by an *arbitrary* spawn-order palette, no track outline, no start point, no selection), and `LiveEventFeed`. The `useRaceWebSocket` hook already receives each car's `colorIndex` + `functions` inside `race_start.cars[].attrs` **but nothing reads them**. There is no selection state, no car-status panel, no way to see a single car's details, and no fixed track/start reference — the minimap re-fits a bounding box to live positions every frame.
  → **Desired**: a lifted `selectedTeamName` selection drives (a) a clickable/highlighted leaderboard, (b) a new `CarDetailPanel` reading color/functions/speed/rank/laps for the chosen car, and (c) an upgraded `TrackMinimap` with a **fixed** coordinate transform, a **stylized ellipse** outline, a **precise start marker**, real per-car colors, and a highlighted + click-selectable dot. Color/functions/rank/laps/cp are consumed from data already on the wire; **speed** and **track geometry (start + bounds)** are added as two small additive Unity broadcasts that ride the existing verbatim relay, each with a client-side approximation fallback.

## Metadata
- **Complexity**: Large
- **Source PRD**: N/A (free-form user request, `/ecc:prp-plan`)
- **PRD Phase**: N/A
- **Predecessors (completed)**: `student-2d-wiring.plan.md`, `live-race-viewer.plan.md`, `student-auto-camera-and-leaderboard-follow.plan.md`, `infinite-race-leaderboard-export.plan.md`, `race-results-to-web-app.plan.md`
- **Estimated Files**: ~15 (7 web-app: 4 UPDATE + 3 CREATE; 4 Unity UPDATE; 1 Unity test; 2 web/server test; 1 CSS)
- **Confidence**: 8/10 for single-pass implementation of the web slice; the two upstream fields require a Unity WebGL rebuild + redeploy to verify end-to-end in prod.

---

## UX Design

### Before
```
Student 2D view  (/#/live/:roomCode) — LiveRacePage, .live-grid
┌───────────────────────────┬──────────────────────────────┐
│  LEADERBOARD (read-only)  │  TRACK MINIMAP                │
│  # Team      Lap  CP      │   • dots only (wrong colors,  │
│  1 Red Team   3   14      │     arbitrary palette)        │
│  2 Blue Team  3   13      │   • NO track outline          │
│  3 Green ...  2   12      │   • NO start point            │
│  (rows do nothing)        │   • re-scales every frame     │
├───────────────────────────┴──────────────────────────────┤
│  EVENT FEED                                               │
└──────────────────────────────────────────────────────────┘
No way to inspect a single car. color/functions data arrives but is ignored.
```

### After
```
Student 2D view — LiveRacePage, .live-grid
┌───────────────────────────┬──────────────────────────────┐
│  LEADERBOARD (clickable)  │  ELLIPTICAL MINIMAP           │
│  # Team      Lap  CP      │      ____________             │
│ ▸1 Red Team★  3   14  ◀sel│    /      ●B      \   ★=START │
│  2 Blue Team  3   13      │   |   ★         ●R◎ | ◎=select│
│  3 Green ...  2   12      │    \____●G________/  real     │
│  (click → select a car)   │   fixed frame, no jitter      │
├───────────────────────────┼──────────────────────────────┤
│  CAR STATUS — Red Team           │  EVENT FEED             │
│  ■ Red    Rank 1   Lap 3  CP 14  │  ...                    │
│  Fn: Facial · Password           │                         │
│  Speed 12.4 u/s (live)           │                         │
└──────────────────────────────────┴─────────────────────────┘
Click a row OR a dot → same car selected everywhere.
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Leaderboard row | Static `<tr>` | `onClick` → `onSelect(entry.name)`; `.selected` highlight; pointer cursor | Rows have no car index — select by **team name** |
| Minimap dot | Fixed palette, no interaction | Real color by `colorIndex`; selected dot ringed/enlarged; **click a dot** → select | Canvas → manual hit-testing |
| Minimap frame | Per-frame bounding box (jitters) | **Fixed** transform from broadcast bounds; **ellipse** outline; **START** marker | Needs `track_geometry` (or fallback) |
| Car status | None | New `CarDetailPanel`: color swatch+name, function chips, speed, rank, laps, cp | Read-only |
| Speed | Not shown / not on wire | Shown; authoritative `s` if Unity rebuilt, else client-side Δpos/Δt (labeled "approx") | Additive `CarNetState.s` |
| Page role | Spectator-only | **Still** spectator-only (no host/event controls) | PRD security property preserved |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/client/src/pages/LiveRacePage.jsx` | 17–71 | Owns hook state + `.live-grid`; where `selectedTeamName` state + `CarDetailPanel` slot in |
| P0 | `web-app/client/src/components/LiveLeaderboard.jsx` | 1–26 | Rankings table; add row `onClick` + `.selected` |
| P0 | `web-app/client/src/components/TrackMinimap.jsx` | 1–68 | The whole minimap draw; ellipse + start + real color + highlight + click go here |
| P0 | `web-app/client/src/hooks/useRaceWebSocket.js` | 38–95 | Parses `race_start`/`state_update`/`leaderboard`; add `track_geometry` case; `cars` already carries attrs |
| P0 | `Assets/Scripts/Network/NetworkMessages.cs` | 81–204 | Authoritative wire schema: `NetCarData`/`NetAttribute`, `CarNetState`, `LeaderboardEntry` |
| P0 | `Assets/Scripts/Network/NetworkSync.cs` | 117–171 | Host broadcasts: `BroadcastStateUpdate`, `BroadcastLeaderboard`, `BroadcastRaceStart` |
| P1 | `Assets/Scripts/Data/CarData.cs` | 82–96 | The exact `ColorIndex`/`Functions` attr contract to mirror web-side |
| P1 | `Assets/Scripts/Race/CarSpawner.cs` | 201–212 | `GetTrailColor(colorIndex)` — canonical colorIndex→RGB palette to mirror |
| P1 | `Assets/Scripts/Events/EventActionBuilder.cs` | 35–52 | Function tag→label + color label→index maps |
| P1 | `Server/server.js` | 580–598, 662–725 | `web_join_room` replay + `WEBAPP_RELAY_TYPES`; verbatim relay |
| P1 | `web-app/client/src/constants.js` | 1–34 | `// Must match Unity` mirror convention (WeatherType); where CAR_COLORS/FUNCTION_LABELS go |
| P1 | `web-app/client/src/index.css` | 192, 226–228, 259–284 | `.response-row:hover`, `.rank-1/2/3`, `.live-grid` + Live-Race tokens to reuse |
| P1 | `Assets/Scripts/Car/CarController.cs` | 314–326, 433–453 | `NavMeshAgent` + `BaseSpeed` accessor; add `CurrentSpeed` |
| P1 | `Assets/Scripts/Race/RaceManager.cs` | ~122 | `LoadAndStartRace` calls `BroadcastRaceStart`; add `BroadcastTrackGeometry` next to it |
| P1 | `web-app/__tests__/adversarial-ws.test.js` | 42–103, 317–402 | Spawn-real-server WS harness + `web_join_room` shapes to mirror |
| P1 | `web-app/__tests__/postProcessing.test.js` | 1–49 | Pure-unit style (`test_*` names, Arrange/Act/Assert) for `carStatus.test.js` |
| P2 | `Assets/Tests/EditMode/NetworkMessagesTests.cs` | 182–201, 341–349 | JsonUtility round-trip + enum/int guard style |
| P2 | `Assets/Scripts/Race/WaypointPath.cs` | 9–16 | `Waypoints[]` (Catmull-Rom loop) — source for optional real outline/bounds |
| P2 | `Assets/Scripts/Race/CheckpointTrigger.cs` | 10–27 | Start/finish line == `CheckpointIndex == 0` collider — precise start source |

## External Documentation
No external research needed — feature uses established internal patterns (React 19 functional components + hooks, HTML5 Canvas 2D, `ws` relay, Unity `JsonUtility` wire structs, Vitest Node env, NUnit EditMode).

---

## Patterns to Mirror

Actual codebase snippets discovered during recon. Follow these exactly.

### WIRE_SCHEMA_POSITIONS (no speed today)
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:162-170
[Serializable]
public struct CarNetState {
    public int i;   // index in spawn order (matches cars[] order)
    public float px, py, pz; // position
    public float ry; // y-axis rotation
    public int l;   // current lap
    public int c;   // total checkpoints passed
}
// state_update relays at ~10Hz. ADD `public float s;` here for authoritative speed.
```

### WIRE_SCHEMA_ROSTER (color + functions already ride here)
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:96-122
[Serializable] public struct NetAttribute { public string k; public string v; }
[Serializable] public struct NetCarData {
    public string teamName;
    public NetAttribute[] attrs;
    public static NetCarData FromCarData(CarData cd) {
        // ...copies EVERY CarData.Attribute k/v verbatim...
        return new NetCarData { teamName = cd.TeamName, attrs = netAttrs };
    }
}
// On the wire: cars:[{teamName:"Red", attrs:[{k:"colorIndex",v:"2"},{k:"functions",v:"facerecog/password"}]}]
```

### CARDATA_ACCESSOR_CONTRACT (mirror this parse web-side)
```csharp
// SOURCE: Assets/Scripts/Data/CarData.cs:85-95
public int ColorIndex => GetIntAttribute("colorIndex", 0);
public string[] Functions {
  get {
    string val = GetAttribute("functions", "");
    if (string.IsNullOrEmpty(val)) return Array.Empty<string>();
    return val.Split('/').Select(f => f.Trim().ToLower()).Where(f => f.Length > 0).ToArray();
  }
}
// Web must read: colorIndex = int(attrs.find(a=>a.k==='colorIndex')?.v ?? 0);
//                functions  = (attrs.find(a=>a.k==='functions')?.v || '').split('/')...
```

### CANONICAL_COLOR_PALETTE (mirror to hex in constants.js)
```csharp
// SOURCE: Assets/Scripts/Race/CarSpawner.cs:201-212
switch (colorIndex) {
  case 0: return new Color(0.2f,0.8f,0.2f); // green  -> #33cc33
  case 1: return new Color(0.3f,0.3f,0.3f); // black  -> #4d4d4d
  case 2: return new Color(0.9f,0.2f,0.2f); // red    -> #e63333
  case 3: return new Color(0.2f,0.4f,0.9f); // blue   -> #3366e6
  case 4: return new Color(0.9f,0.9f,0.9f); // white  -> #e6e6e6
  default: return Color.white;
}
// EventActionBuilder.Colors label map: Green=0, Black=1, Red=2, Blue=3, White=4.
```

### FUNCTION_LABEL_MAP
```csharp
// SOURCE: Assets/Scripts/Events/EventActionBuilder.cs:35-52
public static readonly (string Label, string Tag)[] Functions = {
  ("Facial","facerecog"), ("Glasses","glasses"), ("Language","language"),
  ("Password","password"), ("Distance","distance"),
};  // plus "male" (EventActionBuilder.Male) is a valid tag
```

### HOST_BROADCAST_PATTERN (where speed + track geometry are populated/added)
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:122-141 (BroadcastStateUpdate)
var states = new CarNetState[cars.Count];
for (int i = 0; i < cars.Count; i++) {
    if (cars[i] == null) continue;
    var t = cars[i].transform;
    var id = cars[i].GetComponent<CarIdentity>();
    states[i] = new CarNetState {
        i = i, px = t.position.x, py = t.position.y, pz = t.position.z,
        ry = t.eulerAngles.y,
        l = id != null ? id.CurrentLap : 0,
        c = id != null ? id.TotalCheckpointsPassed : 0
        // ADD: s = cars[i].GetComponent<CarController>()?.CurrentSpeed ?? 0f
    };
}
```

### LEADERBOARD_BROADCAST (rank/name/lap/cp; capped 15; no index)
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:143-161
int count = Mathf.Min(ranked.Count, 15);
msg.rankings = new LeaderboardEntry[count];
for (int i = 0; i < count; i++)
  msg.rankings[i] = new LeaderboardEntry {
      rank = i + 1, name = ranked[i].TeamName,
      lap = ranked[i].CurrentLap, cp = ranked[i].TotalCheckpointsPassed };
// NO car index -> join a clicked row to a dot by teamName. >15 cars: tail cars have no row.
```

### HOOK_STATE_STORE (attrs already stored; nothing reads them)
```javascript
// SOURCE: web-app/client/src/hooks/useRaceWebSocket.js:45-55, 94
case 'race_start':  setGamePhase('Racing'); setCars(msg.cars || []); break;   // cars[i]={teamName,attrs}
case 'state_update': setPositions(msg.cars || []); setRaceTime(msg.t || 0); break; // positions[]={i,px,py,pz,ry,l,c}
case 'leaderboard': setLeaderboard(msg.rankings || []); break;
// ...
return { connected, gamePhase, cars, positions, leaderboard, events, raceTime };
// ADD: case 'track_geometry': setTrackGeometry(msg); + return trackGeometry
```

### MINIMAP_DRAW_LOOP (replace bbox + palette; add ellipse/start/highlight/click)
```javascript
// SOURCE: web-app/client/src/components/TrackMinimap.jsx:22-58
let minX=Infinity,maxX=-Infinity,minZ=Infinity,maxZ=-Infinity;
for (const p of positions){ /* per-frame bbox -> REPLACE with fixed transform */ }
const scale = Math.min((w-PADDING*2)/rangeX,(h-PADDING*2)/rangeZ);
ctx.fillStyle='#14141f'; ctx.fillRect(0,0,w,h);
for (const p of positions){
  const x = offsetX + (p.px-minX)*scale;
  const y = offsetZ + (p.pz-minZ)*scale;          // NOTE: no vertical flip today
  const color = COLORS[p.i % COLORS.length];       // <-- WRONG: arbitrary palette
  ctx.beginPath(); ctx.arc(x,y,DOT_RADIUS,0,Math.PI*2); ctx.fillStyle=color; ctx.fill();
  const carName = cars && cars[p.i] ? cars[p.i].teamName : `#${p.i+1}`;
  ctx.fillText(carName, x, y-DOT_RADIUS-3);
}
```

### LEADERBOARD_ROW (add onClick + selected class)
```javascript
// SOURCE: web-app/client/src/components/LiveLeaderboard.jsx:14-21
{rankings.map((entry, i) => (
  <tr key={i} className="response-row">      {/* ADD onClick + `${sel?' selected':''}` */}
    <td className={entry.rank <= 3 ? `rank-${entry.rank}` : ''}>{entry.rank}</td>
    <td>{entry.name}</td><td>{entry.lap}</td><td>{entry.cp}</td>
  </tr>
))}
```

### CONSTANTS_MIRROR_CONVENTION
```javascript
// SOURCE: web-app/client/src/constants.js:22-24 (WeatherType/WeatherTypeLabels)
// Must match Unity C# enum values exactly
export const WeatherType = { None: 0, Snow: 1, Night: 2, Sunset: 3 };
// -> ADD CAR_COLORS / CAR_COLOR_NAMES / FUNCTION_LABELS in the same style
```

### SERVER_RELAY_WHITELIST + LATE-JOINER REPLAY
```javascript
// SOURCE: Server/server.js:~690-725 (relay) and 587-596 (replay)
const WEBAPP_RELAY_TYPES = ['state_update','leaderboard','game_state',
  'event_triggered','race_start','race_end','race_results'];   // ADD 'track_geometry'
// web_join_room replay (587-596): sendRaceStartTo -> latestState -> latestLeaderboard -> latestConfig
// ADD: cache room.latestTrackGeometry on receipt + replay it after latestConfig.
```

### WEB_WS_TEST_HARNESS (integration, spawn real server — no mocks)
```javascript
// SOURCE: web-app/__tests__/adversarial-ws.test.js:42-103
function makeClient(){ const ws=new WebSocket(WS_URL); const inbox=[]; const waiters=[];
  ws.on('message',(d)=>{ const m=JSON.parse(d.toString()); inbox.push(m);
    for(let i=waiters.length-1;i>=0;i--){ if(waiters[i].pred(m)){waiters[i].resolve(m); waiters.splice(i,1);} } });
  return { ws, ready:()=>..., send:(o)=>ws.send(JSON.stringify(o)),
    next(pred,timeoutMs=1500){ /* positive assertion */ },
    collect(ms){ /* negative window */ }, close:()=>ws.close() }; }
// beforeAll spawns REAL relay: spawn('node',[SERVER_PATH],{env:{PORT, REQUIRE_HOST_TOKEN:'true', ...}})
```

### UNITY_WIRE_TEST (JsonUtility round-trip + enum guard)
```csharp
// SOURCE: Assets/Tests/EditMode/NetworkMessagesTests.cs:182-201, 341-349
[Test] public void StateUpdateMessage_JsonRoundTrip_PreservesCarStates() {
    var original = new StateUpdateMessage { t = 45.5f,
        cars = new[]{ new CarNetState{ i=0, px=1f, py=2f, pz=3f, ry=90f, l=2, c=5 } } };
    string json = JsonUtility.ToJson(original);
    var restored = JsonUtility.FromJson<StateUpdateMessage>(json);
    Assert.AreEqual(90f, restored.cars[0].ry); Assert.AreEqual(2, restored.cars[0].l);
}
// Global namespace (rootNamespace empty), [Test] Method_Scenario_Expected, no MonoBehaviour lifecycle.
```

### PURE_UNIT_TEST_STYLE (for carStatus.test.js — Node env, no jsdom)
```javascript
// SOURCE: web-app/__tests__/postProcessing.test.js:1-49
import { applyPostProcessing } from '../src/routes/export.js';
describe('post-processing', () => {
  it('test_average_gt_only_tags_strictly_above_mean', () => {
    // Arrange ... / Act ... / Assert ...
  });
});
```

---

## Files to Change

### Upstream (Unity + relay server + Unity test) — enables authoritative speed + precise start
| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Car/CarController.cs` | UPDATE | Add `public float CurrentSpeed => agent != null ? agent.velocity.magnitude : 0f;` (mirror `BaseSpeed`) — only host source of live speed |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add `public float s;` to `CarNetState`; add `[Serializable] TrackGeometryMessage` (start xz + bounds + optional waypoint polyline) |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Populate `s` in `BroadcastStateUpdate`; add host-gated `BroadcastTrackGeometry()` |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Call `NetworkSync.BroadcastTrackGeometry()` next to `BroadcastRaceStart(carDataList)` in `LoadAndStartRace` (~line 122) |
| `Assets/Tests/EditMode/NetworkMessagesTests.cs` | UPDATE | JsonUtility round-trip: `CarNetState.s` survives; `TrackGeometryMessage` round-trips (+ ship its `.meta` if a new file) |
| `Server/server.js` | UPDATE | Add `'track_geometry'` to `WEBAPP_RELAY_TYPES`; cache `room.latestTrackGeometry`; replay it in `web_join_room`. Speed needs **no** server change |
| `web-app/__tests__/adversarial-ws.test.js` | UPDATE | Relay + replay test for `track_geometry` via the spawn-real-server harness |

### Web client (React) — the actual feature
| File | Action | Justification |
|---|---|---|
| `web-app/client/src/constants.js` | UPDATE | Add `CAR_COLORS`, `CAR_COLOR_NAMES`, `FUNCTION_LABELS` mirrors |
| `web-app/client/src/lib/carStatus.js` | CREATE | Pure (DOM-free) selectors: `parseCarAttrs`, `resolveSelectedCar`, `deriveSpeed`, `normalize`/`fitEllipse` |
| `web-app/__tests__/carStatus.test.js` | CREATE | Unit tests for `carStatus.js` in the existing Node Vitest env |
| `web-app/client/src/hooks/useRaceWebSocket.js` | UPDATE | Add `track_geometry` case + return `trackGeometry`; (optional) `prevPositions` ref for the speed fallback |
| `web-app/client/src/pages/LiveRacePage.jsx` | UPDATE | Lift `selectedTeamName` state; destructure `trackGeometry`; pass selection down; render `CarDetailPanel` |
| `web-app/client/src/components/LiveLeaderboard.jsx` | UPDATE | Clickable rows (`onSelect(entry.name)`) + `.selected` |
| `web-app/client/src/components/TrackMinimap.jsx` | UPDATE | Real colors, fixed transform, ellipse, start marker, highlight, click hit-testing |
| `web-app/client/src/components/CarDetailPanel.jsx` | CREATE | The car-status panel (color/functions/speed/rank/laps/cp) |
| `web-app/client/src/index.css` | UPDATE | `.response-row.selected`, clickable cursor, `.car-detail-panel`/swatch/chip styles, grid slot |

## NOT Building
- No changes to the **Unity in-game WebGL HUD** or the student's own-car view — only additive network broadcast fields.
- No **live re-broadcast** of mutated per-car `functions` after event actions — the panel shows the **race_start loadout** (labeled as such).
- No **host/professor or event-trigger controls** on the 2D page — it stays **spectator-only** (PRD security property verified by `student-2d-wiring`).
- No parsing of `race_results.resultsJson` for richer final stats — panel uses live roster/positions/leaderboard.
- No **weather** in the panel — `weather_state` is not in `WEBAPP_RELAY_TYPES`.
- No new dedicated color/functions wire fields on `race_start` (they already ride in `attrs`); no `colorHex`/`colorName` resolved upstream — the JS palette mirror is used.
- No change to the **top-15 leaderboard cap** or to `LeaderboardEntry` (no car index added) — selection joins by `teamName`.
- No `jsdom`/React-Testing-Library component-test infra unless separately approved (see Task 15).

---

## Step-by-Step Tasks

> **Ordering rule**: upstream data-contract changes (Tasks 1–6) land before the web UI that consumes them (Tasks 7–14), so the web layer can be validated against real frames. The web layer degrades gracefully if the Unity rebuild is not yet deployed.

### Task 1: Unity — expose `CurrentSpeed` on `CarController`
- **ACTION**: Add a public read-only live-speed accessor.
- **IMPLEMENT**: In `Assets/Scripts/Car/CarController.cs`, beside `public float BaseSpeed => baseSpeed;` (~line 453), add `public float CurrentSpeed => agent != null ? agent.velocity.magnitude : 0f;`.
- **MIRROR**: `CarController.cs:453` (`BaseSpeed` accessor); `agent` is the `NavMeshAgent` used in `ApplyCompositSpeed` (314–326).
- **IMPORTS**: none (UnityEngine already imported).
- **GOTCHA**: Use `agent.velocity.magnitude` (true ground speed incl. event modifiers), **not** `agent.speed` (target) and **not** transform deltas.
- **VALIDATE**: Unity compiles; no other call sites affected.

### Task 2: Unity — add `s` to `CarNetState` and populate it
- **ACTION**: Add speed to the per-car wire frame and set it in the broadcast.
- **IMPLEMENT**: In `NetworkMessages.cs` add `public float s;` to `struct CarNetState` (~162–170). In `NetworkSync.cs` `BroadcastStateUpdate` (122–141) set `s = cars[i].GetComponent<CarController>()?.CurrentSpeed ?? 0f` (fetch alongside the existing `CarIdentity`).
- **MIRROR**: `HOST_BROADCAST_PATTERN` (NetworkSync.cs:122–141).
- **GOTCHA**: Keep the compact single-letter key convention (`i/px/py/pz/ry/l/c/s`). `state_update` is already in `WEBAPP_RELAY_TYPES`, so `s` **auto-relays** with no `server.js` change.
- **VALIDATE**: Task 4 round-trip test; wire JSON shows a numeric `s` per car.

### Task 3: Unity — `TrackGeometryMessage` + `BroadcastTrackGeometry()` + call at race start
- **ACTION**: Send the start point + track bounds (and optional outline) once at race start.
- **IMPLEMENT**:
  - `NetworkMessages.cs`: `[Serializable] class TrackGeometryMessage { public string type = "track_geometry"; public float startX, startZ, minX, maxX, minZ, maxZ; public float[] wpx = Array.Empty<float>(); public float[] wpz = Array.Empty<float>(); }`.
  - `NetworkSync.cs`: add host-gated `BroadcastTrackGeometry()` reading the `CheckpointTrigger` whose `CheckpointIndex == 0` (true start/finish; fall back to `CarSpawner.SpawnPoint.position`) for `startX/startZ`, and `WaypointPath.Waypoints[]` world `xz` for `minX/maxX/minZ/maxZ` + `wpx/wpz`.
  - `RaceManager.cs`: call it immediately after `NetworkSync.BroadcastRaceStart(carDataList)` in `LoadAndStartRace` (~line 122).
- **MIRROR**: `BroadcastRaceStart` (NetworkSync.cs:163–171); `RaceManager.cs:122`.
- **GOTCHA**: `JsonUtility` can't serialize `Vector3[]` across this contract — **flatten** to parallel `float[] wpx/wpz`. If `WaypointPath` is absent, still send start + bounds and leave the polyline empty (web falls back to a drawn ellipse). Broadcast is host + `Racing` gated like the other sends.
- **VALIDATE**: Task 4 round-trip passes; one `track_geometry` JSON emitted per race start.

### Task 4: Unity — EditMode tests for the new wire fields
- **ACTION**: Guard the two new wire shapes.
- **IMPLEMENT**: In `Assets/Tests/EditMode/NetworkMessagesTests.cs` add: (a) `CarNetState` with `s=12.5f` survives `JsonUtility.ToJson`/`FromJson`; (b) `TrackGeometryMessage` round-trips `startX/startZ/min*/max*/wpx/wpz`.
- **MIRROR**: `UNITY_WIRE_TEST` (NetworkMessagesTests.cs:182–201).
- **GOTCHA**: Global namespace (rootNamespace empty), `[Test] Method_Scenario_Expected`, no MonoBehaviour lifecycle. Any **new** C# test file must include its generated `.meta` (note the repo already has untracked `EventActionBuilderTests.cs.meta` — don't repeat that).
- **VALIDATE**: Run the `Tests.EditMode` asmdef suite — new tests green.

### Task 5: Server — relay + cache + replay `track_geometry`
- **ACTION**: Make the one-time geometry reach live **and** late-joining web viewers.
- **IMPLEMENT**: In `Server/server.js`: add `'track_geometry'` to `WEBAPP_RELAY_TYPES` (~719); where `latestState`/`latestLeaderboard` are cached (~666–698) add `room.latestTrackGeometry = raw` when `msg.type === 'track_geometry'`; in the `web_join_room` replay block add `if (webRoom.latestTrackGeometry) ws.send(webRoom.latestTrackGeometry);` after the `latestConfig` send (line 595); init `latestTrackGeometry: null` in the room factory (~446–453).
- **MIRROR**: `SERVER_RELAY_WHITELIST + LATE-JOINER REPLAY` (server.js:587–596, 690–725).
- **GOTCHA**: Both the live whitelist **and** the replay must be updated or reconnecting/late viewers miss the one-time geometry. Relay is **verbatim** (`send(raw)`) — do not reshape. Speed needs no server change.
- **VALIDATE**: Task 6 integration test confirms live + replay.

### Task 6: Server — relay integration test for `track_geometry`
- **ACTION**: Prove a web viewer receives geometry live and on join-replay.
- **IMPLEMENT**: In `web-app/__tests__/adversarial-ws.test.js` add a case: spawn the real server, connect a host + a `web_join_room` viewer, host sends a `track_geometry` frame → assert viewer gets it (`next(m=>m.type==='track_geometry')`); then connect a second late viewer → assert it receives the cached geometry on join.
- **MIRROR**: `WEB_WS_TEST_HARNESS` (adversarial-ws.test.js:42–103, 317–402).
- **IMPORTS**: reuse `makeClient()/ready()/send()/next()/collect()`; helpers live in `test-helpers.js` (never `*.test.js`).
- **GOTCHA**: `next()` for positive assertions, `collect()` for negative windows.
- **VALIDATE**: `npx vitest run` in `web-app/` passes the new case.

### Task 7: Web — color/function constant mirrors
- **ACTION**: Add the Unity→web palette + label maps.
- **IMPLEMENT**: In `web-app/client/src/constants.js` append:
  ```javascript
  // Must match Unity CarSpawner.GetTrailColor (colorIndex 0-4)
  export const CAR_COLORS = ['#33cc33', '#4d4d4d', '#e63333', '#3366e6', '#e6e6e6'];
  export const CAR_COLOR_NAMES = ['Green', 'Black', 'Red', 'Blue', 'White'];
  // Must match Unity EventActionBuilder.Functions tag->label
  export const FUNCTION_LABELS = { facerecog: 'Facial', glasses: 'Glasses', language: 'Language', password: 'Password', distance: 'Distance', male: 'Male' };
  ```
- **MIRROR**: `CONSTANTS_MIRROR_CONVENTION` (constants.js:22–24).
- **GOTCHA**: Do **not** reuse `TrackMinimap`'s existing `COLORS` array — it's an unrelated spawn-order palette. `colorIndex` is an integer index, not a hex. **VALIDATION NOTE**: `GetTrailColor` is the *trail* color; if the car *body* prefab color differs, confirm the prefab order matches these five before shipping (see Risks).
- **VALIDATE**: Imports resolve; values match `CarSpawner.cs:201–212` order.

### Task 8: Web — pure `carStatus` util + unit tests
- **ACTION**: All non-DOM logic in one testable module.
- **IMPLEMENT**: Create `web-app/client/src/lib/carStatus.js` exporting:
  - `parseCarAttrs(car)` → `{ colorIndex: int(attr 'colorIndex' ?? 0), functions: (attr 'functions' || '').split('/').map(s=>s.trim().toLowerCase()).filter(Boolean) }` — read `car.attrs.find(a=>a.k==='colorIndex')?.v` (attrs is an **array of {k,v}**, not an object).
  - `resolveSelectedCar(selectedTeamName, cars, positions, leaderboard)` → merged view-model `{ index, teamName, colorIndex, functions, rank, lap, cp, px, pz, ry, speed }` joining `leaderboard.name === cars[i].teamName` and `positions.find(p=>p.i===index)`.
  - `deriveSpeed(prevPos, curPos, dt)` → `hypot(dpx,dpz)/dt` fallback (undefined on frame 1).
  - minimap helpers `normalize(px, pz, transform)` (apply `pz→y` flip) + `fitEllipse(bounds)`.
  - Create `web-app/__tests__/carStatus.test.js` covering: attr parse incl. **missing** attrs; teamName join; a >15-car car with **no** leaderboard row → `rank: null`; speed derivation.
- **MIRROR**: `PURE_UNIT_TEST_STYLE` (postProcessing.test.js:1–49) — `test_*` names, Arrange/Act/Assert.
- **GOTCHA**: Keep the file **DOM-free** so it runs in the existing Node Vitest env (no jsdom). Join by `teamName` (both originate from the same `CarSpawner` name — compare as-is, first-match semantics per `CarLookup`). Leaderboard cap 15 → return `rank: null` for tail cars.
- **VALIDATE**: `npx vitest run` passes `carStatus.test.js`.

### Task 9: Web — hook handles `track_geometry` (+ optional speed fallback)
- **ACTION**: Surface geometry to the minimap; keep speed working without a rebuild.
- **IMPLEMENT**: In `useRaceWebSocket.js` add `trackGeometry` state, `case 'track_geometry': setTrackGeometry(msg);`, and include `trackGeometry` in the returned object. `positions` already carry the new `s` (via `setPositions(msg.cars)`), so no change there. If the speed fallback is wanted, keep a `prevPositions` ref keyed by `i` and expose derived speed only when `p.s` is absent.
- **MIRROR**: `HOOK_STATE_STORE` (useRaceWebSocket.js:45–55, 94).
- **GOTCHA**: On reconnect the server re-replays `race_start` + `latestState` + `latestLeaderboard` + `latestTrackGeometry`; tolerate `positions` momentarily resetting (auto-reconnect up to 5× / 3s). The event feed is **not** replayed (unrelated here).
- **VALIDATE**: Component receives `trackGeometry`; when Unity sends `s`, `positions[i].s` is numeric.

### Task 10: Web — lift selection state in `LiveRacePage` + render `CarDetailPanel`
- **ACTION**: One selection source drives leaderboard, minimap, and panel.
- **IMPLEMENT**: In `LiveRacePage.jsx` add `const [selectedTeamName, setSelectedTeamName] = useState(null);`; destructure `trackGeometry` from the hook; pass `selectedTeamName` + `onSelect={setSelectedTeamName}` to `LiveLeaderboard` and `TrackMinimap` (and `trackGeometry` to the minimap); render `<CarDetailPanel selectedTeamName cars positions leaderboard />` inside the `.live-grid` block (62–68).
- **MIRROR**: `LiveRacePage.jsx:17–19` (hook destructure), 62–68 (`.live-grid`).
- **GOTCHA**: `cars`/`positions` are empty in Setup; the grid only renders in Racing/Paused/Finished. Guard against a `selectedTeamName` that no longer resolves after `race_end`. Optional: clicking the same row/dot again clears selection.
- **VALIDATE**: Clicking a row highlights it and populates the panel; page still renders only in Racing/Paused/Finished.

### Task 11: Web — clickable leaderboard rows
- **ACTION**: Select a car from the table.
- **IMPLEMENT**: In `LiveLeaderboard.jsx` accept `{ rankings, selectedTeamName, onSelect }`; on the `<tr>` set `className={`response-row${entry.name === selectedTeamName ? ' selected' : ''}`}` and `onClick={() => onSelect(entry.name)}`.
- **MIRROR**: `LEADERBOARD_ROW` (LiveLeaderboard.jsx:14–21).
- **GOTCHA**: Rows have no car index — the selection key is `entry.name` (consistent with the `carStatus` join). Names aren't guaranteed unique; first-match is acceptable.
- **VALIDATE**: Clicking a row adds `.selected` styling and drives the panel + minimap highlight.

### Task 12: Web — upgrade `TrackMinimap` (real colors, fixed transform, ellipse, start, highlight, click)
- **ACTION**: Turn the scatter plot into a stable elliptical map with a start marker and selectable/highlighted dots in real colors.
- **IMPLEMENT**: Accept `{ positions, cars, selectedTeamName, trackGeometry }`. Inside the `useEffect`:
  1. Build a **fixed** transform from `trackGeometry` bounds (`minX/maxX/minZ/maxZ`) instead of the per-frame bbox; apply `pz→y` flip (`y = h - ...`) via the shared `normalize()` from `carStatus.js`.
  2. Draw the **track outline** — a fitted **ellipse** (`fitEllipse(bounds)`, honoring the "椭圆地图" request); if `trackGeometry.wpx/wpz` is present, optionally stroke the waypoint polyline instead for accuracy.
  3. Draw the **START** marker at `(startX, startZ)`.
  4. Color each dot via `CAR_COLORS[parseCarAttrs(cars[p.i]).colorIndex]` (fallback to spawn-order palette if `colorIndex` absent).
  5. **Ring/enlarge** the dot whose `cars[p.i].teamName === selectedTeamName`.
  6. Record each dot's canvas `x/y` in a ref and add an `onClick` that hit-tests click coords (nearest dot within `DOT_RADIUS`) → `onSelect(cars[p.i].teamName)`.
  7. DPR-scale the canvas for crisp rendering.
  Add `selectedTeamName` + `trackGeometry` to the effect deps.
- **MIRROR**: `MINIMAP_DRAW_LOOP` (TrackMinimap.jsx:22–59).
- **GOTCHA**: Canvas has **no DOM nodes** — clicks need manual hit-testing. Everything drawn (highlight/start/ellipse) must be **inside the same useEffect** or it's erased on the next ~10Hz frame. If `trackGeometry` is absent (Unity not yet rebuilt), fall back to **accumulated** (not per-frame) bounds + the first-post-`race_start` cluster centroid as start, and label the map **approximate**.
- **VALIDATE**: Dots show true colors; the map/start no longer drift; the selected car is ringed; clicking a dot selects it.

### Task 13: Web — `CarDetailPanel` component
- **ACTION**: Render the selected car's full status.
- **IMPLEMENT**: Create `web-app/client/src/components/CarDetailPanel.jsx` (functional, destructured props) calling `resolveSelectedCar(selectedTeamName, cars, positions, leaderboard)` and rendering: color swatch (`CAR_COLORS[colorIndex]`) + `CAR_COLOR_NAMES[colorIndex]`; equipped-function chips (`FUNCTION_LABELS[tag] || tag`) or "No functions"; speed (1 decimal + unit, or "n/a"); rank (or "—" outside top 15); laps; checkpoints. Render `null` (or an empty hint) when `selectedTeamName` is null.
- **MIRROR**: `LiveLeaderboard.jsx` component shape; `index.css:259–284` (Live-Race tokens).
- **GOTCHA**: Label functions as the **initial loadout** (they can go stale after events); label speed **approximate** if client-derived. Reuse CSS vars (`--bg-card`, `--accent`, `--text-dim`, `--border`) and `.rank-1/2/3`. No weather.
- **VALIDATE**: Selecting each car shows correct color/name, chips, rank, laps; missing-data fallbacks render cleanly.

### Task 14: Web — styles for selection + detail panel
- **ACTION**: Add the visual affordances.
- **IMPLEMENT**: In `index.css` add: `.response-row { cursor: pointer; }` (or a scoped clickable class) + `.response-row.selected { ... }` next to the existing `:hover` (line 192) using `--accent`; a grid slot/overlay for `.car-detail-panel` in `.live-grid` (line 273); `.car-detail-panel`, `.color-swatch`, `.function-chip` styles reusing the tokens.
- **MIRROR**: `index.css:192, 226–228, 259–284`.
- **GOTCHA**: Flat hand-written classNames + one `index.css` (no CSS modules / Tailwind). `.live-grid` is 2col/2row — give the panel its own cell or an overlay.
- **VALIDATE**: Selected row and panel are visually distinct and consistent with the existing Live-Race styling.

### Task 15: (Optional, approval-gated) Web component/hook test infra
- **ACTION**: Only if UI-layer tests are required.
- **IMPLEMENT**: Per `technical-preferences.md` "Allowed Libraries", flag the new-dependency decision and, on approval, add `@vitejs/plugin-react` + `jsdom` + `@testing-library/react` + `@testing-library/jest-dom` to `web-app/client` and a client-scoped `vitest.config` with `test.environment='jsdom'`; add a smoke test (e.g., `CarDetailPanel` renders selected status).
- **MIRROR**: `web-app/client/vite.config.js:1–20` (reuse the `react()` plugin).
- **GOTCHA**: `web-app/client/package.json` has **zero** test deps and `web-app/vitest.config.js` is Node-env only — this is **net-new infra requiring explicit approval**. Do not assume jsdom/RTL exist. Maximize coverage via the DOM-free `carStatus` util (Task 8) in the existing Node env to avoid blocking on this.
- **VALIDATE**: If added, the smoke component test passes under jsdom.

---

## Testing Strategy

### Unit / Contract Tests
| Test | Input | Expected | Layer |
|---|---|---|---|
| `carStatus: parse attrs` | `car.attrs=[{k:'colorIndex',v:'2'},{k:'functions',v:'facerecog/password'}]` | `{colorIndex:2, functions:['facerecog','password']}` | Web (Node) |
| `carStatus: missing attrs` | `car.attrs=[]` | `{colorIndex:0, functions:[]}` | Web (Node) |
| `carStatus: teamName join` | selected name + cars/positions/leaderboard | merged VM with rank/lap/px | Web (Node) |
| `carStatus: >15 cars, no row` | car not in top 15 | `rank: null` (lap/cp/pos still resolve) | Web (Node) |
| `carStatus: deriveSpeed` | prev/cur pos + dt | `hypot(dpx,dpz)/dt`; frame-1 undefined | Web (Node) |
| `CarNetState.s round-trip` | `s=12.5f` | preserved through JsonUtility | Unity EditMode |
| `TrackGeometryMessage round-trip` | start/bounds/wpx/wpz | preserved | Unity EditMode |
| `relay: track_geometry live` | host sends geometry | web viewer receives it | Web integration |
| `relay: track_geometry replay` | late `web_join_room` | cached geometry delivered on join | Web integration |

### Edge Cases Checklist
- [ ] Survey omits `colorIndex` and/or `functions` → default color + "No functions" (no crash)
- [ ] Selected car outside top-15 leaderboard → rank "—", other fields from state_update/roster
- [ ] `race_end` then a stale `selectedTeamName` → panel guards / clears
- [ ] Duplicate team names → first-match selection (documented)
- [ ] Unity **not** rebuilt (no `s`, no `track_geometry`) → speed labeled approx, ellipse from accumulated bounds
- [ ] WS reconnect mid-race → roster/positions/leaderboard/geometry replayed; selection tolerates positions reset
- [ ] Canvas click on empty space (no dot within `DOT_RADIUS`) → no selection change
- [ ] `>15` cars: all appear on minimap, only 15 in leaderboard

---

## Validation Commands

### Web unit + relay tests
```bash
cd web-app && npx vitest run
```
EXPECT: existing suites + new `carStatus.test.js` + new `adversarial-ws` `track_geometry` case all pass.

### Web client lint
```bash
cd web-app/client && npm run lint
```
EXPECT: oxlint clean on changed files.

### Web client build
```bash
cd web-app/client && npm run build
```
EXPECT: Vite build succeeds (no unresolved imports / JSX errors).

### Unity EditMode tests
```bash
# Via UnitySkills API (http://localhost:8090) or the Unity Test Runner (Tests.EditMode asmdef)
# game-ci/unity-test-runner@v4 in CI
```
EXPECT: `NetworkMessagesTests` new cases green; full EditMode suite no regressions.

### Manual (browser) validation
```bash
cd web-app/client && npm run dev     # + a running relay/Unity host with an active race
```
- [ ] Open `/#/live/:roomCode` during a race — leaderboard + minimap render
- [ ] Click a leaderboard row → row highlights, panel shows color/functions/speed/rank/laps, minimap dot ringed
- [ ] Click a minimap dot → same car selected everywhere
- [ ] Minimap shows a stable ellipse + a start marker that does **not** drift as cars spread
- [ ] Dot colors match the Unity car colors
- [ ] Join mid-race in a second tab → panel/minimap populate from replay

---

## Acceptance Criteria
- [ ] Leaderboard rows are clickable and highlight the selected car
- [ ] Clicking a car (row or dot) shows **color, equipped functions, speed, ranking, laps finished** (+ cp)
- [ ] Minimap renders a **stable elliptical** track with a **precise START** marker and **highlights** the selected car in its **real color**
- [ ] Color/functions/rank/laps consumed from existing wire data; speed + start added upstream with graceful fallbacks
- [ ] Page remains **spectator-only** (no host/event controls)
- [ ] All validation commands pass; new tests written and passing; no lint/build errors

## Completion Checklist
- [ ] Web components follow the flat-className + `index.css` token convention
- [ ] `attrs` read as an **array of {k,v}** (not an object); functions split on `/`
- [ ] Selection joins by `teamName` (not index) end-to-end
- [ ] All canvas overlays drawn inside the single `useEffect` (deps include `selectedTeamName` + `trackGeometry`)
- [ ] Upstream broadcasts ride the verbatim relay; `track_geometry` added to **both** whitelist and replay
- [ ] Unity tests ship their `.meta`; tests in global namespace
- [ ] No hardcoded car colors (use `CAR_COLORS` mirror); no reuse of the stale `TrackMinimap.COLORS`
- [ ] Self-contained — no further codebase searching needed to implement

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Speed + precise start need a Unity WebGL **rebuild + redeploy** (prod uses IthacaServer pinned compose) | Medium | Those two fields stay approximate until a rebuild ships; can't verify E2E in prod without redeploy | Ship graceful client-side fallbacks (Δpos/Δt speed labeled approx; accumulated-bounds + first-frame-cluster start); read `p.s`/`trackGeometry` defensively so the feature upgrades automatically post-rebuild |
| `colorIndex`/`functions` are **survey-config-dependent** — a custom survey may omit them | Medium | Panel/dot color could blank out | `parseCarAttrs` defaults `colorIndex=0`, `functions=[]`; panel shows "default color"/"No functions"; dots fall back to spawn-order palette |
| Leaderboard capped at **top 15** while state_update has all cars | Medium | A selected tail car has no rank | `resolveSelectedCar` returns `rank:null` → panel shows "—"; laps/cp/pos from state_update, color/functions from roster |
| Equipped functions broadcast **once** at race_start; event actions mutate them host-side without re-broadcast | Medium | Panel may show stale loadout after events | Label as **initial loadout**; live re-broadcast explicitly out of scope (possible follow-up) |
| Canvas highlight/start drawn outside the ~10Hz redraw effect gets **erased** | Low | Flicker / non-responsive clicks | Draw all overlays inside the one `useEffect`; add deps; store dot x/y in a ref for hit-testing |
| **Color source ambiguity**: `GetTrailColor` (trail) vs `CarPrefabs[colorIndex]` (body prefab) may differ | Low–Med | Dot color could mismatch the car body | Verify the prefab body-color order matches the five `CAR_COLORS` before shipping; trail palette is the best available RGB mirror |
| Adding client component-test infra (jsdom/RTL) is a **new-dependency** decision | High (if treated mandatory) | Could stall the plan | Put all deterministic logic in the DOM-free `carStatus` util (Node Vitest); component tests optional/approval-gated (Task 15) |

## Notes — Decision Points (defaults chosen; override before implementing if desired)
The recon surfaced four decisions. Defaults below are derived from the user's wording and the cheapest-correct path; each is reversible.
1. **Speed source** — *Default: authoritative upstream `s` on `CarNetState`* (tiny Unity change, rides the existing relay, no server edit), **with** the client-side Δpos/Δt approximation as the no-rebuild MVP. Both are implemented; which is the *launch* target only affects whether a Unity rebuild is scheduled.
2. **Precise START** — *Default: upstream one-time `track_geometry` broadcast* (real start coords + bounds), because the request says **"精确显示起点"** (precisely show the start). Client-side approximation (first-frame cluster + accumulated bounds) is the fallback if a rebuild is out of scope for the first release.
3. **Track shape** — *Default: stylized **ellipse*** fitted to the broadcast bounds, honoring **"椭圆地图"**. `track_geometry` also carries an optional waypoint polyline (`wpx/wpz`) if a *true* course outline is later preferred — no schema change needed to switch.
4. **UI component tests** — *Default: do **not** add jsdom/RTL* (approval-gated new dependency). Coverage comes from the pure `carStatus` unit tests + Unity EditMode + relay integration tests. Task 15 remains available on approval.

If the caller prefers a **web-only first release** (no Unity rebuild), implement Tasks 7–14 with the fallbacks and defer Tasks 1–6; the plan is structured so the UI upgrades automatically when the rebuilt WebGL later emits `s` + `track_geometry`.

> **Next step**: `/prp-implement .claude/PRPs/plans/student-2d-leaderboard-car-status.plan.md`
