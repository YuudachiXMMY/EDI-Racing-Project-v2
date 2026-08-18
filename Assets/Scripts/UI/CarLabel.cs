using UnityEngine;

/// <summary>
/// World-space floating team name label above a car.
/// Billboards toward the camera with distance-based culling
/// and throttled rotation updates for performance.
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
    private int frameCounter;
    private int staggerOffset;

    public void Initialize(Transform carTransform)
    {
        target = carTransform;
        cam = Camera.main != null ? Camera.main.transform : null;
        canvas = GetComponent<Canvas>();
        staggerOffset = GetInstanceID() % 4;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + Vector3.up * HeightOffset;

        if (cam == null) cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
        {
            // Distance culling — disable Canvas to eliminate draw calls
            float sqrDist = (cam.position - transform.position).sqrMagnitude;
            bool visible = sqrDist < MaxVisibleDistance * MaxVisibleDistance;
            if (canvas != null && canvas.enabled != visible)
                canvas.enabled = visible;

            // Billboard only every 4th frame (rotation change is subtle)
            frameCounter++;
            if (visible && (frameCounter + staggerOffset) % 4 == 0)
            {
                Vector3 lookDir = cam.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }
}
