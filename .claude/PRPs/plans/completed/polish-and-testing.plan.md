# Plan: Polish & Testing (Phase 7)

## Summary
Add weather visual effects (snow particles, night skybox transition), car trail renderers, race finish detection with UI, performance optimization for 50-car WebGL, and a README with deployment guide. This is the final polish pass before classroom deployment.

## User Story
As a professor, I want the race to have immersive visual effects (snow, night, car trails) and a polished finish experience, so that the EDI demonstration feels engaging and professional in a classroom setting.

## Problem -> Solution
Current state: WeatherEffect.cs only tracks boolean state (IsSnowActive/IsNightActive) with no visual output. Cars have no trails. Race has no finish UI. No README exists.
Desired state: Snow triggers falling particles, night dims the scene and swaps the skybox, cars leave colored trails, race ends with a winner announcement, and the project has complete deployment documentation.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 7 — Polish & Testing
- **Estimated Files**: 8-12 files (5 new scripts, 3-4 modified scripts, 1 README)

---

## UX Design

### Before
```
+---------------------------------------------+
| Race runs, events trigger, console logs      |
| "[Weather] Snow started" but nothing changes |
| visually. Cars are plain with no trails.     |
| Race finishes silently via console log.      |
| No deployment docs for professors.           |
+---------------------------------------------+
```

