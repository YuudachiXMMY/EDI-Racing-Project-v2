using UnityEngine;

/// <summary>
/// Marks a predefined camera position in the scene.
/// CameraManager discovers these at startup and maps F1-F9 to them.
/// </summary>
public class FixedCameraPoint : MonoBehaviour
{
    [Tooltip("Index 0-8, maps to F1-F9 keys")]
    public int PointIndex;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);
        Gizmos.DrawRay(transform.position, transform.forward * 3f);
    }
}
