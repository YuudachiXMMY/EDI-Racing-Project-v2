# Plan: Web-App ↔ Unity Gap Analysis & Game Hosting Guide

## Summary
This is a **research & analysis plan** — not an implementation plan. It documents how the Unity game is currently hosted, maps all existing web-app ↔ Unity integration points, and identifies feature gaps that could be implemented next. The deliverable is this document itself, which serves as a roadmap.

## User Story
As a professor/developer,
I want to understand how to host the EDI Racing game and what web-app ↔ Unity features are missing,
So that I can prioritize the next features to build.

## Metadata
- **Complexity**: N/A (analysis document)
- **Source PRD**: N/A
- **PRD Phase**: N/A
- **Estimated Files**: 0 (this is a gap analysis, not an implementation)

---

## Part 1: How to Host the Game in Unity

### Architecture Overview

The EDI Racing Project uses a **3-tier Docker deployment**:

```
┌─────────────────────────────────────────────────────────┐
│                    nginx (port 80)                       │
│  ┌──────────────────┐  ┌─────────────────────────────┐  │
│  │  Unity WebGL      │  │  /survey/ → web-app:3001    │  │
│  │  (static files)   │  │  /api/   → web-app:3001    │  │
│  │  /index.html      │  │  /ws     → WS server:3000  │  │
│  └──────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
         ↕ WebSocket (/ws)              ↕ HTTP REST
┌──────────────────────┐      ┌─────────────────────────┐
│  WebSocket Server     │      │  Survey Web App (Express)│
│  (Node.js, port 3000)│      │  React SPA + REST API    │
│  Room management      │      │  SQLite database         │
│  Message relay        │      │  port 3001               │
└──────────────────────┘      └─────────────────────────┘
```

### Step-by-Step Hosting Guide

#### 1. Build the Unity WebGL Game

In Unity Editor:
- **File → Build Settings → WebGL**
- Set compression to **Brotli** (nginx is configured for `.br` files)
- Build output goes to `Deploy/webgl-build/`

#### 2. Docker Deployment (Recommended)

```bash
cd Deploy/
docker compose up -d --build
```

This starts two containers:
| Container | Port | Description |
|---|---|---|
| `edi-racing-game` | 3900 → 80 | nginx serving Unity WebGL + WebSocket proxy |
| `edi-racing-api` | 3901 → 3001 | Survey web app (Express + React + SQLite) |

**Key files:**
- `Deploy/Dockerfile` — nginx + Node.js WebSocket server
- `Deploy/docker-compose.yml` — orchestrates both services
- `Deploy/nginx/nginx.conf` — reverse proxy config
- `Deploy/start.sh` — entrypoint (starts WS server + nginx)
- `Server/server.js` — WebSocket room management server

#### 3. Domain Configuration

The `docker-compose.yml` supports Traefik labels for domain routing:
- `GAME_DOMAIN=edi-racing.localhost` (or your real domain)
- `API_DOMAIN=api.edi-racing.localhost`

For production, set these in a `.env` file alongside docker-compose.yml.

#### 4. Access Points

| URL | What |
|---|---|
| `http://host:3900/` | Unity WebGL game (professor hosts room here) |
| `http://host:3900/survey/` | Survey web app dashboard (professor creates surveys) |
| `http://host:3900/survey/#/s/{shareCode}` | Student survey link |
| `ws://host:3900/ws` | WebSocket endpoint (auto-detected by Unity) |

---

## Part 2: Current Web-App ↔ Unity Integration Map

### Data Flow Overview

