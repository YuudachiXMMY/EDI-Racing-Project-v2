# Plan: Improve Track Setup — Auto-detect NavMesh Geometry

## Summary
Rewrite `TrackSetupEditor.cs` to auto-detect the actual track mesh bounds and generate waypoints/checkpoints that align with the baked NavMesh, replacing the hardcoded `radiusX=120, radiusZ=60` ellipse centered at origin that doesn't match the real track geometry.

## User Story
As a professor setting up the race, I want `EDI Racing > Setup Track` to automatically place waypoints and checkpoints on the actual track surface, so that cars follow the NavMesh correctly without manual adjustment.

## Problem → Solution
Waypoints generated at hardcoded ellipse `(0,0,0)` center with `radiusX=120, radiusZ=60` → waypoints don't land on the NavMesh baked on `1TARMAC_oval` (which sits at `(~0, 0, -160.5)` with `-90° X rotation`). **→** Auto-detect track mesh bounds, compute center and radii dynamically, validate every waypoint against NavMesh.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 1 — Core Racing Loop (improvement)
- **Estimated Files**: 2 modified

---

## UX Design

### Before
```
Menu: EDI Racing > Setup Track
  → Generates 14 WP + 14 CP at hardcoded ellipse
  → Many waypoints miss NavMesh (console warning: "Few waypoints on NavMesh")
  → Cars get stuck or take bizarre paths
```

### After
```
Menu: EDI Racing > Setup Track
  → Window opens with:
    [Track Mesh]     ← auto-detected (1TARMAC_oval) or drag-assign
    [Waypoint Count] ← slider 8-24, default 16
    [Track Y Offset] ← default 0.5 (above ground)
    [Inset Factor]   ← slider 0.5-1.0, default 0.85
    [Info Box]       ← shows detected bounds center + extents
    [Generate]       ← creates waypoints + checkpoints + RaceManager
  → All waypoints land on NavMesh (validated)
  → Console: "[TrackSetup] 16/16 waypoints on NavMesh"
  → SpawnPoint placed at first waypoint area
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Menu item | Instant execute, no UI | Opens EditorWindow with controls | User can adjust before generating |
| Track detection | Hardcoded radii | Auto-detect from mesh bounds | Falls back to manual input |
| Waypoint count | Fixed 14 | Configurable 8-24 | Default 16 for smoother turns |
| Validation | Weak warning | Per-waypoint pass/fail log | Clear feedback |
| SpawnPoint | Fixed at (0, 0.5, -60) | Placed near first waypoint on track | Matches actual track |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/Editor/TrackSetupEditor.cs` | all | File being rewritten |
| P0 (critical) | `Assets/Scenes/complete_track_demo.unity` | 307-440 | 1TARMAC_oval transform: position `(~0, 0, -160.5)`, rotation `-90° X`, has NavMeshModifier |
| P1 (important) | `Assets/Scripts/Race/WaypointPath.cs` | all | WaypointPath component created by setup |
| P1 (important) | `Assets/Scripts/Race/CheckpointTrigger.cs` | all | CheckpointTrigger component created by setup |
| P1 (important) | `Assets/Scripts/Race/LapTracker.cs` | 12 | `totalCheckpoints = 14` hardcoded — must sync with new count |
| P2 (reference) | `Assets/Scripts/Race/CarSpawner.cs` | 13-14 | SpawnPoint usage |
| P2 (reference) | `Assets/Scripts/Race/RaceConfig.cs` | all | Config ScriptableObject path |

