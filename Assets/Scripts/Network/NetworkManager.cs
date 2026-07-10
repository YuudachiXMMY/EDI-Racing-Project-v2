using System;
using UnityEngine;

/// <summary>
/// Manages WebSocket connection lifecycle, room creation/joining,
/// and message routing. Attach to a GameObject in the scene.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("WebSocket server URL (ws://host:port)")]
    public string ServerUrl = "ws://localhost:8080";

    public bool IsConnected => bridge != null && bridge.IsConnected;
    public bool IsHost { get; private set; }
    public string RoomCode { get; private set; }
    public int StudentCount { get; private set; }

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnRoomCreated;
    public event Action<string> OnRoomJoined;
    public event Action<int> OnStudentCountChanged;
    public event Action<string> OnMessageReceived;
    public event Action<string> OnConnectionError;

    private WebSocketBridge bridge;

    private void Awake()
    {
        // Find or create WebSocketBridge on a child GameObject
        bridge = GetComponentInChildren<WebSocketBridge>();
        if (bridge == null)
        {
            var go = new GameObject("WebSocketBridge");
            go.transform.SetParent(transform);
            bridge = go.AddComponent<WebSocketBridge>();
        }

        bridge.OnOpen += HandleOpen;
        bridge.OnMessage += HandleMessage;
        bridge.OnClose += HandleClose;
        bridge.OnError += HandleError;
    }

    private void OnDestroy()
    {
        if (bridge != null)
        {
            bridge.OnOpen -= HandleOpen;
            bridge.OnMessage -= HandleMessage;
            bridge.OnClose -= HandleClose;
            bridge.OnError -= HandleError;
        }
        Disconnect();
    }

    public void Connect()
    {
        if (bridge == null || bridge.IsConnected) return;
        Debug.Log($"[NetworkManager] Connecting to {ServerUrl}...");
        bridge.Connect(ServerUrl);
    }

    public void Disconnect()
    {
        if (bridge != null)
            bridge.Close();
        IsHost = false;
        RoomCode = null;
        StudentCount = 0;
    }

    public void Send(string json)
    {
        if (bridge != null && bridge.IsConnected)
            bridge.Send(json);
    }

    public void CreateRoom()
    {
        Connect();
        // Will send create_room after connection opens (queued via pendingAction)
        pendingAction = () =>
        {
            IsHost = true;
            Send(JsonUtility.ToJson(new CreateRoomMessage()));
        };
        if (bridge.IsConnected)
        {
            pendingAction();
            pendingAction = null;
        }
    }

    public void JoinRoom(string code)
    {
        Connect();
        var msg = new JoinRoomMessage { roomCode = code.ToUpper() };
        pendingAction = () =>
        {
            IsHost = false;
            Send(JsonUtility.ToJson(msg));
        };
        if (bridge.IsConnected)
        {
            pendingAction();
            pendingAction = null;
        }
    }

    private Action pendingAction;

    private void HandleOpen()
    {
        Debug.Log("[NetworkManager] Connected");
        OnConnected?.Invoke();
        if (pendingAction != null)
        {
            pendingAction();
            pendingAction = null;
        }
    }

    private void HandleClose()
    {
        Debug.Log("[NetworkManager] Disconnected");
        IsHost = false;
        RoomCode = null;
        StudentCount = 0;
        OnDisconnected?.Invoke();
    }

    private void HandleError(string error)
    {
        Debug.LogWarning($"[NetworkManager] Error: {error}");
        OnConnectionError?.Invoke(error);
    }

    private void HandleMessage(string json)
    {
        var baseMsg = JsonUtility.FromJson<NetworkMessage>(json);

        switch (baseMsg.type)
        {
            case "room_created":
                var rc = JsonUtility.FromJson<RoomCreatedMessage>(json);
                RoomCode = rc.roomCode;
                Debug.Log($"[NetworkManager] Room created: {RoomCode}");
                OnRoomCreated?.Invoke(RoomCode);
                break;

            case "room_joined":
                var rj = JsonUtility.FromJson<RoomJoinedMessage>(json);
                RoomCode = rj.roomCode;
                Debug.Log($"[NetworkManager] Joined room: {RoomCode}");
                OnRoomJoined?.Invoke(RoomCode);
                break;

            case "student_count":
                var sc = JsonUtility.FromJson<StudentCountMessage>(json);
                StudentCount = sc.count;
                OnStudentCountChanged?.Invoke(sc.count);
                break;

            case "error":
                var err = JsonUtility.FromJson<ErrorMessage>(json);
                Debug.LogWarning($"[NetworkManager] Server error: {err.message}");
                OnConnectionError?.Invoke(err.message);
                break;

            default:
                // Forward to NetworkSync for game-specific messages
                OnMessageReceived?.Invoke(json);
                break;
        }
    }
}
