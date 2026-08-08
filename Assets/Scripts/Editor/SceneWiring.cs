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

        // All lookups below use FindObjectsInactive.Include because several UI panels start
        // inactive (JoinScreen/EventPanel/RaceControlPanel/LeaderboardPanel are SetActive(false)
        // for role/state gating). Without Include, re-running wiring can't see an existing
        // inactive panel — it leaves RaceUI references dangling and FindOrCreate duplicates
        // components. Mirrors RaceUI.ResolveMissingReferences.
        // Locate core singletons
        var raceManager = Object.FindFirstObjectByType<RaceManager>(FindObjectsInactive.Include);
        if (raceManager == null)
        {
            Debug.LogError("[WireAll] No RaceManager in scene. Run 'EDI Racing > Setup Track' first.");
            return;
        }

        var networkManager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
        var networkSync = Object.FindFirstObjectByType<NetworkSync>(FindObjectsInactive.Include);
        var scoreManager = Object.FindFirstObjectByType<ScoreManager>(FindObjectsInactive.Include);
        var carSpawner = Object.FindFirstObjectByType<CarSpawner>(FindObjectsInactive.Include);
        var lapTracker = Object.FindFirstObjectByType<LapTracker>(FindObjectsInactive.Include);
        var eventManager = Object.FindFirstObjectByType<EventManager>(FindObjectsInactive.Include);
        var sessionManager = Object.FindFirstObjectByType<SessionManager>(FindObjectsInactive.Include);
        var weatherEffect = Object.FindFirstObjectByType<WeatherEffect>(FindObjectsInactive.Include);
        var cameraManager = Object.FindFirstObjectByType<CameraManager>(FindObjectsInactive.Include);
        var waypointPath = Object.FindFirstObjectByType<WaypointPath>(FindObjectsInactive.Include);
        var configManager = FindOrCreate<SurveyConfigManager>(raceManager.gameObject, "SurveyConfigManager");

        // UI singletons
        var raceUI = Object.FindFirstObjectByType<RaceUI>(FindObjectsInactive.Include);
        var setupScreen = Object.FindFirstObjectByType<SetupScreen>(FindObjectsInactive.Include);
        var joinScreen = Object.FindFirstObjectByType<JoinScreen>(FindObjectsInactive.Include);
        var leaderboard = Object.FindFirstObjectByType<LeaderboardPanel>(FindObjectsInactive.Include);
        var eventPanel = Object.FindFirstObjectByType<EventPanel>(FindObjectsInactive.Include);
        var controlPanel = Object.FindFirstObjectByType<RaceControlPanel>(FindObjectsInactive.Include);

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
            EditorUtility.SetDirty(networkSync);
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
            EditorUtility.SetDirty(raceUI);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: HUD panel data sources
        // The panels are visible via RaceUI, but stay empty unless their own manager
        // reference is set — WireAll previously wired RaceUI.Leaderboard/Events but not
        // the ScoreManager/EventManager the panels read from.
        // ═══════════════════════════════════════════════════════
        if (leaderboard != null)
        {
            Wire(ref leaderboard.ScoreManager, scoreManager, "LeaderboardPanel.ScoreManager");
            EditorUtility.SetDirty(leaderboard);
        }
        if (eventPanel != null)
        {
            Wire(ref eventPanel.EventManager, eventManager, "EventPanel.EventManager");
            EditorUtility.SetDirty(eventPanel);
        }
        // RaceControlPanel.RaceManager is the professor's Pause/Save/Export hook. Its
        // cross-object link to the RaceManager singleton has dropped to {fileID: 0} in
        // serialization before (button refs survive, this one doesn't), silently killing
        // every control — every handler early-returns on a null RaceManager. Re-wire here
        // so "Wire All References" repairs it, mirroring RaceUI.ResolveMissingReferences.
        if (controlPanel != null)
        {
            Wire(ref controlPanel.RaceManager, raceManager, "RaceControlPanel.RaceManager");
            Wire(ref controlPanel.CameraManager, cameraManager, "RaceControlPanel.CameraManager");

            // Scenes built before the Auto Cam feature only have Pause/Save/Export, so the button
            // reference is null and the professor has no on-screen toggle (only the 'C' hotkey).
            // Create + wire it here so "Wire All References" upgrades an existing scene, matching
            // the layout TrackSetupEditor uses for freshly-created panels.
            if (controlPanel.AutoCamButton == null)
            {
                var panelRt = controlPanel.GetComponent<RectTransform>();
                if (panelRt != null && (panelRt.offsetMax.x - panelRt.offsetMin.x) < 500f)
                {
                    // Widen the panel so the fourth button sits on its background instead of past it.
                    panelRt.offsetMin = new Vector2(-250f, panelRt.offsetMin.y);
                    panelRt.offsetMax = new Vector2(250f, panelRt.offsetMax.y);
                }

                controlPanel.AutoCamButton = CreateButton(controlPanel.transform, "AutoCamBtn", "Auto Cam",
                    new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(370, -17), new Vector2(480, 17));
                controlPanel.AutoCamLabel = controlPanel.AutoCamButton.GetComponentInChildren<Text>();
                createdCount++;
            }

            EditorUtility.SetDirty(controlPanel);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: SetupScreen
        // ═══════════════════════════════════════════════════════
        if (setupScreen != null)
        {
            Wire(ref setupScreen.RaceManager, raceManager, "SetupScreen.RaceManager");
            Wire(ref setupScreen.NetworkManager, networkManager, "SetupScreen.NetworkManager");
            Wire(ref setupScreen.SurveyConfigManager, configManager, "SetupScreen.SurveyConfigManager");

            // Create Web App Import UI if missing
            if (setupScreen.ImportJsonButton == null)
            {
                setupScreen.ImportJsonButton = CreateButton(
                    setupScreen.transform, "ImportJsonBtn", "Import JSON",
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-150, -75), new Vector2(-10, -40));
                createdCount++;
            }

            if (setupScreen.ImportPanel == null)
            {
                var panelObj = new GameObject("ImportPanel");
                panelObj.transform.SetParent(setupScreen.transform, false);
                var panelImg = panelObj.AddComponent<Image>();
                panelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
                var panelRt = panelObj.GetComponent<RectTransform>();
                panelRt.anchorMin = new Vector2(0.1f, 0.2f);
                panelRt.anchorMax = new Vector2(0.9f, 0.8f);
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
                setupScreen.ImportPanel = panelObj;
                panelObj.SetActive(false);
                createdCount++;

                // Title text inside panel
                CreateText(panelObj.transform, "ImportTitle", "Paste Web App JSON Export",
                    new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(10, -40), new Vector2(-10, -10));

                // InputField inside panel
                var inputObj = new GameObject("JsonInputField");
                inputObj.transform.SetParent(panelObj.transform, false);
                var inputImg = inputObj.AddComponent<Image>();
                inputImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                var inputRt = inputObj.GetComponent<RectTransform>();
                inputRt.anchorMin = new Vector2(0.05f, 0.25f);
                inputRt.anchorMax = new Vector2(0.95f, 0.85f);
                inputRt.offsetMin = Vector2.zero;
                inputRt.offsetMax = Vector2.zero;

                var placeholderObj = new GameObject("Placeholder");
                placeholderObj.transform.SetParent(inputObj.transform, false);
                var placeholder = placeholderObj.AddComponent<Text>();
                placeholder.text = "Paste JSON here...";
                placeholder.fontSize = 14;
                placeholder.fontStyle = FontStyle.Italic;
                placeholder.color = new Color(0.5f, 0.5f, 0.5f);
                placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var phRt = placeholderObj.GetComponent<RectTransform>();
                phRt.anchorMin = Vector2.zero;
                phRt.anchorMax = Vector2.one;
                phRt.offsetMin = new Vector2(5, 2);
                phRt.offsetMax = new Vector2(-5, -2);

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(inputObj.transform, false);
                var inputText = textObj.AddComponent<Text>();
                inputText.fontSize = 14;
                inputText.color = Color.white;
                inputText.supportRichText = false;
                inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var txtRt = textObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = new Vector2(5, 2);
                txtRt.offsetMax = new Vector2(-5, -2);

                var inputField = inputObj.AddComponent<InputField>();
                inputField.textComponent = inputText;
                inputField.placeholder = placeholder;
                inputField.lineType = InputField.LineType.MultiLineNewline;

                setupScreen.JsonInputField = inputField;
                createdCount++;

                // Confirm button inside panel
                setupScreen.ConfirmImportButton = CreateButton(
                    panelObj.transform, "ConfirmImportBtn", "Confirm Import",
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-80, 10), new Vector2(80, 45));
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

            // Create TeamNameInput if missing
            if (joinScreen.TeamNameInput == null)
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                var teamObj = new GameObject("TeamNameInput");
                teamObj.transform.SetParent(joinScreen.transform, false);
                var teamImg = teamObj.AddComponent<Image>();
                teamImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
                var teamRt = teamObj.GetComponent<RectTransform>();
                teamRt.anchorMin = new Vector2(0.5f, 0.5f);
                teamRt.anchorMax = new Vector2(0.5f, 0.5f);
                teamRt.pivot = new Vector2(0.5f, 0.5f);
                teamRt.sizeDelta = new Vector2(200, 35);
                teamRt.anchoredPosition = new Vector2(0, -30);

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(teamObj.transform, false);
                var textComp = textObj.AddComponent<Text>();
                textComp.font = font;
                textComp.fontSize = 18;
                textComp.alignment = TextAnchor.MiddleCenter;
                textComp.color = Color.white;
                var txtRt = textObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = new Vector2(5, 2);
                txtRt.offsetMax = new Vector2(-5, -2);

                var phObj = new GameObject("Placeholder");
                phObj.transform.SetParent(teamObj.transform, false);
                var phText = phObj.AddComponent<Text>();
                phText.font = font;
                phText.fontSize = 18;
                phText.fontStyle = FontStyle.Italic;
                phText.color = new Color(1, 1, 1, 0.3f);
                phText.alignment = TextAnchor.MiddleCenter;
                phText.text = "TEAM NAME";
                var phRt = phObj.GetComponent<RectTransform>();
                phRt.anchorMin = Vector2.zero;
                phRt.anchorMax = Vector2.one;
                phRt.offsetMin = new Vector2(5, 2);
                phRt.offsetMax = new Vector2(-5, -2);

                var inputField = teamObj.AddComponent<InputField>();
                inputField.textComponent = textComp;
                inputField.placeholder = phText;
                inputField.characterLimit = 30;
                joinScreen.TeamNameInput = inputField;
                createdCount++;
                Debug.Log("[WireAll] Created TeamNameInput on JoinScreen");
            }

            EditorUtility.SetDirty(joinScreen);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: Host / Student launch bootstraps (URL role binding)
        // ═══════════════════════════════════════════════════════
        // Both read the WebGL launch URL (role=host / role=play) on Start and live on the
        // NetworkManager GameObject. StudentJoinBootstrap in particular MUST exist, or the
        // student link never hard-locks the client to Student — regenerating the scene without
        // it would silently reopen that security hole. Idempotent via FindOrCreate.
        if (networkManager != null && raceUI != null)
        {
            var hostBootstrap = FindOrCreate<HostLaunchBootstrap>(networkManager.gameObject, "HostLaunchBootstrap");
            Wire(ref hostBootstrap.NetworkManager, networkManager, "HostLaunchBootstrap.NetworkManager");
            Wire(ref hostBootstrap.RaceUI, raceUI, "HostLaunchBootstrap.RaceUI");
            EditorUtility.SetDirty(hostBootstrap);

            var studentBootstrap = FindOrCreate<StudentJoinBootstrap>(networkManager.gameObject, "StudentJoinBootstrap");
            Wire(ref studentBootstrap.NetworkManager, networkManager, "StudentJoinBootstrap.NetworkManager");
            Wire(ref studentBootstrap.RaceUI, raceUI, "StudentJoinBootstrap.RaceUI");
            EditorUtility.SetDirty(studentBootstrap);
        }

        // ═══════════════════════════════════════════════════════
        // WIRE: CameraManager + SpectatorCamera + CarLabelSpawner
        // ═══════════════════════════════════════════════════════
        if (cameraManager != null)
        {
            var freeCam = Object.FindFirstObjectByType<RaceCameraController>(FindObjectsInactive.Include);
            var spectatorCam = Object.FindFirstObjectByType<SpectatorCamera>(FindObjectsInactive.Include);
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
            // Wire SpectatorCamera.ScoreManager
            if (spectatorCam != null)
            {
                Wire(ref spectatorCam.ScoreManager, scoreManager, "SpectatorCamera.ScoreManager");
                EditorUtility.SetDirty(spectatorCam);
            }
            EditorUtility.SetDirty(cameraManager);
        }

        // CarLabelSpawner
        var carLabelSpawner = Object.FindFirstObjectByType<CarLabelSpawner>(FindObjectsInactive.Include);
        if (carLabelSpawner != null)
        {
            Wire(ref carLabelSpawner.RaceManager, raceManager, "CarLabelSpawner.RaceManager");
            EditorUtility.SetDirty(carLabelSpawner);
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
        var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
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