```
┌──────────────────┐                           ┌──────────────────┐
│   Web App (React)│                           │   Unity (WebGL)  │
│                  │                           │                  │
│  Professor:      │     ┌──────────────┐      │  Professor:      │
│  • Create Survey │────>│  REST API    │      │  • Host Room     │
│  • Edit Questions│     │  (Express)   │      │  • View Room Code│
│  • Share Link    │     └──────────────┘      │  • Start Race    │
│  • View Responses│                           │                  │
│  • Send to Game ─┼──── WebSocket relay ─────>│  • Receive Data  │
│                  │     (survey_import)        │  • Auto-start    │
│                  │                           │                  │
│  Student:        │                           │  Student:        │
│  • Fill Survey   │──── REST POST ──────────> │  • Join Room     │
│    (web form)    │     (responses table)      │  • Watch Race    │
│                  │                           │  • Fill Survey   │
│                  │                           │    (in-game)     │
└──────────────────┘                           └──────────────────┘
```

### Integration Points (6 existing)

| # | Feature | Direction | Protocol | Files |
|---|---------|-----------|----------|-------|
| 1 | **Room hosting** | Unity → WS Server | WebSocket `create_room` | `NetworkManager.cs`, `Server/server.js` |
| 2 | **Student joining** | Unity → WS Server | WebSocket `join_room` | `JoinScreen.cs`, `Server/server.js` |
| 3 | **Survey direct send** | Web → WS → Unity | WebSocket `survey_import` via REST proxy | `SendToGameModal.jsx`, `export.js:117`, `SetupScreen.cs:297` |
| 4 | **Race state sync** | Unity → Unity (via WS) | WebSocket `state_update`, `leaderboard` | `NetworkSync.cs` |
| 5 | **In-game survey** | Unity → Unity (via WS) | WebSocket `survey_questions/response` | `SurveyCollector.cs`, `StudentSurveyPanel.cs` |
| 6 | **Export JSON** | Web → Download | REST GET `/api/surveys/:id/export` | `export.js:84`, `EditorPage.jsx:44` |

---

## Part 3: Feature Gap Analysis

### GAP 1: Race Results → Web App (HIGH PRIORITY)
**Status:** Not implemented
**Problem:** After a race finishes, results (rankings, lap times, event log) exist only inside Unity. The web app has no way to see race outcomes.

**Current state:**
- Unity can export results to CSV via `SessionManager.ExportResults()` (keyboard shortcut X)
- Uses `WebSocketBridge_DownloadFile` to trigger browser download
- Web app has no endpoint to receive or display race results

**What's needed:**
- Unity sends `race_results` message via WebSocket after race ends
- Server relays to web-app client (or stores in DB)
- Web app displays results in a new "Results" tab per survey
- Enables professor to review outcomes without being at the game screen

### GAP 2: Real-Time Race Viewer in Web App (MEDIUM PRIORITY)
**Status:** Not implemented
**Problem:** Only students who join via Unity can watch the race. The web app has no live view.

**Current state:**
- Students join Unity WebGL and see cars via `NetworkSync` interpolation
- Web app only manages surveys — no game visualization

**What's needed:**
- A "Live Race" page in the web app that connects via WebSocket
- Receives `state_update` and `leaderboard` messages
- Renders a simplified 2D minimap or leaderboard-only view
- Useful for projecting on classroom screen without Unity

### GAP 3: Game Status Visibility in Web App (HIGH PRIORITY)
**Status:** Not implemented
**Problem:** The web app has no idea whether the Unity game is running, whether a room exists, or what state the race is in.

**Current state:**
- `SendToGameModal` blindly tries to connect and shows error if room not found
- No way to know if room is open, race started, or how many students joined

**What's needed:**
- WebSocket or REST endpoint to query room status
- Web app shows room status badge (e.g., "Room ABCD: 5 students, Setup phase")
- Avoids confusion when professor tries to send data to a room that doesn't exist

### GAP 4: Bi-directional Config Sync (MEDIUM PRIORITY)
**Status:** Partially implemented (one-way only: web → Unity)
**Problem:** Survey configs created in Unity's built-in `SurveyBuilderPanel` can't be exported to the web app, and vice versa (web configs can only be sent, not synced).

