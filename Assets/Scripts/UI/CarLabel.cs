using UnityEngine;

/// <summary>
/// World-space floating team name label above a car.
/// Billboards toward the currently active camera (any angle) with distance-based culling.
/// </summary>
public class CarLabel : MonoBehaviour
{
    [Tooltip("Vertical offset above the car pivot")]
    public float HeightOffset = 4f;

    [Tooltip("Labels beyond this distance from camera are hidden")]
    public float MaxVisibleDistance = 80f;

    private Transform target;
    private Transform cam;
    private Canvas canvas;

    public void Initialize(Transform carTransform)
    {
        target = carTransform;
        cam = Camera.main != null ? Camera.main.transform : null;
        canvas = GetComponent<Canvas>();
    }

    /// <summary>
    /// Rotation that makes a world-space label read correctly toward the camera from any angle.
    /// A UGUI canvas reads right when its forward (+Z) points the SAME way the camera looks, i.e.
    /// AWAY from the camera — pointing +Z toward the camera mirrors the text. Pure and
    /// deterministic so the billboard math is unit-testable without a live camera. Falls back to
    /// identity when the label sits on top of the camera (degenerate dir).
    /// </summary>
    public static Quaternion ComputeFacingRotation(Vector3 labelPos, Vector3 camPos, Vector3 camUp)
    {
        Vector3 lookDir = labelPos - camPos;               // +Z away from camera → text not mirrored
        if (lookDir.sqrMagnitude < 0.0001f) return Quaternion.identity;
        return Quaternion.LookRotation(lookDir, camUp);    // full 3D, faces camera from any angle
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + Vector3.up * HeightOffset;

        Transform activeCam = ResolveActiveCamera();
        if (activeCam == null) return;

        // Distance culling — disable Canvas to eliminate draw calls when far away.
        float sqrDist = (activeCam.position - transform.position).sqrMagnitude;
        bool visible = sqrDist < MaxVisibleDistance * MaxVisibleDistance;
        if (canvas != null && canvas.enabled != visible)
            canvas.enabled = visible;

        // Face the active camera from any angle, every visible frame, so a mode switch or a
        // fast AutoCam cut never leaves a label edge-on or aimed at the previous camera.
        if (visible)
            transform.rotation = ComputeFacingRotation(transform.position, activeCam.position, activeCam.up);
    }

    // Reuse the cached camera while it is the live active one; re-resolve when it has been
    // disabled or destroyed (camera-mode switch, or a future second camera taking the tag).
    private Transform ResolveActiveCamera()
    {
        if (cam != null && cam.gameObject.activeInHierarchy)
        {
            var c = cam.GetComponent<Camera>();
            if (c == null || c.isActiveAndEnabled) return cam;
        }
        cam = Camera.main != null ? Camera.main.transform : null;
        return cam;
    }
}
