# Plan: Phase 7 — 50-Car Performance Optimization & Cross-Browser Testing

## Summary

Optimize the EDI Racing Game to sustain 60 FPS with 50+ simultaneous cars in WebGL. The three bottleneck domains are: (1) per-car physics — every car runs a NavMeshAgent + kinematic Rigidbody + SphereCast each frame; (2) per-car UI — each car spawns its own world-space Canvas with LateUpdate billboard; (3) unconstrained physics collision matrix. A secondary goal is cross-browser validation (Chrome, Firefox, Safari, Edge).

## User Story

As a professor running an EDI racing demonstration,
I want the game to run smoothly with 50+ student-generated cars,
So that large classes can participate without frame drops or browser freezes.

## Problem -> Solution

**Current**: With 50 cars, each car runs Update (6 operations: waypoint check, curvature calc, SphereCast, collision speed, composite speed, stuck check) + LateUpdate (CarLabel billboard) + individual world-space Canvas + full physics collision matrix. Physics solver at 6 iterations, all 32 layers collide with all 32 layers.

**Desired**: Targeted optimizations to reduce per-frame cost by ~40-60%, enabling smooth 50-car races in WebGL across major browsers.

## Metadata

- **Complexity**: Large
- **Source PRD**: N/A
- **PRD Phase**: Phase 7 (remaining work from DESIGN.md)
- **Estimated Files**: 8-12

---

## UX Design

### Before
N/A — internal performance change. No visible UI changes.

### After
N/A — cars look and behave identically, just faster.

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Race with 50 cars | Frame drops in WebGL | Smooth 60 FPS | Invisible to user |
| Car labels | Always visible, always billboard | Distance-culled, throttled updates | Labels beyond camera range hidden |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Car/CarController.cs` | all (445 lines) | Main per-frame cost: Update with SphereCast, curvature, stuck detection |
| P0 | `Assets/Scripts/UI/CarLabel.cs` | all (37 lines) | LateUpdate billboard per car — distance culling target |
| P0 | `Assets/Scripts/UI/CarLabelSpawner.cs` | all (101 lines) | Creates individual world-space Canvas per car — batching target |
| P0 | `Assets/Scripts/Race/CarSpawner.cs` | all (232 lines) | Runtime component injection, trail renderer per car |
| P1 | `Assets/Scripts/Race/RaceConfig.cs` | all (72 lines) | Configurable parameters — add perf settings here |
| P1 | `Assets/Scripts/Race/RaceManager.cs` | 318-354 | Update loop — keyboard polling |
| P1 | `ProjectSettings/DynamicsManager.asset` | all | Physics settings: solver iterations, collision matrix |
| P2 | `Assets/Scripts/Race/WaypointPath.cs` | all | GetWaypoint called 6+ times per car per frame |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: CarController.cs:1-12
// PascalCase for public methods/properties, camelCase for private fields
// No namespaces — all classes in global namespace
public class CarController : MonoBehaviour
{
    private NavMeshAgent agent;
    private float baseSpeed;
    public float BaseSpeed => baseSpeed;
}
```

### ERROR_HANDLING
```csharp
// SOURCE: CarController.cs:117-118
// Null-guard early return pattern
if (agent == null || waypointPath == null) return;
```

### CONFIG_PATTERN
```csharp
// SOURCE: RaceConfig.cs:7-8
// ScriptableObject with [Header] groups and [Tooltip] on every field
[CreateAssetMenu(fileName = "RaceConfig", menuName = "EDI Racing/Race Config")]
public class RaceConfig : ScriptableObject
{
    [Header("Car Settings")]
    [Tooltip("description")]
    public float DefaultSpeed = 40f;
}
```

### COMPONENT_INJECTION
```csharp
// SOURCE: CarSpawner.cs:76-80
// Runtime AddComponent with null-check-then-add pattern
var rb = car.GetComponent<Rigidbody>();
if (rb == null) rb = car.AddComponent<Rigidbody>();
rb.isKinematic = true;
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Car/CarController.cs` | UPDATE | Throttle SphereCast, reduce per-frame waypoint lookups |
| `Assets/Scripts/UI/CarLabel.cs` | UPDATE | Add distance-based culling and throttled billboarding |
| `Assets/Scripts/UI/CarLabelSpawner.cs` | UPDATE | Use single shared Canvas instead of one per car |
| `Assets/Scripts/Race/CarSpawner.cs` | UPDATE | Reduce obstacle avoidance quality, optimize trail settings |
| `Assets/Scripts/Race/RaceConfig.cs` | UPDATE | Add performance tuning fields |
| `ProjectSettings/DynamicsManager.asset` | UPDATE | Reduce solver iterations, configure collision matrix |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Minor: cache keyboard reference |
| `Assets/Scripts/Editor/PerformanceValidator.cs` | CREATE | Editor tool to verify FPS with N cars |

