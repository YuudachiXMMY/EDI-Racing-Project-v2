# Plan: Student-Side Weather Sync (host-authoritative broadcast + relay cache)

## Summary
On the deployed server, students who join the professor's live race never see weather
visuals: neither the autonomous day/sunset **cycle** ("evening alternating") nor
**event-triggered** Snow/Night/Sunset. The professor's screen shows them; students stay
stuck on the day skybox with no particles. This plan makes the professor the single
authority for weather: whenever the professor's `WeatherEffect` changes visible state
(cycle transition *or* event), the host broadcasts a new `weather_state` message; the
relay caches the latest one and replays it to late joiners; students apply it directly
without running any local cycle or event logic.

## User Story
As a **student watching the professor's hosted race**, I want the sky, lighting, and
snow to match what the professor sees, so that when the professor triggers snow/night or
the day cycle rolls into evening, my screen shows the same weather instead of a frozen
daytime sky.

## Problem → Solution
**Current:** Weather is computed independently on each client. The host runs
`WeatherEffect.StartCycle()` (via `RaceManager.LoadAndStartRace`) and applies event
weather (via `RaceManager.OnEventTriggered → WeatherEffect.ActivateSnow/Night/Sunset`).
The student path (`RaceManager.LoadAndStartRaceVisualOnly`) **never calls `StartCycle()`**
and **never applies event weather** — `NetworkSync.HandleEventTriggered` only logs. So no
weather is ever visible on the student.
**Desired:** A single replicated `weather_state` channel. The host emits the current
visible weather on every change; the relay caches + replays it to late joiners (mirroring
the just-shipped `latestRaceStart` fix); students apply the state directly and run no local
weather simulation. All students — including those who join mid-snow or mid-evening — see
exactly what the professor sees.

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A
- **PRD Phase**: N/A
- **Estimated Files**: 5 (2 C# runtime, 1 relay, 2 test files)

---

## UX Design

### Before
```
Professor screen:  Day -> Sunset -> Day ...   |  Snow event  |  Night event
Student screen:    Day (frozen forever, no particles, no lighting change)
```

### After
```
Professor screen:  Day -> Sunset -> Day ...   |  Snow event  |  Night event
Student screen:    Day -> Sunset -> Day ...   |  Snow event  |  Night event
                   (mirrors professor; late joiner snaps to professor's CURRENT weather)
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Professor triggers Snow/Night/Sunset event | Student logs text only | Student shows skybox + particles + lighting | Driven by `weather_state`, not `event_triggered` |
| Day/sunset cycle rolls to evening | Student sees nothing | Student's sky transitions with professor's | Host broadcasts each cycle transition |
| Student joins mid-race during weather | Student always shows day | Student snaps to professor's current weather | Relay replays cached `latestWeather` |
| 2D React spectator (`web_join_room`) | No weather | No weather (unchanged) | Out of scope — React has no skybox |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Events/WeatherEffect.cs` | 96-165, 303-383, 453-479 | All transition points to hook + `ApplyNetworkState` target visuals; `Start` captures originals both sides |
| P0 | `Assets/Scripts/Network/NetworkSync.cs` | 40-74, 172-183, 239-275, 297-387 | Host subscribe/broadcast site; student message dispatch + `HandleEventTriggered` |
| P0 | `Assets/Scripts/Network/NetworkMessages.cs` | 1-13, 162-180 | `NetworkMessage` base + `EventTriggeredMessage` shape to mirror |
| P0 | `Server/server.js` | 88-148, 430-500, 550-580, 640-707 | Room shape, `sendRaceStartTo` pattern, create_room defaults, default professor->students relay |
| P1 | `Assets/Scripts/Events/WeatherType.cs` | all | Enum `{ None=0, Snow=1, Night=2, Sunset=3 }` — the wire value |
| P1 | `Assets/Scripts/Race/RaceManager.cs` | 226-258 | `LoadAndStartRaceVisualOnly` (student path — must NOT StartCycle) + host `OnEventTriggered` |
| P2 | `Assets/Tests/EditMode/NetworkMessagesTests.cs` | 157-289 | JSON round-trip test pattern to mirror |
| P2 | `web-app/__tests__/adversarial-ws.test.js` | late-join roster block | WS integration test harness + late-join replay assertions to mirror |
| P2 | `web-app/client/src/constants.js` | 22-24 | `WeatherType` JS mirror — keep int values aligned if touched |

## External Documentation
No external research needed — feature uses established internal patterns (JsonUtility
messages, `ws` relay caching, MonoBehaviour events). Unity `JsonUtility` serializes C#
enums as their integer value; the wire field is typed `int` to match `web-app`'s existing
`WeatherType = { None:0, Snow:1, Night:2, Sunset:3 }` convention.

---

## Patterns to Mirror

### NAMING_CONVENTION — network message class
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:172-180
[Serializable]
public class EventTriggeredMessage
{
    public string type = "event_triggered";
    public int index;
    public string name;
    public int affected;
    public int total;
}
```

### HOST_BROADCAST — host-only guard + JsonUtility.ToJson send
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:172-183
private void OnEventTriggered(EventRule rule, int affectedCount)
{
    if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
    var cars = RaceManager.SpawnedCars;
    var msg = new EventTriggeredMessage
    {
        name = rule.DisplayName,
        affected = affectedCount,
        total = cars != null ? cars.Count : 0
    };
    NetworkManager.Send(JsonUtility.ToJson(msg));
}
```

### STUDENT_DISPATCH — switch on message type
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:243-274
switch (baseMsg.type)
{
    case "race_start":      HandleRaceStart(json);      break;
    case "state_update":    HandleStateUpdate(json);    break;
    case "event_triggered": HandleEventTriggered(json); break;
    // ... add: case "weather_state": HandleWeatherState(json); break;
}
```

### SUBSCRIBE_UNSUBSCRIBE — Start/OnDestroy event lifecycle
```csharp
// SOURCE: Assets/Scripts/Network/NetworkSync.cs:52-55, 72-73
if (RaceManager != null && RaceManager.EventManager != null)
    RaceManager.EventManager.OnEventTriggered += OnEventTriggered;   // Start
// ...
if (RaceManager != null && RaceManager.EventManager != null)
    RaceManager.EventManager.OnEventTriggered -= OnEventTriggered;   // OnDestroy
```

### WEATHER_VISUAL — how a state maps to skybox/light/particles
```csharp
// SOURCE: Assets/Scripts/Events/WeatherEffect.cs:303-353
public void ActivateSnow(float duration)   // Snow:   particles.Play(); TransitionTo(SnowSkybox, originalLightIntensity*0.7f, originalLightColor, SnowAmbientColor)
public void ActivateNight(float duration)  // Night:  TransitionTo(NightSkybox, NightLightIntensity, originalLightColor, NightAmbientColor)
public void ActivateSunset(float duration) // Sunset: TransitionTo(SunsetSkybox, SunsetLightIntensity, SunsetLightColor, SunsetAmbientColor)
// Day baseline (None): TransitionTo(DaySkybox, originalLightIntensity, originalLightColor, DayAmbientColor)  — see EndEventOverride:377 / cycle:162
```

### RELAY_CACHE_REPLAY — cache latest host state, replay to late joiner
```javascript
// SOURCE: Server/server.js:135-148  (sendRaceStartTo — the pattern to mirror for sendWeatherStateTo)
function sendRaceStartTo(ws, room, teamName) {
  if (!room.raceStarted || !room.latestRaceStart || ws.readyState !== 1) return;
  try {
    const startMsg = JSON.parse(room.latestRaceStart);
    // ...personalize...
    ws.send(JSON.stringify({ ...startMsg, yourCarIndex: yourIndex }));
  } catch { /* malformed cache */ }
}
```
```javascript
// SOURCE: Server/server.js:681-696  (per-type caching in the default professor relay branch)
} else if (msg.type === 'state_update') {
  room.latestState = raw;
} else if (msg.type === 'leaderboard') {
  room.latestLeaderboard = raw;
}
// default branch already calls broadcastToStudents(info.roomCode, raw) at :698 for ANY type,
// so weather_state is forwarded WITHOUT new forwarding code — only add a cache branch.
```

### TEST_STRUCTURE — JsonUtility round-trip (EditMode)
```csharp
// SOURCE: Assets/Tests/EditMode/NetworkMessagesTests.cs:271-289
[Test]
public void EventTriggeredMessage_JsonRoundTrip_PreservesFields()
{
    var original = new EventTriggeredMessage { index = 3, name = "Snow Storm", affected = 5, total = 8 };
    string json = JsonUtility.ToJson(original);
    var restored = JsonUtility.FromJson<EventTriggeredMessage>(json);
    Assert.AreEqual(3, restored.index);
    // ...
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add `WeatherStateMessage` class |
| `Assets/Scripts/Events/WeatherEffect.cs` | UPDATE | Add `OnWeatherStateChanged` event fired at every transition; add `ApplyNetworkState(WeatherType)` for students |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Host: subscribe + broadcast `weather_state`. Student: `HandleWeatherState` applies it |
| `Server/server.js` | UPDATE | Cache `latestWeather`; `sendWeatherStateTo` replay to late-joining Unity students |
| `Assets/Tests/EditMode/NetworkMessagesTests.cs` | UPDATE | `WeatherStateMessage` type + round-trip tests |
| `web-app/__tests__/adversarial-ws.test.js` | UPDATE | Late-join `weather_state` replay integration tests |

## NOT Building
- **2D React spectator weather** — `web_join_room` viewers have no skybox/particle system; `weather_state` is not replayed to `webapps`. (The relay's generic web relay at `server.js:701-703` is NOT extended to `weather_state`.)
- **Event roster/duration changes** — `event_triggered` message stays exactly as-is; it remains a text/log signal. Weather visuals move entirely to `weather_state`.
- **Independent student-side cycle** — students never call `StartCycle()`; no local `Update()` weather simulation. (Explicitly rejected to avoid host/student drift for late joiners.)
- **Smooth cross-fade sync** — the student runs its own `SkyTransition` coroutine on apply; we do not attempt to synchronize transition progress frame-by-frame, only the target state.
- **Snow particle tuning / new skybox assets** — reuse existing serialized `WeatherEffect` fields and materials.

---

## Step-by-Step Tasks

### Task 1: Add `WeatherStateMessage` to the wire protocol
- **ACTION**: Add a new `[Serializable]` message class in `NetworkMessages.cs` after `EventTriggeredMessage` (after line 180).
- **IMPLEMENT**:
  ```csharp
  [Serializable]
  public class WeatherStateMessage
  {
      public string type = "weather_state";
      public int weather;    // WeatherType: 0=None/Day, 1=Snow, 2=Night, 3=Sunset
      public float duration; // informational only; host is authoritative on end
  }
  ```
- **MIRROR**: NAMING_CONVENTION (`EventTriggeredMessage`).
- **IMPORTS**: none new (`System` already imported for `[Serializable]`).
- **GOTCHA**: Store weather as `int`, not the `WeatherType` enum, to keep the JSON value aligned with `web-app/client/src/constants.js:22` (`WeatherType = { None:0, Snow:1, Night:2, Sunset:3 }`). `JsonUtility` would serialize the enum as its int anyway, but an explicit `int` documents the contract.
- **VALIDATE**: `WeatherStateMessage_TypeField_IsCorrect` and a round-trip test pass (Task 5).

### Task 2: Emit weather changes from `WeatherEffect` (host side) + add student apply
- **ACTION**: In `WeatherEffect.cs`, (a) declare a public event, (b) fire it at every point the *visible* weather changes, (c) add a network-apply method for students.
- **IMPLEMENT**:
  - Add near the top of the class (with the other public members, ~line 15):
    ```csharp
    /// <summary>Fires on the HOST whenever the visible weather changes (cycle transition or
    /// event). NetworkSync broadcasts this to students. Args: (state, durationSeconds).
    /// duration is 0 for continuous cycle states.</summary>
    public event System.Action<WeatherType, float> OnWeatherStateChanged;
    ```
  - Fire it at each transition (add the raise line right after each existing `TransitionTo(...)` / state set):
    - `Update()` cycle -> sunset (after line 156): `OnWeatherStateChanged?.Invoke(WeatherType.Sunset, 0f);`
    - `Update()` cycle -> day (after line 162): `OnWeatherStateChanged?.Invoke(WeatherType.None, 0f);`
    - `ActivateSnow` (after line 308): `OnWeatherStateChanged?.Invoke(WeatherType.Snow, duration);`
    - `ActivateNight` (after line 327): `OnWeatherStateChanged?.Invoke(WeatherType.Night, duration);`
    - `ActivateSunset` (after line 344): `OnWeatherStateChanged?.Invoke(WeatherType.Sunset, duration);`
    - `EndEventOverride` — after it decides the resumed state (lines 373-382), emit the resumed state: `WeatherType.Sunset` in the sunset branch, else `WeatherType.None`.
    - `ResetAll` (after line 477 restores originals): `OnWeatherStateChanged?.Invoke(WeatherType.None, 0f);` so students revert on race reset.
  - Add a student apply method (no coroutine, no cycle, no auto-deactivate):
    ```csharp
    /// <summary>Student-side: apply a host-authoritative weather state directly. Does NOT
    /// start the auto-deactivate coroutine or the day cycle — the host drives all changes.</summary>
    public void ApplyNetworkState(WeatherType state)
    {
        if (!hasStoredOriginals) return; // Start() not run yet; ignore (host resends on next change)
        IsSnowActive = state == WeatherType.Snow;
        IsNightActive = state == WeatherType.Night;
        IsSunsetActive = state == WeatherType.Sunset;
        switch (state)
        {
            case WeatherType.Snow:
                if (snowParticles != null) snowParticles.Play();
                TransitionTo(SnowSkybox, originalLightIntensity * 0.7f, originalLightColor, SnowAmbientColor);
                break;
            case WeatherType.Night:
                if (snowParticles != null) snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                TransitionTo(NightSkybox, NightLightIntensity, originalLightColor, NightAmbientColor);
                break;
            case WeatherType.Sunset:
                if (snowParticles != null) snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                TransitionTo(SunsetSkybox, SunsetLightIntensity, SunsetLightColor, SunsetAmbientColor);
                break;
            default: // None / Day
                if (snowParticles != null) snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                TransitionTo(DaySkybox, originalLightIntensity, originalLightColor, DayAmbientColor);
                break;
        }
    }
    ```
- **MIRROR**: WEATHER_VISUAL (reuse the exact skybox/intensity/color args from `ActivateSnow/Night/Sunset`).
- **IMPORTS**: none new.
- **GOTCHA**: `IsSnowActive` MUST be set true for Snow so `LateUpdate` (line 287-301) keeps the snow emitter centered on the student's camera; set it false for every other state or stale snow follows the camera invisibly. Do NOT call `ActivateSnow/Night/Sunset` on the student — those start a `DeactivateAfter` coroutine that would revert the weather locally and desync from the host.
- **GOTCHA**: `ApplyNetworkState` deliberately does NOT raise `OnWeatherStateChanged`, so even if a student's `WeatherEffect` is wired to broadcast it can't echo.
- **VALIDATE**: Editor compiles; confirm each `TransitionTo` call still compiles and the new method references only existing serialized fields (`SnowSkybox`, `NightSkybox`, `SunsetSkybox`, `DaySkybox`, `NightLightIntensity`, `SunsetLightIntensity`, `SunsetLightColor`, `*AmbientColor`).

### Task 3: Host broadcast + student handle in `NetworkSync`
- **ACTION**: Subscribe to `WeatherEffect.OnWeatherStateChanged` on the host and broadcast; add the student dispatch + handler.
- **IMPLEMENT**:
  - In `Start()` (after the EventManager subscribe at lines 52-55):
    ```csharp
    if (RaceManager != null && RaceManager.WeatherEffect != null)
        RaceManager.WeatherEffect.OnWeatherStateChanged += OnWeatherStateChanged;
    ```
  - In `OnDestroy()` (mirror at lines 72-73):
    ```csharp
    if (RaceManager != null && RaceManager.WeatherEffect != null)
        RaceManager.WeatherEffect.OnWeatherStateChanged -= OnWeatherStateChanged;
    ```
  - Add the host broadcast handler (next to `OnEventTriggered`, ~line 183):
    ```csharp
    private void OnWeatherStateChanged(WeatherType state, float duration)
    {
        if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
        var msg = new WeatherStateMessage { weather = (int)state, duration = duration };
        NetworkManager.Send(JsonUtility.ToJson(msg));
    }
    ```
  - Add the dispatch case in `HandleGameMessage` (in the switch at lines 243-274):
    ```csharp
    case "weather_state":
        HandleWeatherState(json);
        break;
    ```
  - Add the student handler (next to `HandleEventTriggered`, ~line 387):
    ```csharp
    private void HandleWeatherState(string json)
    {
        var msg = JsonUtility.FromJson<WeatherStateMessage>(json);
        if (RaceManager != null && RaceManager.WeatherEffect != null)
            RaceManager.WeatherEffect.ApplyNetworkState((WeatherType)msg.weather);
        Debug.Log($"[NetworkSync] Weather -> {(WeatherType)msg.weather}");
    }
    ```
- **MIRROR**: HOST_BROADCAST, STUDENT_DISPATCH, SUBSCRIBE_UNSUBSCRIBE.
- **IMPORTS**: none new (`WeatherType`, `JsonUtility` already in scope; `RaceManager.WeatherEffect` is a public field per `TrackSetupEditor.cs:380`).
- **GOTCHA**: The host-only guard in `OnWeatherStateChanged` is essential — without `IsHost`, a mis-wired student would rebroadcast. Keep it identical to `OnEventTriggered`'s guard.
- **GOTCHA**: `HandleEventTriggered` stays unchanged (still just logs). Do not route weather through it.
- **VALIDATE**: Editor compiles; message flows in a two-client manual test (Manual Validation).

### Task 4: Relay — cache `latestWeather` and replay to late-joining students
- **ACTION**: In `Server/server.js`, add room field + cache branch + late-join replay helper and call sites.
- **IMPLEMENT**:
  - Room shape doc comment (line 88): append `latestWeather: string|null` to the list, and add a note under the existing `latestRaceStart` note (lines 90-91):
    ```
    // latestWeather holds the most recent weather_state (day cycle transition OR event weather)
    // so a late joiner is snapped to the professor's CURRENT weather. Unity students only.
    ```
  - `create_room` defaults (after `latestRaceStart: null,` at line 437):
    ```javascript
    latestWeather: null,
    ```
  - Cache branch in the default professor relay (add to the `else if` chain after the `race_end` branch, ~line 696 — BEFORE the `broadcastToStudents` at line 698, which already forwards it):
    ```javascript
    } else if (msg.type === 'weather_state') {
      room.latestWeather = raw;
    }
    ```
  - New replay helper (after `sendRaceStartTo`, ~line 148):
    ```javascript
    // Replay the cached weather so a late-joining Unity student snaps to the professor's
    // current sky/lighting/particles. No-op before any weather change or if nothing cached.
    // Weather is independent of cars, so ordering vs latestState/race_start does not matter.
    function sendWeatherStateTo(ws, room) {
      if (!room.latestWeather || ws.readyState !== 1) return;
      ws.send(room.latestWeather);
    }
    ```
  - Call it right after each `sendRaceStartTo(ws, room, teamName);` for **Unity students only**:
    - `join_room` (after line 495): `sendWeatherStateTo(ws, room);`
    - `rejoin_room` student branch (after line 554): `sendWeatherStateTo(ws, room);`
    - **Do NOT** add it after the `web_join_room` call at line 578 (React viewers have no skybox).
- **MIRROR**: RELAY_CACHE_REPLAY (`sendRaceStartTo` + per-type cache branch).
- **IMPORTS**: none (plain `ws`).
- **GOTCHA**: The `default` branch already calls `broadcastToStudents(info.roomCode, raw)` at line 698 for every professor message type, so live `weather_state` frames reach students with **no new forwarding code** — the only additions are the cache and the late-join replay. Do NOT add `weather_state` to `WEBAPP_RELAY_TYPES` (line 701-702).
- **GOTCHA**: `latestWeather` is intentionally not cleared on `race_end`/`race_reset` (parity with `latestRaceStart`, which also persists). A new race's first `weather_state` (e.g., `StartCycle`'s first transition, or `ResetAll`'s `None`) overwrites it. Clearing in `destroyRoom` is out of scope.
- **VALIDATE**: `node --check Server/server.js` clean; new WS integration tests pass (Task 5).

### Task 5: Tests
- **ACTION**: Add EditMode round-trip tests and WS late-join integration tests.
- **IMPLEMENT**:
  - `NetworkMessagesTests.cs` — mirror lines 271-289:
    ```csharp
    [Test]
    public void WeatherStateMessage_TypeField_IsCorrect()
    {
        var msg = new WeatherStateMessage();
        Assert.AreEqual("weather_state", msg.type);
    }

    [Test]
    public void WeatherStateMessage_JsonRoundTrip_PreservesFields()
    {
        var original = new WeatherStateMessage { weather = (int)WeatherType.Night, duration = 12.5f };
        string json = JsonUtility.ToJson(original);
        var restored = JsonUtility.FromJson<WeatherStateMessage>(json);
        Assert.AreEqual((int)WeatherType.Night, restored.weather);
        Assert.AreEqual(12.5f, restored.duration);
    }

    [Test]
    public void WeatherStateMessage_WeatherInt_MatchesWeatherTypeEnum()
    {
        // Guards the wire contract shared with web-app/client/src/constants.js
        Assert.AreEqual(0, (int)WeatherType.None);
        Assert.AreEqual(1, (int)WeatherType.Snow);
        Assert.AreEqual(2, (int)WeatherType.Night);
        Assert.AreEqual(3, (int)WeatherType.Sunset);
    }
    ```
  - `web-app/__tests__/adversarial-ws.test.js` — add a `describe('weather_state late-join replay ...')` block mirroring the existing late-join roster block. Use the harness helpers (`hostARoom()`, `joinAsStudent()`, `.next(pred)`, `.collect(ms)`):
    1. **Live forward:** host sends `{type:'weather_state', weather:1, duration:10}`; an already-joined student receives it (`weather===1`).
    2. **Late-join replay:** host sends `weather_state` (weather:2), THEN a new student joins → that student receives a `weather_state` with `weather===2` after its `race_start` replay.
    3. **No cache → no replay:** a student joining a room where the professor never sent `weather_state` receives no `weather_state` frame (assert none within a `collect(150)` window).
    4. **Latest wins:** host sends weather:1 then weather:3; late joiner receives only `weather===3` as the cached value.
    5. **Not sent to web viewers:** a `web_join_room` viewer does NOT receive a replayed `weather_state` (assert none in a `collect` window).
- **MIRROR**: TEST_STRUCTURE + the existing late-join roster `describe` block in `adversarial-ws.test.js`.
- **IMPORTS**: none new.
- **GOTCHA**: Follow `test-standards.md` — arrange/act/assert, deterministic, self-cleaning (the harness spawns/kills the real `Server/server.js` process). Every assertion pins an exact value (no `weather > 0`).
- **VALIDATE**: `cd web-app && npx vitest run __tests__/adversarial-ws.test.js` green; Unity EditMode suite green.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `WeatherStateMessage_TypeField_IsCorrect` | `new WeatherStateMessage()` | `type == "weather_state"` | No |
| `WeatherStateMessage_JsonRoundTrip_PreservesFields` | weather=Night(2), duration=12.5 | fields preserved through JSON | No |
| `WeatherStateMessage_WeatherInt_MatchesWeatherTypeEnum` | enum casts | None=0,Snow=1,Night=2,Sunset=3 | Contract guard |
| WS: live forward | host `weather_state` weather=1 | joined student gets weather=1 | No |
| WS: late-join replay | cache weather=2, then join | new student gets weather=2 | Late join |
| WS: no cache -> no replay | join before any weather | student gets no `weather_state` | Empty input |
| WS: latest wins | weather=1 then 3, then join | late joiner gets 3 | Overwrite |
| WS: web viewer excluded | `web_join_room` after cache | no `weather_state` to webapp | Boundary |

### Edge Cases Checklist
- [x] Empty input — no weather cached -> no replay (test 3)
- [x] Overwrite/latest-wins — multiple weather changes (test 4)
- [x] Wrong recipient — 2D web viewer excluded (test 5)
- [x] Late join mid-weather — replay snaps to current (test 2)
- [ ] `ApplyNetworkState` before `Start()` ran — guarded by `hasStoredOriginals` (manual/scene-only, not unit-testable headlessly)
- [ ] Concurrent access — single-threaded relay + Unity main thread; N/A

---

## Validation Commands

### Static Analysis
```bash
node --check Server/server.js
```
EXPECT: no output (syntax OK)

### Unit Tests (relay / WS)
```bash
cd web-app && npx vitest run __tests__/adversarial-ws.test.js
```
EXPECT: all weather_state late-join tests pass, no regressions in existing late-join block

### Full Web Test Suite
```bash
cd web-app && npx vitest run
```
EXPECT: full suite green (no regressions)

### Unity EditMode Tests
```bash
# Via UnitySkills API (preferred) or Unity Test Runner (EditMode)
# game-ci/unity-test-runner@v4 (CI) — EditMode filter includes NetworkMessagesTests
```
EXPECT: `NetworkMessagesTests` (incl. 3 new WeatherState tests) pass; no EditMode regressions

### Manual Validation (two-client, deployed or local)
- [ ] Professor hosts, starts race. Student (Unity, Enter 3D Game) joins.
- [ ] Wait for the day cycle to roll to evening on the professor screen -> student sky transitions to sunset within `TransitionTime`.
- [ ] Professor triggers a **Snow** event -> student shows falling snow particles + snow skybox + dimmed light.
- [ ] Professor triggers **Night** -> student sky/lighting go dark; snow stops.
- [ ] Snow/Night event duration ends on professor -> student reverts to the professor's current cycle state (day or sunset).
- [ ] **Late join:** trigger Snow on professor, THEN have a second student join -> the new student immediately shows snow (cached replay).
- [ ] 2D spectate (React) viewer is unaffected (no errors).

---

## Acceptance Criteria
- [ ] Student Unity clients render the day/sunset cycle in sync with the professor.
- [ ] Student Unity clients render Snow/Night/Sunset events (skybox + particles + lighting).
- [ ] A student joining mid-weather snaps to the professor's current weather.
- [ ] All new + existing automated tests pass; `node --check` clean.
- [ ] No new type/lint errors; 2D React viewer unchanged.

## Completion Checklist
- [ ] Weather visuals driven solely by `weather_state` (event_triggered untouched)
- [ ] Host-only guard on broadcast; students never simulate weather locally
- [ ] Relay caching mirrors `latestRaceStart`; replay to Unity students only
- [ ] Reuses existing `WeatherEffect` serialized fields (no new assets/hardcoded values)
- [ ] Tests follow `test-standards.md` (AAA, deterministic, self-cleaning)
- [ ] Self-contained — no further codebase search needed

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `RaceManager.WeatherEffect` unset in the deployed scene -> host never broadcasts | Low | High (feature silently dead) | `TrackSetupEditor.cs:380` wires it; add a `Debug.LogWarning` if null at subscribe time; verify in manual test |
| Student `WeatherEffect.Start()` hasn't run when first `weather_state` arrives | Low | Low (one dropped frame) | `ApplyNetworkState` guards on `hasStoredOriginals`; host resends on next change; late-join replay arrives after scene load |
| Relay change touches production `server.js` again (recently modified) | Med | Med | Change is additive (one cache branch + one helper); covered by WS integration tests; deploy note below |
| Deploy forgets to rebuild the relay/game container | Med | High | Call out in PR body (same as the late-join fix): `weather_state` lives in `Server/server.js` + Unity build, not web-app |
| Cycle transition spam (host emits every Update tick) | Very Low | Low | `Update()` only emits on the `!cycleInSunset <-> cycleInSunset` edge (lines 153/159), not every frame — already edge-triggered |

## Notes
- **Why unified (not two mechanisms):** Routing both events and the cycle through one
  `weather_state` channel means late joiners see in-progress **events** too (a student
  joining mid-snow sees snow), and there is a single apply path/cache to reason about.
  `event_triggered` stays a pure text/log signal.
- **Why students don't run the cycle:** `WeatherEffect.Update()` is time-based off local
  `Time.time`; a late joiner starting its own cycle would drift from the professor. Host
  authority + replay keeps every student aligned with the professor's current state.
- **Relay forwarding is already generic:** `server.js:698` broadcasts any professor
  message to students, so only *caching* and *late-join replay* are new relay logic.
- **Deploy:** like the late-join car-spawn fix, this spans the **relay** (`Server/server.js`)
  and the **Unity build** (`NetworkSync`/`WeatherEffect`/`NetworkMessages`). The Ithaca
  deploy must rebuild/redeploy the relay + game container; web-app-only redeploy is
  insufficient. See [[ediracing-deploy-pinned-compose]].
