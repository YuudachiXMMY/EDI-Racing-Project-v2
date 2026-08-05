using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-level UI controller. Manages panel visibility based on
/// user role (Professor/Student) and current GameState.
/// </summary>
public class RaceUI : MonoBehaviour
{
    public enum UserRole { Professor, Student }

    [Header("Role")]
    // Defaults to Professor because the manual local-host flow (no URL params) relies on the
    // Setup/Host screen being visible on a plain launch — flipping this to Student would hide
    // the Host button and break in-editor / single-machine hosting. The security-critical
    // fail-closed guard for the *shared student link* lives in StudentJoinBootstrap, which
    // calls LockAsStudent() whenever role=="play" (even on a malformed room code), so a student
    // link can never fall through to this Professor default. Server-side host-token enforcement
    // is the actual authority: a student socket cannot create a room or trigger events regardless.
    public UserRole Role = UserRole.Professor;

    [Header("References")]
    public RaceManager RaceManager;
    public CameraManager CameraManager;
    [Tooltip("Optional. When assigned, creating a room locks the UI to Professor automatically.")]
    public NetworkManager NetworkManager;

    [Header("Panels")]
    public LeaderboardPanel Leaderboard;
    public EventPanel Events;
    public RaceControlPanel Controls;
    public SetupScreen Setup;
    public JoinScreen JoinScreen;

    private void Start()
    {
        ResolveMissingReferences();

        if (RaceManager != null)
            RaceManager.OnStateChanged += OnStateChanged;
        if (NetworkManager != null)
            NetworkManager.OnRoomCreated += HandleRoomCreated;

        ApplyRole();
        OnStateChanged(RaceManager != null ? RaceManager.CurrentState : GameState.Setup);
    }

    // Defensive auto-wire: the HUD panels and core managers are unique per scene, so if a scene's
    // RaceUI references were lost during serialization (the panels stay {fileID: 0} in the .unity
    // file), resolve them by type here. The panels start inactive, so FindObjectsInactive.Include
    // is required — without it the leaderboard/events/controls would never re-appear at Racing.
    // NetworkManager is intentionally NOT auto-resolved: its null state is a meaningful "no network"
    // signal (see the field tooltip), so leave whatever the scene assigned.
    private void ResolveMissingReferences()
    {
        if (RaceManager == null)
            RaceManager = FindFirstObjectByType<RaceManager>(FindObjectsInactive.Include);
        if (CameraManager == null)
            CameraManager = FindFirstObjectByType<CameraManager>(FindObjectsInactive.Include);
        if (Leaderboard == null)
            Leaderboard = FindFirstObjectByType<LeaderboardPanel>(FindObjectsInactive.Include);
        if (Events == null)
            Events = FindFirstObjectByType<EventPanel>(FindObjectsInactive.Include);
        if (Controls == null)
            Controls = FindFirstObjectByType<RaceControlPanel>(FindObjectsInactive.Include);
        if (Setup == null)
            Setup = FindFirstObjectByType<SetupScreen>(FindObjectsInactive.Include);
        if (JoinScreen == null)
            JoinScreen = FindFirstObjectByType<JoinScreen>(FindObjectsInactive.Include);
    }

    private void OnDestroy()
    {
        if (RaceManager != null)
            RaceManager.OnStateChanged -= OnStateChanged;
        if (NetworkManager != null)
            NetworkManager.OnRoomCreated -= HandleRoomCreated;
    }

    // Any successful room creation (Dashboard host launch OR manual in-game Host) locks the
    // UI to Professor so role state cannot drift from the network truth.
    private void HandleRoomCreated(string _) => SetRoleFromNetwork(true);

    // Once locked to Student (URL student launch, Phase 5), the role cannot be changed by any
    // later network event or manual Host click — a hard, one-way lock for the non-host client.
    private bool roleLocked;

    /// <summary>
    /// Set role from network state. Called by NetworkSync or external code. Ignored once the
    /// role has been hard-locked to Student via <see cref="LockAsStudent"/>.
    /// </summary>
    public void SetRoleFromNetwork(bool isHost)
    {
        if (roleLocked) return;
        Role = isHost ? UserRole.Professor : UserRole.Student;
        ApplyRole();
        OnStateChanged(RaceManager != null ? RaceManager.CurrentState : GameState.Setup);
    }

    /// <summary>
    /// Hard-lock this client to the Student role (Phase 5 student URL launch). Hides the
    /// EventPanel / race controls / Host Setup screen, shows the JoinScreen, and blocks any later
    /// <see cref="SetRoleFromNetwork"/> or <see cref="HandleRoomCreated"/> from flipping to Professor.
    /// </summary>
    public void LockAsStudent()
    {
        Role = UserRole.Student;
        ApplyRole();
        OnStateChanged(RaceManager != null ? RaceManager.CurrentState : GameState.Setup);
        roleLocked = true;
    }

    private void ApplyRole()
    {
        bool isProfessor = Role == UserRole.Professor;

        if (Events != null) Events.gameObject.SetActive(isProfessor);
        if (Controls != null) Controls.gameObject.SetActive(isProfessor);
        if (Setup != null) Setup.gameObject.SetActive(isProfessor);
        if (JoinScreen != null) JoinScreen.gameObject.SetActive(!isProfessor);

        // Switch camera mode based on role
        if (CameraManager != null)
        {
            if (isProfessor)
                CameraManager.SetMode(CameraManager.CameraMode.Free);
            else
                CameraManager.SetMode(CameraManager.CameraMode.Spectator);
        }
    }

    private void OnStateChanged(GameState state)
    {
        bool isSetup = state == GameState.Setup;
        bool isRacing = state == GameState.Racing || state == GameState.Paused;
        bool isProfessor = Role == UserRole.Professor;

        // Setup is the host room-creation screen — only the professor ever sees it, so gate on
        // role too. Without this, a locked student in GameState.Setup would have the Host UI
        // re-shown here right after ApplyRole() hid it.
        if (Setup != null) Setup.gameObject.SetActive(isSetup && isProfessor);
        if (Leaderboard != null) Leaderboard.gameObject.SetActive(isRacing);

        if (isProfessor)
        {
            if (Events != null) Events.gameObject.SetActive(isRacing);
            if (Controls != null) Controls.gameObject.SetActive(isRacing);

            // The keyboard-shortcut hint only matters to the professor (free/fixed camera + event
            // keys). The student uses the spectator camera and never triggers events, so keep it
            // hidden for them.
            if (cameraHint == null) BuildCameraHint();
            if (cameraHint != null) cameraHint.SetActive(isRacing);
        }
        else if (cameraHint != null)
        {
            cameraHint.SetActive(false);
        }
    }

    // Runtime-built keyboard-shortcut overlay. There is no such object in the scene, so RaceUI
    // creates it lazily under its own Canvas (bottom-left, where no other HUD panel sits). Kept
    // ASCII because the built-in LegacyRuntime font has no CJK glyphs.
    private GameObject cameraHint;

    private void BuildCameraHint()
    {
        var obj = new GameObject("CameraHint");
        obj.transform.SetParent(transform, false);

        var text = obj.AddComponent<Text>();
        text.text = "Camera:  WASD move  |  Right-drag look  |  Q/E up-down  |  Scroll speed\n"
                  + "F1-F9 fixed cams  |  Esc free cam  |  Keys 1-9 trigger events";
        text.fontSize = 15;
        text.alignment = TextAnchor.LowerLeft;
        text.color = new Color(1f, 1f, 1f, 0.75f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(14f, 12f);
        rt.sizeDelta = new Vector2(720f, 44f);

        cameraHint = obj;
    }
}