## NOT Building

- LOD mesh swapping (cars use simple prefabs already, no high-poly models)
- ECS/DOTS migration (overkill for 50 cars)
- Object pooling for cars (cars spawn once, not recycled)
- GPU instancing changes (URP handles this)
- Custom NavMesh solution (NavMeshAgent is fine at 50)
- Automated cross-browser CI pipeline (manual testing sufficient)

---

## Step-by-Step Tasks

### Task 1: Physics Settings Optimization

- **ACTION**: Reduce physics solver iterations and configure collision matrix
- **IMPLEMENT**:
  - `DynamicsManager.asset`: Reduce `m_DefaultSolverIterations` from 6 to 2 (cars are kinematic — solver iterations are wasted)
  - `DynamicsManager.asset`: Reduce `m_DefaultSolverVelocityIterations` from 1 to 1 (keep)
  - `DynamicsManager.asset`: Increase `m_SleepThreshold` from 0.005 to 0.02 (sleep idle rigidbodies faster)
  - `ProjectSettings/TimeManager.asset`: Check fixedDeltaTime — if 0.02 (50Hz), change to 0.04 (25Hz) since cars are kinematic and don't need high-freq physics
- **MIRROR**: Direct YAML edit of Unity serialized assets
- **GOTCHA**: Cars use `isKinematic = true` (CarSpawner.cs:80) — they don't use the physics solver at all. Solver iterations only matter for non-kinematic bodies. The real win is reducing FixedUpdate frequency.
- **VALIDATE**: Open Unity, verify cars still move correctly. Check Physics stats in Profiler.

### Task 2: Reduce NavMeshAgent Obstacle Avoidance Quality

- **ACTION**: Lower obstacle avoidance from HighQuality to MediumQuality for cars
- **IMPLEMENT**: In `CarSpawner.cs:91`, change:
  ```csharp
  agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
  ```
  to:
  ```csharp
  agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
  ```
- **MIRROR**: COMPONENT_INJECTION pattern
- **GOTCHA**: With 50 cars, HighQuality avoidance is O(n^2) in worst case. MedQuality provides 90% of the behavior at lower cost. Test that cars don't clip through each other.
- **VALIDATE**: Run race with 50 cars, verify no stuck/clipping regressions.

### Task 3: Throttle CarController SphereCast

- **ACTION**: Run `DetectCarsAhead()` SphereCast every 3rd frame instead of every frame, staggered across cars
- **IMPLEMENT**: In `CarController.cs`, add a frame counter and stagger offset:
  ```csharp
  private int frameCounter;
  private int staggerOffset; // set in Initialize() from car index

  // In Initialize():
  staggerOffset = GetInstanceID() % 3;

  // In Update(), replace direct DetectCarsAhead() call:
  frameCounter++;
  if ((frameCounter + staggerOffset) % 3 == 0)
      DetectCarsAhead();
  ```
- **MIRROR**: Existing Update pattern in CarController.cs:116-140
- **GOTCHA**: `forwardSpeedMultiplier` is already smoothed via `MoveTowards` (line 301-302), so skipping frames won't cause jitter. The stagger offset ensures not all 50 cars cast on the same frame.
- **VALIDATE**: Cars should still brake behind slower cars. Verify no pile-ups at 50 cars.

### Task 4: Throttle Curvature Calculation

- **ACTION**: Run `UpdateCurvatureSpeed()` every 5th frame (curvature changes slowly)
- **IMPLEMENT**: Same stagger pattern:
  ```csharp
  if ((frameCounter + staggerOffset) % 5 == 0)
      UpdateCurvatureSpeed();
  ```
- **MIRROR**: Same as Task 3
- **GOTCHA**: `curvatureSpeedMultiplier` already uses `MoveTowards` smoothing (line 265-266). Safe to throttle.
- **VALIDATE**: Cars should still slow in curves. Verify no overshooting turns.

