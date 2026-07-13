# EDI Racing Game v2

A Unity 6 WebGL racing game for university EDI (Equity, Diversity & Inclusion) education. Professors import student survey data as CSV, which generates autonomous racing cars. During the race, the professor triggers real-time events (weather, speed penalties/boosts) to demonstrate how different factors create unequal outcomes.

## Prerequisites

- **Unity 6** (with URP, AI Navigation, Input System)
- **Node.js 18+** (for the WebSocket server)
- **Docker & Docker Compose** (for deployment)

## Quick Start (Editor)

1. Open the project in Unity Hub (requires Unity 6)
2. Open the scene: `Assets/Scenes/complete_track_demo.unity`
3. Enter Play Mode
4. Click **Start Race** to race with the default CSV data, or **Load Session** to resume a saved session

### Track Setup (first time)

1. **Bake NavMesh**: `Window > AI > Navigation` — create a custom "Car" agent type, then bake
2. **Setup Track**: `EDI Racing > Setup Track` (menu) — auto-places waypoints and checkpoints

## CSV Data Format

```
teamName,colorIndex,functionList
Bimonliftz,0,facerecog/glasses/password/distance/male
Zoom,2,password
```

- **colorIndex**: 0=green, 1=black, 2=red, 3=blue, 4=white
- **functionList**: slash-separated tags (facerecog, glasses, language, password, distance, male)

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `1`-`7` | Trigger events 1-7 |
| `F1`-`F9` | Fixed camera positions |
| `Escape` | Return to free camera |
| `WASD` | Free camera movement |
| `Right-click + mouse` | Free camera look |
| `Q` / `E` | Camera down / up |
| `Scroll wheel` | Camera speed |
| `T` | Print scoreboard (console) |
| `P` | Save session |
| `L` | Load latest session |
| `X` | Export results CSV |

## Multi-Client Usage

1. **Professor**: Click **Host Room** on the setup screen — a 6-character room code appears
2. **Students**: Open the same URL in their browser, enter the room code, and click **Join**
3. Professor starts the race — all connected student browsers show the race in spectator mode

## Scripts & Commands

<!-- AUTO-GENERATED from scripts/, Deploy/, Server/, web-app/ -->

| Command | Description |
|---------|-------------|
| `./scripts/build-webgl.sh` | Build WebGL from CLI (auto-detects Unity 6 on macOS, or set `UNITY_PATH`) |
| `./Deploy/fix-webgl.sh` | Validate and patch `index.html` with correct Build artifact filenames |
| `./Deploy/fix-webgl.sh --serve` | Patch + start local HTTP server on port 8080 for testing |
| `cd Deploy && docker-compose up --build` | Build and start all services: nginx + WebSocket + survey web-app |
| `cd Server && npm start` | Run WebSocket relay server standalone (for local dev) |
| `cd web-app && npm start` | Run survey web-app API server standalone (port 3001) |
| `cd web-app && npm run dev` | Run survey web-app API with `--watch` hot reload |
| `cd web-app/client && npm run dev` | Start Vite dev server for survey React client |
| `cd web-app/client && npm run build` | Production build of survey React client |
| `cd web-app/client && npm run lint` | Lint survey client with oxlint |

<!-- /AUTO-GENERATED -->

## Environment Variables

<!-- AUTO-GENERATED from Server/server.js, web-app/src/, Deploy/docker-compose.yml -->

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `PORT` | No | `8080` (standalone) / `3000` (Docker) | WebSocket server listen port. Set in `docker-compose.yml` as `3000`; nginx proxies `/ws` to this port |
| `API_PORT` | No | `3001` | Survey web-app API listen port |
| `DB_PATH` | No | `web-app/data/edi-survey.db` (standalone) / `/app/data/edi-survey.db` (Docker) | SQLite database path for survey data. Docker volume `survey-data` persists this |
| `UNITY_PATH` | No | Auto-detected | Path to Unity executable; used by `scripts/build-webgl.sh` |

<!-- /AUTO-GENERATED -->

## WebGL Build

### From Unity Editor

`File > Build Settings > WebGL > Build`

