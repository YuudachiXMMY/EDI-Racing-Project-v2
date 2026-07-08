using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Switches between Free, Fixed, and Spectator camera modes.
/// Professor uses Free + Fixed (F1-F9); Student uses Spectator.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("Cameras")]
    public RaceCameraController FreeCamera;
    public SpectatorCamera SpectatorCam;

    [Header("Fixed Points")]
    [Tooltip("Discovered at startup via FindObjectsByType")]
    public FixedCameraPoint[] FixedPoints;

    public enum CameraMode { Free, Fixed, Spectator }

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

    public void SetMode(CameraMode mode, int fixedIndex = 0)
    {
        CurrentMode = mode;

        bool isFree = mode == CameraMode.Free;
        bool isSpectator = mode == CameraMode.Spectator;

        if (FreeCamera != null) FreeCamera.enabled = isFree;
        if (SpectatorCam != null) SpectatorCam.enabled = isSpectator;

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