### Task 5: CarLabel Distance Culling

- **ACTION**: Hide labels beyond a configurable distance from camera; throttle billboard rotation
- **IMPLEMENT**: In `CarLabel.cs`:
  ```csharp
  [Tooltip("Labels beyond this distance from camera are hidden")]
  public float MaxVisibleDistance = 80f;

  private Canvas canvas;
  private int frameCounter;
  private int staggerOffset;

  public void Initialize(Transform carTransform, Canvas sharedCanvas = null)
  {
      target = carTransform;
      cam = Camera.main != null ? Camera.main.transform : null;
      canvas = GetComponent<Canvas>();
      staggerOffset = GetInstanceID() % 4;
  }

  private void LateUpdate()
  {
      if (target == null) return;
      transform.position = target.position + Vector3.up * HeightOffset;

      // Distance culling
      if (cam == null) cam = Camera.main != null ? Camera.main.transform : null;
      if (cam != null)
      {
          float sqrDist = (cam.position - transform.position).sqrMagnitude;
          bool visible = sqrDist < MaxVisibleDistance * MaxVisibleDistance;
          if (canvas != null && canvas.enabled != visible)
              canvas.enabled = visible;

          // Billboard only every 4th frame (rotation change is subtle)
          frameCounter++;
          if (visible && (frameCounter + staggerOffset) % 4 == 0)
          {
              Vector3 lookDir = cam.position - transform.position;
              lookDir.y = 0f;
              if (lookDir.sqrMagnitude > 0.001f)
                  transform.rotation = Quaternion.LookRotation(lookDir);
          }
      }
  }
  ```
- **MIRROR**: Existing LateUpdate null-guard pattern
- **GOTCHA**: Use sqrMagnitude to avoid sqrt. Canvas.enabled toggle is cheap and prevents draw calls entirely.
- **VALIDATE**: Zoom camera away — labels should disappear. Zoom in — labels reappear. No visual popping at threshold.

### Task 6: Reduce Trail Renderer Overhead

- **ACTION**: Reduce trail vertex density and disable shadows (already done for shadows)
- **IMPLEMENT**: In `CarSpawner.cs:197`, increase `minVertexDistance`:
  ```csharp
  trail.minVertexDistance = 1.5f; // was 0.5f — 3x fewer vertices
  ```
  Also add in RaceConfig.cs:
  ```csharp
  [Header("Performance")]
  [Tooltip("Minimum distance between trail vertices. Higher = fewer vertices, less GPU cost.")]
  public float TrailMinVertexDistance = 1.5f;
  ```
  And use `Config.TrailMinVertexDistance` in CarSpawner.
- **MIRROR**: CONFIG_PATTERN — all tunable values go through RaceConfig
- **GOTCHA**: `shadowCastingMode = Off` and `receiveShadows = false` already set (line 198-199). Trail material uses `new Material(shader)` per car — that's 50 unique materials. Consider sharing one.
- **VALIDATE**: Trails should look slightly less smooth but still visible. Verify no visual artifacts.

### Task 7: Share Trail Material

- **ACTION**: Create one shared trail material instead of `new Material()` per car
- **IMPLEMENT**: In `CarSpawner.cs`, cache the material:
  ```csharp
  private Material sharedTrailMaterial;

  private Material GetSharedTrailMaterial()
  {
      if (sharedTrailMaterial != null) return sharedTrailMaterial;
      var shader = Shader.Find("Universal Render Pipeline/Unlit")
                   ?? Shader.Find("Sprites/Default")
                   ?? Shader.Find("UI/Default");
      if (shader != null)
          sharedTrailMaterial = new Material(shader);
      return sharedTrailMaterial;
  }
  ```
  In `AddTrailRenderer`, replace `trail.material = new Material(trailShader)` with:
  ```csharp
  trail.sharedMaterial = GetSharedTrailMaterial();
  ```
  Colors are set per-TrailRenderer via `startColor`/`endColor` which are per-instance, not per-material.
- **MIRROR**: Cached field pattern (see `cachedAgentTypeID` in CarSpawner.cs:28)
- **GOTCHA**: Trail colors are vertex colors, not material properties — sharing material is safe. Use `sharedMaterial` not `material` to avoid creating copies.
- **VALIDATE**: Verify all 5 car colors still show correct trail colors.

### Task 8: Performance Config Fields

