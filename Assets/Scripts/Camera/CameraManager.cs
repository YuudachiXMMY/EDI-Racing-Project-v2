using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Switches between Free, Fixed, Spectator, and the two Auto Cam modes.
/// Professor uses Free + Fixed (F1-F9) plus Auto Cam (the 'C' hotkey / Auto Cam button), which
/// flips between AutoTopCars (chase cam cycling the top 3) and AutoAllCams (park at the fixed
/// camera second-closest to the leader, aimed at it). Esc/F1-F9 exit Auto Cam. Student uses Spectator.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("Cameras")]
    public RaceCameraController FreeCamera;
    public SpectatorCamera SpectatorCam;

    [Header("Fixed Points")]
    [Tooltip("Discovered at startup via FindObjectsByType")]
    public FixedCameraPoint[] FixedPoints;

    [Header("Auto Cam")]
    [Tooltip("How many top-ranked cars the AutoTopCars chase cam cycles through")]
    public int AutoSwitchCarCount = 3;

    [Tooltip("Key the professor presses to toggle / flip the Auto Cam modes")]
    public Key AutoSwitchKey = Key.C;

    // Free/Fixed/Spectator: the original three modes. The two Auto Cam modes both reuse SpectatorCam:
    //   AutoTopCars  — chase cam cycling the top-N cars.
    //   AutoAllCams  — park at the fixed camera second-closest to the leader, aimed at it.
    public enum CameraMode { Free, Fixed, Spectator, AutoTopCars, AutoAllCams }

    public CameraMode CurrentMode { get; private set; } = CameraMode.Free;

    private Camera mainCamera;

    private void Start()
    {
        if (FixedPoints == null || FixedPoints.Length == 0)
            FixedPoints = FindObjectsByType<FixedCameraPoint>(FindObjectsSortMode.None);

        // Order by PointIndex so AutoAllCams cuts through the cameras in the same F1→F9 order the
        // professor sees, instead of the arbitrary FindObjectsByType order.
        if (FixedPoints != null)
            System.Array.Sort(FixedPoints, (a, b) => a.PointIndex.CompareTo(b.PointIndex));

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
    /// Cycle the Auto Cam modes. Entering from Free/Fixed starts on AutoTopCars (top-3 chase);
    /// once in an Auto Cam mode it flips to the other. It never turns Auto Cam off — use Esc or
    /// F1-F9 for that. Safe to call from a UI button or the keyboard hotkey.
    /// </summary>
    public void ToggleAutoSwitch()
    {
        SetMode(CurrentMode == CameraMode.AutoTopCars ? CameraMode.AutoAllCams : CameraMode.AutoTopCars);
    }

    public void SetMode(CameraMode mode, int fixedIndex = 0)
    {
        CurrentMode = mode;

        bool isFree = mode == CameraMode.Free;
        // Spectator (student leader-follow) and both Auto Cam modes all drive the SpectatorCamera
        // component — they differ only in its follow behaviour, configured below.
        bool useSpectatorCam = mode == CameraMode.Spectator
                               || mode == CameraMode.AutoTopCars
                               || mode == CameraMode.AutoAllCams;

        if (FreeCamera != null) FreeCamera.enabled = isFree;
        if (SpectatorCam != null)
        {
            if (mode == CameraMode.AutoTopCars)
            {
                SpectatorCam.FixedPoints = FixedPoints;
                SpectatorCam.SetFollowMode(SpectatorCamera.FollowMode.ChaseTopN, Mathf.Max(1, AutoSwitchCarCount));
            }
            else if (mode == CameraMode.AutoAllCams)
            {
                SpectatorCam.FixedPoints = FixedPoints;
                SpectatorCam.SetFollowMode(SpectatorCamera.FollowMode.FixedPointsOnLeader, 1);
            }
            else if (mode == CameraMode.Spectator)
            {
                SpectatorCam.SetFollowMode(SpectatorCamera.FollowMode.ChaseTopN, 1);
            }
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