## External Documentation
| Topic | Source | Key Takeaway |
|---|---|---|
| NavMesh.SamplePosition | Unity AI Navigation docs | Snaps a point to nearest NavMesh within maxDistance; returns false if no NavMesh nearby |
| Renderer.bounds | Unity docs | World-space AABB of the mesh — includes rotation and position transforms |
| EditorWindow | Unity Editor scripting docs | Persistent window with OnGUI for inspector-like controls |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Editor/TrackSetupEditor.cs:1-3
// Editor scripts use PascalCase class names, placed in Assets/Scripts/Editor/
// Menu items under "EDI Racing/" prefix
[MenuItem("EDI Racing/Setup Track")]
```

### COMPONENT_CREATION
```csharp
// SOURCE: Assets/Scripts/Editor/TrackSetupEditor.cs:78-96
// Pattern: create GameObject, parent it, add components, set fields
var wp = new GameObject($"WP_{i:D2}");
wp.transform.parent = waypointsParent.transform;
wp.transform.position = positions[i];
```

### NAVMESH_SAMPLING
```csharp
// SOURCE: Assets/Scripts/Editor/TrackSetupEditor.cs:56-65
// Pattern: attempt NavMesh snap, fall back to raw position
NavMeshHit hit;
if (NavMesh.SamplePosition(new Vector3(x, 0, z), out hit, 50f, NavMesh.AllAreas))
{
    positions[i] = hit.position + Vector3.up * 0.5f;
    validCount++;
}
```

### RACE_MANAGER_WIRING
```csharp
// SOURCE: Assets/Scripts/Editor/TrackSetupEditor.cs:106-148
// Pattern: find/create RaceManager, load config asset, wire references
var config = AssetDatabase.LoadAssetAtPath<RaceConfig>("Assets/Settings/RaceConfig.asset");
raceManager.Config = config;
carSpawner.WaypointPath = waypointPath;
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Editor/TrackSetupEditor.cs` | UPDATE | Rewrite waypoint generation to auto-detect track mesh bounds |
| `Assets/Scripts/Race/LapTracker.cs` | UPDATE | Make `totalCheckpoints` auto-set from actual checkpoint count instead of hardcoded 14 |

## NOT Building
- Runtime waypoint adjustment (editor-only tool)
- Visual waypoint editor with drag handles
- Multiple track support (single oval track for now)
- Custom track shapes beyond ellipse approximation
- NavMesh re-baking (user bakes manually; this tool only places waypoints on existing NavMesh)

---

## Step-by-Step Tasks

### Task 1: Update LapTracker to auto-detect checkpoint count
- **ACTION**: Remove hardcoded `totalCheckpoints = 14`; auto-detect from scene at Start()
- **IMPLEMENT**:
  ```csharp
  // Assets/Scripts/Race/LapTracker.cs
  // Change: make totalCheckpoints auto-detected
  private int totalCheckpoints;

  private void Start()
  {
      var checkpoints = FindObjectsByType<CheckpointTrigger>(FindObjectsSortMode.None);
      totalCheckpoints = checkpoints.Length;
      if (totalCheckpoints == 0)
          Debug.LogError("[LapTracker] No CheckpointTriggers found in scene!");
  }
  ```
- **MIRROR**: Existing FindFirstObjectByType usage in CheckpointTrigger.cs
- **GOTCHA**: Must run after CheckpointTrigger objects exist in scene. `FindObjectsByType` is Unity 6 preferred over deprecated `FindObjectsOfType`.
- **VALIDATE**: LapTracker.totalCheckpoints matches actual checkpoint count regardless of how many are created

### Task 2: Rewrite TrackSetupEditor as EditorWindow with auto-detection
- **ACTION**: Convert from instant menu action to EditorWindow; add track mesh auto-detection and configurable waypoint count
- **IMPLEMENT**: Full rewrite of TrackSetupEditor.cs:
  ```csharp
  using UnityEngine;
  using UnityEditor;
  using UnityEngine.AI;

  public class TrackSetupEditor : EditorWindow
  {
      private GameObject trackMeshObject;
      private int waypointCount = 16;
      private float trackYOffset = 0.5f;
      private float insetFactor = 0.85f;
      private string boundsInfo = "";

      [MenuItem("EDI Racing/Setup Track")]
      public static void ShowWindow()
      {
          var window = GetWindow<TrackSetupEditor>("Track Setup");
          window.minSize = new Vector2(350, 300);
          window.AutoDetectTrackMesh();
      }

      private void OnEnable()
      {
          AutoDetectTrackMesh();
      }

      private void OnGUI()
      {
          EditorGUILayout.LabelField("Track Setup", EditorStyles.boldLabel);
          EditorGUILayout.Space();

          trackMeshObject = (GameObject)EditorGUILayout.ObjectField(
              "Track Mesh", trackMeshObject, typeof(GameObject), true);

          waypointCount = EditorGUILayout.IntSlider("Waypoint Count", waypointCount, 8, 24);
          trackYOffset = EditorGUILayout.FloatField("Track Y Offset", trackYOffset);
          insetFactor = EditorGUILayout.Slider("Inset Factor", insetFactor, 0.5f, 1.0f);

          EditorGUILayout.Space();
          if (!string.IsNullOrEmpty(boundsInfo))
              EditorGUILayout.HelpBox(boundsInfo, MessageType.Info);

          EditorGUILayout.Space();
          if (GUILayout.Button("Detect Track"))
              AutoDetectTrackMesh();

          EditorGUI.BeginDisabledGroup(trackMeshObject == null);
          if (GUILayout.Button("Generate Waypoints & Checkpoints"))
          {
              CreateWaypointsAndCheckpoints();
              CreateRaceManager();
          }
          EditorGUI.EndDisabledGroup();
      }

      private void AutoDetectTrackMesh()
      {
          // Strategy 1: Find by name containing "TARMAC"
          var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
          foreach (var r in renderers)
          {
              if (r.gameObject.name.IndexOf("TARMAC",
                  System.StringComparison.OrdinalIgnoreCase) >= 0)
              {
                  trackMeshObject = r.gameObject;
                  UpdateBoundsInfo();
                  return;
              }
          }
          boundsInfo = "No track mesh found. Drag one into the Track Mesh field.";
      }

      private void UpdateBoundsInfo()
      {
          if (trackMeshObject == null) { boundsInfo = ""; return; }
          var renderer = trackMeshObject.GetComponent<Renderer>();
          if (renderer == null) { boundsInfo = "No Renderer on track mesh."; return; }
          var b = renderer.bounds;
          boundsInfo = $"Detected: {trackMeshObject.name}\n" +
              $"Center: ({b.center.x:F1}, {b.center.y:F1}, {b.center.z:F1})\n" +
              $"Extents: ({b.extents.x:F1}, {b.extents.y:F1}, {b.extents.z:F1})\n" +
              $"Ellipse radii: X={b.extents.x * insetFactor:F1}, " +
              $"Z={b.extents.z * insetFactor:F1}";
      }

      private void CreateWaypointsAndCheckpoints()
      {
          var renderer = trackMeshObject.GetComponent<Renderer>();
          if (renderer == null)
          {
              Debug.LogError("[TrackSetup] Track mesh has no Renderer.");
              return;
          }

          Bounds bounds = renderer.bounds;
          Vector3 center = bounds.center;
          float radiusX = bounds.extents.x * insetFactor;
          float radiusZ = bounds.extents.z * insetFactor;

          Debug.Log($"[TrackSetup] Bounds center: {center}, " +
              $"radii: X={radiusX:F1} Z={radiusZ:F1}");

          // Clean up existing
          var existingWP = GameObject.Find("Waypoints");
          if (existingWP != null) DestroyImmediate(existingWP);
          var existingCP = GameObject.Find("Checkpoints");
          if (existingCP != null) DestroyImmediate(existingCP);

          var waypointsParent = new GameObject("Waypoints");
          var checkpointsParent = new GameObject("Checkpoints");
          var positions = new Vector3[waypointCount];
          int validCount = 0;

          for (int i = 0; i < waypointCount; i++)
          {
              float angle = (float)i / waypointCount * Mathf.PI * 2f;
              float x = center.x + Mathf.Sin(angle) * radiusX;
              float z = center.z - Mathf.Cos(angle) * radiusZ;

              NavMeshHit hit;
              if (NavMesh.SamplePosition(
                  new Vector3(x, center.y, z), out hit, 30f, NavMesh.AllAreas))
              {
                  positions[i] = hit.position + Vector3.up * trackYOffset;
                  validCount++;
              }
              else
              {
                  positions[i] = new Vector3(x, center.y + trackYOffset, z);
                  Debug.LogWarning(
                      $"[TrackSetup] WP_{i:D2} NOT on NavMesh " +
                      $"at ({x:F1}, {z:F1})");
              }
          }

          Debug.Log($"[TrackSetup] {validCount}/{waypointCount} " +
              "waypoints on NavMesh");

          for (int i = 0; i < waypointCount; i++)
          {
              var wp = new GameObject($"WP_{i:D2}");
              wp.transform.parent = waypointsParent.transform;
              wp.transform.position = positions[i];

              var cp = new GameObject($"CP_{i:D2}");
              cp.transform.parent = checkpointsParent.transform;
              cp.transform.position = positions[i];

              int nextIdx = (i + 1) % waypointCount;
              Vector3 dir = (positions[nextIdx] - positions[i]).normalized;
              if (dir != Vector3.zero)
                  cp.transform.rotation = Quaternion.LookRotation(dir);

              var box = cp.AddComponent<BoxCollider>();
              box.isTrigger = true;
              box.size = new Vector3(30f, 10f, 3f);

              var trigger = cp.AddComponent<CheckpointTrigger>();
              trigger.CheckpointIndex = i;
          }

          var wpPath = waypointsParent.AddComponent<WaypointPath>();
          var waypoints = new Transform[waypointCount];
          for (int i = 0; i < waypointCount; i++)
              waypoints[i] = waypointsParent.transform.Find($"WP_{i:D2}");
          wpPath.Waypoints = waypoints;

          EditorUtility.SetDirty(waypointsParent);
          EditorUtility.SetDirty(checkpointsParent);
      }

      // CreateRaceManager() — same as current but with
      // dynamic SpawnPoint placement (see Task 3)
  }
  ```
- **MIRROR**: COMPONENT_CREATION, NAVMESH_SAMPLING, RACE_MANAGER_WIRING
- **IMPORTS**: `UnityEngine`, `UnityEditor`, `UnityEngine.AI`
- **GOTCHA**:
  - `Renderer.bounds` is world-space AABB — already accounts for rotation and parent transforms
  - The `-90° X rotation` on the FBX mesh means the mesh's Y extent in model space becomes Z extent in world space — `bounds` handles this automatically
  - Ellipse waypoints follow track center, not edge — inset factor keeps them on the road
  - `center.y` for NavMesh sampling — the track surface is at whatever Y the mesh renders at
  - Must handle case where NavMesh is not yet baked (warn user)
- **VALIDATE**: Run `EDI Racing > Setup Track`, see window, click Generate → all waypoints on NavMesh

### Task 3: Update SpawnPoint placement in CreateRaceManager
- **ACTION**: Place SpawnPoint at first waypoint area instead of hardcoded `(0, 0.5, -60)`
- **IMPLEMENT**:
  ```csharp
  // In CreateRaceManager(), replace:
  //   spawnPoint.transform.position = new Vector3(0, 0.5f, -60f);
  // With:
  var waypointPath = Object.FindFirstObjectByType<WaypointPath>();
  if (waypointPath != null && waypointPath.Waypoints.Length > 0)
  {
      spawnPoint.transform.position = waypointPath.Waypoints[0].position;
      if (waypointPath.Waypoints.Length > 1)
      {
          Vector3 dir = (waypointPath.Waypoints[1].position
              - waypointPath.Waypoints[0].position).normalized;
          spawnPoint.transform.rotation = Quaternion.LookRotation(dir);
      }
  }
  ```
- **MIRROR**: RACE_MANAGER_WIRING pattern
- **GOTCHA**: SpawnPoint rotation should face the racing direction so cars spawn facing the right way
- **VALIDATE**: Cars spawn on the track surface near the start line, facing correct direction

---

## Testing Strategy

### Manual Tests (Unity Editor)

| Test | Input | Expected | Edge Case? |
|---|---|---|---|
| Auto-detect track mesh | Open window | Finds `1TARMAC_oval` automatically | No |
| No track mesh in scene | Empty scene | Warning message, no crash | Yes |
| NavMesh not baked | Run before baking | Warning: "0/N waypoints on NavMesh" | Yes |
| 8 waypoints | Set count to 8 | 8 WP + 8 CP, cars complete laps | No |
| 24 waypoints | Set count to 24 | 24 WP + 24 CP, smooth racing line | No |
| Re-run setup | Run twice | Old WP/CP cleaned up, new ones created | No |
| Inset factor 1.0 | Edge of bounds | Some waypoints may miss NavMesh | Yes |
| Inset factor 0.5 | Center of bounds | Waypoints too far inward | Yes |
| LapTracker auto-count | 16 checkpoints | LapTracker.totalCheckpoints = 16 | No |

### Edge Cases Checklist
- [ ] NavMesh not baked — clear warning, no crash
- [ ] Track mesh has zero-size bounds — reject with error
- [ ] Track mesh deleted after detection — null check
- [ ] Running Setup Track multiple times — clean up old objects
- [ ] LapTracker works with any checkpoint count (not just 14)

---

## Validation Commands

### Compile Check
Open Unity Editor — zero compile errors in Console

### Visual Validation
1. Open `complete_track_demo` scene
2. Select `Waypoints` parent → yellow gizmo spheres should trace the oval track
3. Select `Checkpoints` parent → collider volumes should span track width

### Play Mode Test
1. Press Play
2. Console: `[RaceManager] Race started with N cars`
3. Cars follow the track without getting stuck
4. Console: `[Race] TeamName completed lap 1/2/3`

---

## Acceptance Criteria
- [ ] `EDI Racing > Setup Track` opens an EditorWindow (not instant execute)
- [ ] Track mesh auto-detected from scene (by name or NavMeshModifier)
- [ ] Waypoint count configurable (8-24, default 16)
- [ ] All waypoints land on NavMesh (validated in console output)
- [ ] Checkpoints span track width and face racing direction
- [ ] SpawnPoint placed at first waypoint area facing correct direction
- [ ] LapTracker auto-detects checkpoint count (no hardcoded 14)
- [ ] Cars complete 3 laps without getting stuck
- [ ] Running Setup Track multiple times cleans up old objects
- [ ] No compile errors

## Completion Checklist
- [ ] Code follows existing TrackSetupEditor patterns
- [ ] No hardcoded track dimensions (all derived from mesh bounds)
- [ ] Clear console diagnostics (detected bounds, valid waypoint count)
- [ ] Handles missing NavMesh gracefully (warning, not crash)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Mesh bounds AABB doesn't perfectly match oval shape | MEDIUM | LOW | Inset factor + NavMesh.SamplePosition compensates; user can tune |
| Some waypoints still miss NavMesh on tight corners | LOW | MEDIUM | Increase NavMesh sample radius; user adjusts inset factor |
| Different track assets have different naming | LOW | LOW | Fallback: user manually assigns track mesh in window |

## Notes
- The `1TARMAC_oval` mesh has a `-90° X rotation` (FBX axis conversion). `Renderer.bounds` already accounts for this — no manual transform math needed.
- The NavMesh Surface uses `m_CollectObjects: 3` (NavMeshModifier-only), meaning only `1TARMAC_oval` (which has NavMeshModifier) contributes to the NavMesh.
- Active NavMesh data: `NavMesh-GameObject.asset` (guid: `57c9dd8d26aa84a1b83f6807bbe4b17e`).
- The user previously extracted `1TARMAC_oval` mesh into `new.FBX` and created `EDI_oval_mod_terrain.prefab` — the auto-detection should work with either version.
- Parent prefab root sits at world origin with no rotation/scale, so `1TARMAC_oval` world position is its local position `(~0, 0, -160.5)` with `-90° X rotation`.