- **ACTION**: Add performance-related fields to RaceConfig for runtime tuning
- **IMPLEMENT**: In `RaceConfig.cs`, add:
  ```csharp
  [Header("Performance")]
  [Tooltip("Minimum distance between trail vertices. Higher = fewer vertices.")]
  public float TrailMinVertexDistance = 1.5f;

  [Tooltip("Max distance from camera before car labels are hidden.")]
  public float LabelVisibleDistance = 80f;

  [Tooltip("NavMeshAgent obstacle avoidance quality. Lower = faster with more cars.")]
  public ObstacleAvoidanceType AvoidanceQuality = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
  ```
- **MIRROR**: CONFIG_PATTERN
- **IMPORTS**: `using UnityEngine.AI;`
- **GOTCHA**: Keep defaults that work well for 50 cars. Professor can tune via Inspector if needed.
- **VALIDATE**: Verify ScriptableObject Inspector shows new fields with tooltips.

### Task 9: Cache Keyboard Reference in RaceManager

- **ACTION**: Cache `Keyboard.current` instead of accessing it every frame
- **IMPLEMENT**: In `RaceManager.cs` Update():
  ```csharp
  private Keyboard cachedKeyboard;

  private void Update()
  {
      if (cachedKeyboard == null) cachedKeyboard = Keyboard.current;
      if (cachedKeyboard == null) return;
      // Use cachedKeyboard instead of Keyboard.current throughout
  }
  ```
- **MIRROR**: Null-guard early return
- **GOTCHA**: `Keyboard.current` can change if devices are hot-plugged. Re-check periodically or on null. Low priority optimization.
- **VALIDATE**: Keyboard shortcuts still work.

### Task 10: Collision Matrix Optimization

- **ACTION**: Configure layer collision matrix so cars only collide with relevant layers
- **IMPLEMENT**:
  - Create a "Car" layer (e.g., layer 8) in Unity Tags & Layers
  - In `CarSpawner.cs`, set spawned car's layer:
    ```csharp
    car.layer = LayerMask.NameToLayer("Car");
    ```
  - In `DynamicsManager.asset`, update `m_LayerCollisionMatrix` so Car layer only collides with Car layer (for trigger detection) and Default (for ground). Disable Car-vs-irrelevant-layer pairs.
- **MIRROR**: Layer assignment in CarSpawner
- **GOTCHA**: `CheckpointTrigger` uses OnTriggerEnter — checkpoints must be on a layer that collides with Car. Verify checkpoint layer setup.
- **VALIDATE**: Cars still detect collisions with each other and pass through checkpoints.

### Task 11: Cross-Browser Testing Checklist

- **ACTION**: Create a manual testing checklist document for WebGL across browsers
- **IMPLEMENT**: Create `production/qa/browser-compatibility-checklist.md` with:
  - Test matrix: Chrome (latest), Firefox (latest), Safari (latest), Edge (latest)
  - Test scenarios: 5 cars, 20 cars, 50 cars
  - Metrics to record: FPS (via `1/Time.deltaTime`), memory (browser DevTools), load time
  - Feature tests: WebSocket connection, keyboard shortcuts, camera modes, events, weather effects
  - Known issues: Safari WebGL2 limitations, Firefox AudioContext autoplay policy
- **MIRROR**: Markdown docs in `production/qa/`
- **GOTCHA**: Safari has WebGL2 limitations and stricter memory limits. Firefox may have different AudioContext behavior.
- **VALIDATE**: Document is complete and actionable.

### Task 12: FPS Counter (Debug)

- **ACTION**: Add a simple runtime FPS counter for performance validation
- **IMPLEMENT**: Create `Assets/Scripts/UI/FpsCounter.cs`:
  ```csharp
  public class FpsCounter : MonoBehaviour
  {
      private float deltaTime;
      private void Update()
      {
          deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
      }
      private void OnGUI()
      {
          #if DEVELOPMENT_BUILD || UNITY_EDITOR
          float fps = 1.0f / deltaTime;
          GUI.Label(new Rect(10, 10, 200, 30), $"FPS: {fps:0.0}");
          #endif
      }
  }
  ```
- **MIRROR**: Simple MonoBehaviour, no dependencies
- **GOTCHA**: Use `#if` to strip from release builds. `OnGUI` is intentional for debug — no Canvas overhead.
- **VALIDATE**: FPS counter visible in Editor and Development builds, hidden in Release.