### From CLI

```bash
./scripts/build-webgl.sh
# Or with explicit Unity path:
UNITY_PATH=/path/to/Unity ./scripts/build-webgl.sh
```

Output: `Deploy/webgl-build/`

### Fix Loading Issues

If the WebGL build gets stuck at loading, run the fixer script:

```bash
./Deploy/fix-webgl.sh          # patch index.html
./Deploy/fix-webgl.sh --serve  # patch + serve locally on :8080
```

## Docker Deployment

```bash
cd Deploy
docker-compose up --build
```

Open `http://localhost:8080` in a browser. Docker Compose runs two services:
- **edi-racing** — nginx (port 80→8080) serving the WebGL build with Brotli support, plus a Node.js WebSocket server on port 3000 (proxied at `/ws`)
- **web-app** — Express API + React SPA for survey management (proxied by nginx at `/survey/` and `/api`). Uses SQLite with a persistent Docker volume (`survey-data`)

## Architecture

```
CSV text -> CsvParser -> List<CarData> -> CarSpawner -> GameObjects
                                                          |
RaceManager registers cars with: ScoreManager, LapTracker, EventManager
                                                          |
Cars drive via CarController (NavMeshAgent + WaypointPath)
                                                          |
CheckpointTrigger -> LapTracker -> lap/ranking updates
```

**Key components:**
- **RaceManager** — central orchestrator; game state machine (Setup -> Racing -> Paused -> Finished)
- **CarController** — NavMeshAgent pathfinding with look-ahead, curvature braking, collision detection
- **EventManager** — attribute-based event rules; professor triggers via keyboard or UI
- **RuleEngine** — evaluates `EventRule` conditions against car attributes (replaces hardcoded event matching)
- **WeatherEffect** — snow particles and night-mode lighting transitions
- **NetworkSync** — WebSocket-based professor-to-student state broadcast
- **SurveyCollector** — collects student survey responses in real-time via WebSocket
- **SessionManager** — JSON save/load + CSV results export

## Project Structure

```
Assets/
  Scripts/
    Car/          CarController, CarIdentity
    Data/         CarData, CsvParser, JsonImporter, SessionManager, SessionData,
                  ResultsExporter, AttributeMapping, SurveyConfig, SurveyConfigManager,
                  SurveyQuestion, SurveyResponseMapper, SurveyTemplates
    Events/       EventManager, EventRule, EventSchedule, RuleEngine,
                  ComparisonOperator, WeatherType, WeatherEffect
    Race/         RaceManager, CarSpawner, LapTracker, ScoreManager, RaceConfig,
                  WaypointPath, CheckpointTrigger
    Camera/       CameraManager, SpectatorCamera, RaceCameraController, FixedCameraPoint
    UI/           RaceUI, SetupScreen, JoinScreen, LeaderboardPanel, EventPanel,
                  RaceFinishPanel, RaceControlPanel, StudentSurveyPanel,
                  SurveyBuilderPanel, ConfigManagerPanel, CarLabel, CarLabelSpawner,
                  GameState, TabButton, BuilderUIFactory, MappingEditorRow,
                  QuestionEditorRow, RuleEditorRow
    Network/      NetworkManager, NetworkSync, NetworkMessages, SurveyCollector
    Editor/       TrackSetupEditor, BuildScript, SceneWiring
    RuntimeSetup.cs
  Settings/       RaceConfig.asset, EventSchedule.asset, URP pipeline assets
  Prefabs/Cars/   Car_Green, Car_Black, Car_Red, Car_Blue, Car_White
  Scenes/         complete_track_demo.unity
Deploy/
  Dockerfile, docker-compose.yml, nginx/, start.sh, fix-webgl.sh, webgl-build/
Server/
  server.js       Node.js WebSocket relay server (ws library)
web-app/
  src/            Express API (index.js, db.js, routes)
  client/         React SPA (Vite + SurveyJS)
  Dockerfile      Multi-stage build (client + API)
scripts/
  build-webgl.sh  CLI WebGL build script
```

## License

University of Guelph — EDI Education Tool
