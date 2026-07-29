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

    /// <summary>
    /// Set role from network state. Called by NetworkSync or external code.
    /// </summary>
    public void SetRoleFromNetwork(bool isHost)
    {
        Role = isHost ? UserRole.Professor : UserRole.Student;
        ApplyRole();
        OnStateChanged(RaceManager != null ? RaceManager.CurrentState : GameState.Setup);
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

        if (Setup != null) Setup.gameObject.SetActive(isSetup);
        if (Leaderboard != null) Leaderboard.gameObject.SetActive(isRacing);

        if (Role == UserRole.Professor)
        {
            if (Events != null) Events.gameObject.SetActive(isRacing);
            if (Controls != null) Controls.gameObject.SetActive(isRacing);
        }

    }
}
