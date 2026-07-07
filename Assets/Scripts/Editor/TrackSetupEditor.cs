using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

/// <summary>
/// Editor window to auto-setup the race track:
/// - Auto-detects track mesh (by name or NavMeshModifier)
/// - Computes ellipse radii from mesh world-space bounds
/// - Creates waypoints and checkpoints snapped to NavMesh
/// - Wires up the RaceManager with dynamic SpawnPoint
/// NOTE: Bake NavMesh manually first via Window > AI > Navigation
/// Run via menu: EDI Racing > Setup Track
/// </summary>
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
        window.minSize = new Vector2(350, 320);
        window.AutoDetectTrackMesh();
    }

    private void OnEnable()
    {
        AutoDetectTrackMesh();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Track Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bake NavMesh first via Window > AI > Navigation > Bake",
            MessageType.Warning);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        trackMeshObject = (GameObject)EditorGUILayout.ObjectField(
            "Track Mesh", trackMeshObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
            UpdateBoundsInfo();

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
            Debug.Log("[TrackSetup] Track setup complete! " +
                "Check yellow gizmo spheres in Scene view.");
        }
        EditorGUI.EndDisabledGroup();
    }

    private void AutoDetectTrackMesh()
    {
        // Strategy 1: Find by name containing "TARMAC"
        var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r.gameObject.name.IndexOf("TARMAC_oval",
                System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                trackMeshObject = r.gameObject;
                UpdateBoundsInfo();
                Debug.Log($"[TrackSetup] Auto-detected track mesh: {r.gameObject.name}");
                return;
            }
        }

        // Strategy 2: Find largest MeshRenderer as fallback
        MeshRenderer largestRenderer = null;
        float largestSize = 0f;
        foreach (var r in renderers)
        {
            float size = r.bounds.size.sqrMagnitude;
            if (size > largestSize)
            {
                largestSize = size;
                largestRenderer = r;
            }
        }

        if (largestRenderer != null)
        {
            trackMeshObject = largestRenderer.gameObject;
            UpdateBoundsInfo();
            Debug.Log($"[TrackSetup] Auto-detected track mesh (largest): " +
                $"{largestRenderer.gameObject.name}");
            return;
        }

        trackMeshObject = null;
        boundsInfo = "No track mesh found. Drag one into the Track Mesh field.";
        Debug.LogWarning("[TrackSetup] Could not auto-detect track mesh.");
    }

    private void UpdateBoundsInfo()
    {
        if (trackMeshObject == null)
        {
            boundsInfo = "";
            return;
        }

        var renderer = trackMeshObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            boundsInfo = "Selected object has no Renderer component.";
            return;
        }

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
            Debug.LogError("[TrackSetup] Track mesh has no Renderer component.");
            return;
        }

        Bounds bounds = renderer.bounds;
        Vector3 center = bounds.center;
        float radiusX = bounds.extents.x * insetFactor;
        float radiusZ = bounds.extents.z * insetFactor;

        if (radiusX < 1f || radiusZ < 1f)
        {
            Debug.LogError("[TrackSetup] Track mesh bounds too small. " +
                $"Extents: {bounds.extents}");
            return;
        }

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
                    $"[TrackSetup] WP_{i:D2} NOT on NavMesh at ({x:F1}, {z:F1})");
            }
        }

        Debug.Log($"[TrackSetup] {validCount}/{waypointCount} waypoints on NavMesh");

        if (validCount == 0)
        {
            Debug.LogError("[TrackSetup] No waypoints landed on NavMesh! " +
                "Bake NavMesh first via Window > AI > Navigation.");
        }

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

    private void CreateRaceManager()
    {
        var existingRM = GameObject.Find("RaceManager");
        if (existingRM != null) DestroyImmediate(existingRM);

        var rmObj = new GameObject("RaceManager");

        var raceManager = rmObj.AddComponent<RaceManager>();
        var carSpawner = rmObj.AddComponent<CarSpawner>();
        var lapTracker = rmObj.AddComponent<LapTracker>();
        var scoreManager = rmObj.AddComponent<ScoreManager>();

        raceManager.CarSpawner = carSpawner;
        raceManager.LapTracker = lapTracker;
        raceManager.ScoreManager = scoreManager;

        var config = AssetDatabase.LoadAssetAtPath<RaceConfig>("Assets/Settings/RaceConfig.asset");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<RaceConfig>();
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");
            AssetDatabase.CreateAsset(config, "Assets/Settings/RaceConfig.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[TrackSetup] Created RaceConfig asset at Assets/Settings/");
        }
        raceManager.Config = config;

        var csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/vehicleGroupData.csv");
        if (csvAsset != null)
            raceManager.DefaultCsvData = csvAsset;

        carSpawner.Config = config;
        var waypointPath = Object.FindFirstObjectByType<WaypointPath>();
        if (waypointPath != null)
            carSpawner.WaypointPath = waypointPath;

        // Place SpawnPoint at first waypoint, facing racing direction
        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.parent = rmObj.transform;

        if (waypointPath != null && waypointPath.Waypoints.Length > 0)
        {
            spawnPoint.transform.position = waypointPath.Waypoints[0].position;
            if (waypointPath.Waypoints.Length > 1)
            {
                Vector3 dir = (waypointPath.Waypoints[1].position
                    - waypointPath.Waypoints[0].position).normalized;
                if (dir != Vector3.zero)
                    spawnPoint.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
        else
        {
            spawnPoint.transform.position = new Vector3(0, 0.5f, 0);
            Debug.LogWarning("[TrackSetup] No waypoints found - SpawnPoint at origin.");
        }

        carSpawner.SpawnPoint = spawnPoint.transform;

        EditorUtility.SetDirty(rmObj);
        Debug.Log("[TrackSetup] RaceManager created and wired");
    }
}
