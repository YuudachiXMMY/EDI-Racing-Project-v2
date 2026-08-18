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

        // Draw waypoint spheres
        Gizmos.color = Color.yellow;
        for (int i = 0; i < Waypoints.Length; i++)
        {
            if (Waypoints[i] == null) continue;
            Gizmos.DrawSphere(Waypoints[i].position, 1f);
        }

        // Draw smooth Catmull-Rom curve between waypoints
        if (Waypoints.Length < 3) return;
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.8f);
        int stepsPerSegment = 8;
        for (int i = 0; i < Waypoints.Length; i++)
        {
            int i0 = (i - 1 + Waypoints.Length) % Waypoints.Length;
            int i1 = i;
            int i2 = (i + 1) % Waypoints.Length;
            int i3 = (i + 2) % Waypoints.Length;

            if (Waypoints[i0] == null || Waypoints[i1] == null
                || Waypoints[i2] == null || Waypoints[i3] == null) continue;

            Vector3 prev = Waypoints[i1].position;
            for (int s = 1; s <= stepsPerSegment; s++)
            {
                float t = (float)s / stepsPerSegment;
                float t2 = t * t;
                float t3 = t2 * t;
                Vector3 p = 0.5f * (
                    (2f * Waypoints[i1].position) +
                    (-Waypoints[i0].position + Waypoints[i2].position) * t +
                    (2f * Waypoints[i0].position - 5f * Waypoints[i1].position
                        + 4f * Waypoints[i2].position - Waypoints[i3].position) * t2 +
                    (-Waypoints[i0].position + 3f * Waypoints[i1].position
                        - 3f * Waypoints[i2].position + Waypoints[i3].position) * t3
                );
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
