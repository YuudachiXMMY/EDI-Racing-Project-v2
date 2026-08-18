# Plan: Core Racing Loop

## Summary
Implement the foundational racing system: car data model, car spawning from CSV data, NavMesh-based autonomous movement along waypoints, checkpoint detection, lap counting, and basic score tracking. This is Phase 1 of the EDI Racing Game v2, upon which all other phases depend.

## User Story
As a professor, I want cars to spawn on the track based on survey data and race autonomously through checkpoints, so that I can demonstrate data-driven outcomes visually.

## Problem -> Solution
No game logic exists in the v2 project (only imported track + car assets) -> Complete racing loop with 15-50 cars racing autonomously on the existing oval track.

## Metadata
- **Complexity**: Large
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 1 — Core Racing Loop
- **Estimated Files**: 12 new C# scripts + 6 prefabs/assets

---

## UX Design

### Before
N/A — no game logic exists. Opening the scene shows a static track with no cars.

### After
```
1. Game loads complete_track_demo scene
2. RaceManager reads vehicleGroupData.csv (via TextAsset)
3. Cars spawn at starting line with random offset
4. Race starts -> cars follow NavMesh waypoints around oval track
5. Checkpoint colliders detect car passage
6. Lap counter increments after full loop
7. Score system tracks rank by checkpoints passed + time
```

### Interaction Changes
N/A — internal change. No professor/student UI in this phase (that is Phase 4).

---

## Mandatory Reading

| Priority | File | Why |
|---|---|---|
| P0 | `Assets/Scenes/complete_track_demo.unity` | Track layout, existing GameObjects, positions |
| P0 | `Assets/Unity Technologies/CarsAssetPack/Prefabs/FBX/Car1.prefab` | Car model structure, components, scale |
| P0 | `Assets/CartoonTracksPack1/Track1/Prefabs/Track/oval_complete_colliders.prefab` | Track collider geometry for NavMesh baking |
| P1 | `Assets/Settings/PC_RPAsset.asset` | URP render settings |
| P2 | `Assets/CartoonTracksPack1/Track1/Prefabs/Props/prop_startline.prefab` | Starting line position reference |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity 6 NavMesh | Unity AI Navigation 2.0 docs | Use `NavMeshSurface` component for runtime baking; `NavMeshAgent` for car movement |
| Unity 6 WebGL | Unity WebGL build docs | `StreamReader` file I/O not available in WebGL; use `TextAsset` or `UnityWebRequest` instead |
| URP materials | Unity URP docs | Car materials may need URP shader upgrade if still using Standard |

---

## Patterns to Mirror

### V1_CAR_SPEC (Data Model)
```csharp
// SOURCE: v1 Assets/Prefabs/Cars/carSpec.cs
// Pure data component — public fields, no logic
public class carSpec : MonoBehaviour
{
    public string groupName;
    public string model;
    public string color;
    public float rbMass = 100f;
    public float rbAngularDrag = 0.5f;
    public float speed;
    public float automoveSpeed = 40f;
    public float automoveAngularSpeed = 800f;
    public float automoveAcceleration = 60f;
    public float automoveBaseOffset = 0.42f;
    public float automoveRankedTime;
    public int automoveRound;
    public int automoveTargetsTotalCount;
    public string[] functionList;
}
```

### V1_AUTOMOVEMENT (NavMesh Driving)
```csharp
// SOURCE: v1 Assets/Scripts/Automovement.cs
// Key pattern: sequential waypoint following with NavMeshAgent
// On reaching waypoint -> increment index -> set next destination
// DashboardTime resets per checkpoint for ranking accuracy
// Speed events: triggerSpeedEvent(addSpeed, waitTime) modifies agent.speed temporarily
```

