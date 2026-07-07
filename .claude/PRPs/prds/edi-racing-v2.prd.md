# EDI Racing Game v2

## Problem Statement

University professors teaching EDI (Equity, Diversity & Inclusion) courses need a way to make abstract survey data tangible and engaging. Current classroom tools (Kahoot, Mentimeter) are either quiz-correctness-based or static visualizations — neither can transform students' lived-experience survey data into a visceral, interactive demonstration of systemic inequality. The previous v1 was functional but inflexible (hardcoded survey columns, poor code architecture, difficult deployment).

## Evidence

- v1 was actively used in University of Guelph ENGG*1100 course across 18 releases (Oct 2023 - Nov 2024)
- v1's hardcoded survey field mapping (columns G-P) prevented reuse in other courses
- Python DataTool required separate distribution, creating deployment friction
- No competing product combines survey-data-driven game mechanics with EDI education
- Assumption - needs validation: demand exists beyond ENGG*1100 for other courses/universities

## Proposed Solution

A rebuilt Unity 6 WebGL racing game where professors configure custom survey questions, students answer them via browser, and the responses generate race cars with data-driven attributes. Cars race autonomously on a track while the professor triggers real-time events (weather, penalties, boosts) that demonstrate how different factors affect outcomes — making systemic inequality visible through gameplay. Deployed via Docker for easy self-hosting and local testing.

## Key Hypothesis

We believe a flexible, browser-accessible survey-to-racing-game tool will enable professors to create impactful EDI demonstrations for their specific course content.
We'll know we're right when professors can set up a complete race session (questions + events) in under 10 minutes without technical assistance, and students can participate directly from their browsers.

## What We're NOT Building

- Real-time multiplayer racing (students controlling cars) - cars race autonomously based on data
- Statistical analysis tool - this is a visualization/demonstration tool, not a research instrument
- Mobile-native app - WebGL browser deployment only
- LMS integration (v1 scope) - standalone tool first
- Student accounts/authentication - anonymous participation via room code

## Success Metrics

| Metric | Target | How Measured |
|--------|--------|--------------|
| Professor setup time | < 10 minutes for a complete session | Timed user testing |
| Student join-to-participate | < 30 seconds | Time from code entry to survey completion |
| Browser compatibility | Chrome, Firefox, Safari, Edge | Manual testing matrix |
| WebGL build size | < 50MB compressed | Build output measurement |
| Customizable questions | Any number/type of survey questions | Feature verification |

## Open Questions (Resolved)

- [x] Students MUST see the race in their own browser (not just professor projection)
- [x] Both in-game survey UI and CSV import supported
- [x] v1 mapping rules reproduced first; flexible rules deferred to future iteration
- [x] Session save/load required
- [x] Target 30-50 cars ideal, 15 minimum
- [x] Events pre-configured before race start (professor sets up event schedule)
- [x] Post-race data export required (CSV)

## Remaining Open Questions (Resolved)

- [x] Multi-client sync: TBD on optimal approach — must be implemented; needs technical spike in Phase 5
- [x] Student browser view: **full 3D spectator** (same Unity WebGL scene, spectator camera)
- [x] Join method: **both room code and direct URL** supported

---

## Users & Context

**Primary User 1: Professor**
- **Who**: University instructor teaching EDI-related courses (initially ENGG*1100, expanding to any course)
- **Current behavior**: Uses v1 with hardcoded survey fields, manually runs Python DataTool to parse CSV, deploys locally
- **Trigger**: Beginning of semester or specific class session focused on diversity/equity demonstration
- **Success state**: Sets up custom survey questions, sees all student responses generate cars, triggers events to demonstrate EDI concepts, leads class discussion using race outcomes

**Primary User 2: Student**
- **Who**: University student participating in an EDI course activity
- **Current behavior**: Fills out external survey, has no direct interaction with the game
- **Trigger**: Professor announces in-class activity and shares a join code/URL
- **Success state**: Opens browser, joins session, answers survey questions, sees their team's car appear on the projected race

**Job to Be Done**

Professor: When teaching about systemic inequality in class, I want to transform student survey data into a visual racing demonstration, so I can facilitate a discussion about how different factors create unequal outcomes.

Student: When participating in a class EDI activity, I want to easily answer survey questions and see results visualized as a race, so I can understand how diverse experiences lead to different outcomes.