---

## Testing Strategy

### Manual Testing

| Test | Setup | Expected | Edge Case? |
|---|---|---|---|
| 50-car race (Editor) | Load CSV with 50 entries | Sustain 60 FPS in Editor | No |
| 50-car race (WebGL Chrome) | Build WebGL, test in Chrome | Sustain 30+ FPS | Yes — WebGL overhead |
| Label culling | Zoom camera far from cars | Labels disappear | No |
| Label restore | Zoom camera back in | Labels reappear smoothly | No |
| Trail colors | Start race with all 5 colors | Correct trail colors | No |
| SphereCast throttle | 50 cars in tight pack | No pile-ups or stuck cars | Yes |
| Checkpoint detection | Car crosses finish line | Lap counted correctly | Yes — after layer change |
| Weather + 50 cars | Trigger snow event | Snow + 50 cars, no freeze | Yes |

### Edge Cases Checklist
- [ ] 50 cars spawned at once (spawn point congestion)
- [ ] All 50 cars on same curve segment (curvature calc flood)
- [ ] Camera very far from all cars (all labels culled)
- [ ] Camera very close (all labels visible)
- [ ] Safari WebGL2 (stricter GPU memory)
- [ ] Firefox private browsing (different storage behavior)

---

## Validation Commands

### Unity Editor
```bash
# Open Unity and enter Play Mode with 50-car CSV
# Check Profiler: Window > Analysis > Profiler
# Verify: CPU < 16ms/frame, no GC spikes > 1ms
```
EXPECT: Smooth 60 FPS with 50 cars

### WebGL Build
```bash
./scripts/build-webgl.sh
./Deploy/fix-webgl.sh --serve
# Open http://localhost:8080 in Chrome, Firefox, Safari, Edge
```
EXPECT: 30+ FPS in all browsers with 50 cars

### Manual Validation
- [ ] 50-car race runs at 60 FPS in Editor
- [ ] 50-car race runs at 30+ FPS in Chrome WebGL
- [ ] Car labels hide at distance, show when close
- [ ] Trail colors are correct for all 5 car colors
- [ ] Checkpoints still count laps
- [ ] Collision triggers still work (speed reduction)
- [ ] SphereCast braking still works (cars slow behind others)
- [ ] Weather effects work with 50 cars
- [ ] Keyboard shortcuts all functional
- [ ] Save/Load session works

---

## Acceptance Criteria
- [ ] 50-car race sustains 60 FPS in Unity Editor
- [ ] 50-car race sustains 30+ FPS in Chrome/Firefox WebGL
- [ ] No visual regressions (labels, trails, weather)
- [ ] No gameplay regressions (laps, events, collisions)
- [ ] FPS counter available in development builds
- [ ] Cross-browser test checklist documented and executed
- [ ] Performance config fields exposed in RaceConfig

## Completion Checklist
- [ ] Code follows discovered patterns (no namespaces, PascalCase/camelCase, ScriptableObject config)
- [ ] Null-guard early returns on all new code paths
- [ ] All new fields have [Tooltip] attributes
- [ ] No allocations in Update/LateUpdate (sqrMagnitude, cached refs)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| SphereCast throttling causes pile-ups | Low | Medium | `forwardSpeedMultiplier` smoothing absorbs gaps; test with 50 cars in tight pack |
| Shared trail material breaks colors | Very Low | Low | Trail colors use vertex colors, not material; verified in code |
| Layer collision matrix breaks checkpoints | Medium | High | Verify checkpoint layer collides with Car layer before shipping |
| Safari WebGL performance still poor | Medium | Low | Safari has known WebGL limits; document as known limitation |
| MedQuality avoidance causes car overlap | Low | Medium | Test extensively; can revert to HighQuality per-car if needed |

## Notes
- Cars use `isKinematic = true` (CarSpawner.cs:80), meaning the physics solver iterations are largely wasted on them. Reducing solver iterations from 6->2 is nearly free.
- The biggest win is likely SphereCast throttling — 50 SphereCasts per frame is expensive in WebGL. Staggering to ~17/frame is a 3x reduction.
- World-space Canvas per car is the second biggest cost — each Canvas generates its own draw call batch. Distance culling eliminates most of these.
- Trail `new Material()` per car creates 50 unique materials preventing dynamic batching. Sharing one material is a quick win.