### V1_CAR_SPAWN (Instantiation)
```csharp
// SOURCE: v1 Assets/Scripts/Game Manager/LoadCharacter.cs
// Pattern: read CSV row -> select prefab by colorIndex -> set carSpec fields
// -> instantiate at spawnPoint + randomOffset -> scale 2.5x
// -> add Rigidbody, NavMeshAgent, Automovement at runtime
Vector3 randomOffset = new Vector3(Random.Range(-7f, 7f), 0, Random.Range(-1.2f, 1.2f));
player = Instantiate(prefab, spawnPoint.position + 5 * randomOffset, spawnPoint.rotation);
player.transform.localScale *= 2.5f;
```

### V1_CHECKPOINT (Detection)
```csharp
// SOURCE: v1 Assets/Scripts/Game Manager/carPointDetect.cs
// Pattern: checkpoint has trigger collider, OnTriggerEnter checks for car component
// Resets car's ranked time, pushes to ScoreDashboard
```

### V1_SCORE (Ranking)
```csharp
// SOURCE: v1 Assets/Scripts/Game Manager/ScoreDashboard.cs
// Pattern: sort cars by automoveTargetsTotalCount DESC, then automoveRankedTime ASC
// Renders to TMPro text with format: (rank) [round] groupName - time
```

---

## Files to Change

| File | Action | Description |
|---|---|---|
| `Assets/Scripts/Data/CarData.cs` | CREATE | Immutable car data struct (replaces carSpec as data) |
| `Assets/Scripts/Data/CsvParser.cs` | CREATE | Parse vehicleGroupData.csv format into CarData list |
| `Assets/Scripts/Car/CarController.cs` | CREATE | NavMeshAgent-based autonomous movement |
| `Assets/Scripts/Car/CarIdentity.cs` | CREATE | MonoBehaviour holding runtime car state |
| `Assets/Scripts/Race/RaceManager.cs` | CREATE | Orchestrates: load data -> spawn cars -> start race |
| `Assets/Scripts/Race/CarSpawner.cs` | CREATE | Instantiates car prefabs from CarData |
| `Assets/Scripts/Race/CheckpointTrigger.cs` | CREATE | Trigger collider on checkpoint |
| `Assets/Scripts/Race/WaypointPath.cs` | CREATE | Holds ordered Transform[] waypoints |
| `Assets/Scripts/Race/LapTracker.cs` | CREATE | Tracks checkpoint progress and lap count |
| `Assets/Scripts/Race/ScoreManager.cs` | CREATE | Ranks cars by laps + time |
| `Assets/Scripts/Race/RaceConfig.cs` | CREATE | ScriptableObject for race settings |

## NOT Building
- Event system (Phase 2)
- Survey UI or question configuration (Phase 3)
- Score dashboard UI / camera controls (Phase 4)
- Networking / multi-client sync (Phase 5)
- WebGL build / Docker (Phase 6)
- Sound effects, weather visuals (Phase 7)
- Any UI beyond debug console output for scores

---

## Step-by-Step Tasks

### Task 1: Project Structure
- **ACTION**: Create folder structure for scripts
- **IMPLEMENT**: Create directories: `Assets/Scripts/Data/`, `Assets/Scripts/Car/`, `Assets/Scripts/Race/`, `Assets/Prefabs/Cars/`, `Assets/Prefabs/Race/`, `Assets/Data/`
- **VALIDATE**: Folders exist in project

### Task 2: CarData (Immutable Data Struct)
- **ACTION**: Create immutable data struct for car attributes
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Data/CarData.cs
using System;

[Serializable]
public struct CarData
{
    public string TeamName;
    public int ColorIndex;       // 0=green,1=black,2=red,3=blue,4=white
    public string[] Functions;   // e.g. ["facerecog","glasses","male"]

