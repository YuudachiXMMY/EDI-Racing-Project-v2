using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-race setup overlay. Shown during GameState.Setup.
///
/// The runtime menu is collapsed to a single "Start Game" button: when the game is launched
/// from the web-app gateway (#role=host&token=…), the survey's responses are pushed in over
/// the WebSocket (survey_import) and cached; pressing Start Game then starts the race with that
/// token data. All other setup buttons (Load Session, Host, Survey Builder, manual JSON import,
/// Push Config) are hidden at runtime but their fields and wiring are kept intact for the Editor.
/// </summary>
public class SetupScreen : MonoBehaviour
{
    [Header("References")]
    public RaceManager RaceManager;

    [Header("UI Elements")]
    public Button StartDefaultButton;
    public Button LoadSessionButton;
    public Text InfoText;

    [Header("Network (Optional)")]
    public NetworkManager NetworkManager;
    public Button HostButton;
    public Text RoomCodeText;
    public Text StudentCountText;
    public Text StudentLinkText;
    public Button CopyLinkButton;
    private string currentStudentLink = "";

    [Header("Survey Config (Optional)")]
    public SurveyConfigManager SurveyConfigManager;
    public Text ActiveConfigText;
    public Button NewSurveyButton;
    public Button LoadConfigButton;
    public Button TemplateButton;
    public Button StartWithSurveyButton;

    [Header("Web App Import (Optional)")]
    public Button ImportJsonButton;
    public InputField JsonInputField;
    public Button ConfirmImportButton;
    public GameObject ImportPanel;

    [Header("Config Sync (Optional)")]
    public Button PushConfigButton;

    [Header("Web Response Sync (Optional)")]
    public Text WebResponseCountText;

    // Web-app token data: survey responses pushed in via the WebSocket (survey_import) after a
    // gateway host launch. Cached here instead of auto-starting so the professor starts the race
    // by pressing the single Start Game button. See StartGame().
    private List<CarData> pendingCars;
    private SavedEventRule[] pendingRules;

    // True when the page was opened via the web gateway host launch (#role=host&token=…), so the
    // race data arrives asynchronously and Start Game waits for it instead of using the default CSV.
    private bool expectingTokenData;

    private void Start()
    {
        if (RaceManager == null)
        {
            RaceManager = FindFirstObjectByType<RaceManager>();
            if (RaceManager == null)
                Debug.LogError("[SetupScreen] No RaceManager found in scene!");
        }

        if (StartDefaultButton != null)
            StartDefaultButton.onClick.AddListener(StartGame);
        if (LoadSessionButton != null)
            LoadSessionButton.onClick.AddListener(LoadLatestSession);
        if (HostButton != null)
            HostButton.onClick.AddListener(HostRoom);

        // Web App import buttons
        if (ImportJsonButton != null)
            ImportJsonButton.onClick.AddListener(ShowImportPanel);
        if (ConfirmImportButton != null)
            ConfirmImportButton.onClick.AddListener(OnConfirmImport);
        if (ImportPanel != null)
            ImportPanel.SetActive(false);

        // Hide network UI if no NetworkManager
        bool hasNetwork = NetworkManager != null;
        if (HostButton != null) HostButton.gameObject.SetActive(hasNetwork);
        if (RoomCodeText != null) RoomCodeText.gameObject.SetActive(false);
        if (StudentCountText != null) StudentCountText.gameObject.SetActive(false);
        if (StudentLinkText != null) StudentLinkText.gameObject.SetActive(false);
        if (CopyLinkButton != null)
        {
            CopyLinkButton.gameObject.SetActive(false);
            CopyLinkButton.onClick.AddListener(OnCopyStudentLink);
        }

        // Config sync button
        if (PushConfigButton != null)
        {
            PushConfigButton.gameObject.SetActive(false);
            PushConfigButton.onClick.AddListener(OnPushConfig);
        }

        RefreshActiveConfigDisplay();

        ConfigureSingleButtonMenu();
    }

    // Collapse the setup menu to a single "Start Game" button (StartDefaultButton, reused). Every
    // other button is hidden at runtime; the fields and their listener wiring above are kept intact
    // so the Editor auto-setup and manual flows still work.
    private void ConfigureSingleButtonMenu()
    {
        // A web gateway host launch carries "#role=host&token=…". In that flow the race data is
        // pushed in asynchronously (survey_import), so wait for it rather than the default CSV.
        var launch = HostLaunchParams.ParseHash(WebSocketBridge.GetPageUrl());
        expectingTokenData = launch.TryGetValue("role", out var role) && role == "host";

        HideButton(LoadSessionButton);
        HideButton(HostButton);
        HideButton(CopyLinkButton);
        HideButton(ImportJsonButton);
        HideButton(ConfirmImportButton);
        HideButton(PushConfigButton);
        HideButton(NewSurveyButton);
        HideButton(LoadConfigButton);
        HideButton(TemplateButton);
        HideButton(StartWithSurveyButton);

        if (StartDefaultButton != null)
        {
            StartDefaultButton.gameObject.SetActive(true);
            var label = StartDefaultButton.GetComponentInChildren<Text>();
            if (label != null) label.text = "Start Game";
            // Wait for token data before enabling in the web-launch flow; always ready otherwise.
            StartDefaultButton.interactable = !expectingTokenData;
        }

        if (InfoText != null)
            InfoText.text = expectingTokenData
                ? "Waiting for data from web app..."
                : "Ready to start race.";
    }

