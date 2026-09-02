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
        if (Leaderboard != null)
        {
            Leaderboard.OnFullscreenChanged += HandleLeaderboardFullscreenChanged;
            Leaderboard.OnCarSelected += HandleCarSelected;
        }

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
        if (Leaderboard != null)
        {
            Leaderboard.OnFullscreenChanged -= HandleLeaderboardFullscreenChanged;
            Leaderboard.OnCarSelected -= HandleCarSelected;
        }
    }

    // True while the leaderboard is in its full-screen mode, which covers the whole race view.
    // The EventPanel is hidden behind it and restored (if we're still racing as professor) when
    // the leaderboard shrinks back — see OnStateChanged, which folds this into the same rule.
    private bool leaderboardFullscreen;

    // The leaderboard raises this whenever the professor cycles its size with Tab. Hide the
    // EventPanel behind the full-screen leaderboard, and bring it back when the board shrinks.
    private void HandleLeaderboardFullscreenChanged(bool fullscreen)
    {
        leaderboardFullscreen = fullscreen;
        // Re-run the standard visibility rule so the panel returns only when it otherwise should
        // (racing + professor), rather than being force-shown here.
        OnStateChanged(RaceManager != null ? RaceManager.CurrentState : GameState.Setup);
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

        // Switch camera mode based on role. The professor free-flies; the student gets the auto-
        // switching broadcast camera (not free control), and click-to-follow drives it from there
        // via HandleCarSelected. AllowFreeControl gates the professor F/C keys off for students.
        if (CameraManager != null)
        {
            CameraManager.AllowFreeControl = isProfessor;
            CameraManager.SetMode(CameraModeForRole(isProfessor));
        }
    }

    /// <summary>
    /// Whether the professor's EventPanel should be visible. It shows only while racing (never in
    /// Setup/Finished), is professor-only, and hides behind a full-screen leaderboard — which
    /// covers the whole race view — returning as soon as the leaderboard shrinks back to a smaller
    /// mode. Pure so the visibility rule can be unit-tested without a live scene.
    /// </summary>
    public static bool ShouldShowEventPanel(bool isProfessor, bool isRacing, bool leaderboardFullscreen)
        => isProfessor && isRacing && !leaderboardFullscreen;

    /// <summary>
    /// Camera mode a role starts in: the professor free-flies (Free); the student gets the auto-
    /// switching top-N chase (AutoTopCars, broadcast feel) rather than the single-car leader lock.
    /// Pure so the mapping is unit-testable without a live scene.
    ///
    /// The professor loads parked at the first fixed camera (Fixed → FixedCam_F1, since SetMode's
    /// default fixedIndex 0 maps to F1) instead of the free-fly camera — a clean static shot on
    /// load. AllowFreeControl stays on, so F1-F9 / Auto Cam / Esc (→ Free) still work as before.
    /// </summary>
    public static CameraManager.CameraMode CameraModeForRole(bool isProfessor)
        => isProfessor ? CameraManager.CameraMode.Fixed : CameraManager.CameraMode.AutoTopCars;

    /// <summary>
    /// Caption for the student Auto Cam button given the camera's current mode: "Auto: All Cam" in
    /// AutoAllCams, otherwise "Auto: Top 3". Pure so the label rule is unit-testable and matches the
    /// professor RaceControlPanel wording. (Shown after a toggle, so the label reflects the new mode.)
    /// </summary>
    public static string AutoCamButtonLabel(CameraManager.CameraMode mode)
        => mode == CameraManager.CameraMode.AutoAllCams ? "Auto: All Cam" : "Auto: Top 3";

    // A row in the full-screen leaderboard was clicked (both roles): follow that car in a 3rd-person
    // chase and shrink the board back to Normal so the race is visible again. Resolving the clicked
    // team name to a spawned car works for host and student alike (both set CarIdentity.TeamName).
    private void HandleCarSelected(string teamName)
    {
        if (CameraManager == null || RaceManager == null) return;
        var car = CarLookup.FindByTeamName(RaceManager.SpawnedCars, teamName);
        if (car == null) return;
        CameraManager.FollowCar(car.transform);
        if (Leaderboard != null) Leaderboard.SetDisplayMode(LeaderboardPanel.DisplayMode.Normal);
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
            // The EventPanel hides behind a full-screen leaderboard and returns once it shrinks
            // back — the fullscreen flag is toggled by HandleLeaderboardFullscreenChanged.
            if (Events != null)
                Events.gameObject.SetActive(ShouldShowEventPanel(isProfessor, isRacing, leaderboardFullscreen));
            if (Controls != null) Controls.gameObject.SetActive(isRacing);

            // The keyboard-shortcut hint only matters to the professor (free/fixed camera + event
            // keys). The student uses the spectator camera and never triggers events, so keep it
            // hidden for them.
            if (cameraHint == null) BuildCameraHint();
            if (cameraHint != null) cameraHint.SetActive(isRacing);

            // The touch controls are student-only. Defensive hide in case a role flip reaches here
            // before the student lock (the professor already has RaceControlPanel for the same jobs).
            if (studentTouchControls != null) studentTouchControls.SetActive(false);
        }
        else
        {
            // Student: the professor camera hint never applies, but the student still needs to know
            // the leaderboard resizes with Tab and that its rows are clickable in fullscreen.
            if (cameraHint != null) cameraHint.SetActive(false);
            if (studentHint == null) BuildStudentHint();
            if (studentHint != null) studentHint.SetActive(isRacing);

            // Touch-friendly buttons for tablet/mobile students (no keyboard): resize the leaderboard
            // and switch the auto camera — the two controls otherwise bound to Tab and the pro-only C.
            if (studentTouchControls == null) BuildStudentTouchControls();
            if (studentTouchControls != null)
            {
                studentTouchControls.SetActive(isRacing);
                // Keep the panel above a full-screen leaderboard (bg alpha 0.95) so its "Leaderboard"
                // button stays tappable to cycle back out of fullscreen.
                if (isRacing) studentTouchControls.transform.SetAsLastSibling();
            }
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
                  + "F1-F9 fixed cams  |  C auto-cam (top 3 / all cams)  |  Esc free cam  |  Keys 1-9 trigger events\n"
                  + "Tab leaderboard size (normal / enlarged / fullscreen)";
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

    // Runtime-built student hint, mirroring BuildCameraHint. Students never see the professor hint;
    // this one names the two controls their spectator view actually has — Tab resizes the leaderboard,
    // and clicking a team name in the full-screen board follows that car (Esc returns to auto cam).
    private GameObject studentHint;

    private void BuildStudentHint()
    {
        var obj = new GameObject("StudentHint");
        obj.transform.SetParent(transform, false);

        var text = obj.AddComponent<Text>();
        text.text = "Tab: leaderboard size (normal / enlarged / fullscreen)   |   "
                  + "Click a team name in fullscreen to follow that car   |   Esc: auto camera";
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
        rt.sizeDelta = new Vector2(820f, 24f);

        studentHint = obj;
    }

    // Runtime-built, student-only touch controls (bottom-right). Mirrors BuildStudentHint/BuildCameraHint:
    // constructed in code under this Canvas so no scene wiring is required and it ships in the WebGL build.
    // Two buttons — resize the leaderboard (shares LeaderboardPanel.CycleDisplayMode with Tab) and switch
    // the auto camera (CameraManager.ToggleAutoSwitch, otherwise the professor-only C key) — for tablet/
    // mobile spectators who have no keyboard.
    private GameObject studentTouchControls;
    private Text autoCamButtonLabel;

    private void BuildStudentTouchControls()
    {
        var panel = new GameObject("StudentTouchControls", typeof(RectTransform));
        panel.transform.SetParent(transform, false);

        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(1f, 0f);
        prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot = new Vector2(1f, 0f);
        prt.anchoredPosition = new Vector2(-14f, 14f);
        prt.sizeDelta = new Vector2(150f, 96f);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        CreateTouchButton(panel.transform, "LeaderboardBtn", "Leaderboard", OnLeaderboardButton, out _);
        CreateTouchButton(panel.transform, "AutoCamBtn",
            AutoCamButtonLabel(CameraManager != null ? CameraManager.CurrentMode
                                                     : CameraManager.CameraMode.AutoTopCars),
            OnAutoCamButton, out autoCamButtonLabel);

        studentTouchControls = panel;
    }

    // Runtime UGUI button: Image (target graphic + touch area) + Button + a centered child Text. Built
    // in-file rather than via the Editor-only TrackSetupEditor.CreateUIButton, which cannot compile into
    // the runtime/WebGL build. Clicks fire on touch and mouse via the scene's existing EventSystem +
    // GraphicRaycaster (the same path LeaderboardPanel.MakeRowClickable relies on for students).
    private void CreateTouchButton(Transform parent, string name, string label,
                                   UnityEngine.Events.UnityAction onClick, out Text labelText)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var rt = txtGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        labelText = txtGO.AddComponent<Text>();
        labelText.text = label;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 16;
        labelText.color = Color.white;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // Tap "Leaderboard": cycle the board size (Normal → Enlarged → Fullscreen → Normal), the same
    // entry the Tab hotkey uses.
    private void OnLeaderboardButton()
    {
        if (Leaderboard != null) Leaderboard.CycleDisplayMode();
    }

    // Tap "Auto Cam": flip the auto camera (Top-3 chase ↔ all-cams-on-leader) and relabel to the new
    // mode. From a click-to-follow shot this returns the student to an auto broadcast camera.
    private void OnAutoCamButton()
    {
        if (CameraManager == null) return;
        CameraManager.ToggleAutoSwitch();
        if (autoCamButtonLabel != null)
            autoCamButtonLabel.text = AutoCamButtonLabel(CameraManager.CurrentMode);
    }
}