**Non-Users**
- Researchers needing rigorous statistical analysis of survey data
- K-12 teachers (UI/content designed for university level)
- Students wanting to play a competitive racing game (this is a demonstration tool, not a game)

---

## Solution Detail

### Core Capabilities (MoSCoW)

| Priority | Capability | Rationale |
|----------|------------|-----------|
| Must | Multi-client race viewing (students see race in their browser) | Core requirement — students spectate via their own devices |
| Must | Car generation from survey data (color, team name, function list) | Core game mechanic; v1 mapping: CSV -> prefab index + function tags |
| Must | Autonomous car racing on track (NavMesh, 15-50 cars) | Core visual experience; minimum 15, target 30-50 cars |
| Must | Professor event system with pre-configuration | 7 event types (v1 parity); events set up before race starts |
| Must | WebGL browser build | Accessibility — both professor and students access via browser |
| Must | Score dashboard / leaderboard | Track race progress and rankings |
| Must | Docker deployment | Easy self-hosting and local testing |
| Must | Survey data import from CSV | v1 format: teamName,colorIndex,functionList |
| Must | Session save/load | Professor can save race config and replay sessions |
| Must | Post-race data export (CSV) | Professor exports results for analysis and discussion |
| Should | In-game survey UI for students | Streamlined experience without external tools |
| Should | Multiple car models/colors (5 colors: green, black, red, blue, white) | v1 parity; visual differentiation |
| Should | Camera system (free camera + fixed positions) | Professor can show different angles |
| Should | Configurable attribute-to-speed mapping rules | Future iteration; v1 rules first |
| Could | Sound effects and music | Atmosphere and engagement |
| Could | Additional track layouts | Variety across sessions |
| Won't | Student-controlled cars | Cars are data-driven, not player-driven |
| Won't | LMS integration | Standalone tool first; defer integration |
| Won't | Flexible question designer (v2.1) | v2.0 reproduces v1 hardcoded fields first |

### MVP Scope

The minimum to validate the hypothesis:
1. Professor can define survey questions in-game
2. Survey data (from CSV import) generates cars with attributes
3. Cars race autonomously on the existing track
4. Professor can trigger at least 3 event types (speed boost, speed penalty, weather)
5. Score dashboard shows rankings
6. WebGL build runs in browser
7. Docker container serves the build

### User Flow

```
Professor Flow:
1. Launch game (browser or local) -> Main Menu
2. "Setup Race" -> Configure survey questions + attribute mapping
3. "Import Data" -> Load CSV or collect responses via in-game survey
4. "Start Race" -> Cars spawn at starting line based on data
5. During race -> Trigger events via Events Panel (keyboard shortcut or UI)
6. Race ends -> View final standings, discuss with class

Student Flow (if in-game survey):
1. Open browser -> Enter room code/URL
2. Answer survey questions
3. See confirmation that their car was created
4. (Optional) Watch race on professor's projected screen
```

---

## Technical Approach

**Feasibility**: HIGH (core racing) / MEDIUM (multi-client sync)

The v1 proved the core racing concept works. Unity 6 + URP + WebGL is well-supported. The main new challenge is **multi-client synchronization** — students viewing the same race in their browsers requires a server-authoritative architecture.

**Architecture Notes**

```
+------------------------------------------------------------------+
|  Docker Container                                                |
|                                                                  |
|  +--------------------+    +----------------------------------+  |
|  | WebSocket Server   |    | nginx :80                        |  |
|  | (Node.js / Python) |    | Serves WebGL build               |  |
|  | :8080              |    +----------------------------------+  |
|  |                    |                                          |
|  | - Session mgmt     |    +----------------------------------+  |
|  | - State broadcast  |    | WebGL Build (Unity 6 URP)        |  |
|  | - Event relay      |    |                                  |  |
|  | - CSV upload API   |    | Professor Client:                |  |
|  | - Survey collect   |    |   - Race setup & config          |  |
|  +--------+-----------+    |   - Event pre-configuration      |  |
|           |                |   - Start/pause/stop controls    |  |
|           |                |   - Camera controls              |  |
|           |                |                                  |  |
|           |  WebSocket     | Student Client:                  |  |
|           +<-------------->|   - Spectator camera (auto)      |  |
|                            |   - Leaderboard view             |  |
|                            |   - Survey UI (if in-game)       |  |
|                            +----------------------------------+  |
+------------------------------------------------------------------+

Data Flow:
  Professor uploads CSV ──> Server parses & stores
  Professor configures events ──> Server stores event schedule
  Professor starts race ──> Server broadcasts "start" to all clients
  Server ticks game state ──> All clients render synchronized race
  Professor triggers event ──> Server broadcasts event to all clients
  Race ends ──> Server exports results CSV
```