### After
```
+---------------------------------------------+
| Snow event: white particle blizzard falls    |
|   from sky, ambient light dims slightly      |
| Night event: skybox goes dark, directional   |
|   light intensity drops, ambient darkens     |
| Cars: colored trail renderers follow each    |
|   car, matching their team color             |
| Race finish: winner banner appears on-screen |
|   with final standings                       |
| README.md: complete setup + deploy guide     |
+---------------------------------------------+
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Snow event triggered | Console log only | Particle blizzard + slight ambient dim | Visual feedback for professor + students |
| Night event triggered | Console log only | Dark skybox + dimmed lighting | Creates dramatic atmosphere |
| Cars racing | Plain prefabs moving | Colored trail renderers behind each car | Visual excitement, easy car tracking |
| Race finishes | Console log "FINISHED" | Winner UI overlay with top 3 | Clear visual conclusion |
| New user setup | No docs | README with quickstart + Docker deploy | Self-service deployment |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Events/WeatherEffect.cs` | all (40 lines) | Extend this — add visual effects to existing boolean tracking |
| P0 | `Assets/Scripts/Car/CarController.cs` | 53-102 | `Initialize()` method — where trail renderers should be added |
| P0 | `Assets/Scripts/Race/CarSpawner.cs` | 29-106 | `SpawnCars()` — trail renderers added here during spawn |
| P0 | `Assets/Scripts/Race/RaceManager.cs` | 168-197 | `OnEventTriggered` + `OnCarCompletedLap` — where finish UI triggers |
| P1 | `Assets/Scripts/UI/RuntimeSetup.cs` | all | Pattern for runtime UI creation — follow this for finish overlay |
| P1 | `Assets/Scripts/Car/CarIdentity.cs` | all (35 lines) | ColorIndex mapping for trail color |
| P1 | `Assets/Scripts/Race/RaceConfig.cs` | all | May add trail/VFX config fields here |
| P2 | `Assets/Scripts/UI/RaceUI.cs` | all | UI role system — finish panel visibility logic |
| P2 | `Deploy/Dockerfile` | all | Reference for README deployment section |
| P2 | `Deploy/docker-compose.yml` | all | Reference for README deployment section |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity ParticleSystem API | Unity 6 docs | Use `ParticleSystem.Play()/Stop()`, `emission.rateOverTime`, `shape.shapeType = ShapeType.Box` for area snow |
| Unity TrailRenderer | Unity 6 docs | Add as component, set `time`, `startWidth`, `endWidth`, `material` |
| Unity RenderSettings | Unity 6 docs | `RenderSettings.ambientLight`, `RenderSettings.skybox` for night mode |
| URP Volume overrides | Unity 6 URP docs | ColorAdjustments for global scene darkening during night |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Events/WeatherEffect.cs:1-9
// - PascalCase class names, public properties
// - No namespaces
// - XML doc comments on class
// - [Header] attributes for Inspector grouping
public class WeatherEffect : MonoBehaviour
{
    public bool IsSnowActive { get; private set; }
```

### COMPONENT_INITIALIZATION
```csharp
// SOURCE: Assets/Scripts/Race/CarSpawner.cs:61-101
// Runtime component addition pattern during spawn
car.transform.localScale *= Config.CarScale;
car.name = data.TeamName;

var identity = car.GetComponent<CarIdentity>();
if (identity == null) identity = car.AddComponent<CarIdentity>();
identity.Initialize(data);
// ...components added in sequence, then Initialize() called
```

### EVENT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Race/RaceManager.cs:37-45
// C# Action<T> delegates, not UnityEvents
public event Action<GameState> OnStateChanged;

private void SetState(GameState state)
{
    CurrentState = state;
    OnStateChanged?.Invoke(state);
}
```

### RUNTIME_UI_CREATION
```csharp
// SOURCE: Assets/Scripts/UI/RuntimeSetup.cs:357-432
// Factory methods for panels, text, buttons
private GameObject CreatePanel(Transform parent, string name,
    Vector2 anchorMin, Vector2 anchorMax,
    Vector2 offsetMin, Vector2 offsetMax)
{
    GameObject obj = new GameObject(name);
    obj.transform.SetParent(parent, false);
    Image bg = obj.AddComponent<Image>();
    bg.color = new Color(0, 0, 0, 0.6f);
    // ...
}
```

### DEBUG_LOG_FORMAT
```csharp
// SOURCE: Multiple files
// [ClassName] prefix on all Debug.Log messages
Debug.Log($"[RaceManager] Race started with {spawnedCars.Count} cars");
Debug.Log("[Weather] Snow started");
Debug.Log($"[CarLabelSpawner] Spawned {spawnedLabels.Count} car labels");
```

### SCRIPTABLEOBJECT_PATTERN
```csharp
// SOURCE: Assets/Scripts/Race/RaceConfig.cs:7-8
[CreateAssetMenu(fileName = "RaceConfig", menuName = "EDI Racing/Race Config")]
public class RaceConfig : ScriptableObject
{
    [Header("Car Settings")]
    [Tooltip("...")]
    public float DefaultSpeed = 40f;
```

### COROUTINE_TIMED_EFFECT
```csharp
// SOURCE: Assets/Scripts/Events/WeatherEffect.cs:13-23
// Activate → coroutine timer → deactivate pattern
public void ActivateSnow(float duration)
{
    IsSnowActive = true;
    Debug.Log("[Weather] Snow started");
    StartCoroutine(DeactivateAfter(duration, () =>
    {
        IsSnowActive = false;
        Debug.Log("[Weather] Snow ended");
    }));
}
```

### COLOR_INDEX_MAPPING
```csharp
// SOURCE: Assets/Scripts/Data/CarData.cs:11
// 0=green, 1=black, 2=red, 3=blue, 4=white
public int ColorIndex; // 0=green, 1=black, 2=red, 3=blue, 4=white
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Events/WeatherEffect.cs` | UPDATE | Add snow particle system and night skybox/lighting transitions |
| `Assets/Scripts/Race/CarSpawner.cs` | UPDATE | Add TrailRenderer to spawned cars (and visual-only cars) |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | Add race finish event, trigger finish UI |
| `Assets/Scripts/Race/RaceConfig.cs` | UPDATE | Add trail and VFX configuration fields |
| `Assets/Scripts/UI/RaceFinishPanel.cs` | CREATE | Overlay showing winner + top 3 standings |
| `Assets/Scripts/UI/RuntimeSetup.cs` | UPDATE | Wire up RaceFinishPanel at runtime |
| `Assets/Scripts/Events/EventMatcher.cs` | UPDATE (minor) | Ensure weather events apply globally (verify) |
| `README.md` | CREATE | Project overview, setup, build, Docker deploy guide |

## NOT Building

- Sound effects and music (Could priority in PRD — defer to a follow-up)
- Additional track layouts (Could priority — defer)
- Automated unit/integration test suite (Unity project — no CLI test pipeline per CLAUDE.md)
- Post-processing VFX beyond weather (bloom, vignette, etc.)
- Performance profiling tooling (manual via Unity Profiler in Editor)
- In-game survey UI (Phase 7 scope is polish of existing features)
- WebGL build optimization (already done in Phase 6 with Brotli compression)

---

## Step-by-Step Tasks

### Task 1: Add Trail Configuration to RaceConfig

- **ACTION**: Add trail renderer settings to the `RaceConfig` ScriptableObject
- **IMPLEMENT**: Add new `[Header("Trail Settings")]` section with fields: `TrailDuration` (float, default 0.5f), `TrailStartWidth` (float, default 0.8f), `TrailEndWidth` (float, default 0.1f). These control how car trails look.
- **MIRROR**: SCRIPTABLEOBJECT_PATTERN — `[Header]`, `[Tooltip]`, sensible defaults
- **IMPORTS**: None needed (already `using UnityEngine`)
- **GOTCHA**: The existing `RaceConfig.asset` will pick up defaults automatically from new field declarations
- **VALIDATE**: Open Unity Editor → inspect RaceConfig asset → new trail fields visible with defaults

### Task 2: Add TrailRenderer to Spawned Cars

- **ACTION**: In `CarSpawner.SpawnCars()`, add a TrailRenderer component to each car after spawning. Also add to `SpawnVisualCars()` for student-side cars.
- **IMPLEMENT**:
  ```csharp
  // After trigger collider, before CarController init:
  var trail = car.AddComponent<TrailRenderer>();
  trail.time = Config.TrailDuration;
  trail.startWidth = Config.TrailStartWidth;
  trail.endWidth = Config.TrailEndWidth;
  trail.material = new Material(Shader.Find("Sprites/Default"));
  trail.startColor = GetTrailColor(data.ColorIndex);
  trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f);
  trail.minVertexDistance = 0.5f;
  trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
  trail.receiveShadows = false;
  ```
  Add private helper `GetTrailColor(int colorIndex)` mapping: 0=green(0.2,0.8,0.2), 1=gray(0.3,0.3,0.3), 2=red(0.9,0.2,0.2), 3=blue(0.2,0.4,0.9), 4=white(0.9,0.9,0.9). For `SpawnVisualCars()`, add the same trail logic (simpler — no controller/collider needed).
- **MIRROR**: COMPONENT_INITIALIZATION pattern — add component, configure, follows spawn sequence
- **IMPORTS**: `UnityEngine.Rendering` (for ShadowCastingMode)
- **GOTCHA**: `Shader.Find("Sprites/Default")` works in editor but may need to be ensured it's included in WebGL build. Unity includes this shader by default. The material must be created per-car to allow different colors. Set `trail.emitting = true` only after car starts moving.
- **VALIDATE**: Enter Play Mode → cars should leave colored trails behind them as they drive

### Task 3: Enhance WeatherEffect with Snow Particles

- **ACTION**: Extend `WeatherEffect.cs` to create and manage a ParticleSystem for snow
- **IMPLEMENT**:
  ```csharp
  private ParticleSystem snowParticles;

  private void Awake()
  {
      CreateSnowSystem();
  }

  private void CreateSnowSystem()
  {
      GameObject snowObj = new GameObject("SnowParticles");
      snowObj.transform.SetParent(transform);
      snowObj.transform.localPosition = Vector3.up * 50f;
      snowParticles = snowObj.AddComponent<ParticleSystem>();

      var main = snowParticles.main;
      main.loop = true;
      main.startLifetime = 5f;
      main.startSpeed = 8f;
      main.startSize = 0.3f;
      main.maxParticles = 2000;
      main.startColor = new Color(0.95f, 0.95f, 1f, 0.8f);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.gravityModifier = 0.3f;

      var emission = snowParticles.emission;
      emission.rateOverTime = 500f;

      var shape = snowParticles.shape;
      shape.shapeType = ParticleSystemShapeType.Box;
      shape.scale = new Vector3(100f, 1f, 100f);

      var renderer = snowObj.GetComponent<ParticleSystemRenderer>();
      renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
      renderer.renderMode = ParticleSystemRenderMode.Billboard;

      snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
  }
  ```
  In `ActivateSnow()`, add `snowParticles.Play()`. In the deactivation callback, add `snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting)`.
  Add `private void LateUpdate()` to keep snow centered on camera: `snowObj.transform.position = Camera.main.transform.position + Vector3.up * 50f`.
- **MIRROR**: COROUTINE_TIMED_EFFECT pattern, DEBUG_LOG_FORMAT
- **IMPORTS**: Already has `using UnityEngine` and `System.Collections`
- **GOTCHA**: ParticleSystem.Play/Stop must be called, not just emission toggle. The snow area (100x100) should be large enough to cover the track from above. `Particles/Standard Unlit` shader is included in URP by default. Must stop with `StopEmitting` not `StopEmittingAndClear` so remaining particles fade naturally.
- **VALIDATE**: Enter Play Mode → press 6 (Snow Weather) → white particles fall from sky for 12 seconds

### Task 4: Enhance WeatherEffect with Night Mode

- **ACTION**: Add night mode visual transition: darken ambient light, reduce directional light intensity, optionally tint the scene
- **IMPLEMENT**:
  ```csharp
  private Light directionalLight;
  private Color originalAmbientColor;
  private float originalLightIntensity;
  private bool hasStoredOriginals;

  private void Start()
  {
      // Cache original lighting state
      directionalLight = FindDirectionalLight();
      if (directionalLight != null)
      {
          originalLightIntensity = directionalLight.intensity;
      }
      originalAmbientColor = RenderSettings.ambientLight;
      hasStoredOriginals = true;
  }

  private Light FindDirectionalLight()
  {
      foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
      {
          if (light.type == LightType.Directional) return light;
      }
      return null;
  }
  ```
  In `ActivateNight()`: Lerp directional light intensity to 0.15f, set `RenderSettings.ambientLight` to dark blue `new Color(0.05f, 0.05f, 0.15f)`.
  On deactivation: restore originals.
  Use a coroutine for smooth transition (0.5s lerp) instead of instant snap.
- **MIRROR**: COROUTINE_TIMED_EFFECT pattern
- **IMPORTS**: None new needed
- **GOTCHA**: `RenderSettings.ambientLight` is a global — must cache and restore original values. Directional light may not exist in all scenes; null-check. The transition coroutine should smoothly lerp to avoid jarring visual change.
- **VALIDATE**: Enter Play Mode → press 7 (Night Weather) → scene darkens smoothly for 15 seconds → restores

### Task 5: Add Race Finish Event and UI

- **ACTION**: Create `RaceFinishPanel.cs` that displays winner + top 3 when race ends. Modify `RaceManager` to fire a finish event.
- **IMPLEMENT**:
  Add to `RaceManager`:
  ```csharp
  public event Action<CarIdentity> OnRaceFinished;
  ```
  In `OnCarCompletedLap()`, when `raceFinished` is set true, invoke:
  ```csharp
  OnRaceFinished?.Invoke(car);
  SetState(GameState.Finished);
  ```

