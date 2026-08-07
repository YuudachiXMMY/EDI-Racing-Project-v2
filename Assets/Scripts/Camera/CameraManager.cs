using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Switches between Free, Fixed, Spectator, and AutoSwitch camera modes.
/// Professor uses Free + Fixed (F1-F9) plus AutoSwitch (the 'C' hotkey / Auto Cam button),
/// an auto-switching chase cam that cycles through the top-3 cars; Student uses Spectator.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("Cameras")]
    public RaceCameraController FreeCamera;
    public SpectatorCamera SpectatorCam;

    [Header("Fixed Points")]
    [Tooltip("Discovered at startup via FindObjectsByType")]
    public FixedCameraPoint[] FixedPoints;

    [Header("Auto-Switch (top-3 chase cam)")]
    [Tooltip("How many top-ranked cars the auto-switch camera cycles through")]
    public int AutoSwitchCarCount = 3;

    [Tooltip("Key the professor presses to toggle the auto-switch top-3 chase cam")]
    public Key AutoSwitchKey = Key.C;

    // Free/Fixed/Spectator: the original three modes. AutoSwitch reuses SpectatorCam but tells it
    // to cycle through the top-N field instead of locking onto the leader.
    public enum CameraMode { Free, Fixed, Spectator, AutoSwitch }

    public CameraMode CurrentMode { get; private set; } = CameraMode.Free;

    private Camera mainCamera;

    private void Start()
    {
        if (FixedPoints == null || FixedPoints.Length == 0)
            FixedPoints = FindObjectsByType<FixedCameraPoint>(FindObjectsSortMode.None);

        mainCamera = Camera.main;
        SetMode(CameraMode.Free);
    }

    private void Update()
    {
        if (CurrentMode == CameraMode.Spectator) return;
        if (Keyboard.current == null) return;

        // Toggle the auto-switching top-3 chase cam (professor hotkey, default 'C').
        if (Keyboard.current[AutoSwitchKey].wasPressedThisFrame)
        {
            ToggleAutoSwitch();
            return;
        }

        // F1-F9 for fixed positions
        for (int i = 0; i < 9; i++)
        {
            Key fKey = Key.F1 + i;
            if (Keyboard.current[fKey].wasPressedThisFrame)
            {
                SetMode(CameraMode.Fixed, i);
                return;
            }
        }

        // Escape returns to free camera
        if (Keyboard.current[Key.Escape].wasPressedThisFrame)
        {
            SetMode(CameraMode.Free);
        }
    }

    /// <summary>
    /// Toggle the auto-switching top-3 chase cam on/off. Turning it off returns to the free camera.
    /// Safe to call from a UI button or the keyboard hotkey.
    /// </summary>
    public void ToggleAutoSwitch()
    {
        SetMode(CurrentMode == CameraMode.AutoSwitch ? CameraMode.Free : CameraMode.AutoSwitch);
    }

    public void SetMode(CameraMode mode, int fixedIndex = 0)
    {
        CurrentMode = mode;

        bool isFree = mode == CameraMode.Free;
        // Both Spectator (student leader-follow) and AutoSwitch (professor top-N cycle) drive the
        // SpectatorCamera component — they differ only in how many cars it cycles through.
        bool useSpectatorCam = mode == CameraMode.Spectator || mode == CameraMode.AutoSwitch;

        if (FreeCamera != null) FreeCamera.enabled = isFree;
        if (SpectatorCam != null)
        {
            SpectatorCam.FollowCount = mode == CameraMode.AutoSwitch ? Mathf.Max(1, AutoSwitchCarCount) : 1;
            SpectatorCam.enabled = useSpectatorCam;
        }

        if (mode == CameraMode.Fixed)
        {
            FixedCameraPoint point = FindFixedPoint(fixedIndex);
            if (point != null && mainCamera != null)
            {
                mainCamera.transform.position = point.transform.position;
                mainCamera.transform.rotation = point.transform.rotation;
            }
            if (FreeCamera != null) FreeCamera.enabled = false;
        }
    }

    private FixedCameraPoint FindFixedPoint(int index)
    {
        if (FixedPoints == null) return null;
        foreach (var point in FixedPoints)
        {
            if (point != null && point.PointIndex == index)
                return point;
        }
        return null;
    }
}
