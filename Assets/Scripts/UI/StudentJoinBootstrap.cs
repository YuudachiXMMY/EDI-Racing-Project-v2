using UnityEngine;

/// <summary>
/// Reads the student 3D-join params from the page URL hash on load (Phase 5). If opened from
/// the student link ("…/#room=CODE&amp;role=play", no host token), auto-joins the room as a passive
/// spectator and hard-locks the UI to Student — hiding the EventPanel / race controls / Host
/// Setup screen so no host controls exist and the role cannot flip to Professor.
///
/// Mirror of <see cref="HostLaunchBootstrap"/>: reuses <see cref="HostLaunchParams.ParseHash"/>
/// and calls <see cref="NetworkManager.JoinRoom"/> (which sets IsHost=false) instead of
/// CreateRoom. Deliberately does NOT clear the URL hash — the student hash carries no secret, so
/// keeping it lets a full page reload auto-rejoin the same room without any persisted-session code.
///
/// Attach to a scene GameObject alongside NetworkManager/RaceUI and assign both refs. Safe to sit
/// on the same GameObject as HostLaunchBootstrap: a URL is either role=host or role=play, so only
/// the matching bootstrap acts. In Editor/Standalone WebSocketBridge.GetPageUrl() returns "", so
/// nothing auto-joins and manual in-editor testing is preserved.
/// </summary>
public class StudentJoinBootstrap : MonoBehaviour
{
    public NetworkManager NetworkManager;
    public RaceUI RaceUI;

    private void Start()
    {
        if (NetworkManager == null || RaceUI == null)
        {
            Debug.LogWarning("[StudentJoinBootstrap] NetworkManager/RaceUI not assigned; skipping.");
            return;
        }

        var p = HostLaunchParams.ParseHash(WebSocketBridge.GetPageUrl());
        p.TryGetValue("role", out var role);
        p.TryGetValue("room", out var room);

        if (!StudentJoinDecision.ShouldAutoJoin(role, room))
            return; // not a student launch — leave the manual Host/Join UI intact

        Debug.Log($"[StudentJoinBootstrap] Auto-joining room {room} as student spectator.");
        // Empty team name = anonymous spectator; the server accepts a blank teamName on join_room.
        NetworkManager.JoinRoom(room, "");
        // Hard-lock to Student: hide EventPanel/Controls/Setup, show JoinScreen, block any flip.
        RaceUI.LockAsStudent();
        // NOTE: intentionally NOT calling WebSocketBridge.ClearUrlHash() — retaining
        // "#room=CODE&role=play" lets a reload re-run this bootstrap and auto-rejoin.
    }
}
