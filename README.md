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

## WebGL Build

### From Unity Editor

`File > Build Settings > WebGL > Build`

### From CLI

```bash
Unity -batchmode -executeMethod BuildScript.BuildWebGL -quit
```

Output: `Deploy/webgl-build/`

## Docker Deployment

```bash
cd Deploy
docker-compose up --build
```

Open `http://localhost:8080` in a browser. The container runs:
- **nginx** on port 80 — serves the WebGL build
- **Node.js WebSocket server** on port 3000 — handles multi-client sync

The `docker-compose.yml` maps container port 80 to host port 8080.

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
- **EventManager** — 7 pre-configured event types; professor triggers via keyboard or UI
- **WeatherEffect** — snow particles and night-mode lighting transitions
- **NetworkSync** — WebSocket-based professor-to-student state broadcast
- **SessionManager** — JSON save/load + CSV results export

## Project Structure

```
Assets/
  Scripts/
    Car/          CarController, CarIdentity
    Data/         CarData, CsvParser, SessionManager, ResultsExporter
    Events/       EventManager, EventMatcher, WeatherEffect, RaceEventConfig
    Race/         RaceManager, CarSpawner, LapTracker, ScoreManager, RaceConfig
    Camera/       CameraManager, SpectatorCamera, RaceCameraController
    UI/           RaceUI, SetupScreen, JoinScreen, LeaderboardPanel, EventPanel, RaceFinishPanel
    Network/      NetworkManager, NetworkSync, NetworkMessages
    Editor/       TrackSetupEditor, BuildScript
  Settings/       RaceConfig.asset, EventSchedule.asset, URP pipeline assets
  Prefabs/Cars/   Car_Green, Car_Black, Car_Red, Car_Blue, Car_White
  Scenes/         complete_track_demo.unity
Deploy/
  Dockerfile, docker-compose.yml, nginx/, start.sh, webgl-build/
Server/
  server.js       Node.js WebSocket server
```

## License

University of Guelph — EDI Education Tool
