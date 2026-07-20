using System;
using System.Collections;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Manages WebSocket connection lifecycle, room creation/joining,
/// message routing, and automatic reconnection with exponential backoff.
/// Attach to a GameObject in the scene.
/// In WebGL builds, the server URL is auto-detected from the page hostname.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("WebSocket server URL. Ignored in WebGL builds (auto-detected from page URL).")]
    public string ServerUrl = "ws://localhost:8080";

    [Header("Reconnection")]
    [Tooltip("Enable automatic reconnection on disconnect")]
    public bool AutoReconnect = true;
    public float InitialDelay = 1f;
    public float MaxDelay = 30f;
    public float BackoffMultiplier = 2f;
    public int MaxAttempts = 10;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string WebSocketBridge_GetPageWebSocketUrl();
#endif

    public bool IsConnected => bridge != null && bridge.IsConnected;
    public bool IsHost { get; private set; }
    public string RoomCode { get; private set; }
    public int StudentCount { get; private set; }
    public bool IsReconnecting { get; private set; }
    public int ReconnectAttempt { get; private set; }

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnRoomCreated;
    public event Action<string> OnRoomJoined;
    public event Action<int> OnStudentCountChanged;
    public event Action<string> OnMessageReceived;
    public event Action<string> OnConnectionError;
    public event Action<int, float> OnReconnecting; // attempt, nextDelay
    public event Action OnReconnected;
    public event Action OnReconnectFailed;

    private WebSocketBridge bridge;
    private Action pendingAction;

    // Reconnection state
    private string sessionId;
    private string lastRoomCode;
    private bool wasHost;
    private string lastServerUrl;
    private Coroutine reconnectCoroutine;
    private bool manualDisconnect;

    private void Awake()
    {
        sessionId = Guid.NewGuid().ToString("N").Substring(0, 12);

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
        CancelReconnect();
        Disconnect();
    }

    public void Connect()
    {
        if (bridge == null || bridge.IsConnected) return;
        lastServerUrl = GetServerUrl();
        Debug.Log($"[NetworkManager] Connecting to {lastServerUrl}...");
        bridge.Connect(lastServerUrl);
    }

    private string GetServerUrl()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string autoUrl = WebSocketBridge_GetPageWebSocketUrl();
        if (!string.IsNullOrEmpty(autoUrl))
        {
            Debug.Log($"[NetworkManager] Auto-detected WebSocket URL: {autoUrl}");
            return autoUrl;
        }
#endif
        return ServerUrl;
    }

    /// <summary>
    /// Manual disconnect — does NOT trigger auto-reconnect.
    /// </summary>
    public void Disconnect()
    {
        manualDisconnect = true;
        CancelReconnect();
        if (bridge != null)
            bridge.Close();
        IsHost = false;
        RoomCode = null;
        StudentCount = 0;
        lastRoomCode = null;
    }

    public void Send(string json)
    {
        if (bridge != null && bridge.IsConnected)
            bridge.Send(json);
    }

    public void CreateRoom()
    {
        manualDisconnect = false;
        Connect();
        pendingAction = () =>
        {
            IsHost = true;
            var msg = new CreateRoomMessage { sessionId = sessionId };
            Send(JsonUtility.ToJson(msg));
        };
        if (bridge.IsConnected)
        {
            pendingAction();
            pendingAction = null;
        }
    }

    public void JoinRoom(string code)
    {
        manualDisconnect = false;
        Connect();
        var joinMsg = new JoinRoomMessage { roomCode = code.ToUpper(), sessionId = sessionId };
        pendingAction = () =>
        {
            IsHost = false;
            Send(JsonUtility.ToJson(joinMsg));
        };
        if (bridge.IsConnected)
        {
            pendingAction();
            pendingAction = null;
        }
    }

    public void CancelReconnect()
    {
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
        IsReconnecting = false;
        ReconnectAttempt = 0;
    }

    // --- Connection callbacks ---

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
        bool wasConnected = RoomCode != null;
        Debug.Log("[NetworkManager] Disconnected");

        // Save state for potential reconnection before clearing
        if (wasConnected && !IsReconnecting)
        {
            lastRoomCode = RoomCode;
            wasHost = IsHost;
        }

        IsHost = false;
        RoomCode = null;
        StudentCount = 0;

        // Attempt auto-reconnect if applicable
        if (!manualDisconnect && AutoReconnect && lastRoomCode != null && !IsReconnecting)
        {
            reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
        }
        else if (!IsReconnecting)
        {
            OnDisconnected?.Invoke();
        }
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
                lastRoomCode = rc.roomCode;
                Debug.Log($"[NetworkManager] Room created: {RoomCode}");
                OnRoomCreated?.Invoke(RoomCode);
                break;

            case "room_joined":
                var rj = JsonUtility.FromJson<RoomJoinedMessage>(json);
                RoomCode = rj.roomCode;
                lastRoomCode = rj.roomCode;
                Debug.Log($"[NetworkManager] Joined room: {RoomCode}");
                OnRoomJoined?.Invoke(RoomCode);
                break;

            case "reconnect_state":
                var rs = JsonUtility.FromJson<ReconnectStateMessage>(json);
                IsHost = wasHost;
                RoomCode = lastRoomCode;
                StudentCount = rs.studentCount;
                Debug.Log($"[NetworkManager] Reconnected to room {RoomCode} (phase: {rs.gamePhase}, students: {rs.studentCount})");
                OnReconnected?.Invoke();
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
                OnMessageReceived?.Invoke(json);
                break;
        }
    }

    // --- Reconnection coroutine ---

    private IEnumerator ReconnectCoroutine()
    {
        IsReconnecting = true;
        float delay = InitialDelay;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ReconnectAttempt = attempt;
            float jitteredDelay = delay * UnityEngine.Random.Range(0.75f, 1.25f);
            OnReconnecting?.Invoke(attempt, jitteredDelay);
            Debug.Log($"[NetworkManager] Reconnect attempt {attempt}/{MaxAttempts} in {jitteredDelay:F1}s");

            yield return new WaitForSeconds(jitteredDelay);

            // Attempt connection
            if (lastServerUrl == null) lastServerUrl = GetServerUrl();
            bridge.Connect(lastServerUrl);

            // Wait for connection result (up to 5s)
            float timeout = 5f;
            while (timeout > 0 && !bridge.IsConnected)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (bridge.IsConnected)
            {
                // Send rejoin request
                var msg = new RejoinRoomMessage { roomCode = lastRoomCode, sessionId = sessionId };
                Send(JsonUtility.ToJson(msg));
                IsReconnecting = false;
                ReconnectAttempt = 0;
                reconnectCoroutine = null;
                // OnReconnected fires when server responds with reconnect_state
                yield break;
            }

            delay = Mathf.Min(delay * BackoffMultiplier, MaxDelay);
        }

        // All attempts exhausted
        IsReconnecting = false;
        ReconnectAttempt = 0;
        reconnectCoroutine = null;
        lastRoomCode = null;
        OnReconnectFailed?.Invoke();
        OnDisconnected?.Invoke();
    }
}