    public CarData(string teamName, int colorIndex, string[] functions)
    {
        TeamName = teamName;
        ColorIndex = colorIndex;
        Functions = functions ?? Array.Empty<string>();
    }
}
```
- **MIRROR**: V1_CAR_SPEC but as immutable struct
- **GOTCHA**: Must be `[Serializable]` for Unity inspector and JSON serialization
- **VALIDATE**: Compiles without errors

### Task 3: CsvParser
- **ACTION**: Create CSV parser for v1 vehicleGroupData.csv format
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Data/CsvParser.cs
// Format per line: teamName,colorIndex,functionList (slash-separated)
// Example: "Bimonliftz,0,facerecog/glasses/password/distance/male"
// WebGL-compatible: accept string content, NOT StreamReader
using System.Collections.Generic;
using System.Linq;

public static class CsvParser
{
    public static List<CarData> Parse(string csvContent)
    {
        var cars = new List<CarData>();
        var lines = csvContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var columns = trimmed.Split(',');
            if (columns.Length < 2) continue;

            string teamName = columns[0].Trim();
            if (!int.TryParse(columns[1].Trim(), out int colorIndex))
                colorIndex = 0;
            string[] functions = columns.Length > 2 && !string.IsNullOrEmpty(columns[2])
                ? columns[2].Split('/').Select(f => f.Trim().ToLower()).Where(f => f.Length > 0).ToArray()
                : System.Array.Empty<string>();

            cars.Add(new CarData(teamName, colorIndex, functions));
        }
        return cars;
    }
}
```
- **MIRROR**: V1_CAR_SPAWN CSV parsing logic
- **GOTCHA**: v1 used `StreamReader` which fails in WebGL — use string input. Handle trailing newlines, empty lines, emoji in team names.
- **VALIDATE**: Parse v1 sample CSV (37 rows) -> 37 CarData objects

### Task 4: RaceConfig (ScriptableObject)
- **ACTION**: Create ScriptableObject for configurable race parameters
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Race/RaceConfig.cs
using UnityEngine;

[CreateAssetMenu(fileName = "RaceConfig", menuName = "EDI Racing/Race Config")]
public class RaceConfig : ScriptableObject
{
    [Header("Car Settings")]
    public float DefaultSpeed = 40f;
    public float AngularSpeed = 800f;
    public float Acceleration = 60f;
    public float BaseOffset = 0.42f;
    public float CarScale = 2.5f;
    public float RigidbodyMass = 100f;
    public float RigidbodyAngularDrag = 0.5f;

    [Header("Spawn Settings")]
    public float SpawnOffsetX = 7f;
    public float SpawnOffsetZ = 1.2f;
    public float SpawnSpreadMultiplier = 5f;

    [Header("Race Settings")]
    public int TotalLaps = 3;
}
```
- **MIRROR**: V1_CAR_SPEC default values (speed=40, angularSpeed=800, etc.)
- **VALIDATE**: ScriptableObject asset creatable via Assets > Create > EDI Racing > Race Config

### Task 5: CarIdentity (Runtime Component)
- **ACTION**: Create MonoBehaviour holding runtime state for each spawned car
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Car/CarIdentity.cs
using UnityEngine;

public class CarIdentity : MonoBehaviour
{
    [Header("Identity")]
    public string TeamName;
    public int ColorIndex;
    public string[] Functions;

    [Header("Race Progress")]
    public int CurrentCheckpointIndex;
    public int TotalCheckpointsPassed;
    public int CurrentLap;
    public float CheckpointTime;

    public void Initialize(CarData data)
    {
        TeamName = data.TeamName;
        ColorIndex = data.ColorIndex;
        Functions = data.Functions;
        CurrentCheckpointIndex = 0;
        TotalCheckpointsPassed = 0;
        CurrentLap = 0;
        CheckpointTime = 0f;
    }

    private void Update()
    {
        CheckpointTime += Time.deltaTime;
    }
}
```
- **MIRROR**: V1_CAR_SPEC (groupName, functionList, automoveRound, automoveRankedTime)
- **VALIDATE**: Component can be added to a GameObject

### Task 6: WaypointPath
- **ACTION**: Create component to hold ordered waypoints for the track
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Race/WaypointPath.cs
using UnityEngine;

