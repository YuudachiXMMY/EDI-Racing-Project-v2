using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Auto-wires Phase 4 camera and UI systems at runtime when scene objects
/// are not yet configured. Attach this to the RaceManager GameObject.
/// Safe to remove once the scene is fully set up in the Editor.
/// </summary>
public class RuntimeSetup : MonoBehaviour
{
    private RaceManager raceManager;
    private CameraManager cameraManager;
    private Canvas screenCanvas;
    private ScoreManager scoreManager;
    private EventManager eventManager;

    private Text leaderboardText;
    private Text eventLogText;
    private Text controlStatusText;
    private Text modeLabel;
    private Button pauseBtn;
    private bool isPaused;

    private float leaderboardTimer;

    private void Awake()
    {
        raceManager = GetComponent<RaceManager>();
        if (raceManager == null)
            raceManager = FindFirstObjectByType<RaceManager>();
        if (raceManager == null) return;

        scoreManager = raceManager.ScoreManager;
        eventManager = raceManager.EventManager;

        SetupCamera();
        SetupEventSystem();
        SetupUI();
        SetupCarLabels();

        raceManager.OnStateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (raceManager != null)
            raceManager.OnStateChanged -= OnStateChanged;
    }

    // ── Camera ─────────────────────────────────────────────

    private void SetupCamera()
    {
        if (FindFirstObjectByType<CameraManager>() != null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Add RaceCameraController (free-look WASD)
        var freeCam = cam.gameObject.GetComponent<RaceCameraController>();
        if (freeCam == null)
            freeCam = cam.gameObject.AddComponent<RaceCameraController>();

        // Add SpectatorCamera
        var spectatorCam = cam.gameObject.GetComponent<SpectatorCamera>();
        if (spectatorCam == null)
        {
            spectatorCam = cam.gameObject.AddComponent<SpectatorCamera>();
            spectatorCam.ScoreManager = scoreManager;
            spectatorCam.enabled = false;
        }

        // Create CameraManager
        GameObject cmObj = new GameObject("CameraManager");
        cameraManager = cmObj.AddComponent<CameraManager>();
        cameraManager.FreeCamera = freeCam;
        cameraManager.SpectatorCam = spectatorCam;

        // Create default fixed camera points around the track
        CreateDefaultFixedPoints();
    }

    private void CreateDefaultFixedPoints()
    {
        // Use existing waypoints or spawn point as reference for fixed cameras
        var spawner = raceManager.CarSpawner;
        if (spawner == null || spawner.SpawnPoint == null) return;

        var waypointPath = spawner.WaypointPath;
        if (waypointPath == null || waypointPath.Waypoints == null) return;

        Transform[] wps = waypointPath.Waypoints;
        int count = Mathf.Min(9, wps.Length);
        int step = Mathf.Max(1, wps.Length / count);

        for (int i = 0; i < count; i++)
        {
            int wpIndex = (i * step) % wps.Length;
            Transform wp = wps[wpIndex];
            if (wp == null) continue;

            GameObject fpObj = new GameObject($"FixedCam_F{i + 1}");
            fpObj.transform.position = wp.position + Vector3.up * 12f + Vector3.right * 8f;
            fpObj.transform.LookAt(wp.position);

            var fp = fpObj.AddComponent<FixedCameraPoint>();
            fp.PointIndex = i;
        }
    }

    // ── EventSystem ────────────────────────────────────────

    private void SetupEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<EventSystem>();
        esObj.AddComponent<InputSystemUIInputModule>();
    }

    // ── UI ─────────────────────────────────────────────────

    private void SetupUI()
    {
        var existingUI = FindFirstObjectByType<RaceUI>();
        if (existingUI != null && existingUI.Setup != null) return;

        // Screen-space Canvas
        GameObject canvasObj = new GameObject("RuntimeUI");
        screenCanvas = canvasObj.AddComponent<Canvas>();
        screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        screenCanvas.sortingOrder = 50;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        BuildLeaderboard(canvasObj.transform);
        BuildControlPanel(canvasObj.transform);
        BuildEventLog(canvasObj.transform);
        BuildModeLabel(canvasObj.transform);
        BuildFinishPanel(canvasObj.transform);
    }

    private void BuildLeaderboard(Transform parent)
    {
        // Semi-transparent background panel, top-left
        GameObject panel = CreatePanel(parent, "LeaderboardPanel",
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(10, -10), new Vector2(280, 400));

        leaderboardText = CreateText(panel.transform, "LeaderboardText",
            "Leaderboard", 16, TextAnchor.UpperLeft,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        panel.SetActive(false); // shown when racing
    }

    private void BuildControlPanel(Transform parent)
    {
        // Bottom-center control bar
        GameObject panel = CreatePanel(parent, "ControlPanel",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-200, 10), new Vector2(200, 50));

        // Pause button
        pauseBtn = CreateButton(panel.transform, "PauseBtn", "Pause",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(10, -5), new Vector2(110, 35));
        pauseBtn.onClick.AddListener(TogglePause);

        // Save button
        var saveBtn = CreateButton(panel.transform, "SaveBtn", "Save",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(120, -5), new Vector2(230, 35));
        saveBtn.onClick.AddListener(() =>
        {
            raceManager.SaveCurrentSession();
            ShowStatus("Session saved!");
        });

        // Export button
        var exportBtn = CreateButton(panel.transform, "ExportBtn", "Export",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(240, -5), new Vector2(350, 35));
        exportBtn.onClick.AddListener(() =>
        {
            raceManager.ExportCurrentResults();
            ShowStatus("Results exported!");
        });

        // Status text
        controlStatusText = CreateText(panel.transform, "StatusText",
            "", 14, TextAnchor.MiddleRight,
            new Vector2(1, 0), new Vector2(1, 1),
            new Vector2(-150, 0), new Vector2(-5, 0));

        panel.SetActive(false);
    }

