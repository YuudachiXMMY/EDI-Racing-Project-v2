using System;
using System.Collections.Concurrent;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

/// <summary>
/// Cross-platform WebSocket wrapper. Uses jslib in WebGL builds,
/// System.Net.WebSockets.ClientWebSocket in Editor/Standalone.
/// Attach to a GameObject named "WebSocketBridge" (jslib SendMessage target).
/// </summary>
public class WebSocketBridge : MonoBehaviour
{
    public event Action OnOpen;
    public event Action<string> OnMessage;
    public event Action OnClose;
    public event Action<string> OnError;

    public bool IsConnected { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void WebSocketBridge_Connect(string url, string goName);
    [DllImport("__Internal")] private static extern void WebSocketBridge_Send(string msg);
    [DllImport("__Internal")] private static extern void WebSocketBridge_Close();
    [DllImport("__Internal")] private static extern int WebSocketBridge_GetState();

    public void Connect(string url)
    {
        WebSocketBridge_Connect(url, gameObject.name);
    }

    public void Send(string message)
    {
        if (IsConnected)
            WebSocketBridge_Send(message);
    }

    public void Close()
    {
        WebSocketBridge_Close();
        IsConnected = false;
    }

    // Called from jslib via SendMessage
    public void OnWebSocketOpen(string _)
    {
        IsConnected = true;
        OnOpen?.Invoke();
    }

    public void OnWebSocketMessage(string data)
    {
        OnMessage?.Invoke(data);
    }

    public void OnWebSocketClose(string code)
    {
        IsConnected = false;
        OnClose?.Invoke();
    }

    public void OnWebSocketError(string error)
    {
        OnError?.Invoke(error);
    }

#else
    private ClientWebSocket socket;
    private CancellationTokenSource cts;
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    public async void Connect(string url)
    {
        Close();

        socket = new ClientWebSocket();
        cts = new CancellationTokenSource();

        try
        {
            await socket.ConnectAsync(new Uri(url), cts.Token);
            IsConnected = true;
            mainThreadActions.Enqueue(() => OnOpen?.Invoke());
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            mainThreadActions.Enqueue(() => OnError?.Invoke(e.Message));
        }
    }

    public async void Send(string message)
    {
        if (socket == null || socket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        try
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocketBridge] Send error: {e.Message}");
        }
    }

    public void Close()
    {
        IsConnected = false;
        cts?.Cancel();
        if (socket != null && socket.State == WebSocketState.Open)
        {
            try { socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
            catch { /* ignore */ }
        }
        socket = null;
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        try
        {
            while (socket != null && socket.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        IsConnected = false;
                        mainThreadActions.Enqueue(() => OnClose?.Invoke());
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                string msg = sb.ToString();
                mainThreadActions.Enqueue(() => OnMessage?.Invoke(msg));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            IsConnected = false;
            mainThreadActions.Enqueue(() => OnClose?.Invoke());
        }
    }

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out var action))
            action();
    }

    private void OnDestroy()
    {
        Close();
    }
#endif

    // --- Static page-URL + persistence bridge (Phase 2 host launch) ---
    // WebGL: window.location + localStorage via jslib. Editor/Standalone: PlayerPrefs fallback,
    // and no page URL (so HostLaunchBootstrap treats "" as "no host params" and never auto-hosts).
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string WebSocketBridge_GetPageUrl();
    [DllImport("__Internal")] private static extern string WebSocketBridge_LocalStorageGet(string key);
    [DllImport("__Internal")] private static extern void WebSocketBridge_LocalStorageSet(string key, string val);
    [DllImport("__Internal")] private static extern void WebSocketBridge_ClearUrlHash();
    [DllImport("__Internal")] private static extern void WebSocketBridge_HostAutoInject(string surveyId, string roomCode);
#endif

    /// <summary>Full page URL (WebGL). Empty string in Editor/Standalone.</summary>
    public static string GetPageUrl()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return WebSocketBridge_GetPageUrl();
#else
        return "";
#endif
    }

    /// <summary>Read a persisted value. localStorage in WebGL, PlayerPrefs otherwise. Never null.</summary>
    public static string StorageGet(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return WebSocketBridge_LocalStorageGet(key);
#else
        return PlayerPrefs.GetString(key, "");
#endif
    }

    /// <summary>Persist a value. localStorage in WebGL, PlayerPrefs otherwise.</summary>
    public static void StorageSet(string key, string val)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebSocketBridge_LocalStorageSet(key, val);
#else
        PlayerPrefs.SetString(key, val);
#endif
    }

    /// <summary>
    /// Strip the URL hash (host-launch params) after consuming it, so a page reload does
    /// not re-mint/re-create a room with a now-stale token. No-op in Editor/Standalone.
    /// </summary>
    public static void ClearUrlHash()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebSocketBridge_ClearUrlHash();
#endif
    }

    /// <summary>
    /// Phase 3 auto-inject: ask the web-app to push the given survey's responses into the
    /// freshly-created room via the existing authenticated send-to-game endpoint. Fire-and-forget
    /// in WebGL; a no-op (logged) in Editor/Standalone since the endpoint + browser auth are absent.
    /// </summary>
    public static void HostAutoInject(string surveyId, string roomCode)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebSocketBridge_HostAutoInject(surveyId, roomCode);
#else
        Debug.Log($"[WebSocketBridge] HostAutoInject noop (editor): survey={surveyId}, room={roomCode}");
#endif
    }
}
