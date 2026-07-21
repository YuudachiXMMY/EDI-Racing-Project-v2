using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
    private Transform spawnPointTransform;
    private int waypointCount = 16;
    private float trackYOffset = 0.5f;
    private float insetFactor = 0.85f;
    private string boundsInfo = "";

    [MenuItem("EDI Racing/Setup Track")]
    public static void ShowWindow()
    {
        var window = GetWindow<TrackSetupEditor>("Track Setup");
        window.minSize = new Vector2(350, 420);
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

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Spawn Point", EditorStyles.miniBoldLabel);
        spawnPointTransform = (Transform)EditorGUILayout.ObjectField(
            "Spawn Position", spawnPointTransform, typeof(Transform), true);
        EditorGUILayout.HelpBox(
            "Drag a Transform from the scene to set where cars spawn. " +
            "Waypoints will start from this position around the track. " +
            "If empty, waypoints start at angle 0 (top of ellipse).",
            MessageType.None);

        EditorGUILayout.Space(4);
        waypointCount = EditorGUILayout.IntSlider("Waypoint Count", waypointCount, 8, 64);
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

        // Compute starting angle from spawn point position on the ellipse
        float startAngle = 0f;
        if (spawnPointTransform != null)
        {
            float dx = spawnPointTransform.position.x - center.x;
            float dz = spawnPointTransform.position.z - center.z;
            startAngle = Mathf.Atan2(dx / radiusX, -dz / radiusZ);
            Debug.Log($"[TrackSetup] Spawn point at ({spawnPointTransform.position.x:F1}, " +
                $"{spawnPointTransform.position.z:F1}), start angle: {startAngle * Mathf.Rad2Deg:F1}°");
        }

        // --- Step 1: Generate seed control points on the ellipse ---
        int seedCount = Mathf.Max(waypointCount, 32);
        var seedPoints = new Vector3[seedCount];
        for (int i = 0; i < seedCount; i++)
        {
            float angle = startAngle + (float)i / seedCount * Mathf.PI * 2f;
            float x = center.x + Mathf.Sin(angle) * radiusX;
            float z = center.z - Mathf.Cos(angle) * radiusZ;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(
                new Vector3(x, center.y, z), out hit, 30f, NavMesh.AllAreas))
            {
                seedPoints[i] = hit.position + Vector3.up * trackYOffset;
            }
            else
            {
                seedPoints[i] = new Vector3(x, center.y + trackYOffset, z);
            }
        }

        // --- Step 2: Catmull-Rom spline → dense smooth curve ---
        int denseSamples = 512;
        var denseCurve = new Vector3[denseSamples];
        for (int i = 0; i < denseSamples; i++)
        {
            float t = (float)i / denseSamples * seedCount;
            denseCurve[i] = CatmullRomLoop(seedPoints, t);
        }

        // --- Step 3: Compute cumulative arc lengths ---
        var arcLengths = new float[denseSamples];
        arcLengths[0] = 0f;
        for (int i = 1; i < denseSamples; i++)
        {
            arcLengths[i] = arcLengths[i - 1]
                + Vector3.Distance(denseCurve[i], denseCurve[i - 1]);
        }
        // Add closing segment (last point → first point)
        float totalLength = arcLengths[denseSamples - 1]
            + Vector3.Distance(denseCurve[denseSamples - 1], denseCurve[0]);

        // --- Step 4: Re-sample at equal arc-length intervals ---
        var positions = new Vector3[waypointCount];
        var tangents = new Vector3[waypointCount];
        int validCount = 0;

        for (int i = 0; i < waypointCount; i++)
        {
            float targetArc = (float)i / waypointCount * totalLength;

            // Binary search for the dense curve segment containing targetArc
            int lo = 0, hi = denseSamples - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) / 2;
                if (arcLengths[mid] <= targetArc) lo = mid;
                else hi = mid;
            }

            // Lerp within the segment for sub-sample accuracy
            float segLen = arcLengths[hi] - arcLengths[lo];
            float frac = segLen > 0.001f
                ? (targetArc - arcLengths[lo]) / segLen
                : 0f;
            Vector3 pos = Vector3.Lerp(denseCurve[lo], denseCurve[hi], frac);

            // Snap to NavMesh for ground conformance
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pos, out hit, 30f, NavMesh.AllAreas))
            {
                positions[i] = hit.position + Vector3.up * trackYOffset;
                validCount++;
            }
            else
            {
                positions[i] = pos;
                Debug.LogWarning(
                    $"[TrackSetup] WP_{i:D2} NOT on NavMesh at ({pos.x:F1}, {pos.z:F1})");
            }

            // Tangent from spline for smooth checkpoint orientation
            int nextDense = (hi + 1) % denseSamples;
            tangents[i] = (denseCurve[nextDense] - denseCurve[lo]).normalized;
        }

        Debug.Log($"[TrackSetup] {validCount}/{waypointCount} waypoints on NavMesh " +
            $"(spline-smoothed, arc-length: {totalLength:F1}m)");

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

            // Use spline tangent for smooth orientation, fall back to next-point direction
            Vector3 dir = tangents[i];
            if (dir.sqrMagnitude < 0.01f)
            {
                int nextIdx = (i + 1) % waypointCount;
                dir = (positions[nextIdx] - positions[i]).normalized;
            }
            dir.y = 0f;
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

    /// <summary>
    /// Catmull-Rom spline evaluation on a closed loop of control points.
    /// Parameter t ranges over [0, points.Length) where integer values
    /// correspond to control points.
    /// </summary>
    private static Vector3 CatmullRomLoop(Vector3[] points, float t)
    {
        int n = points.Length;
        int i1 = Mathf.FloorToInt(t) % n;
        if (i1 < 0) i1 += n;
        int i0 = (i1 - 1 + n) % n;
        int i2 = (i1 + 1) % n;
        int i3 = (i1 + 2) % n;

        float f = t - Mathf.Floor(t);
        float f2 = f * f;
        float f3 = f2 * f;

        // Standard Catmull-Rom basis (tau = 0.5)
        return 0.5f * (
            (2f * points[i1]) +
            (-points[i0] + points[i2]) * f +
            (2f * points[i0] - 5f * points[i1] + 4f * points[i2] - points[i3]) * f2 +
            (-points[i0] + 3f * points[i1] - 3f * points[i2] + points[i3]) * f3
        );
    }

    private void CreateRaceManager()
    {
        var existingRM = GameObject.Find("RaceManager");
        if (existingRM != null) DestroyImmediate(existingRM);

        var rmObj = new GameObject("RaceManager");

        // --- Core components (on RaceManager GameObject) ---
        var raceManager = rmObj.AddComponent<RaceManager>();
        var carSpawner = rmObj.AddComponent<CarSpawner>();
        var lapTracker = rmObj.AddComponent<LapTracker>();
        var scoreManager = rmObj.AddComponent<ScoreManager>();
        var eventManager = rmObj.AddComponent<EventManager>();
        var sessionManager = rmObj.AddComponent<SessionManager>();
        var weatherEffect = rmObj.AddComponent<WeatherEffect>();

        // Wire core references
        raceManager.CarSpawner = carSpawner;
        raceManager.LapTracker = lapTracker;
        raceManager.ScoreManager = scoreManager;
        raceManager.EventManager = eventManager;
        raceManager.SessionManager = sessionManager;
        raceManager.WeatherEffect = weatherEffect;

        // --- Config assets ---
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
        carSpawner.Config = config;

        var eventSchedule = AssetDatabase.LoadAssetAtPath<EventSchedule>(
            "Assets/Settings/EventSchedule.asset");
        if (eventSchedule != null)
            eventManager.Schedule = eventSchedule;
        else
            Debug.LogWarning("[TrackSetup] EventSchedule.asset not found at Assets/Settings/. " +
                "Create one via Assets > Create > EDI Racing > Event Schedule.");

        var csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/vehicleGroupData.csv");
        if (csvAsset != null)
            raceManager.DefaultCsvData = csvAsset;

        // --- CarSpawner wiring ---
        carSpawner.CarPrefabs = LoadOrCreateCarPrefabs();

        var waypointPath = Object.FindFirstObjectByType<WaypointPath>();
        if (waypointPath != null)
            carSpawner.WaypointPath = waypointPath;

        // Place SpawnPoint at user-specified position, or fall back to first waypoint
        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.parent = rmObj.transform;

        if (spawnPointTransform != null)
        {
            spawnPoint.transform.position = spawnPointTransform.position;
            spawnPoint.transform.rotation = spawnPointTransform.rotation;

            if (spawnPointTransform.rotation == Quaternion.identity
                && waypointPath != null && waypointPath.Waypoints.Length > 0)
            {
                Vector3 dir = (waypointPath.Waypoints[0].position
                    - spawnPoint.transform.position).normalized;
                if (dir != Vector3.zero)
                    spawnPoint.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
        else if (waypointPath != null && waypointPath.Waypoints.Length > 0)
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

        // --- Starting Grid Positions under SpawnPoint ---
        int gridCount = 27;
        var gridPositions = new Transform[gridCount];
        float spacing = 3.5f;
        int cols = 2;
        for (int i = 0; i < gridCount; i++)
        {
            var gridObj = new GameObject($"GridPos_{i + 1}");
            gridObj.transform.parent = spawnPoint.transform;
            int row = i / cols;
            int col = i % cols;
            float xOff = (col - (cols - 1) * 0.5f) * spacing;
            float zOff = -row * spacing;
            gridObj.transform.localPosition = new Vector3(xOff, 0, zOff);
            gridObj.transform.localRotation = Quaternion.identity;
            gridPositions[i] = gridObj.transform;
        }
        carSpawner.StartingGridPositions = gridPositions;

        // --- Network (on child GameObject) ---
        var netObj = new GameObject("Network");
        netObj.transform.parent = rmObj.transform;
        var networkManager = netObj.AddComponent<NetworkManager>();
        var networkSync = netObj.AddComponent<NetworkSync>();

        networkSync.RaceManager = raceManager;
        networkSync.NetworkManager = networkManager;
        networkSync.ScoreManager = scoreManager;
        raceManager.NetworkSync = networkSync;

        // --- Survey (on child GameObject) ---
        var surveyObj = new GameObject("Survey");
        surveyObj.transform.parent = rmObj.transform;
        var surveyCollector = surveyObj.AddComponent<SurveyCollector>();
        var surveyConfigManager = surveyObj.AddComponent<SurveyConfigManager>();
        var studentSurveyPanel = surveyObj.AddComponent<StudentSurveyPanel>();

        surveyCollector.NetworkManager = networkManager;
        surveyCollector.ConfigManager = surveyConfigManager;
        studentSurveyPanel.NetworkManager = networkManager;
        networkSync.SurveyCollector = surveyCollector;
        networkSync.StudentSurveyPanel = studentSurveyPanel;
        raceManager.SurveyConfigManager = surveyConfigManager;

        // --- Camera (on child GameObject) ---
        var camObj = new GameObject("CameraManager");
        camObj.transform.parent = rmObj.transform;
        var cameraManager = camObj.AddComponent<CameraManager>();

        // Wire or create main camera components
        var mainCam = Object.FindFirstObjectByType<Camera>();
        GameObject mainCamObj = mainCam != null ? mainCam.gameObject : null;

        if (mainCamObj == null)
        {
            mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            mainCamObj.AddComponent<Camera>();
            mainCamObj.AddComponent<AudioListener>();
        }

        var freeCam = mainCamObj.GetComponent<RaceCameraController>();
        if (freeCam == null) freeCam = mainCamObj.AddComponent<RaceCameraController>();
        cameraManager.FreeCamera = freeCam;

        var spectatorCam = mainCamObj.GetComponent<SpectatorCamera>();
        if (spectatorCam == null) spectatorCam = mainCamObj.AddComponent<SpectatorCamera>();
        spectatorCam.enabled = false;
        spectatorCam.ScoreManager = scoreManager;
        cameraManager.SpectatorCam = spectatorCam;

        // Fixed cameras
        var fixedPoints = new FixedCameraPoint[9];
        for (int i = 0; i < 9; i++)
        {
            string camName = $"FixedCam_F{i + 1}";
            var existing = GameObject.Find(camName);
            if (existing != null)
            {
                fixedPoints[i] = existing.GetComponent<FixedCameraPoint>();
                if (fixedPoints[i] == null) fixedPoints[i] = existing.AddComponent<FixedCameraPoint>();
            }
            else
            {
                var fcObj = new GameObject(camName);
                fixedPoints[i] = fcObj.AddComponent<FixedCameraPoint>();
            }
            fixedPoints[i].PointIndex = i;
        }
        cameraManager.FixedPoints = fixedPoints;

        // --- CarLabelSpawner ---
        var labelSpawnerObj = new GameObject("CarLabelSpawner");
        var carLabelSpawner = labelSpawnerObj.AddComponent<CarLabelSpawner>();
        carLabelSpawner.RaceManager = raceManager;

        // --- EventSystem ---
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }

        // --- UI (Canvas + panels) ---
        // Reuse existing Canvas or create one
        var existingCanvas = Object.FindFirstObjectByType<Canvas>();
        GameObject canvasObj;
        if (existingCanvas != null)
        {
            canvasObj = existingCanvas.gameObject;
        }
        else
        {
            canvasObj = new GameObject("UICanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // RaceUI controller
        var raceUI = canvasObj.GetComponent<RaceUI>();
        if (raceUI == null) raceUI = canvasObj.AddComponent<RaceUI>();
        raceUI.RaceManager = raceManager;
        raceUI.CameraManager = cameraManager;

        // --- Wire or create each panel ---
        raceUI.Setup = WireOrCreateSetupScreen(canvasObj.transform, raceManager, networkManager);
        raceUI.Leaderboard = WireOrCreateLeaderboard(canvasObj.transform, scoreManager);
        raceUI.Events = WireOrCreateEventPanel(canvasObj.transform, eventManager);
        raceUI.Controls = WireOrCreateControlPanel(canvasObj.transform, raceManager);
        raceUI.JoinScreen = WireOrCreateJoinScreen(canvasObj.transform, networkManager);

        EditorUtility.SetDirty(canvasObj);
        EditorUtility.SetDirty(rmObj);
        Debug.Log("[TrackSetup] RaceManager created with all managers wired: " +
            "CarSpawner, LapTracker, ScoreManager, EventManager, SessionManager, " +
            "WeatherEffect, NetworkManager, NetworkSync, CameraManager, RaceUI + full UI hierarchy");
    }

    // ── Car Prefab Loader ──────────────────────────────────

    private static readonly string CarPrefabDir = "Assets/Prefabs/Cars";
    private static readonly string[] CarColorNames = { "Green", "Black", "Red", "Blue", "White" };
    private static readonly Color[] CarColors =
    {
        new Color(0.2f, 0.8f, 0.2f),
        new Color(0.15f, 0.15f, 0.15f),
        new Color(0.9f, 0.2f, 0.2f),
        new Color(0.2f, 0.4f, 0.9f),
        Color.white
    };

    private GameObject[] LoadOrCreateCarPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(CarPrefabDir))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Cars");

        var prefabs = new GameObject[CarColorNames.Length];
        bool anyCreated = false;

        for (int i = 0; i < CarColorNames.Length; i++)
        {
            string path = $"{CarPrefabDir}/Car_{CarColorNames[i]}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                prefabs[i] = existing;
                continue;
            }

            // Create placeholder: a colored cube scaled to car-like proportions
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Car_{CarColorNames[i]}";
            go.transform.localScale = new Vector3(1f, 0.5f, 2f);

            // Remove the collider — CarSpawner adds its own trigger collider
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            // Apply color via a new material
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = CarColors[i];
                string matPath = $"{CarPrefabDir}/Mat_{CarColorNames[i]}.mat";
                AssetDatabase.CreateAsset(mat, matPath);
                renderer.sharedMaterial = mat;
            }

            prefabs[i] = PrefabUtility.SaveAsPrefabAsset(go, path);
            DestroyImmediate(go);
            anyCreated = true;
        }

        if (anyCreated)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[TrackSetup] Created placeholder car prefabs in Assets/Prefabs/Cars/. " +
                "Replace them with real car models when ready.");
        }

        return prefabs;
    }

    // ── UI Panel Builders ─────────────────────────────────

    private SetupScreen WireOrCreateSetupScreen(Transform canvasRoot, RaceManager rm, NetworkManager nm)
    {
        var existing = Object.FindFirstObjectByType<SetupScreen>();
        if (existing != null) return existing;

        var panel = CreateUIPanel(canvasRoot, "SetupScreen",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-200, -120), new Vector2(200, 120));

        var setup = panel.AddComponent<SetupScreen>();
        setup.RaceManager = rm;
        setup.NetworkManager = nm;

        // Title
        CreateLabel(panel.transform, "Title", "EDI Racing Setup", 24, TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -50), new Vector2(-10, -10));

        // Info text
        setup.InfoText = CreateLabel(panel.transform, "InfoText", "Ready to start race.", 16, TextAnchor.MiddleCenter,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f),
            new Vector2(10, -10), new Vector2(-10, 10));

        // Start Default button
        setup.StartDefaultButton = CreateUIButton(panel.transform, "StartDefaultBtn", "Start Race (Default CSV)",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-150, 60), new Vector2(150, 95));

        // Load Session button
        setup.LoadSessionButton = CreateUIButton(panel.transform, "LoadSessionBtn", "Load Session",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-150, 15), new Vector2(150, 50));

        // Host Room button (network)
        setup.HostButton = CreateUIButton(panel.transform, "HostBtn", "Host Room",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-150, -30), new Vector2(-10, 5));

        // Room Code text
        setup.RoomCodeText = CreateLabel(panel.transform, "RoomCodeText", "", 16, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, -30), new Vector2(150, 5));

        // Student Count text
        setup.StudentCountText = CreateLabel(panel.transform, "StudentCountText", "", 14, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-150, -60), new Vector2(150, -35));

        return setup;
    }

    private LeaderboardPanel WireOrCreateLeaderboard(Transform canvasRoot, ScoreManager sm)
    {
        var existing = Object.FindFirstObjectByType<LeaderboardPanel>();
        if (existing != null) return existing;

        var panel = CreateUIPanel(canvasRoot, "LeaderboardPanel",
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(10, -400), new Vector2(290, -10));

        var lb = panel.AddComponent<LeaderboardPanel>();
        lb.ScoreManager = sm;

        // Title
        CreateLabel(panel.transform, "Title", "Leaderboard", 20, TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(5, -30), new Vector2(-5, -5));

        // Content parent for rows
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(panel.transform, false);
        var contentRT = contentObj.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.offsetMin = new Vector2(5, 5);
        contentRT.offsetMax = new Vector2(-5, -35);
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        lb.ContentParent = contentObj.transform;

        // Row prefab reference
        var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/LeaderboardRow.prefab");
        if (rowPrefab != null)
            lb.RowPrefab = rowPrefab;
        else
            Debug.LogWarning("[TrackSetup] LeaderboardRow.prefab not found at Assets/Prefabs/UI/. " +
                "Create a prefab with a Text component.");

        panel.SetActive(false); // shown during Racing state
        return lb;
    }

    private EventPanel WireOrCreateEventPanel(Transform canvasRoot, EventManager em)
    {
        var existing = Object.FindFirstObjectByType<EventPanel>();
        if (existing != null) return existing;

        var panel = CreateUIPanel(canvasRoot, "EventPanel",
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-290, -350), new Vector2(-10, -10));

        var ep = panel.AddComponent<EventPanel>();
        ep.EventManager = em;

        // Title
        CreateLabel(panel.transform, "Title", "Events", 20, TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(5, -30), new Vector2(-5, -5));

        // Content parent for event rows
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(panel.transform, false);
        var contentRT = contentObj.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.offsetMin = new Vector2(5, 5);
        contentRT.offsetMax = new Vector2(-5, -35);
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        ep.ContentParent = contentObj.transform;

        // Event row prefab reference
        var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/EventRow.prefab");
        if (rowPrefab != null)
            ep.EventRowPrefab = rowPrefab;
        else
            Debug.LogWarning("[TrackSetup] EventRow.prefab not found at Assets/Prefabs/UI/. " +
                "Create a prefab with a Button + Text component.");

        panel.SetActive(false);
        return ep;
    }

    private RaceControlPanel WireOrCreateControlPanel(Transform canvasRoot, RaceManager rm)
    {
        var existing = Object.FindFirstObjectByType<RaceControlPanel>();
        if (existing != null) return existing;

        var panel = CreateUIPanel(canvasRoot, "RaceControlPanel",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-220, 10), new Vector2(220, 60));

        var cp = panel.AddComponent<RaceControlPanel>();
        cp.RaceManager = rm;

        // Pause/Resume button
        cp.PauseResumeButton = CreateUIButton(panel.transform, "PauseResumeBtn", "Pause",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(10, -17), new Vector2(120, 17));
        cp.PauseResumeLabel = cp.PauseResumeButton.GetComponentInChildren<Text>();

        // Save button
        cp.SaveButton = CreateUIButton(panel.transform, "SaveBtn", "Save",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(130, -17), new Vector2(240, 17));

        // Export button
        cp.ExportButton = CreateUIButton(panel.transform, "ExportBtn", "Export",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(250, -17), new Vector2(360, 17));

        // Status text
        cp.StatusText = CreateLabel(panel.transform, "StatusText", "", 14, TextAnchor.MiddleRight,
            new Vector2(1, 0), new Vector2(1, 1),
            new Vector2(-70, 0), new Vector2(-5, 0));

        panel.SetActive(false);
        return cp;
    }

    private JoinScreen WireOrCreateJoinScreen(Transform canvasRoot, NetworkManager nm)
    {
        var existing = Object.FindFirstObjectByType<JoinScreen>();
        if (existing != null) return existing;

        var panel = CreateUIPanel(canvasRoot, "JoinScreen",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-180, -100), new Vector2(180, 100));

        var js = panel.AddComponent<JoinScreen>();
        js.NetworkManager = nm;

        // Title
        CreateLabel(panel.transform, "Title", "Join Race", 24, TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -50), new Vector2(-10, -10));

        // Room code input field
        var inputObj = new GameObject("RoomCodeInput");
        inputObj.transform.SetParent(panel.transform, false);
        var inputImg = inputObj.AddComponent<Image>();
        inputImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        var inputRT = inputObj.GetComponent<RectTransform>();
        inputRT.anchorMin = new Vector2(0.5f, 0.5f);
        inputRT.anchorMax = new Vector2(0.5f, 0.5f);
        inputRT.pivot = new Vector2(0.5f, 0.5f);
        inputRT.sizeDelta = new Vector2(200, 35);
        inputRT.anchoredPosition = new Vector2(0, 10);

        var inputText = CreateLabel(inputObj.transform, "Text", "", 18, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(5, 2), new Vector2(-5, -2));

        var placeholder = CreateLabel(inputObj.transform, "Placeholder", "ROOM CODE", 18, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(5, 2), new Vector2(-5, -2));
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.color = new Color(1, 1, 1, 0.3f);

        var inputField = inputObj.AddComponent<InputField>();
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;
        inputField.characterLimit = 6;
        js.RoomCodeInput = inputField;

        // Team name input field
        var teamInputObj = new GameObject("TeamNameInput");
        teamInputObj.transform.SetParent(panel.transform, false);
        var teamInputImg = teamInputObj.AddComponent<Image>();
        teamInputImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        var teamInputRT = teamInputObj.GetComponent<RectTransform>();
        teamInputRT.anchorMin = new Vector2(0.5f, 0.5f);
        teamInputRT.anchorMax = new Vector2(0.5f, 0.5f);
        teamInputRT.pivot = new Vector2(0.5f, 0.5f);
        teamInputRT.sizeDelta = new Vector2(200, 35);
        teamInputRT.anchoredPosition = new Vector2(0, -30);

        var teamText = CreateLabel(teamInputObj.transform, "Text", "", 18, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(5, 2), new Vector2(-5, -2));

        var teamPlaceholder = CreateLabel(teamInputObj.transform, "Placeholder", "TEAM NAME", 18, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(5, 2), new Vector2(-5, -2));
        teamPlaceholder.fontStyle = FontStyle.Italic;
        teamPlaceholder.color = new Color(1, 1, 1, 0.3f);

        var teamInputField = teamInputObj.AddComponent<InputField>();
        teamInputField.textComponent = teamText;
        teamInputField.placeholder = teamPlaceholder;
        teamInputField.characterLimit = 30;
        js.TeamNameInput = teamInputField;

        // Join button
        js.JoinButton = CreateUIButton(panel.transform, "JoinBtn", "Join",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-80, 15), new Vector2(80, 50));

        // Status text
        js.StatusText = CreateLabel(panel.transform, "StatusText", "Enter room code to join.", 14, TextAnchor.MiddleCenter,
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(10, -25), new Vector2(-10, 10));

        panel.SetActive(false); // shown for Student role only
        return js;
    }

    // ── UI Factory Helpers ────────────────────────────────

    private GameObject CreateUIPanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var bg = obj.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return obj;
    }

    private Text CreateLabel(Transform parent, string name, string content,
        int fontSize, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var text = obj.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return text;
    }

    private Button CreateUIButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var bg = obj.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        var btn = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
        btn.colors = colors;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        CreateLabel(obj.transform, "Label", label, 16, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return btn;
    }
}
