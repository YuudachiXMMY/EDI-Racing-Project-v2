# Game Overview — EDI Racing

> **Status**: Accepted — reverse-engineered from codebase (v2)
> **Last Updated**: 2026-07-23

---

## 1. Overview

EDI Racing is an educational racing game that uses autonomous car racing as a
visual metaphor for systemic inequality. In a classroom setting, a professor
operates the game while students participate via web browser. Student survey
responses are transformed into car attributes, and professor-triggered events
apply speed penalties or boosts based on those attributes — making abstract
concepts like privilege, accessibility barriers, and systemic bias tangible and
observable in real time.

The game runs as a Unity WebGL application served via Docker Compose alongside a
React-based survey web app and a Node.js WebSocket synchronization server.

---

## 2. Player Fantasy

**Professor (Operator)**: "I can demonstrate systemic inequality to my class in a
visceral, memorable way. Students see their own survey data drive the race
outcomes. When I trigger an event like 'Language Barrier', every team whose
primary language isn't English slows down — and the class can see the gap widen
in real time."

**Student (Participant/Observer)**: "I filled out a survey, my team got a car,
and now I'm watching it race. When certain events happen, some cars slow down and
others speed up. I can see how the things we answered about ourselves — like
whether we use assistive technology — directly affect our car's chance of
winning. That's not fair... and that's the point."

---

## 3. Detailed Rules

### Roles

| Role | Input | Control | View |
|------|-------|---------|------|
| **Professor** | Configures surveys, imports data, triggers events | Full (keyboard 1-9, UI buttons) | Unity WebGL in-browser |
| **Student** | Fills out survey, joins room via code | None (observer) | Simplified web viewer via WebSocket |

### Game Loop

```
Setup → Racing → Finished
  ↑                  │
  └──── Reset ◄──────┘
```

1. **Setup** (`GameState.Setup`)
   - Professor loads car data (CSV import or Web App survey responses)
   - Optionally selects a survey template (V1 Parity, Accessibility, Diversity, ENGG*1100)
   - Configures event rules mapping attributes to speed effects
   - Students join via room code (optional multiplayer)

2. **Racing** (`GameState.Racing`)
   - Cars autonomously navigate the track using NavMesh
   - Professor triggers events via keyboard (Digit1-9) or UI
   - Events evaluate car attributes against rules and apply speed modifiers
   - Weather effects provide visual reinforcement (snow, night, sunset)
   - Scoreboard tracks ranking by checkpoint progress

3. **Finished** (`GameState.Finished`)
   - First car to complete `TotalLaps` (default 3) wins
   - Results exported as CSV (rankings, lap times, event log)
   - Session saved as JSON for replay/analysis

4. **Paused** (`GameState.Paused`)
   - `Time.timeScale = 0` — race freezes
   - Professor can show scoreboard, discuss events with class

### Core Educational Mechanic

```
Survey Response → Car Attribute → Event Rule Match → Speed Modifier
```

The connection between identity/experience data and race outcomes creates the
"aha moment": the race is rigged by design, mirroring how real systems create
unequal outcomes based on individual characteristics.

---

## 4. Formulas

### Ranking

```
Primary sort:  TotalCheckpointsPassed (descending)
Secondary sort: CheckpointTime (ascending — lower is better)
```

### Race Completion

```
if car.CurrentLap >= Config.TotalLaps → race finished, car wins
```

### Event Effect

```
affected = RuleEngine.IsAffected(rule, car)
if affected:
    car.agent.speed += rule.SpeedDelta    // applied for rule.Duration seconds
```

(See `car-movement.md` for composite speed formula and `event-system.md` for
condition matching logic.)

---

## 5. Edge Cases

| Scenario | Handling |
|----------|----------|
| No CSV data loaded | Auto-starts only if `DefaultCsvData` is assigned; otherwise stays in Setup |
| No SetupScreen in scene | Falls back to auto-start with `DefaultCsvData` |
| Student disconnects mid-race | Auto-reconnect with exponential backoff (up to 10 attempts) |
| Multiple cars finish same frame | First `OnLapCompleted` callback wins; subsequent cars still tracked |
| Zero event rules configured | Race runs without events — pure race with no modifiers |
| Race reset during active events | `WeatherEffect.ResetAll()` + `EventManager.ClearRegisteredCars()` |

---

## 6. Dependencies

| System | Depends On | Nature |
|--------|-----------|--------|
| Race System | Car Movement, Score Manager, Lap Tracker | Core loop |
| Event System | Rule Engine, Car Attributes, Weather | Gameplay |
| Survey Pipeline | Web App (React+Express), JSON Import | Data input |
| Network | WebSocket Server (Node.js), WebGL Bridge | Multiplayer |
| Weather | Event System (triggers), Camera (particle follow) | Visual |
| Deployment | Docker Compose, nginx, SQLite | Infrastructure |

---

## 7. Tuning Knobs

| Parameter | Default | Location | Effect |
|-----------|---------|----------|--------|
| TotalLaps | 3 | RaceConfig | Laps to finish race |
| DefaultSpeed | 40 m/s | RaceConfig | Base car speed |
| DayCycleDuration | 90 s | WeatherEffect | Day/sunset cycle period |
| Survey Template | V1 Parity | SurveyTemplates | Question set and default rules |

(Comprehensive parameter tables in each subsystem's GDD.)

---

## 8. Acceptance Criteria

- [ ] Professor can load car data from CSV or Web App and start a race
- [ ] Cars autonomously complete laps on a NavMesh track
- [ ] Events triggered by keyboard affect only matching cars' speed
- [ ] Students can join via room code and observe the race
- [ ] Race results export as CSV with rankings and event log
- [ ] At least one survey template demonstrates inequality (speed gap visible)
- [ ] Session save/load preserves car data, event rules, and race config
- [ ] WebGL build runs at 60 FPS with ≤ 2048 MB memory
