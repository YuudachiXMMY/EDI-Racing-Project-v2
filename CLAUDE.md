# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EDI Racing Game v2 — a Unity 6 WebGL racing game for university EDI (Equity, Diversity & Inclusion) education. Professors import student survey data as CSV, which generates autonomous racing cars. During the race, the professor triggers real-time events (weather, speed penalties/boosts) to demonstrate how different factors create unequal outcomes.

**Target**: WebGL browser deployment via Docker (nginx + WebSocket server). Both professor and student access via browser.

## Unity Environment

- **Engine**: Unity 6 with URP 17.3.0
- **Input**: New Input System (`UnityEngine.InputSystem`)
- **Navigation**: AI Navigation package (NavMeshAgent-driven cars)
- **Scenes**: `Assets/Scenes/complete_track_demo.unity` (main race scene)
- **ScriptableObjects**: Created via `Assets > Create > EDI Racing > ...`
  - `RaceConfig` — car physics, spawn, collision, lap settings
  - `EventSchedule` — pre-configured race events with keyboard triggers

## Build & Development

This is a Unity project — there is no CLI build/test pipeline. All operations go through the Unity Editor:

### MCP Tools (ALWAYS use these for Unity development and debugging)

- **UnityMCP** (`http://127.0.0.1:8080`) — Direct Unity Editor control: inspect/modify GameObjects, components, scene hierarchy, run C# in Editor, enter/exit Play Mode. Docs: https://coplaydev.github.io/unity-mcp/getting-started
- **UnitySkills** (`http://localhost:8090`) — DOTween animations, advanced Unity patterns, and scripting recipes. Docs: https://deepwiki.com/Besty0728/Unity-Skills/7.25-dotween-skills
- Prefer MCP tools over manual Editor steps whenever possible

- **Open**: Unity Hub > Open Project (requires Unity 6)
- **Play**: Enter Play Mode in `complete_track_demo` scene
- **Track Setup**: Menu `EDI Racing > Setup Track` (requires NavMesh baked first via `Window > AI > Navigation`)
- **NavMesh Agent Type**: A custom "Car" agent type must exist in Navigation settings; falls back to Humanoid if missing
- **Editor scripts**: `Assets/Scripts/Editor/` — only compiled in Editor, not in builds

## Architecture

### Game State Machine

`GameState` enum (`Setup → Racing → Paused → Finished`) drives the entire application. `RaceManager.OnStateChanged` event propagates state to all UI panels and camera systems.

### Core Loop (RaceManager orchestrates everything)

```
CSV text → CsvParser → List<CarData> → CarSpawner → List<GameObject>
                                                         ↓
RaceManager registers cars with: ScoreManager, LapTracker, EventManager
                                                         ↓
Cars drive autonomously via CarController (NavMeshAgent + WaypointPath)
                                                         ↓
CheckpointTrigger → LapTracker.OnCarPassedCheckpoint → lap/ranking updates
```

### Key Relationships

- **CarData** (immutable struct) → parsed from CSV, stored in sessions
- **CarIdentity** (MonoBehaviour on each car) → runtime state: team name, color, functions, lap progress
- **CarController** (MonoBehaviour) → NavMeshAgent pathfinding with waypoint following, stuck detection/recovery, collision slowdown, event speed modifiers
- **RaceManager** → central orchestrator; holds references to CarSpawner, LapTracker, ScoreManager, EventManager, SessionManager

### Event System

7 event types matching v1 parity. Events are pre-configured in `EventSchedule` ScriptableObject. Professor triggers via keyboard (1-7) or UI panel. `EventMatcher.IsAffected()` determines which cars are hit. `CarController.ApplySpeedModifier()` applies temporary speed changes via coroutine stacking.

### UI / Camera (Role-based)

`RaceUI.UserRole` (Professor/Student) controls:
- **Professor**: Free camera (WASD+mouse), fixed positions (F1-F9), event panel, race controls, setup screen
- **Student**: Spectator camera (auto-follow leader), leaderboard only

`CameraManager` switches between Free/Fixed/Spectator modes. UI panels subscribe to `RaceManager.OnStateChanged` for visibility.

### Data Pipeline

- **Import**: `CsvParser.Parse()` — format: `teamName,colorIndex,functionList` (functions slash-separated)
- **Session**: `SessionManager` saves/loads JSON to `Application.persistentDataPath/Sessions/`
- **Export**: `ResultsExporter` generates CSV with rankings and event log
- **Color mapping**: 0=green, 1=black, 2=red, 3=blue, 4=white

### Debug Keyboard Shortcuts (during play)

- `T` — print scoreboard to console
- `P` — save session
- `L` — load latest session
- `X` — export results CSV
- `1-7` — trigger events
- `F1-F9` — fixed camera positions
- `Escape` — return to free camera

## Implementation Status

Phases 1-6 complete (core racing, events, data pipeline, UI/camera, WebSocket sync, WebGL/Docker, flexible survey system with integration tests). Remaining:
- **Phase 7**: Polish, weather VFX, browser testing, 50-car performance

## Conventions

- All scripts are plain MonoBehaviours or ScriptableObjects — no third-party frameworks
- No namespaces used; all classes are in the global namespace
- ScriptableObjects use `[CreateAssetMenu]` under the "EDI Racing" menu
- Events use C# `Action<T>` delegates, not UnityEvents
- Car spawning adds components at runtime (Rigidbody, NavMeshAgent, BoxCollider trigger, CarController)
- PRD and implementation plans live in `.claude/PRPs/`
