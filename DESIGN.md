# EDI Racing Game v2 — Design Document

## Design Overview

### Goals

- Provide an interactive, visual tool for university EDI (Equity, Diversity & Inclusion) education
- Allow professors to demonstrate systemic inequality through autonomous racing simulations
- Support real-time multi-client (professor + students) WebGL browser deployment
- Enable flexible survey-driven data ingestion — professors define custom survey questions that map to car attributes

### Non-Goals

- Not a competitive multiplayer racing game — cars are fully autonomous
- Not a general-purpose simulation framework — tightly scoped to EDI education
- No mobile-native builds — WebGL browser only

## Architecture

### Overall Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                      Unity WebGL Client                      │
│                                                              │
│  ┌─────────┐   ┌──────────┐   ┌───────────┐   ┌──────────┐ │
│  │ SetupUI │──>│CsvParser │──>│CarSpawner │──>│  Cars    │ │
│  │(Survey) │   │          │   │           │   │(NavMesh) │ │
│  └─────────┘   └──────────┘   └───────────┘   └────┬─────┘ │
│                                                     │       │
│  ┌─────────────────────────────────────────────────┐│       │
│  │              RaceManager (Orchestrator)          ││       │
│  │  ┌────────────┬────────────┬───────────────┐    ││       │
│  │  │ScoreManager│ LapTracker │ EventManager  │<───┘│       │
│  │  └────────────┴────────────┴───────────────┘     │       │
│  └──────────────────────────────────────────────────┘       │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐ │
│  │CameraManager │  │   RaceUI     │  │  NetworkSync       │ │
│  │(Free/Fixed/  │  │(Prof/Student)│  │  (WebSocket client)│ │
│  │ Spectator)   │  │              │  │                    │ │
│  └──────────────┘  └──────────────┘  └─────────┬──────────┘ │
└────────────────────────────────────────────────┬────────────┘
                                                 │ WebSocket
                                         ┌───────┴───────┐
                                         │ Node.js Server │
                                         │  (ws relay)    │
                                         └───────────────┘
```

### Core Components

- **RaceManager**: Central orchestrator. Drives the `GameState` state machine (`Setup → Racing → Paused → Finished`) and coordinates all subsystems. All UI panels and cameras subscribe to `OnStateChanged`.
- **CarController**: NavMeshAgent-based autonomous driving with waypoint following, look-ahead curvature braking, stuck detection/recovery, collision slowdown, and event speed modifiers applied via coroutine stacking.
- **CarIdentity**: MonoBehaviour attached to each car at spawn. Holds runtime state: team name, color, function tags, lap progress. Immutable `CarData` struct is its data source.
- **CsvParser**: Parses CSV text (`teamName,colorIndex,functionList`) into `List<CarData>`. Functions are slash-separated tags used by the event system.
- **EventManager + RuleEngine**: 7 event types matching v1 parity. `EventSchedule` ScriptableObject holds `EventRule` structs. `RuleEngine.Evaluate()` uses `ComparisonOperator` to determine affected cars by attribute conditions.
- **LapTracker + CheckpointTrigger**: Checkpoint-based lap counting. `CheckpointTrigger` fires `OnCarPassedCheckpoint` events consumed by `LapTracker` for ranking.
- **CameraManager**: Role-based camera modes — Free (WASD+mouse for professors), Fixed (F1-F9 preset positions), Spectator (auto-follow leader for students).
- **NetworkSync + SurveyCollector**: WebSocket client/server architecture. Professor broadcasts race state; students receive spectator updates. `SurveyCollector` gathers real-time student survey responses.
- **SessionManager**: JSON save/load to `Application.persistentDataPath/Sessions/`. `ResultsExporter` generates CSV with rankings and event log.
- **WeatherEffect**: Visual feedback system — snow particles, skybox transitions (day/sunset), night-mode lighting.

### Data Flow

```
Survey Responses ──> SurveyResponseMapper ──> CSV text
        OR
Manual CSV Upload ──────────────────────────> CSV text
                                                  │
                                                  v
                                            CsvParser.Parse()
                                                  │
                                                  v
                                          List<CarData> (immutable)
                                                  │
                                                  v
                                         CarSpawner.SpawnCars()
                                                  │
                                    ┌─────────────┼─────────────┐
                                    v             v             v
                              CarIdentity   CarController   Rigidbody
                              (state)       (NavMesh AI)    (physics)