public class WaypointPath : MonoBehaviour
{
    public Transform[] Waypoints;

    public Transform GetWaypoint(int index)
    {
        return Waypoints[index % Waypoints.Length];
    }

    public int Count => Waypoints.Length;

    private void OnDrawGizmos()
    {
        if (Waypoints == null || Waypoints.Length < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < Waypoints.Length; i++)
        {
            if (Waypoints[i] == null) continue;
            Gizmos.DrawSphere(Waypoints[i].position, 1f);
            var next = Waypoints[(i + 1) % Waypoints.Length];
            if (next != null)
                Gizmos.DrawLine(Waypoints[i].position, next.position);
        }
    }
}
```
- **MIRROR**: V1_AUTOMOVEMENT targets array
- **GOTCHA**: Waypoints must be placed manually along track center. OnDrawGizmos helps visualize.
- **VALIDATE**: Visible gizmo path in editor when waypoints assigned

### Task 7: CarController (NavMesh Autonomous Movement)
- **ACTION**: Create NavMeshAgent-based car controller
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Car/CarController.cs
using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CarIdentity))]
public class CarController : MonoBehaviour
{
    private NavMeshAgent agent;
    private WaypointPath waypointPath;
    private int currentWaypointIndex;
    private float baseSpeed;

    public void Initialize(WaypointPath path, float speed, float angularSpeed, float acceleration)
    {
        agent = GetComponent<NavMeshAgent>();
        waypointPath = path;
        baseSpeed = speed;

        agent.speed = speed;
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        agent.autoBraking = false;

        currentWaypointIndex = 0;
        SetNextDestination();
    }

    private void Update()
    {
        if (agent == null || waypointPath == null) return;
        if (!agent.pathPending && agent.remainingDistance < 2f)
        {
            currentWaypointIndex++;
            SetNextDestination();
        }
    }

    private void SetNextDestination()
    {
        Transform target = waypointPath.GetWaypoint(currentWaypointIndex);
        agent.SetDestination(target.position);
    }

    public void ApplySpeedModifier(float delta, float duration)
    {
        StartCoroutine(SpeedModifierCoroutine(delta, duration));
    }

    private IEnumerator SpeedModifierCoroutine(float delta, float duration)
    {
        agent.speed += delta;
        yield return new WaitForSeconds(duration);
        agent.speed = baseSpeed;
    }

    public float BaseSpeed => baseSpeed;
}
```
- **MIRROR**: V1_AUTOMOVEMENT (sequential waypoints, speed events via coroutine)
- **GOTCHA**: `autoBraking = false` prevents deceleration at waypoints. `remainingDistance < 2f` threshold prevents stuck cars. ApplySpeedModifier is the hook for Phase 2 events.
- **VALIDATE**: Car follows waypoints without getting stuck

### Task 8: CheckpointTrigger
- **ACTION**: Create checkpoint trigger collider component
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Race/CheckpointTrigger.cs
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Index of this checkpoint in the track sequence")]
    public int CheckpointIndex;

    private LapTracker lapTracker;

    private void Start()
    {
        lapTracker = FindFirstObjectByType<LapTracker>();
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var carIdentity = other.GetComponentInParent<CarIdentity>();
        if (carIdentity == null) return;
        lapTracker?.OnCarPassedCheckpoint(carIdentity, CheckpointIndex);
    }
}
```
- **MIRROR**: V1_CHECKPOINT
- **GOTCHA**: Use `GetComponentInParent` since collider may be on child mesh. Must span full track width.
- **VALIDATE**: Fires when car passes through

### Task 9: LapTracker
- **ACTION**: Create lap and checkpoint progress tracker
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Race/LapTracker.cs
using UnityEngine;
using System;

public class LapTracker : MonoBehaviour
{
    [SerializeField] private int totalCheckpoints = 14;

    public event Action<CarIdentity> OnLapCompleted;
    public event Action<CarIdentity, int> OnCheckpointPassed;

    public void OnCarPassedCheckpoint(CarIdentity car, int checkpointIndex)
    {
        int expectedIndex = car.CurrentCheckpointIndex % totalCheckpoints;
        if (checkpointIndex != expectedIndex) return;

        car.TotalCheckpointsPassed++;
        car.CurrentCheckpointIndex++;
        car.CheckpointTime = 0f;

        OnCheckpointPassed?.Invoke(car, checkpointIndex);

        if (car.CurrentCheckpointIndex % totalCheckpoints == 0)
        {
            car.CurrentLap++;
            OnLapCompleted?.Invoke(car);
        }
    }
}
```
- **MIRROR**: V1_CHECKPOINT + V1_SCORE
- **GOTCHA**: Enforce sequential checkpoint passage. v1 used 14 checkpoints.
- **VALIDATE**: Lap increments after all checkpoints passed in order

