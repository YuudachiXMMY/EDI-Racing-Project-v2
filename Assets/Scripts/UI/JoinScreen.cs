using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Student room-code entry screen. Shown during GameState.Setup for Student role.
/// Mirrors SetupScreen structure.
/// </summary>
public class JoinScreen : MonoBehaviour
{
    [Header("References")]
    public NetworkManager NetworkManager;

    [Header("UI Elements")]
    public InputField RoomCodeInput;
    public InputField TeamNameInput;
    public Button JoinButton;
    public Text StatusText;

    private void Start()
    {
        if (JoinButton != null)
            JoinButton.onClick.AddListener(OnJoinClicked);

        if (RoomCodeInput != null)
            RoomCodeInput.characterLimit = 6;

        if (TeamNameInput != null)
            TeamNameInput.characterLimit = 30;

        if (StatusText != null)
            StatusText.text = "Enter room code and team name to join.";
    }

    private void OnEnable()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnRoomJoined += OnRoomJoined;
            NetworkManager.OnConnectionError += OnError;
            NetworkManager.OnDisconnected += OnDisconnected;
            NetworkManager.OnReconnecting += OnReconnecting;
            NetworkManager.OnReconnected += OnReconnected;
            NetworkManager.OnReconnectFailed += OnReconnectFailed;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnRoomJoined -= OnRoomJoined;
            NetworkManager.OnConnectionError -= OnError;
            NetworkManager.OnDisconnected -= OnDisconnected;
            NetworkManager.OnReconnecting -= OnReconnecting;
            NetworkManager.OnReconnected -= OnReconnected;
            NetworkManager.OnReconnectFailed -= OnReconnectFailed;
        }
    }

    private void OnJoinClicked()
    {
        if (NetworkManager == null)
        {
            SetStatus("Network not available.");
            return;
        }

        string code = RoomCodeInput != null ? RoomCodeInput.text.Trim().ToUpper() : "";
        if (code.Length != 6)
        {
            SetStatus("Room code must be 6 characters.");
            return;
        }

        string teamName = TeamNameInput != null ? TeamNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(teamName))
        {
            SetStatus("Please enter your team name.");
            return;
        }

        SetStatus("Connecting...");
        if (JoinButton != null) JoinButton.interactable = false;
        NetworkManager.JoinRoom(code, teamName);
    }

    private void OnRoomJoined(string roomCode)
    {
        SetStatus($"Joined room {roomCode}. Waiting for race...");
        gameObject.SetActive(false);
    }

    private void OnError(string error)
    {
        SetStatus($"Error: {error}");
        if (JoinButton != null) JoinButton.interactable = true;
    }

    private void OnDisconnected()
    {
        SetStatus("Disconnected from server.");
        if (JoinButton != null) JoinButton.interactable = true;
    }

    private void OnReconnecting(int attempt, float delay)
    {
        SetStatus($"Reconnecting... ({attempt}/{NetworkManager.MaxAttempts})");
        if (JoinButton != null) JoinButton.interactable = false;
    }

    private void OnReconnected()
    {
        SetStatus("Reconnected!");
    }

    private void OnReconnectFailed()
    {
        SetStatus("Connection lost. Please re-enter room code.");
        if (JoinButton != null) JoinButton.interactable = true;
    }

    private void SetStatus(string message)
    {
        if (StatusText != null)
            StatusText.text = message;
    }
}