    private void BuildEventLog(Transform parent)
    {
        // Top-right event log
        GameObject panel = CreatePanel(parent, "EventLogPanel",
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-280, -10), new Vector2(-10, 200));

        eventLogText = CreateText(panel.transform, "EventLogText",
            "Events", 14, TextAnchor.UpperLeft,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        if (eventManager != null)
            eventManager.OnEventTriggered += OnEventTriggered;

        panel.SetActive(false);
    }

    private void BuildModeLabel(Transform parent)
    {
        // Top-center camera mode indicator
        modeLabel = CreateText(parent, "ModeLabel",
            "", 18, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(-100, -40), new Vector2(100, -10));
        modeLabel.color = Color.yellow;
    }

    private RaceFinishPanel finishPanel;

    private void BuildFinishPanel(Transform parent)
    {
        GameObject obj = new GameObject("FinishPanel");
        obj.transform.SetParent(parent, false);
        finishPanel = obj.AddComponent<RaceFinishPanel>();
        finishPanel.RaceManager = raceManager;
        finishPanel.ScoreManager = scoreManager;
        obj.SetActive(false);
    }

    // ── State Management ───────────────────────────────────

    private void OnStateChanged(GameState state)
    {
        if (screenCanvas == null) return;

        bool racing = state == GameState.Racing || state == GameState.Paused;
        Transform root = screenCanvas.transform;

        var lb = root.Find("LeaderboardPanel");
        if (lb != null) lb.gameObject.SetActive(racing || state == GameState.Finished);

        var cp = root.Find("ControlPanel");
        if (cp != null) cp.gameObject.SetActive(racing);

        var ep = root.Find("EventLogPanel");
        if (ep != null) ep.gameObject.SetActive(racing || state == GameState.Finished);

        // Finish panel: keep active during Racing (event subscription via OnEnable),
        // overlay builds itself only when OnRaceFinished fires
        if (finishPanel != null)
        {
            if (state == GameState.Racing)
            {
                finishPanel.Hide();
                finishPanel.gameObject.SetActive(true);
            }
            else if (state == GameState.Setup)
            {
                finishPanel.Hide();
                finishPanel.gameObject.SetActive(false);
            }
        }
    }

    // ── Update Loop ────────────────────────────────────────

    private void Update()
    {
        leaderboardTimer += Time.unscaledDeltaTime;
        if (leaderboardTimer >= 0.5f)
        {
            leaderboardTimer = 0f;
            RefreshLeaderboard();
        }

        UpdateModeLabel();
    }

    private void RefreshLeaderboard()
    {
        if (leaderboardText == null || scoreManager == null) return;
        if (raceManager.CurrentState == GameState.Setup) return;

        List<CarIdentity> ranked = scoreManager.GetRankedCars();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>Leaderboard</b>");
        sb.AppendLine("─────────────────");

        int count = Mathf.Min(ranked.Count, 15);
        for (int i = 0; i < count; i++)
        {
            var car = ranked[i];
            string medal = i == 0 ? "<color=yellow>" : i == 1 ? "<color=#C0C0C0>" : i == 2 ? "<color=#CD7F32>" : "<color=white>";
            sb.AppendLine($"{medal}{i + 1}. [{car.CurrentLap}] {car.TeamName}</color>");
        }

        leaderboardText.text = sb.ToString();
    }

    private void UpdateModeLabel()
    {
        if (modeLabel == null) return;
        if (cameraManager == null)
            cameraManager = FindFirstObjectByType<CameraManager>();
        if (cameraManager == null) return;

        modeLabel.text = $"Camera: {cameraManager.CurrentMode}  |  F1-F9: Fixed  |  ESC: Free";
    }

    // ── Event Handling ─────────────────────────────────────

    private readonly List<string> eventLogEntries = new List<string>();

    private void OnEventTriggered(RaceEventConfig config, int affectedCount)
    {
        string entry = $"[{Time.time:F0}s] {config.DisplayName} ({affectedCount} cars)";
        eventLogEntries.Add(entry);

        if (eventLogEntries.Count > 10)
            eventLogEntries.RemoveAt(0);

        if (eventLogText != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Events</b>");
            sb.AppendLine("─────────────────");
            foreach (var e in eventLogEntries)
                sb.AppendLine(e);
            eventLogText.text = sb.ToString();
        }
    }

    private void TogglePause()
    {
        if (raceManager == null) return;

        if (isPaused)
        {
            raceManager.ResumeRace();
            isPaused = false;
            if (pauseBtn != null)
                pauseBtn.GetComponentInChildren<Text>().text = "Pause";
        }
        else
        {
            raceManager.PauseRace();
            isPaused = true;
            if (pauseBtn != null)
                pauseBtn.GetComponentInChildren<Text>().text = "Resume";
        }
    }

    private void ShowStatus(string message)
    {
        if (controlStatusText != null)
            controlStatusText.text = message;
    }

    // ── Car Labels ─────────────────────────────────────────

    private void SetupCarLabels()
    {
        if (FindFirstObjectByType<CarLabelSpawner>() != null) return;

        GameObject obj = new GameObject("CarLabelSpawner");
        var spawner = obj.AddComponent<CarLabelSpawner>();
        spawner.RaceManager = raceManager;
    }

    // ── UI Factory Helpers ─────────────────────────────────

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.6f);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return obj;
    }

    private Text CreateText(Transform parent, string name, string content,
        int fontSize, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Text text = obj.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return text;
    }

    private Button CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        Button btn = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
        btn.colors = colors;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        // Button label
        CreateText(obj.transform, "Label", label, 16, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return btn;
    }
}
