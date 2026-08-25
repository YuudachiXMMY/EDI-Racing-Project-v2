using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Switches between Free, Fixed, Spectator, the two Auto Cam modes, and FollowCar.
/// Professor uses Free + Fixed (F1-F9) plus Auto Cam (the 'C' hotkey / Auto Cam button), which
/// flips between AutoTopCars (chase cam cycling the top 3) and AutoAllCams (park at the fixed
/// camera second-closest to the leader, aimed at it). Esc/F1-F9 exit Auto Cam.
/// The student spectator now starts in AutoTopCars (broadcast auto cam) rather than the plain
/// Spectator leader-follow, and <see cref="AllowFreeControl"/> is turned off for them so the F/C
/// keys don't fly the camera. Either role can enter FollowCar (chase one car) via
/// <see cref="FollowCar"/>, driven by the full-screen leaderboard click-to-follow.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("Cameras")]
    public RaceCameraController FreeCamera;
    public SpectatorCamera SpectatorCam;

    [Tooltip("When false, the professor spatial keys (Auto Cam 'C', F1-F9 fixed cams) are ignored. " +
             "RaceUI turns this off for students so a spectator can't fly the broadcast camera; Esc " +
             "still returns them to the auto camera.")]
    public bool AllowFreeControl = true;

    [Header("Fixed Points")]
    [Tooltip("Discovered at startup via FindObjectsByType")]
    public FixedCameraPoint[] FixedPoints;

    [Header("Auto Cam")]
    [Tooltip("How many top-ranked cars the AutoTopCars chase cam cycles through")]
    public int AutoSwitchCarCount = 3;

    [Tooltip("Key the professor presses to toggle / flip the Auto Cam modes")]
    public Key AutoSwitchKey = Key.C;

    // Free/Fixed/Spectator: the original three modes. The Auto Cam modes and FollowCar all reuse
    // SpectatorCam, differing only in its follow behaviour:
    //   AutoTopCars  — chase cam cycling the top-N cars.
    //   AutoAllCams  — park at the fixed camera second-closest to the leader, aimed at it.
    //   FollowCar    — chase one specific car chosen via FollowCar(Transform) (click-to-follow).
    public enum CameraMode { Free, Fixed, Spectator, AutoTopCars, AutoAllCams, FollowCar }

    public CameraMode CurrentMode { get; private set; } = CameraMode.Free;

    private Camera mainCamera;

    // Self-initialize in Awake, not Start: the default SetMode(Free) must run BEFORE any consumer's
    // Start() sets the role camera. RaceUI.ApplyRole() and StudentJoinBootstrap.LockAsStudent() both
    // set the mode in Start(); cross-GameObject Start() order is undefined, so if this ran in Start()
    // it could clobber the student's AutoTopCars with Free (leaving them in free-fly). Awake always
    // precedes every Start(), so the consumer's mode deterministically wins.
    private void Awake()
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

        // Escape is available to everyone and returns to the role-appropriate default: the professor
        // drops back to free cam, a student (no free control) back to the auto broadcast camera. This
        // is a student's one way out of a click-to-follow FollowCar shot.
        if (Keyboard.current[Key.Escape].wasPressedThisFrame)
        {
            SetMode(ModeForEscape(AllowFreeControl));
            return;
        }

        // The remaining keys are professor spatial control. Students have AllowFreeControl == false,
        // so they can't toggle Auto Cam sub-modes or jump to fixed cameras.
        if (!AllowFreeControl) return;

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
    }

    /// <summary>
    /// Camera mode the Escape key returns to: the professor drops to free cam, a student (no free
    /// control) to the auto broadcast camera (AutoTopCars) — a student's one way out of a
    /// click-to-follow shot. Pure so the Esc rule is unit-testable and shared with <see cref="Update"/>.
    /// </summary>
    public static CameraMode ModeForEscape(bool allowFreeControl)
        => allowFreeControl ? CameraMode.Free : CameraMode.AutoTopCars;

    /// <summary>
    /// Enter FollowCar: chase one specific car in 3rd person (full-screen leaderboard click-to-follow,
    /// both roles). No-op on a null target. Does NOT route through <see cref="ToggleAutoSwitch"/> — the
    /// target is applied here immediately after the mode switch enables the spectator camera.
    /// </summary>
    public void FollowCar(Transform target)
    {
        if (target == null) return;
        SetMode(CameraMode.FollowCar);
        if (SpectatorCam != null) SpectatorCam.SetFollowTarget(target);
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
        // Spectator (leader-follow), both Auto Cam modes, and FollowCar all drive the SpectatorCamera
        // component — they differ only in its follow behaviour. AutoTopCars/AutoAllCams/Spectator are
        // configured below; FollowCar's target is set by FollowCar() right after this call returns.
        bool useSpectatorCam = mode == CameraMode.Spectator
                               || mode == CameraMode.AutoTopCars
                               || mode == CameraMode.AutoAllCams
                               || mode == CameraMode.FollowCar;

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