**Current state:**
- Web app: full CRUD for surveys with persistent SQLite storage
- Unity: `SurveyConfigManager` saves/loads configs as local JSON files
- Web → Unity: `send-to-game` sends processed CarData + rules (one-shot, loses raw config)
- Unity → Web: no path

**What's needed:**
- Unity could POST its `SurveyConfig` JSON to the web app REST API
- Web app could import Unity-format configs
- Or: deprecate Unity's built-in survey builder in favor of the web app (simpler)

### GAP 5: Student Identity Linking (LOW PRIORITY)
**Status:** Not implemented
**Problem:** Students who fill out the web survey and students who join the Unity room are not linked. A student could fill the survey on the web but not see "their" car in the game.

**Current state:**
- Web survey collects email + teamName, stores in SQLite `responses` table
- Unity room join only requires room code (no identity)
- No way to show a student "your car is #3 (Team Alpha)"

**What's needed:**
- Student identifies themselves when joining Unity room (team name or email)
- Server matches web survey response to Unity player
- Unity highlights "your car" for each student viewer

### GAP 6: Multi-Room / Session History (LOW PRIORITY)
**Status:** Not implemented
**Problem:** Each game run is ephemeral. When the room closes, all data is lost from the server.

**Current state:**
- `Server/server.js` stores rooms in-memory `Map`
- When professor disconnects, room is deleted and students are notified
- Unity can save/load sessions locally (`SessionManager`)
- No persistent game session history

**What's needed:**
- Server persists room sessions (or delegates to web app DB)
- Web app shows history of past game sessions with their results
- Professor can review, compare, and export historical data

### GAP 7: Error Recovery & Reconnection (MEDIUM PRIORITY)
**Status:** Not implemented
**Problem:** If WebSocket connection drops, there's no automatic reconnection.

**Current state:**
- `NetworkManager.cs` has no reconnect logic
- `WebSocketBridge.jslib` does not retry
- If connection drops mid-race, students see frozen cars

**What's needed:**
- Automatic reconnection with exponential backoff
- Server preserves room state for reconnecting clients
- Students receive latest state on reconnection (partially exists: `latestState` cache in server)

### GAP 8: Survey Response Real-Time Sync (LOW PRIORITY)
**Status:** Not implemented
**Problem:** When students submit survey responses on the web, the Unity game doesn't know until professor clicks "Send to Game".

**Current state:**
- Web survey submissions go to REST API → SQLite
- Unity only gets data when professor explicitly sends via `SendToGameModal`
- No push notification to Unity when new responses arrive

**What's needed:**
- WebSocket notification to Unity when new web responses arrive
- Unity shows live response count from web surveys
- Optional auto-send when response threshold reached

---

## Priority Ranking

| Priority | Gap | Effort | Value |
|---|---|---|---|
| **P0** | GAP 1: Race Results → Web App | Medium | High — closes the feedback loop |
| **P0** | GAP 3: Game Status Visibility | Small | High — reduces user confusion |
| **P1** | GAP 7: Error Recovery | Medium | Medium — reliability |
| **P1** | GAP 2: Real-Time Race Viewer | Large | Medium — classroom UX |
| **P2** | GAP 5: Student Identity Linking | Medium | Medium — personalization |
| **P2** | GAP 8: Survey Response Real-Time Sync | Small | Low — convenience |
| **P3** | GAP 4: Bi-directional Config Sync | Medium | Low — can use web-only workflow |
| **P3** | GAP 6: Multi-Room / Session History | Large | Low — nice to have |

---

## Recommended Next Steps

1. **Start with GAP 3** (Game Status Visibility) — small effort, immediate UX win
2. **Then GAP 1** (Race Results → Web App) — completes the professor workflow loop
3. **Then GAP 7** (Reconnection) — makes the system production-reliable

> To implement any of these, run `/prp-plan <gap description>` to generate a detailed implementation plan for the specific gap you want to tackle.
