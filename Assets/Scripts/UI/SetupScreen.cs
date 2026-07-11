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
        }
    }

    private void OnDisable()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnRoomCreated -= OnRoomCreated;
            NetworkManager.OnStudentCountChanged -= OnStudentCountChanged;
            NetworkManager.OnConnectionError -= OnNetworkError;
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