**Recommended sync approach**: Single WebGL build serves both professor and student roles. A lightweight WebSocket server (Node.js in the same Docker container) manages session state, relays professor commands, and broadcasts game events. Each client runs the same deterministic simulation seeded with the same data, ensuring visual consistency without streaming heavy 3D state.

**v1 Data Model (to reproduce)**

```
vehicleGroupData.csv format:
  teamName,colorIndex,functionList
  Bimonliftz,0,facerecog/glasses/password/distance/male
  Zoom,2,password

Color mapping: 0=green, 1=black, 2=red, 3=blue, 4=white
Functions: facerecog, glasses, language, password, distance, male

Car spawn: Random offset Vector3([-7,7], 0, [-1.2,1.2]), scale 2.5x
NavMeshAgent: speed/angularSpeed/acceleration from carSpec component
Events modify NavMeshAgent.speed at runtime with configurable duration
```

- **Survey Manager**: CSV import parser (v1 format compatible); future: in-game survey UI with WebSocket collection
- **Car Spawner**: Maps CSV rows to car prefabs by colorIndex; assigns function tags from slash-separated list
- **Event System**: 7 event types (v1 parity) pre-configured before race; professor triggers via UI during race
- **Race Controller**: NavMesh agent movement, checkpoint detection, scoring, camera management
- **Session Manager**: Save/load race configuration (questions, data, events) as JSON; export results as CSV
- **Sync Layer**: WebSocket relay ensures all connected browsers see the same race state

**Technical Risks**

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| WebGL performance with 30-50 cars | MEDIUM | Profile early; LOD system; reduce draw calls; test at 50 cars |
| Multi-client sync latency | MEDIUM | Deterministic simulation; only sync events, not full state |
| WebGL build size too large | LOW | Strip unused assets; compress textures; Brotli compression |
| NavMesh agent collision/clustering | MEDIUM | Stagger start positions; add avoidance priority; v1 used random offset |
| Browser compatibility issues | LOW | Test matrix across Chrome/Firefox/Safari/Edge |
| WebSocket connection stability | LOW | Reconnection logic; state snapshot on rejoin |

---

## Implementation Phases

| # | Phase | Description | Status | Parallel | Depends | PRP Plan |
|---|-------|-------------|--------|----------|---------|----------|
| 1 | Core Racing Loop | Car spawning, NavMesh movement, checkpoint system, scoring | complete | - | - | [core-racing-loop.plan.md](../plans/core-racing-loop.plan.md) |
| 2 | Event System | 7 pre-configurable event types with live triggering | complete | with 3, 4 | 1 | [event-system.plan.md](../plans/completed/event-system.plan.md) |
| 3 | Survey & Data | CSV import (v1 format), session save/load, results export | pending | with 2, 4 | 1 | - |
| 4 | UI & Camera | Professor controls, student spectator view, score dashboard | pending | with 2, 3 | 1 | - |
| 5 | Multi-Client Sync | WebSocket server, session management, deterministic sync | pending | - | 1, 2, 3, 4 | - |
| 6 | WebGL Build & Docker | WebGL optimization, Dockerfile, nginx + WS server, compose | pending | - | 5 | - |
| 7 | Polish & Testing | Visual effects, browser compat, 50-car perf testing, docs | pending | - | 6 | - |

### Phase Details

**Phase 1: Core Racing Loop**
- **Goal**: Cars spawn on track, race autonomously via NavMesh, pass through checkpoints, complete laps
- **Scope**: Car prefab setup with NavMeshAgent (v1 carSpec params); waypoint/checkpoint system on existing track; data model (teamName, colorIndex, functionList); lap counter; collision avoidance with random spawn offset
- **Success signal**: 15+ cars spawn at starting line and complete 3 laps without getting stuck

**Phase 2: Event System**
- **Goal**: Professor pre-configures events before race and triggers them during race
- **Scope**: 7 event types matching v1 (name-length penalty, color boost/penalty, function boost/penalty, snow weather, night weather); event pre-configuration UI; configurable speed delta and duration; keyboard shortcuts for live triggering
- **Success signal**: Professor pre-configures 3 events, starts race, triggers them in sequence; affected cars respond correctly