  Create `RaceFinishPanel.cs`:
  ```csharp
  public class RaceFinishPanel : MonoBehaviour
  {
      public RaceManager RaceManager;
      public ScoreManager ScoreManager;

      private GameObject panel;

      private void OnEnable()
      {
          if (RaceManager != null)
              RaceManager.OnRaceFinished += ShowFinish;
      }

      private void OnDisable()
      {
          if (RaceManager != null)
              RaceManager.OnRaceFinished -= ShowFinish;
      }

      private void ShowFinish(CarIdentity winner)
      {
          // Build center-screen overlay with winner name + top 3
          // Using RuntimeSetup UI factory pattern
      }
  }
  ```
- **MIRROR**: EVENT_PATTERN, RUNTIME_UI_CREATION
- **IMPORTS**: `using UnityEngine; using UnityEngine.UI; using System.Collections.Generic;`
- **GOTCHA**: `GameState.Finished` already exists in the `GameState` enum — verify. The finish panel should work with `Time.timeScale = 0f` if paused (use `unscaledDeltaTime`). Don't auto-pause on finish — let cars keep running for visual effect.
- **VALIDATE**: Start race with 1-lap config → first car finishes → winner overlay appears with name and top 3

### Task 6: Wire RaceFinishPanel in RuntimeSetup

- **ACTION**: Add finish panel creation to `RuntimeSetup.SetupUI()` so it auto-wires at runtime
- **IMPLEMENT**: Add `BuildFinishPanel(canvasObj.transform)` call in `SetupUI()`. The method creates a center-screen semi-transparent panel (hidden by default) and assigns `RaceManager`/`ScoreManager` references to the `RaceFinishPanel` component.
- **MIRROR**: RUNTIME_UI_CREATION — follow the exact `CreatePanel`/`CreateText` factory pattern from RuntimeSetup
- **IMPORTS**: None new
- **GOTCHA**: The panel must listen to `OnStateChanged` for `GameState.Finished` to show/hide. Must set `gameObject.SetActive(false)` initially.
- **VALIDATE**: Enter Play Mode without pre-built UI prefabs → RuntimeSetup creates finish panel → race completes → panel shows

### Task 7: Create README.md

- **ACTION**: Write a comprehensive README.md at project root
- **IMPLEMENT**: Include:
  - Project overview (what EDI Racing Game v2 is)
  - Prerequisites (Unity 6, Node.js 18+, Docker)
  - Quick start (Unity Editor play mode)
  - Track setup (NavMesh baking, waypoint path, checkpoints)
  - WebGL build instructions (Unity CLI command or Editor)
  - Docker deployment (`docker-compose up` from Deploy/)
  - Multi-client usage (professor hosts room, students join with code)
  - Keyboard shortcuts table
  - CSV data format
  - Architecture overview (brief)
  - Project structure
- **MIRROR**: N/A — markdown document
- **IMPORTS**: N/A
- **GOTCHA**: WebGL build command is `Unity -batchmode -executeMethod BuildScript.BuildWebGL`. Docker compose context is parent dir of Deploy/. Port 8080 maps to container's 80.
- **VALIDATE**: Read README → follow quickstart → successfully run the game

### Task 8: Verify GameState.Finished Exists

- **ACTION**: Check `Assets/Scripts/UI/GameState.cs` to confirm `Finished` is in the enum. Add it if missing.
- **IMPLEMENT**: The enum should be: `Setup, Racing, Paused, Finished`
- **MIRROR**: Existing enum pattern
- **IMPORTS**: None
- **GOTCHA**: If `Finished` doesn't exist, all UI panels subscribing to `OnStateChanged` need no changes — they just won't react to a state they don't know about, which is fine. Only the finish panel needs to handle it.
- **VALIDATE**: Confirm enum has 4 values

---

## Testing Strategy

### Manual Tests (Unity Editor)

| Test | Steps | Expected Result | Edge Case? |
|---|---|---|---|
| Snow VFX | Play → press 6 | White particles fall 12s then stop | No |
| Night VFX | Play → press 7 | Scene darkens 15s then restores | No |
| Snow + Night combo | Press 6 then 7 | Both effects active simultaneously | Yes |
| Car trails | Play → observe cars | Colored trails follow each car | No |
| Trail colors | Check each color prefab | Trail matches car color (green/black/red/blue/white) | No |
| Race finish | Set 1 lap → race → finish | Winner banner appears with top 3 | No |
| 50-car performance | Import 50-car CSV → race | >= 30 FPS in Editor (profiler) | Yes |
| Student view finish | Multi-client → race finishes | Student sees finish overlay too | Yes |
| Weather on student | Snow/night on professor → student sees it? | Student should see network event notification (no local VFX needed on student — they receive visual state via NetworkSync) | Yes |
| Trail on visual-only cars | Student-side spawned cars | Visual-only cars also have trails | No |
| Reset after finish | Finish → reset race | All VFX cleared, trails gone, lighting restored | Yes |

### Edge Cases Checklist
- [ ] Snow activated twice rapidly (AllowRepeat=true)
- [ ] Night activated while snow still active
- [ ] Race finishes while weather effect is active (lighting restores properly)
- [ ] Race reset during active weather effect
- [ ] 50 cars with trails — GPU memory impact
- [ ] Trail renderer on cars that get stuck/warped
- [ ] No directional light in scene (null safety)
- [ ] WebGL build includes required shaders (Particles/Standard Unlit, Sprites/Default)

---

## Validation Commands

### Static Analysis
```
# Open project in Unity 6 → Console window → check for compilation errors
# All scripts must compile without errors
```
EXPECT: Zero compilation errors

### Play Mode Test
```
# Unity Editor → Play Mode in complete_track_demo scene
# 1. Verify car trails appear
# 2. Press 6 → snow particles appear
# 3. Press 7 → night mode activates
# 4. Wait for race finish → finish panel appears
```
EXPECT: All visual effects work correctly

### WebGL Build
```bash
# From Unity command line:
Unity -batchmode -executeMethod BuildScript.BuildWebGL -quit
```
EXPECT: Build succeeds, WebGL output in Deploy/webgl-build/

### Docker Deployment
```bash
cd Deploy && docker-compose up --build
# Open browser to http://localhost:8080
```
EXPECT: Game loads, all visual effects work in WebGL

### Performance Check
```
# Unity Profiler → Play Mode with 50-car CSV
# Monitor: FPS, GPU time, Particle count, Trail vertex count
```
EXPECT: >= 30 FPS with 50 cars + active weather effects

---

## Acceptance Criteria
- [ ] Snow weather event triggers visible particle blizzard that follows camera
- [ ] Night weather event darkens scene lighting with smooth transition
- [ ] All cars have colored trail renderers matching their team color
- [ ] Race finish displays winner overlay with top 3 standings
- [ ] Weather effects restore to original state after duration expires
- [ ] Effects work correctly when stacked (snow + night simultaneously)
- [ ] Visual effects cleaned up on race reset
- [ ] README.md provides complete setup and deployment instructions
- [ ] No compilation errors or warnings in Unity Console
- [ ] 50-car race maintains >= 30 FPS in Editor

## Completion Checklist
- [ ] Code follows discovered patterns (MonoBehaviour, Action events, Debug.Log format)
- [ ] Error handling matches codebase style (null checks, Debug.LogWarning)
- [ ] No hardcoded values (trail/VFX params in RaceConfig)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `Particles/Standard Unlit` shader stripped from WebGL build | LOW | HIGH — snow invisible | Add shader to Always Included Shaders in Graphics Settings, or use a shader variant reference |
| 50 cars with trails causes GPU performance drop | MEDIUM | MEDIUM — FPS drops below 30 | Limit trail `time` to 0.5s, reduce `minVertexDistance`, disable trails if FPS drops |
| Night mode RenderSettings change not synced to students | LOW | LOW — student view unaffected | Students receive event notification via NetworkSync; full VFX sync is out of scope |
| Trail renderers on warped/teleported cars leave visual artifacts | LOW | LOW — brief visual glitch | Call `trail.Clear()` after NavMesh warp in CarController.WarpToWaypoint() |

## Notes
- Sound effects are **out of scope** for this plan. The PRD lists them as "Could" priority. They can be added in a follow-up if time permits.
- Browser compatibility testing (Chrome/Firefox/Safari/Edge) is a manual QA task to be done after WebGL build, not scriptable.
- The `WeatherEffect` component is already referenced by `RaceManager.WeatherEffect` — no new wiring needed for weather effects.
- Performance testing at 50 cars should be done in the Unity Editor Profiler. If bottlenecks are found, specific optimizations (LOD, trail reduction, particle budget cuts) can be applied based on profiler data.
- Student-side clients run visual-only cars — they receive weather event notifications via `NetworkSync.HandleEventTriggered()` but don't run local particle systems. This is acceptable since the professor's projection shows the full VFX.
