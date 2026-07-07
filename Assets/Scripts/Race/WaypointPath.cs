using UnityEngine;

/// <summary>
/// Holds ordered waypoints defining the racing line.
/// Wraps around for looping track. Draws gizmos in editor.
/// </summary>
public class WaypointPath : MonoBehaviour
{
    public Transform[] Waypoints;

    public Transform GetWaypoint(int index)
    {
        return Waypoints[index % Waypoints.Length];
    }

    public int Count => Waypoints.Length;

    private void OnDrawGizmos()
    {
        if (Waypoints == null || Waypoints.Length < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < Waypoints.Length; i++)
        {
            if (Waypoints[i] == null) continue;
            Gizmos.DrawSphere(Waypoints[i].position, 1f);
            var next = Waypoints[(i + 1) % Waypoints.Length];
            if (next != null)
                Gizmos.DrawLine(Waypoints[i].position, next.position);
        }
    }
}