### Task 10: ScoreManager
- **ACTION**: Create score ranking system
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Race/ScoreManager.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private readonly List<CarIdentity> cars = new List<CarIdentity>();

    public void RegisterCar(CarIdentity car)
    {
        cars.Add(car);
    }

    public List<CarIdentity> GetRankedCars()
    {
        return cars
            .OrderByDescending(c => c.TotalCheckpointsPassed)
            .ThenBy(c => c.CheckpointTime)
            .ToList();
    }

    public string GetScoreboardText()
    {
        var ranked = GetRankedCars();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < ranked.Count; i++)
        {
            var c = ranked[i];
            sb.AppendLine($"({i + 1}) [{c.CurrentLap}] {c.TeamName} - {c.CheckpointTime:F1}s");
        }
        return sb.ToString();
    }
}
```
- **MIRROR**: V1_SCORE ranking (totalCount DESC, rankedTime ASC)
- **VALIDATE**: Correct rank order after checkpoint events

### Task 11: CarSpawner
- **ACTION**: Create car instantiation system
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Race/CarSpawner.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CarSpawner : MonoBehaviour
{
    [Tooltip("Car prefabs indexed by color: 0=green,1=black,2=red,3=blue,4=white")]
    public GameObject[] CarPrefabs;
    public Transform SpawnPoint;
    public WaypointPath WaypointPath;
    public RaceConfig Config;

    public List<GameObject> SpawnCars(List<CarData> carDataList)
    {
        var spawnedCars = new List<GameObject>();
        foreach (var data in carDataList)
        {
            int prefabIndex = Mathf.Clamp(data.ColorIndex, 0, CarPrefabs.Length - 1);
            GameObject prefab = CarPrefabs[prefabIndex];

            Vector3 randomOffset = new Vector3(
                Random.Range(-Config.SpawnOffsetX, Config.SpawnOffsetX),
                0,
                Random.Range(-Config.SpawnOffsetZ, Config.SpawnOffsetZ)
            );
            Vector3 spawnPos = SpawnPoint.position + Config.SpawnSpreadMultiplier * randomOffset;

            GameObject car = Instantiate(prefab, spawnPos, SpawnPoint.rotation);
            car.transform.localScale *= Config.CarScale;
            car.name = data.TeamName;

            var identity = car.GetComponent<CarIdentity>();
            if (identity == null) identity = car.AddComponent<CarIdentity>();
            identity.Initialize(data);

            var rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = car.AddComponent<Rigidbody>();
            rb.mass = Config.RigidbodyMass;
            rb.angularDamping = Config.RigidbodyAngularDrag;

            var agent = car.GetComponent<NavMeshAgent>();
            if (agent == null) agent = car.AddComponent<NavMeshAgent>();
            agent.baseOffset = Config.BaseOffset;

            var controller = car.GetComponent<CarController>();
            if (controller == null) controller = car.AddComponent<CarController>();
            controller.Initialize(WaypointPath, Config.DefaultSpeed, Config.AngularSpeed, Config.Acceleration);

            spawnedCars.Add(car);
        }
        return spawnedCars;
    }
}
```
- **MIRROR**: V1_CAR_SPAWN (randomOffset, scale 2.5x, runtime component addition)
- **GOTCHA**: Unity 6 renamed `angularDrag` to `angularDamping`. Clamp colorIndex to valid range.
- **VALIDATE**: 37 cars spawn from v1 CSV with correct colors

