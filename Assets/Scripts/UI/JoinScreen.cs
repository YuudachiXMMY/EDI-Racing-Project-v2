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
    public Button JoinButton;
    public Text StatusText;

    private void Start()
    {
        if (JoinButton != null)
            JoinButton.onClick.AddListener(OnJoinClicked);

        if (RoomCodeInput != null)
            RoomCodeInput.characterLimit = 6;

        if (StatusText != null)
            StatusText.text = "Enter room code to join.";
    }

    private void OnEnable()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnRoomJoined += OnRoomJoined;
            NetworkManager.OnConnectionError += OnError;
            NetworkManager.OnDisconnected += OnDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnRoomJoined -= OnRoomJoined;
            NetworkManager.OnConnectionError -= OnError;
            NetworkManager.OnDisconnected -= OnDisconnected;
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

        SetStatus("Connecting...");
        if (JoinButton != null) JoinButton.interactable = false;
        NetworkManager.JoinRoom(code);
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

    private void SetStatus(string message)
    {
        if (StatusText != null)
            StatusText.text = message;
    }
}
