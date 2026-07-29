using UnityEngine;

/// <summary>
/// Reads professor host-launch params from the page URL hash on load (Phase 2). If launched
/// as host from the Dashboard "Host Game" button, auto-creates the room carrying the host
/// token and locks the UI to Professor (hiding the student Join UI). On a plain reload of a
/// previously-launched host tab, resumes the room via the persisted sessionId instead of
/// creating a duplicate.
///
/// Attach to a scene GameObject alongside NetworkManager/RaceUI and assign both refs.
/// In Editor/Standalone WebSocketBridge.GetPageUrl() returns "", so nothing auto-hosts and
/// manual in-editor testing is preserved.
/// </summary>
public class HostLaunchBootstrap : MonoBehaviour
{
    public NetworkManager NetworkManager;
    public RaceUI RaceUI;

    private void Start()
    {
        if (NetworkManager == null || RaceUI == null)
        {
            Debug.LogWarning("[HostLaunchBootstrap] NetworkManager/RaceUI not assigned; skipping.");
            return;
        }

        var p = HostLaunchParams.ParseHash(WebSocketBridge.GetPageUrl());

        if (p.TryGetValue("role", out var role) && role == "host")
        {
            p.TryGetValue("token", out var token);
            p.TryGetValue("survey", out var surveyId);
            Debug.Log("[HostLaunchBootstrap] Launching as host from Dashboard.");

            // Phase 3: once the room exists, auto-inject the launched survey's responses so the
            // professor never opens the manual Send-to-Game modal. One-shot — unsubscribe on the
            // first fire so a later reconnect/create never re-injects. Subscribe BEFORE CreateRoom
            // so the handler is attached when room_created returns. Captures surveyId now, before
            // ClearUrlHash() wipes the launch params.
            if (HostAutoInjectDecision.ShouldAutoInject(role, surveyId))
            {
                void OnCreated(string roomCode)
                {
                    NetworkManager.OnRoomCreated -= OnCreated;
                    Debug.Log($"[HostLaunchBootstrap] Auto-injecting survey {surveyId} into room {roomCode}.");
                    WebSocketBridge.HostAutoInject(surveyId, roomCode);
                }
                NetworkManager.OnRoomCreated += OnCreated;
            }

            NetworkManager.CreateRoom(token);
            // Drop the hash so a reload resumes the room instead of re-minting with a stale token.
            WebSocketBridge.ClearUrlHash();
            RaceUI.SetRoleFromNetwork(true);
            return;
        }

        if (NetworkManager.HasPersistedHostSession())
        {
            Debug.Log("[HostLaunchBootstrap] Resuming persisted host session after reload.");
            NetworkManager.ResumeHostSession();
            RaceUI.SetRoleFromNetwork(true);
        }
    }
}
