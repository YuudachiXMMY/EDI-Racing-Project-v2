# Race System

> **Status**: Accepted — reverse-engineered from codebase (v2)
> **Last Updated**: 2026-07-23
> **Source**: `RaceManager.cs`, `LapTracker.cs`, `ScoreManager.cs`,
> `CarSpawner.cs`, `CheckpointTrigger.cs`, `RaceConfig.cs`

---

## 1. Overview

The race system orchestrates the full race lifecycle: loading car data, spawning
cars on the track, tracking lap/checkpoint progress, managing pause/resume, and
collecting results. It is the central coordinator connecting the car movement,
event, weather, scoring, and network subsystems.

---

## 2. Player Fantasy

"I watch a class of 10-20 teams' cars line up on the track, the race starts, and
I trigger events that make some cars speed up and others slow down. The
leaderboard shifts in real time. At the end, I export the results and ask: why
did Team X finish last?"

---

## 3. Detailed Rules

### State Machine

```
Setup ──► Racing ──► Finished
  ▲          │
  │          ▼
  └──── Paused ────┘
         (resume)
```

| State | `Time.timeScale` | User Actions |
|-------|------------------|--------------|
| Setup | 1.0 | Load CSV, select template, join network |
| Racing | 1.0 | Trigger events (1-9), pause (UI), export |
| Paused | 0.0 | Resume, view scoreboard |
| Finished | 1.0 | Export results, reset race |

### Checkpoint System

- `CheckpointTrigger` components placed sequentially around the track
- Auto-detected at startup via `FindObjectsByType<CheckpointTrigger>()`
- Each checkpoint has a sequential `CheckpointIndex` (0, 1, 2, ..., N-1)
- Cars must pass checkpoints **in order** — skipping is ignored

### Lap Completion

```
expectedIndex = car.CurrentCheckpointIndex % totalCheckpoints
if checkpointIndex == expectedIndex:
    car.TotalCheckpointsPassed++
    car.CurrentCheckpointIndex++
    car.CheckpointTime = 0

    if car.CurrentCheckpointIndex % totalCheckpoints == 0:
        car.CurrentLap++
        fire OnLapCompleted(car)
```

### Race Finish

```
if car.CurrentLap >= Config.TotalLaps:
    if !raceFinished:
        raceFinished = true
        fire OnRaceFinished(car)  // winner
        SetState(GameState.Finished)
```

### Ranking Algorithm (ScoreManager)

```csharp
cars.OrderByDescending(c => c.TotalCheckpointsPassed)
    .ThenBy(c => c.CheckpointTime)
```

- **Primary**: Most checkpoints passed (further = better)
- **Secondary**: Least time since last checkpoint (faster = better)

### Results Collection

```
RaceResults {
    CarResult[] Rankings     // Rank, TeamName, Attributes, Laps, Checkpoints, Time
    EventLogEntry[] EventLog // Timestamp, EventName, AffectedCount, TotalCars
    float TotalRaceTime      // seconds since race start
}
```

### Debug Keyboard Shortcuts

| Key | Action | Condition |
|-----|--------|-----------|
| T | Print scoreboard to console | Race started |
| P | Save session to JSON | Race started, SessionManager present |
| L | Load latest saved session | SessionManager present |
| X | Export results to CSV | Race started, SessionManager present |

---

## 4. Formulas

### Ranking Score (implicit)

```
rank_priority = (TotalCheckpointsPassed * -1, CheckpointTime)
// sorted ascending: more checkpoints first, then lower time
```

### Race Duration

```
TotalRaceTime = Time.time - raceStartTime
```

### Scoreboard Display

```
"({rank}) [Lap {currentLap}] {teamName} - {checkpointTime:F1}s"
```

---

## 5. Edge Cases

| Scenario | Handling |
|----------|----------|
| Car passes checkpoint out of order | Ignored — `expectedIndex` check rejects it |
| Two cars finish on same frame | First `OnLapCompleted` sets `raceFinished = true`; second is tracked but not announced as winner |
| Zero checkpoints in scene | `LapTracker` logs error; laps never complete |
| Reset during active race | Destroys all cars, clears scores, resets weather, returns to Setup |
| Load session with different track | RaceConfig applied; cars re-spawned; checkpoint count from scene |
| No SessionManager assigned | Save/load/export shortcuts silently skip |

---

## 6. Dependencies

| Dependency | Role | Required? |
|-----------|------|-----------|
| CarSpawner | Instantiates car GameObjects | Yes |
| LapTracker | Monitors checkpoint progress | Yes |
| ScoreManager | Ranks cars, collects results | Yes |
| RaceConfig (SO) | Provides tunable parameters | Yes |
| EventManager | Handles event triggers | Optional |
| WeatherEffect | Visual weather effects | Optional |
| NetworkSync | Broadcasts to student clients | Optional |
| SessionManager | Save/load/export | Optional |
| SurveyConfigManager | Survey template management | Optional |

---

## 7. Tuning Knobs

| Parameter | Default | Type | Effect |
|-----------|---------|------|--------|
| TotalLaps | 3 | int | Laps required to finish |
| SpawnOffsetX | 7 m | float | Grid horizontal spacing |
| SpawnOffsetZ | 1.2 m | float | Grid depth spacing |
| SpawnSpreadMultiplier | 5 | float | Random spread when no grid positions |

---

## 8. Acceptance Criteria

- [ ] Cars spawn at designated grid positions or random offsets
- [ ] Checkpoints enforce sequential passage (no shortcuts)
- [ ] Lap counter increments correctly after full checkpoint circuit
- [ ] First car to reach `TotalLaps` triggers race finish
- [ ] Scoreboard ranks by checkpoints (desc) then time (asc)
- [ ] Pause sets `Time.timeScale = 0`; resume restores to 1
- [ ] Race reset destroys all cars and returns to Setup state
- [ ] Results export includes rankings + event log as CSV
- [ ] Session save/load preserves cars, rules, config, and results
