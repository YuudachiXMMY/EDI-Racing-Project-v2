using UnityEngine;

/// <summary>
/// Top-level UI controller. Manages panel visibility based on
/// user role (Professor/Student) and current GameState.
/// </summary>
public class RaceUI : MonoBehaviour
{
    public enum UserRole { Professor, Student }

    [Header("Role")]
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
        if (RaceManager != null)
            RaceManager.OnStateChanged += OnStateChanged;
        if (NetworkManager != null)
            NetworkManager.OnRoomCreated += HandleRoomCreated;

        ApplyRole();
        OnStateChanged(RaceManager != null ? RaceManager.CurrentState : GameState.Setup);
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

        if (Role == UserRole.Professor)
        {
            if (Events != null) Events.gameObject.SetActive(isRacing);
            if (Controls != null) Controls.gameObject.SetActive(isRacing);
        }

    }
}