    private static void HideButton(Button button)
    {
        if (button != null) button.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnRoomCreated += OnRoomCreated;
            NetworkManager.OnStudentCountChanged += OnStudentCountChanged;
            NetworkManager.OnConnectionError += OnNetworkError;
            NetworkManager.OnMessageReceived += OnNetworkMessage;
            NetworkManager.OnReconnecting += OnReconnecting;
            NetworkManager.OnReconnected += OnNetworkReconnected;
            NetworkManager.OnReconnectFailed += OnReconnectFailed;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnRoomCreated -= OnRoomCreated;
            NetworkManager.OnStudentCountChanged -= OnStudentCountChanged;
            NetworkManager.OnConnectionError -= OnNetworkError;
            NetworkManager.OnMessageReceived -= OnNetworkMessage;
            NetworkManager.OnReconnecting -= OnReconnecting;
            NetworkManager.OnReconnected -= OnNetworkReconnected;
            NetworkManager.OnReconnectFailed -= OnReconnectFailed;
        }
    }

    public void RefreshActiveConfigDisplay()
    {
        if (ActiveConfigText == null) return;

        if (SurveyConfigManager == null || SurveyConfigManager.ActiveConfig == null)
        {
            ActiveConfigText.text = "No active config";
            return;
        }

        var config = SurveyConfigManager.ActiveConfig;
        int qCount = config.Questions != null ? config.Questions.Length : 0;
        int rCount = config.Rules != null ? config.Rules.Length : 0;
        ActiveConfigText.text = $"Active: {config.ConfigName} ({qCount} questions, {rCount} rules)";
    }

    // --- Network ---

    private void HostRoom()
    {
        if (NetworkManager == null) return;
        if (InfoText != null) InfoText.text = "Connecting...";
        if (HostButton != null) HostButton.interactable = false;
        NetworkManager.CreateRoom();
    }

    private void OnRoomCreated(string roomCode)
    {
        if (RoomCodeText != null)
        {
            RoomCodeText.gameObject.SetActive(true);
            RoomCodeText.text = $"Room: {roomCode}";
        }
        if (InfoText != null) InfoText.text = "Room created. Start when ready.";

        // Show push-config button if we have an active config
        bool canPush = SurveyConfigManager != null && SurveyConfigManager.ActiveConfig != null;
        if (PushConfigButton != null) PushConfigButton.gameObject.SetActive(canPush);

        ShowStudentLink(roomCode);
    }

    // Build and surface the shareable student join link (room code only, no host token) so the
    // professor can copy it once the room exists. Empty origin (Editor) → keep the UI hidden.
    private void ShowStudentLink(string roomCode)
    {
        currentStudentLink = StudentLinkBuilder.BuildJoinLink(WebSocketBridge.GetPageOrigin(), roomCode);
        if (string.IsNullOrEmpty(currentStudentLink)) return;
        if (StudentLinkText != null)
        {
            StudentLinkText.gameObject.SetActive(true);
            StudentLinkText.text = $"学生链接: {currentStudentLink}";
        }
        if (CopyLinkButton != null) CopyLinkButton.gameObject.SetActive(true);
    }

    private void OnCopyStudentLink()
    {
        if (!string.IsNullOrEmpty(currentStudentLink))
            WebSocketBridge.CopyToClipboard(currentStudentLink);
    }

    private void OnStudentCountChanged(int count)
    {
        if (StudentCountText != null)
        {
            StudentCountText.gameObject.SetActive(true);
            StudentCountText.text = $"{count} student(s) connected";
        }
    }

    private void OnNetworkError(string error)
    {
        if (InfoText != null) InfoText.text = $"Network error: {error}";
        if (HostButton != null) HostButton.interactable = true;
    }

    private void OnReconnecting(int attempt, float delay)
    {
        if (InfoText != null) InfoText.text = $"Reconnecting... ({attempt}/{NetworkManager.MaxAttempts})";
        if (HostButton != null) HostButton.interactable = false;
    }

    private void OnNetworkReconnected()
    {
        if (InfoText != null) InfoText.text = "Reconnected! Room restored.";
        if (RoomCodeText != null && NetworkManager.RoomCode != null)
        {
            RoomCodeText.gameObject.SetActive(true);
            RoomCodeText.text = $"Room: {NetworkManager.RoomCode}";
        }
        if (StudentCountText != null)
        {
            StudentCountText.gameObject.SetActive(true);
            StudentCountText.text = $"{NetworkManager.StudentCount} student(s) connected";
        }
        if (NetworkManager.RoomCode != null) ShowStudentLink(NetworkManager.RoomCode);
    }

    private void OnReconnectFailed()
    {
        if (InfoText != null) InfoText.text = "Connection lost. Room may have expired.";
        if (HostButton != null) HostButton.interactable = true;
        if (RoomCodeText != null) RoomCodeText.gameObject.SetActive(false);
        if (StudentCountText != null) StudentCountText.gameObject.SetActive(false);
        if (StudentLinkText != null) StudentLinkText.gameObject.SetActive(false);
        if (CopyLinkButton != null) CopyLinkButton.gameObject.SetActive(false);
    }

    // --- Web App Direct Send ---

    private void OnNetworkMessage(string json)
    {
        if (NetworkManager == null || !NetworkManager.IsHost) return;
        if (RaceManager == null || RaceManager.CurrentState != GameState.Setup) return;

        var baseMsg = JsonUtility.FromJson<NetworkMessage>(json);

        if (baseMsg.type == "student_joined")
        {
            var joinMsg = JsonUtility.FromJson<StudentJoinedMessage>(json);
            if (InfoText != null)
                InfoText.text = $"'{joinMsg.teamName}' joined ({joinMsg.count} student(s))";
            return;
        }

        if (baseMsg.type == "student_list")
        {
            var listMsg = JsonUtility.FromJson<StudentListMessage>(json);
            if (StudentCountText != null)
            {
                StudentCountText.gameObject.SetActive(true);
                string names = listMsg.teamNames != null && listMsg.teamNames.Length > 0
                    ? string.Join(", ", listMsg.teamNames)
                    : "(none named)";
                StudentCountText.text = $"{listMsg.count} student(s): {names}";
            }
            return;
        }

        if (baseMsg.type == "new_web_response")
        {
            var webMsg = JsonUtility.FromJson<NewWebResponseMessage>(json);
            if (WebResponseCountText != null)
            {
                WebResponseCountText.gameObject.SetActive(true);
                WebResponseCountText.text = $"Web responses: {webMsg.responseCount} (latest: {webMsg.teamName})";
            }
            return;
        }

        if (baseMsg.type == "config_import")
        {
            var configMsg = JsonUtility.FromJson<ConfigImportMessage>(json);
            if (string.IsNullOrEmpty(configMsg.configJson))
            {
                if (InfoText != null) InfoText.text = "Received empty config from web app.";
                return;
            }

            SurveyConfig config;
            try
            {
                config = JsonUtility.FromJson<SurveyConfig>(configMsg.configJson);
            }
            catch (System.Exception e)
            {
                if (InfoText != null) InfoText.text = $"Config import error: {e.Message}";
                return;
            }

            if (SurveyConfigManager != null)
            {
                SurveyConfigManager.SetActiveConfig(config);
                SurveyConfigManager.SaveConfig(config);
                RefreshActiveConfigDisplay();
            }

            int qCount = config.Questions != null ? config.Questions.Length : 0;
            int mCount = config.Mappings != null ? config.Mappings.Length : 0;
            int rCount = config.Rules != null ? config.Rules.Length : 0;

            Debug.Log($"[SetupScreen] Imported config from web app: {config.ConfigName} ({qCount}Q, {mCount}M, {rCount}R)");
            if (InfoText != null) InfoText.text = $"Config imported: {config.ConfigName} ({qCount} questions, {mCount} mappings, {rCount} rules)";

            return;
        }

        if (baseMsg.type == "config_sync_ack")
        {
            var ackMsg = JsonUtility.FromJson<ConfigSyncAckMessage>(json);
            if (ackMsg.success)
            {
                if (InfoText != null && ackMsg.direction == "export")
                    InfoText.text = "Config sent to web app successfully.";
            }
            else
            {
                if (InfoText != null) InfoText.text = $"Config sync error: {ackMsg.error}";
            }
            return;
        }

        if (baseMsg.type != "survey_import") return;

        var msg = JsonUtility.FromJson<SurveyImportMessage>(json);
        if (string.IsNullOrEmpty(msg.exportJson))
        {
            if (InfoText != null) InfoText.text = "Received empty data from web app.";
            return;
        }

        var result = JsonImporter.Parse(msg.exportJson);
        if (!result.Success)
        {
            if (InfoText != null) InfoText.text = $"Web app import error: {result.Error}";
            return;
        }

        if (result.Cars.Count == 0)
        {
            if (InfoText != null) InfoText.text = "Web app sent 0 cars. Export with responses first.";
            return;
        }

        Debug.Log($"[SetupScreen] Received {result.Cars.Count} cars, {result.EventRules.Length} rules from web app (token data)");

        // Cache the token data and let the professor start the race by pressing Start Game,
        // rather than auto-starting the moment the data arrives.
        pendingCars = result.Cars;
        pendingRules = result.EventRules;
        if (InfoText != null)
            InfoText.text = $"Data ready: {result.Cars.Count} cars, {result.EventRules.Length} rules. Press Start Game.";
        if (StartDefaultButton != null) StartDefaultButton.interactable = true;
    }

    // --- Config Sync ---

    private void OnPushConfig()
    {
        if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
        if (SurveyConfigManager == null || SurveyConfigManager.ActiveConfig == null)
        {
            if (InfoText != null) InfoText.text = "No active config to push.";
            return;
        }

        var config = SurveyConfigManager.ActiveConfig;
        string configJson = JsonUtility.ToJson(config);

        var msg = new ConfigExportMessage
        {
            configName = config.ConfigName,
            configJson = configJson
        };

        NetworkManager.Send(JsonUtility.ToJson(msg));
        if (InfoText != null) InfoText.text = $"Config '{config.ConfigName}' sent to web app.";
        Debug.Log($"[SetupScreen] Pushed config to web app: {config.ConfigName}");
    }

    // --- Web App Import ---

    private void ShowImportPanel()
    {
        if (ImportPanel != null) ImportPanel.SetActive(true);
        if (JsonInputField != null) JsonInputField.text = "";
    }

    private void OnConfirmImport()
    {
        if (JsonInputField == null || RaceManager == null) return;

        string json = JsonInputField.text;
        if (string.IsNullOrEmpty(json))
        {
            if (InfoText != null) InfoText.text = "Paste JSON data first.";
            return;
        }

        var result = JsonImporter.Parse(json);
        if (!result.Success)
        {
            if (InfoText != null) InfoText.text = $"Import error: {result.Error}";
            return;
        }

        if (result.Cars.Count == 0)
        {
            if (InfoText != null) InfoText.text = "No cars found in JSON. Export with student responses first.";
            return;
        }

        RaceManager.LoadAndStartRaceWithRules(result.Cars, result.EventRules);
        if (ImportPanel != null) ImportPanel.SetActive(false);
        gameObject.SetActive(false);

        Debug.Log($"[SetupScreen] Imported {result.Cars.Count} cars, {result.EventRules.Length} rules from Web App JSON");
    }

    // --- Race Start ---

    // Single "Start Game" button handler. Prefers the web-app token data (survey responses pushed
    // in via WebSocket); if launched via the gateway but that data has not arrived yet, it waits.
    // In the Editor/standalone (no token launch) it starts the default CSV so local testing works.
    private void StartGame()
    {
        if (RaceManager == null)
        {
            Debug.LogError("[SetupScreen] RaceManager is null! Cannot start race.");
            if (InfoText != null) InfoText.text = "Error: RaceManager not found.";
            return;
        }

        if (pendingCars != null && pendingCars.Count > 0)
        {
            RaceManager.LoadAndStartRaceWithRules(pendingCars, pendingRules);
            gameObject.SetActive(false);
            return;
        }

        if (expectingTokenData)
        {
            if (InfoText != null) InfoText.text = "Waiting for data from web app...";
            return;
        }

        StartWithDefaultData();
    }

    private void StartWithDefaultData()
    {
        if (RaceManager == null)
        {
            Debug.LogError("[SetupScreen] RaceManager is null! Cannot start race.");
            if (InfoText != null)
                InfoText.text = "Error: RaceManager not found.";
            return;
        }

        if (RaceManager.DefaultCsvData != null)
        {
            RaceManager.LoadAndStartRace(RaceManager.DefaultCsvData.text);
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[SetupScreen] No DefaultCsvData assigned on RaceManager.");
            if (InfoText != null)
                InfoText.text = "No default CSV data assigned.";
        }
    }

    private void LoadLatestSession()
    {
        if (RaceManager == null || RaceManager.SessionManager == null)
        {
            Debug.LogWarning("[SetupScreen] RaceManager or SessionManager is null.");
            return;
        }

        string path = RaceManager.SessionManager.FindLatestSession();
        if (path != null)
        {
            var session = RaceManager.SessionManager.LoadSession(path);
            if (session != null)
            {
                RaceManager.LoadFromSession(session);
                gameObject.SetActive(false);
            }
        }
        else
        {
            if (InfoText != null)
                InfoText.text = "No saved sessions found.";
        }
    }
}