```

## Design Decisions

### Decision Record

| Date | Decision | Rationale | Impact |
|------|----------|-----------|--------|
| 2024 | Unity 6 + URP | WebGL target needs lightweight render pipeline; URP 17.3 has mature WebGL support | All shaders must be URP-compatible |
| 2024 | NavMeshAgent for car AI | Deterministic pathfinding on baked mesh; no ML training needed | Requires pre-baked NavMesh with custom "Car" agent type |
| 2024 | No namespaces | Small team, flat script structure, Unity convention for simple projects | All classes in global namespace |
| 2024 | C# Action delegates over UnityEvents | Type-safe, no serialization overhead, cleaner for code-driven events | Events not configurable in Inspector |
| 2024 | Runtime component injection | Cars spawned from generic prefabs; Rigidbody, NavMeshAgent, BoxCollider added at runtime | Flexible but harder to debug in Inspector |
| 2024 | ScriptableObjects for config | `RaceConfig` and `EventSchedule` as SO assets — editable in Inspector, serializable | Configuration changes don't require code changes |
| 2025 | WebSocket relay (not peer-to-peer) | Simplest multi-client sync for professor-student broadcast pattern | Single point of failure; server must be deployed |
| 2025 | Survey-driven data pipeline | Professors define custom survey questions mapped to car attributes | More flexible than fixed CSV format; requires mapping UI |

### Technology Stack

- **Engine**: Unity 6 (C#)
- **Render Pipeline**: URP 17.3.0
- **Input**: New Input System (`UnityEngine.InputSystem`)
- **AI Navigation**: Unity AI Navigation (NavMeshAgent)
- **Networking**: Node.js WebSocket server (`ws` library)
- **Deployment**: Docker (nginx + Node.js) serving WebGL build
- **Rationale**: Unity provides the best WebGL export with 3D rendering; URP keeps GPU requirements low for browser; NavMesh gives deterministic car behavior without ML complexity

## Trade-offs

### Known Limitations

- **50-car performance ceiling**: NavMeshAgent per car + physics + UI labels may bottleneck at 50+ cars in WebGL. Mitigation: LOD, label culling, physics optimization (Phase 7 remaining work).
- **Single-scene architecture**: All gameplay in `complete_track_demo.unity`. Simplifies state management but limits track variety.
- **No offline/PWA support**: Requires active WebSocket connection for multi-client features.
- **Browser-dependent rendering**: WebGL performance varies significantly across browsers and GPU drivers.

### Technical Debt

- **No namespaces**: All classes in global namespace — acceptable for current scale but would need refactoring if project grows significantly. | Reason: Unity convention for small projects | Plan: Add namespaces if script count exceeds ~100.
- **Runtime component injection**: Makes Inspector debugging harder. | Reason: Flexibility for CSV-driven car generation | Plan: Consider prefab variants if car types stabilize.

## Security Considerations

### Threat Model

- **WebSocket injection**: Malicious clients could send crafted messages to manipulate race state. Mitigation: server validates message structure; professor role requires host action.
- **CSV injection**: Malformed CSV input could cause parser errors or unexpected behavior. Mitigation: `CsvParser` validates input format and sanitizes fields.
- **Session file tampering**: JSON session files on disk could be modified. Mitigation: low risk in educational context; sessions are not used for grading.

### Security Measures

- **Input validation**: CSV parser validates format before processing; WebSocket server validates message structure.
- **Role separation**: Professor and Student roles have different UI capabilities and camera access; network messages are role-tagged.
- **No authentication**: Appropriate for classroom use — room codes provide session isolation, not security.

## Changelog

### 2025 — Phases 1-6 Complete

**Content**: Core racing loop, event system, data pipeline, UI/camera, WebSocket sync, WebGL/Docker deployment, flexible survey system with integration tests.

### 2025 — Phase 7 (In Progress)

**Content**: Weather VFX (skybox transitions, day/sunset cycle, snow particles). Remaining: browser testing, 50-car performance optimization.

### 2026-07-12 — Document Created

**Content**: Initial DESIGN.md generated and populated from project architecture.
