using UnityEngine;

/// <summary>
/// World-space floating team name label above a car.
/// Billboards toward the camera each frame.
/// </summary>
public class CarLabel : MonoBehaviour
{
    [Tooltip("Vertical offset above the car pivot")]
    public float HeightOffset = 4f;

    private Transform target;
    private Transform cam;

    public void Initialize(Transform carTransform)
    {
        target = carTransform;
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + Vector3.up * HeightOffset;

        // Billboard: face camera (y-rotation only to prevent tilt)
        if (cam == null) cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
        {
            Vector3 lookDir = cam.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }
}