### Task 12: RaceManager (Orchestrator)
- **ACTION**: Create main orchestrator
- **IMPLEMENT**:
```csharp
// Assets/Scripts/Race/RaceManager.cs
using System.Collections.Generic;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
    [Header("References")]
    public CarSpawner CarSpawner;
    public LapTracker LapTracker;
    public ScoreManager ScoreManager;
    public RaceConfig Config;

    [Header("Data")]
    public TextAsset DefaultCsvData;

    private List<GameObject> spawnedCars;
    private bool raceStarted;

    private void Start()
    {
        if (DefaultCsvData != null)
        {
            LoadAndStartRace(DefaultCsvData.text);
        }
    }

    public void LoadAndStartRace(string csvContent)
    {
        var carDataList = CsvParser.Parse(csvContent);
        Debug.Log($"[RaceManager] Parsed {carDataList.Count} cars from CSV");

        spawnedCars = CarSpawner.SpawnCars(carDataList);

        foreach (var car in spawnedCars)
        {
            var identity = car.GetComponent<CarIdentity>();
            ScoreManager.RegisterCar(identity);
        }

        LapTracker.OnLapCompleted += OnCarCompletedLap;
        raceStarted = true;
        Debug.Log($"[RaceManager] Race started with {spawnedCars.Count} cars");
    }

    private void OnCarCompletedLap(CarIdentity car)
    {
        Debug.Log($"[Race] {car.TeamName} completed lap {car.CurrentLap}");
        if (car.CurrentLap >= Config.TotalLaps)
            Debug.Log($"[Race] {car.TeamName} FINISHED!");
    }

    private void Update()
    {
        if (raceStarted && Input.GetKeyDown(KeyCode.F1))
            Debug.Log("[Scoreboard]\n" + ScoreManager.GetScoreboardText());
    }
}
```
- **MIRROR**: V1 LoadCharacter.cs orchestration
- **GOTCHA**: TextAsset for WebGL compatibility. F1 debug output is temporary (Phase 4 adds UI).
- **VALIDATE**: Full race: CSV -> spawn -> 3 laps -> finish messages

### Task 13: Scene Setup
- **ACTION**: Configure complete_track_demo scene with all racing components
- **IMPLEMENT**:
  1. Bake NavMesh on track surface (add NavMeshSurface to track ground mesh)
  2. Create "Waypoints" parent with 14+ child empty transforms along racing line
  3. Create "Checkpoints" parent with 14 children, each with BoxCollider(isTrigger) + CheckpointTrigger
  4. Create "RaceManager" GameObject: attach RaceManager, CarSpawner, LapTracker, ScoreManager, WaypointPath
  5. Create "SpawnPoint" transform at starting line
  6. Create RaceConfig asset at `Assets/Settings/RaceConfig.asset`
  7. Place vehicleGroupData.csv as TextAsset at `Assets/Data/vehicleGroupData.csv`
  8. Wire all references in Inspector
- **GOTCHA**: NavMesh must cover track surface only. Checkpoint colliders must be wide and tall enough for 2.5x scaled cars.
- **VALIDATE**: Play mode -> cars spawn and complete laps

### Task 14: Car Prefab Setup
- **ACTION**: Create 5 color-variant car prefabs
- **IMPLEMENT**:
  1. Use models from `Assets/Unity Technologies/CarsAssetPack/Prefabs/FBX/` (Car1-Car5)
  2. Duplicate each -> rename RaceCar_{Green,Black,Red,Blue,White}
  3. Add CarIdentity component to each
  4. Ensure colliders exist for NavMesh detection
  5. Apply color-tinted URP materials
  6. Save to `Assets/Prefabs/Cars/`