**Phase 3: Survey & Data**
- **Goal**: Full data pipeline from survey input to race output
- **Scope**: CSV import parser (v1 format: teamName,colorIndex,functionList); session save/load as JSON (questions, data, events, results); post-race CSV export; future: in-game survey UI
- **Success signal**: Import v1 vehicleGroupData.csv -> generate 37 cars correctly; save session; reload session; export results

**Phase 4: UI & Camera**
- **Goal**: Complete UI for both professor and student views
- **Scope**: Professor view: setup screen, event panel, free camera (WASD+mouse), fixed positions (1-9), pause/resume. Student view: spectator camera (auto-follow), leaderboard overlay. Shared: team name labels on cars, score dashboard
- **Success signal**: Professor and student see appropriate UI for their role; camera controls work; leaderboard updates in real-time

**Phase 5: Multi-Client Sync**
- **Goal**: Multiple browsers see the same race simultaneously
- **Scope**: Lightweight WebSocket server (Node.js); session/room management; professor commands relay (start, pause, trigger event); deterministic simulation sync (all clients run same simulation from same seed + data); student join flow (URL or room code)
- **Success signal**: Professor starts race in browser A; student opens browser B with room code; both see identical race progression

**Phase 6: WebGL Build & Docker**
- **Goal**: One-command deployment serving both WebGL game and WebSocket server
- **Scope**: WebGL build optimization (Brotli compression, memory settings); Dockerfile with nginx (static files) + Node.js (WebSocket); docker-compose.yml; build automation script
- **Success signal**: `docker-compose up` -> professor opens `localhost:8080` -> student opens same URL with room code -> race runs synchronized

**Phase 7: Polish & Testing**
- **Goal**: Production-ready for classroom deployment
- **Scope**: Weather visual effects (snow particles, night skybox); car trails; sound effects; browser testing (Chrome/Firefox/Safari/Edge); performance profiling at 50 cars (target 30fps); edge cases; README and deployment guide
- **Success signal**: Full session with 30+ cars runs smoothly across 4 browsers with weather effects active

### Parallelism Notes

- Phases 2, 3, and 4 can run in parallel after Phase 1 — they are independent systems integrated through the car data model
- Phase 5 (networking) depends on all gameplay phases being stable before adding sync
- Phase 6 (deployment) wraps everything into Docker
- Phase 7 is final polish

---

## Decisions Log

| Decision | Choice | Alternatives | Rationale |
|----------|--------|--------------|-----------|
| Render pipeline | URP | Standard RP, HDRP | URP balances quality and WebGL performance; project already converted |
| Survey parsing | C# in Unity | Python DataTool (v1), external API | Eliminates external dependency; single deployment unit |
| Car navigation | NavMesh Agent | Physics-based, spline-following | Proven in v1; reliable autonomous movement |
| Multi-client sync | WebSocket + deterministic sim | Unity Netcode, state streaming | WebGL limitations; lightweight; only sync events not full state |
| Deployment | Docker (nginx + Node.js WS) | GitHub Pages, cloud hosting | Professor self-hosts; single container; works offline |
| Data format | v1 CSV format first | JSON, database | Backwards compatible; proven; simple |
| Build target | WebGL | Desktop standalone, mobile | Browser access is core requirement; no install needed |
| Event timing | Pre-configured + live trigger | Live-only (v1), fully automated | Professor prepares before class; triggers during race for effect |

---

## Research Summary

**Market Context**
- No existing product combines survey-data-driven game mechanics with EDI education — genuine whitespace
- Kahoot/Blooket/Gimkit are quiz-correctness platforms poorly suited for sensitive EDI content
- Best practices from competitors: zero-friction join (room code), professor-as-controller, anonymous responses
- Anti-patterns to avoid: public individual leaderboards, celebratory game-show tone, quiz-correctness-only mechanics

**Technical Context**
- v1 proved the core concept across 18 releases with real classroom use
- Unity 6 WebGL is well-supported; 24.7% of web game devs use Unity
- Key packages already installed: AI Navigation, URP 17.3.0, Input System, Timeline, UGUI, UnitySkills MCP
- Race track (CartoonTracksPack1) and car models (CarsAssetPack) already imported in v2 project
- UnitySkills MCP (726 skills) available for editor automation during development

---

*Generated: 2026-07-06*
*Updated: 2026-07-06 (all open questions resolved)*
*Status: READY FOR IMPLEMENTATION*
