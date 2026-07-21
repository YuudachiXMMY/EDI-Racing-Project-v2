using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-race setup overlay. Shown during GameState.Setup.
/// Allows starting race with default CSV data or loading a saved session.
/// Integrates with SurveyBuilderPanel for config creation (Phase 4).
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

    [Header("Survey Builder (Optional)")]
    public SurveyConfigManager SurveyConfigManager;
    public SurveyBuilderPanel BuilderPanel;
    public ConfigManagerPanel ConfigPanel;
    public Button NewSurveyButton;
    public Button LoadConfigButton;
    public Button TemplateButton;
    public Button StartWithSurveyButton;
    public Text ActiveConfigText;

    [Header("Web App Import (Optional)")]
    public Button ImportJsonButton;
    public InputField JsonInputField;
    public Button ConfirmImportButton;
    public GameObject ImportPanel;

    [Header("Survey Collection (Optional)")]
    public SurveyCollector SurveyCollector;
    public Button DistributeSurveyButton;
    public Button StartWithResponsesButton;
    public Text ResponseCountText;

    [Header("Web Response Sync (Optional)")]
    public Text WebResponseCountText;

    private void Start()
    {
        if (RaceManager == null)
        {
            RaceManager = FindFirstObjectByType<RaceManager>();
            if (RaceManager == null)
                Debug.LogError("[SetupScreen] No RaceManager found in scene!");
        }

        if (StartDefaultButton != null)
            StartDefaultButton.onClick.AddListener(StartWithDefaultData);
        if (LoadSessionButton != null)
            LoadSessionButton.onClick.AddListener(LoadLatestSession);
        if (HostButton != null)
            HostButton.onClick.AddListener(HostRoom);

        // Survey builder buttons
        if (NewSurveyButton != null)
            NewSurveyButton.onClick.AddListener(OpenNewSurvey);
        if (LoadConfigButton != null)
            LoadConfigButton.onClick.AddListener(OpenLoadConfig);
        if (TemplateButton != null)
            TemplateButton.onClick.AddListener(OpenTemplates);
        if (StartWithSurveyButton != null)
            StartWithSurveyButton.onClick.AddListener(StartWithSurveyConfig);

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

        // Hide survey UI if no ConfigManager
        bool hasSurvey = SurveyConfigManager != null;
        if (NewSurveyButton != null) NewSurveyButton.gameObject.SetActive(hasSurvey);
        if (LoadConfigButton != null) LoadConfigButton.gameObject.SetActive(hasSurvey);
        if (TemplateButton != null) TemplateButton.gameObject.SetActive(hasSurvey);
        if (StartWithSurveyButton != null) StartWithSurveyButton.gameObject.SetActive(hasSurvey);
        if (ActiveConfigText != null) ActiveConfigText.gameObject.SetActive(hasSurvey);

        // Survey collection buttons
        if (DistributeSurveyButton != null)
        {
            DistributeSurveyButton.gameObject.SetActive(false);
            DistributeSurveyButton.onClick.AddListener(OnDistributeSurvey);
        }
        if (StartWithResponsesButton != null)
        {
            StartWithResponsesButton.gameObject.SetActive(false);
            StartWithResponsesButton.onClick.AddListener(OnStartWithResponses);
        }
        if (ResponseCountText != null) ResponseCountText.gameObject.SetActive(false);

        if (InfoText != null)
            InfoText.text = "Ready to start race.";

        RefreshActiveConfigDisplay();
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

    // --- Survey Builder Integration ---

    private void OpenNewSurvey()
    {
        if (BuilderPanel == null) return;
        gameObject.SetActive(false);
        BuilderPanel.Show(null);
    }

    private void OpenLoadConfig()
    {
        if (ConfigPanel == null) return;
        ConfigPanel.ShowLoadPanel();
    }

    private void OpenTemplates()
    {
        if (ConfigPanel == null) return;
        ConfigPanel.ShowTemplatePanel();
    }

    private void StartWithSurveyConfig()
    {
        if (SurveyConfigManager == null || SurveyConfigManager.ActiveConfig == null)
        {
            if (InfoText != null) InfoText.text = "No active config. Load or create one first.";
            return;
        }

        if (RaceManager == null || RaceManager.EventManager == null)
        {
            if (InfoText != null) InfoText.text = "Error: RaceManager not ready.";
            return;
        }

        // Apply rules from config to EventSchedule
        SurveyConfigManager.ApplyRulesToSchedule(RaceManager.EventManager.Schedule);

        // Start with default CSV data but using custom rules
        if (RaceManager.DefaultCsvData != null)
        {
            RaceManager.LoadAndStartRace(RaceManager.DefaultCsvData.text);
            gameObject.SetActive(false);
        }
        else
        {
            if (InfoText != null) InfoText.text = "No CSV data. Import CSV or wait for survey responses.";
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

        // Show distribute button if we have an active config
        bool canDistribute = SurveyConfigManager != null && SurveyConfigManager.ActiveConfig != null
            && SurveyCollector != null;
        if (DistributeSurveyButton != null) DistributeSurveyButton.gameObject.SetActive(canDistribute);
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
    }

    private void OnReconnectFailed()
    {
        if (InfoText != null) InfoText.text = "Connection lost. Room may have expired.";
        if (HostButton != null) HostButton.interactable = true;
        if (RoomCodeText != null) RoomCodeText.gameObject.SetActive(false);
        if (StudentCountText != null) StudentCountText.gameObject.SetActive(false);
    }

    // --- Survey Collection ---

    private void OnDistributeSurvey()
    {
        if (SurveyCollector == null || SurveyConfigManager == null) return;
        if (SurveyConfigManager.ActiveConfig == null)
        {
            if (InfoText != null) InfoText.text = "No active config. Load or create one first.";
            return;
        }

        SurveyCollector.DistributeSurvey(SurveyConfigManager.ActiveConfig);
        SurveyCollector.OnResponseReceived += OnSurveyResponseReceived;

        if (DistributeSurveyButton != null) DistributeSurveyButton.interactable = false;
        if (ResponseCountText != null)
        {
            ResponseCountText.gameObject.SetActive(true);
            ResponseCountText.text = "Responses: 0";
        }
        if (StartWithResponsesButton != null) StartWithResponsesButton.gameObject.SetActive(true);
        if (InfoText != null) InfoText.text = "Survey distributed. Waiting for responses...";
    }

    private void OnSurveyResponseReceived(int count)
    {
        if (ResponseCountText != null)
            ResponseCountText.text = $"Responses: {count}";
        if (StartWithResponsesButton != null)
            StartWithResponsesButton.interactable = count > 0;
    }

    private void OnStartWithResponses()
    {
        if (SurveyCollector == null || !SurveyCollector.HasResponses) return;
        if (RaceManager == null) return;

        // Close survey for students
        SurveyCollector.CloseSurvey();

        // Apply rules from config to EventSchedule
        if (SurveyConfigManager != null && SurveyConfigManager.ActiveConfig != null
            && RaceManager.EventManager != null)
        {
            SurveyConfigManager.ApplyRulesToSchedule(RaceManager.EventManager.Schedule);
        }

        // Start race with collected responses
        var carDataList = SurveyCollector.GetAllCarData();
        RaceManager.LoadAndStartRace(carDataList);
        gameObject.SetActive(false);
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

        Debug.Log($"[SetupScreen] Received {result.Cars.Count} cars, {result.EventRules.Length} rules from web app direct send");
        if (InfoText != null) InfoText.text = $"Received from web app: {result.Cars.Count} cars, {result.EventRules.Length} rules. Starting race...";

        RaceManager.LoadAndStartRaceWithRules(result.Cars, result.EventRules);
        gameObject.SetActive(false);
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