- **GOTCHA**: Models may use Standard shader — need URP upgrade. Base scale should be normal (2.5x applied at spawn time).
- **VALIDATE**: 5 prefabs visible with distinct colors in play mode

---

## Testing Strategy

### Manual Tests (Unity Play Mode)

| Test | Input | Expected | Edge Case? |
|---|---|---|---|
| CSV parse empty | Empty string | 0 cars, no error | Yes |
| CSV parse v1 data | 37-row v1 CSV | 37 CarData correct | No |
| CSV parse bad colorIndex | "Team,99,func" | Clamps to valid range | Yes |
| Spawn 15 cars | 15-row CSV | 15 cars on track | No |
| Spawn 50 cars | 50-row CSV | 50 cars, no stuck | Yes |
| Car reaches waypoint | Play race | Car turns to next | No |
| Car completes lap | Run 1 lap | Lap counter = 1 | No |
| Checkpoint skip | Car skips one | Checkpoint rejected | Yes |
| Score ranking | 5 cars | Correct order | No |
| Speed modifier | +20 for 5s | Increases then reverts | No |

### Edge Cases Checklist
- [ ] Empty CSV (no cars)
- [ ] Duplicate team names
- [ ] Missing function list column
- [ ] Car stuck on geometry
- [ ] 50 cars NavMesh congestion
- [ ] High-speed waypoint overshoot
- [ ] Checkpoint too small for scaled cars

---

## Validation Commands

### Compile Check
```bash
curl -s http://localhost:8090/skills/debug/compile-check | jq .
```
EXPECT: Zero compile errors

### Play Mode Test
```
1. Open Assets/Scenes/complete_track_demo.unity
2. Press Play
3. Observe: cars spawn at starting line
4. Observe: cars race around track
5. Press F1: scoreboard in Console
6. Wait 3 laps: "FINISHED" messages
```
EXPECT: 15+ cars complete 3 laps

---

## Acceptance Criteria
- [ ] CsvParser parses v1 vehicleGroupData.csv (37 cars)
- [ ] 5 color-variant car prefabs created
- [ ] Cars spawn at starting line with random spread
- [ ] NavMesh baked, cars follow waypoints
- [ ] 14+ checkpoints with trigger colliders
- [ ] Lap counting works (3 laps)
- [ ] Score ranking by checkpoints then time
- [ ] Speed modifier API works (Phase 2 hook)
- [ ] Minimum 15 cars race simultaneously
- [ ] Target 30-50 cars without severe degradation
- [ ] No compile errors
- [ ] WebGL-compatible (no file I/O)

## Completion Checklist
- [ ] Immutable patterns (CarData is struct)
- [ ] No hardcoded values (all in RaceConfig)
- [ ] Error handling for CSV edge cases
- [ ] No string-based FindObjectByName lookups
- [ ] Components decoupled (events/delegates for communication)
- [ ] Self-contained — no questions needed

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| NavMesh not covering track | MEDIUM | HIGH | Verify track mesh colliders; add NavMeshModifier if needed |
| Cars stuck at turns | MEDIUM | MEDIUM | Tune waypoint placement; increase agent radius |
| 50 cars NavMesh congestion | MEDIUM | MEDIUM | Spread avoidance priority; stagger spawn |
| Car scale mismatch | LOW | LOW | Test with prefabs; adjust RaceConfig |
| Unity 6 API renames | LOW | MEDIUM | angularDrag->angularDamping confirmed |

## Notes
- v1 used FindObjectByName extensively — v2 uses direct inspector references
- v1 had 14 checkpoints (expanded from 8 in earlier releases)
- All scripts must be WebGL-compatible from day one
- WaypointPath and CheckpointTrigger placement is manual — consider UnitySkills MCP for automation
- Phase 2 integrates via CarController.ApplySpeedModifier() and CarIdentity.Functions
