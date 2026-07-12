using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Comprehensive scene wiring utility. Validates and auto-wires ALL component
/// references across the entire EDI Racing scene hierarchy.
/// Run via menu: EDI Racing > Wire All References
/// Idempotent — safe to run multiple times without duplicating objects.
/// </summary>
public static class SceneWiring
{
    private static int wiredCount;
    private static int createdCount;
    private static int warningCount;

    [MenuItem("EDI Racing/Wire All References")]
    public static void WireAll()
    {
        wiredCount = 0;
        createdCount = 0;
        warningCount = 0;

        Debug.Log("[WireAll] ═══ Starting full scene wiring check ═══");

        // Locate core singletons
        var raceManager = Object.FindFirstObjectByType<RaceManager>();
        if (raceManager == null)
        {
            Debug.LogError("[WireAll] No RaceManager in scene. Run 'EDI Racing > Setup Track' first.");
            return;
        }

        var networkManager = Object.FindFirstObjectByType<NetworkManager>();
        var networkSync = Object.FindFirstObjectByType<NetworkSync>();
        var scoreManager = Object.FindFirstObjectByType<ScoreManager>();
        var carSpawner = Object.FindFirstObjectByType<CarSpawner>();
        var lapTracker = Object.FindFirstObjectByType<LapTracker>();
        var eventManager = Object.FindFirstObjectByType<EventManager>();
        var sessionManager = Object.FindFirstObjectByType<SessionManager>();
        var weatherEffect = Object.FindFirstObjectByType<WeatherEffect>();
        var cameraManager = Object.FindFirstObjectByType<CameraManager>();
        var waypointPath = Object.FindFirstObjectByType<WaypointPath>();
        var configManager = FindOrCreate<SurveyConfigManager>(raceManager.gameObject, "SurveyConfigManager");
        var collector = FindOrCreate<SurveyCollector>(
            networkSync != null ? networkSync.gameObject : raceManager.gameObject, "SurveyCollector");

        // UI singletons
        var raceUI = Object.FindFirstObjectByType<RaceUI>();
        var setupScreen = Object.FindFirstObjectByType<SetupScreen>();
        var joinScreen = Object.FindFirstObjectByType<JoinScreen>();
        var leaderboard = Object.FindFirstObjectByType<LeaderboardPanel>();
        var eventPanel = Object.FindFirstObjectByType<EventPanel>();
        var controlPanel = Object.FindFirstObjectByType<RaceControlPanel>();
        var builderPanel = Object.FindFirstObjectByType<SurveyBuilderPanel>();
        var configPanel = Object.FindFirstObjectByType<ConfigManagerPanel>();

        // StudentSurveyPanel — needs Canvas
        var surveyPanel = Object.FindFirstObjectByType<StudentSurveyPanel>();
        if (surveyPanel == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var panelObj = new GameObject("StudentSurveyPanel");
                panelObj.transform.SetParent(canvas.transform, false);
                var rt = panelObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                surveyPanel = panelObj.AddComponent<StudentSurveyPanel>();
                createdCount++;
                Debug.Log("[WireAll] Created StudentSurveyPanel on Canvas");
            }
            else
            {
                Warn("No Canvas found — cannot create StudentSurveyPanel");
            }
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: RaceManager
        // ═══════════════════════════════════════════════════════
        Wire(ref raceManager.CarSpawner, carSpawner, "RaceManager.CarSpawner");
        Wire(ref raceManager.LapTracker, lapTracker, "RaceManager.LapTracker");
        Wire(ref raceManager.ScoreManager, scoreManager, "RaceManager.ScoreManager");
        Wire(ref raceManager.EventManager, eventManager, "RaceManager.EventManager");
        Wire(ref raceManager.SessionManager, sessionManager, "RaceManager.SessionManager");
        Wire(ref raceManager.WeatherEffect, weatherEffect, "RaceManager.WeatherEffect");
        Wire(ref raceManager.NetworkSync, networkSync, "RaceManager.NetworkSync");
        Wire(ref raceManager.SurveyConfigManager, configManager, "RaceManager.SurveyConfigManager");

        // Config asset
        if (raceManager.Config == null)
        {
            var config = AssetDatabase.LoadAssetAtPath<RaceConfig>("Assets/Settings/RaceConfig.asset");
            if (config != null) { raceManager.Config = config; wiredCount++; }
            else Warn("RaceConfig.asset not found at Assets/Settings/");
        }

        EditorUtility.SetDirty(raceManager);

        // ═══════════════════════════════════════════════════════
        // WIRE: CarSpawner
        // ═══════════════════════════════════════════════════════
        if (carSpawner != null)
        {
            Wire(ref carSpawner.WaypointPath, waypointPath, "CarSpawner.WaypointPath");
            if (carSpawner.Config == null && raceManager.Config != null)
            {
                carSpawner.Config = raceManager.Config;
                wiredCount++;
            }
            EditorUtility.SetDirty(carSpawner);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: EventManager
        // ═══════════════════════════════════════════════════════
        if (eventManager != null)
        {
            if (eventManager.Schedule == null)
            {
                var schedule = AssetDatabase.LoadAssetAtPath<EventSchedule>("Assets/Settings/EventSchedule.asset");
                if (schedule != null) { eventManager.Schedule = schedule; wiredCount++; }
                else Warn("EventSchedule.asset not found at Assets/Settings/");
            }
            EditorUtility.SetDirty(eventManager);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: NetworkSync
        // ═══════════════════════════════════════════════════════
        if (networkSync != null)
        {
            Wire(ref networkSync.RaceManager, raceManager, "NetworkSync.RaceManager");
            Wire(ref networkSync.NetworkManager, networkManager, "NetworkSync.NetworkManager");
            Wire(ref networkSync.ScoreManager, scoreManager, "NetworkSync.ScoreManager");
            Wire(ref networkSync.SurveyCollector, collector, "NetworkSync.SurveyCollector");
            Wire(ref networkSync.StudentSurveyPanel, surveyPanel, "NetworkSync.StudentSurveyPanel");
            EditorUtility.SetDirty(networkSync);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: SurveyCollector
        // ═══════════════════════════════════════════════════════
        if (collector != null)
        {
            Wire(ref collector.NetworkManager, networkManager, "SurveyCollector.NetworkManager");
            Wire(ref collector.ConfigManager, configManager, "SurveyCollector.ConfigManager");
            EditorUtility.SetDirty(collector);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: StudentSurveyPanel
        // ═══════════════════════════════════════════════════════
        if (surveyPanel != null)
        {
            Wire(ref surveyPanel.NetworkManager, networkManager, "StudentSurveyPanel.NetworkManager");
            EditorUtility.SetDirty(surveyPanel);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: RaceUI
        // ═══════════════════════════════════════════════════════
        if (raceUI != null)
        {
            Wire(ref raceUI.RaceManager, raceManager, "RaceUI.RaceManager");
            Wire(ref raceUI.CameraManager, cameraManager, "RaceUI.CameraManager");
            Wire(ref raceUI.Leaderboard, leaderboard, "RaceUI.Leaderboard");
            Wire(ref raceUI.Events, eventPanel, "RaceUI.Events");
            Wire(ref raceUI.Controls, controlPanel, "RaceUI.Controls");
            Wire(ref raceUI.Setup, setupScreen, "RaceUI.Setup");
            Wire(ref raceUI.JoinScreen, joinScreen, "RaceUI.JoinScreen");
            Wire(ref raceUI.StudentSurvey, surveyPanel, "RaceUI.StudentSurvey");
            EditorUtility.SetDirty(raceUI);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: SetupScreen
        // ═══════════════════════════════════════════════════════
        if (setupScreen != null)
        {
            Wire(ref setupScreen.RaceManager, raceManager, "SetupScreen.RaceManager");
            Wire(ref setupScreen.NetworkManager, networkManager, "SetupScreen.NetworkManager");
            Wire(ref setupScreen.SurveyConfigManager, configManager, "SetupScreen.SurveyConfigManager");
            Wire(ref setupScreen.BuilderPanel, builderPanel, "SetupScreen.BuilderPanel");
            Wire(ref setupScreen.ConfigPanel, configPanel, "SetupScreen.ConfigPanel");
            Wire(ref setupScreen.SurveyCollector, collector, "SetupScreen.SurveyCollector");

            // Create survey distribution UI if missing
            if (setupScreen.DistributeSurveyButton == null)
            {
                setupScreen.DistributeSurveyButton = CreateButton(
                    setupScreen.transform, "DistributeSurveyBtn", "Distribute Survey",
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-150, -110), new Vector2(-10, -75));
                setupScreen.DistributeSurveyButton.gameObject.SetActive(false);
                createdCount++;
            }

            if (setupScreen.StartWithResponsesButton == null)
            {
                setupScreen.StartWithResponsesButton = CreateButton(
                    setupScreen.transform, "StartWithResponsesBtn", "Start Race (Responses)",
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(10, -110), new Vector2(150, -75));
                setupScreen.StartWithResponsesButton.interactable = false;
                setupScreen.StartWithResponsesButton.gameObject.SetActive(false);
                createdCount++;
            }

            if (setupScreen.ResponseCountText == null)
            {
                setupScreen.ResponseCountText = CreateText(
                    setupScreen.transform, "ResponseCountText", "",
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-150, -135), new Vector2(150, -115));
                setupScreen.ResponseCountText.gameObject.SetActive(false);
                createdCount++;
            }

            EditorUtility.SetDirty(setupScreen);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: JoinScreen
        // ═══════════════════════════════════════════════════════
        if (joinScreen != null)
        {
            Wire(ref joinScreen.NetworkManager, networkManager, "JoinScreen.NetworkManager");
            EditorUtility.SetDirty(joinScreen);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: CameraManager
        // ═══════════════════════════════════════════════════════
        if (cameraManager != null)
        {
            var freeCam = Object.FindFirstObjectByType<RaceCameraController>();
            var spectatorCam = Object.FindFirstObjectByType<SpectatorCamera>();
            if (freeCam != null && cameraManager.FreeCamera == null)
            {
                cameraManager.FreeCamera = freeCam;
                wiredCount++;
            }
            if (spectatorCam != null && cameraManager.SpectatorCam == null)
            {
                cameraManager.SpectatorCam = spectatorCam;
                wiredCount++;
            }
            EditorUtility.SetDirty(cameraManager);
        }

        // ═══════════════════════════════════════════════════════
        // REPORT
        // ═══════════════════════════════════════════════════════
        Debug.Log($"[WireAll] ═══ Complete ═══\n" +
            $"  Wired: {wiredCount} references\n" +
            $"  Created: {createdCount} new objects\n" +
            $"  Warnings: {warningCount} (check above)");

        if (warningCount == 0)
            Debug.Log("[WireAll] All references connected successfully.");
    }

    // ── Helpers ────────────────────────────────────────────

    private static void Wire<T>(ref T field, T value, string label) where T : Object
    {
        if (field != null) return; // already wired
        if (value == null)
        {
            Warn($"{label} — target not found in scene");
            return;
        }
        field = value;
        wiredCount++;
    }

    private static T FindOrCreate<T>(GameObject host, string label) where T : Component
    {
        var existing = Object.FindFirstObjectByType<T>();
        if (existing != null) return existing;

        var component = host.AddComponent<T>();
        createdCount++;
        Debug.Log($"[WireAll] Created {label} on {host.name}");
        return component;
    }

    private static void Warn(string msg)
    {
        warningCount++;
        Debug.LogWarning($"[WireAll] {msg}");
    }

    private static Button CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var bg = obj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.3f, 0.15f, 0.9f);

        var btn = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.2f, 0.45f, 0.2f);
        colors.pressedColor = new Color(0.1f, 0.2f, 0.1f);
        btn.colors = colors;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(obj.transform, false);
        var text = labelObj.AddComponent<Text>();
        text.text = label;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        return btn;
    }

    private static Text CreateText(Transform parent, string name, string content,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<Text>();
        text.text = content;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return text;
    }
}
