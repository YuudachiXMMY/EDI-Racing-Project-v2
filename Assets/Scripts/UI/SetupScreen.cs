using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-race setup overlay. Shown during GameState.Setup.
/// Allows starting race with default CSV data or loading a saved session.
/// Optionally hosts a networked room for multi-client sync (Phase 5).
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

    private void Start()
    {
        // Auto-find RaceManager if not assigned in Inspector
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

        // Hide network UI if no NetworkManager
        bool hasNetwork = NetworkManager != null;
        if (HostButton != null) HostButton.gameObject.SetActive(hasNetwork);
        if (RoomCodeText != null) RoomCodeText.gameObject.SetActive(false);
        if (StudentCountText != null) StudentCountText.gameObject.SetActive(false);

        if (InfoText != null)
            InfoText.text = "Ready to start race.";
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
