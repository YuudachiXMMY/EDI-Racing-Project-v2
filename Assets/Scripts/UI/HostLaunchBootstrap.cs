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
            // professor never opens the manual Send-to-Game modal, then clear the launch hash.
            // Both are one-shot: they self-unsubscribe on the first room_created. Subscribe
            // BEFORE CreateRoom so the handler is attached when room_created returns. Captures
            // surveyId now, before ClearUrlHash() wipes the launch params.
            //
            // Clearing the hash is deferred to room_created (not done synchronously) so a tab
            // reload during the connect window keeps the token — otherwise the professor would
            // retry with an *untokened* create. Once confirmed, the persisted host session is
            // already set, so a later reload resumes via sessionId.
            bool autoInject = HostAutoInjectDecision.ShouldAutoInject(role, surveyId);

            void Unsubscribe()
            {
                NetworkManager.OnRoomCreated -= OnCreated;
                NetworkManager.OnConnectionError -= OnCreateFailed;
            }

            void OnCreated(string roomCode)
            {
                Unsubscribe();
                if (autoInject)
                {
                    Debug.Log($"[HostLaunchBootstrap] Auto-injecting survey {surveyId} into room {roomCode}.");
                    WebSocketBridge.HostAutoInject(surveyId, roomCode);
                }
                WebSocketBridge.ClearUrlHash();
            }

            // If the tokened create fails (e.g. invalid/expired host token → server 'error'),
            // tear down these one-shot handlers. Otherwise a later manual Host retry's
            // room_created would fire this stale closure and auto-inject the ORIGINAL survey
            // into the retried room.
            void OnCreateFailed(string _) => Unsubscribe();

            NetworkManager.OnRoomCreated += OnCreated;
            NetworkManager.OnConnectionError += OnCreateFailed;

            NetworkManager.CreateRoom(token);
            RaceUI.SetRoleFromNetwork(true);
            return;
        }

        // Only resume a persisted host session on a *plain* reload. A student launch (role=play)
        // in a browser that previously hosted (edi-was-host=="1") must NOT be hijacked into
        // resuming the old host room — StudentJoinBootstrap will join it as a student instead.
        if (role != "play" && NetworkManager.HasPersistedHostSession())
        {
            Debug.Log("[HostLaunchBootstrap] Resuming persisted host session after reload.");
            NetworkManager.ResumeHostSession();
            RaceUI.SetRoleFromNetwork(true);
        }
    }
}
