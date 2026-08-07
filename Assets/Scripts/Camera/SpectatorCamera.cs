using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Auto-follow camera for student spectator view and the professor "Auto Cam" modes.
/// Two behaviours, selected by <see cref="Mode"/>:
///   • ChaseTopN — trails behind and looks at the target car. FollowCount == 1 tracks the
///     leader (student view); FollowCount > 1 cycles through the top-N cars (professor top-3).
///   • FixedPointsOnLeader — parks at each scene FixedCameraPoint in turn, always aimed at the
///     leader (professor "all cams on leader" broadcast mode).
/// </summary>
public class SpectatorCamera : MonoBehaviour
{
    public enum FollowMode { ChaseTopN, FixedPointsOnLeader }

    [Header("References")]
    public ScoreManager ScoreManager;

    [Tooltip("Fixed camera points used by FixedPointsOnLeader mode. Assigned by CameraManager.")]
    public FixedCameraPoint[] FixedPoints;

    [Header("Follow Settings")]
    [Tooltip("Distance behind the target car")]
    public float FollowDistance = 15f;

    [Tooltip("Height above the target car")]
    public float FollowHeight = 8f;

    [Tooltip("Smooth damp time for position")]
    public float SmoothTime = 0.5f;

    [Tooltip("How often (seconds) to check for leader change")]
    public float LeaderCheckInterval = 3f;

    [Header("Auto-Switch")]
    [Tooltip("Which follow behaviour to use. Set by CameraManager for the professor's Auto Cam modes.")]
    public FollowMode Mode = FollowMode.ChaseTopN;

    [Tooltip("ChaseTopN: how many top-ranked cars to cycle through. 1 = follow the leader only; " +
             "set to 3 for the professor's auto-switching top-3 chase cam.")]
    public int FollowCount = 1;

    [Tooltip("Seconds to hold on each car / fixed camera before switching to the next (cycling modes only)")]
    public float CycleInterval = 4f;

    private Transform currentTarget;
    private Vector3 velocity;
    private float leaderCheckTimer;
    private int cycleIndex;

    /// <summary>
    /// Switch follow behaviour and restart the cycle cleanly (first car / first camera, immediate
    /// re-pick). Called by CameraManager when entering or switching Auto Cam sub-modes.
    /// </summary>
    public void SetFollowMode(FollowMode mode, int followCount)
    {
        Mode = mode;
        FollowCount = followCount;
        ResetCycle();
    }

    // Reset the cycle whenever the camera is (re)enabled so it always starts from the leader / first
    // camera and re-picks a target immediately instead of drifting from a stale one.
    private void OnEnable() => ResetCycle();

    private void ResetCycle()
    {
        cycleIndex = 0;
        leaderCheckTimer = 0f;
        currentTarget = null;
    }

    private bool HasFixedPoints => FixedPoints != null && FixedPoints.Length > 0;

    private void LateUpdate()
    {
        // Cycling modes advance on CycleInterval; a plain leader-follow just re-checks on the slower
        // LeaderCheckInterval. FixedPointsOnLeader always cycles — it cuts between placed fixed
        // cameras, or (when too few are placed) orbits the leader from several angles.
        bool cycling = (Mode == FollowMode.ChaseTopN && FollowCount > 1)
                       || (Mode == FollowMode.FixedPointsOnLeader);
        float switchInterval = cycling ? CycleInterval : LeaderCheckInterval;

        leaderCheckTimer += Time.unscaledDeltaTime;
        bool timedSwitch = leaderCheckTimer >= switchInterval;
        if (timedSwitch || currentTarget == null)
        {
            leaderCheckTimer = 0f;
            // Only advance the cycle on a genuine timed switch — a null target (first frame or a
            // despawned car) should re-pick the current slot, not skip ahead, so cycling always
            // opens on the first car / first camera.
            if (timedSwitch && cycling) cycleIndex++;
            UpdateTarget();
        }

        if (currentTarget == null) return;

        if (Mode == FollowMode.FixedPointsOnLeader)
        {
            // Broadcast cut: jump to the current shot, then continuously aim at the leader.
            // Prefer real placed fixed cameras; if fewer than two are placed (the default scene
            // leaves FixedCam_F1..F9 stacked at the origin), orbit the leader so the cut is visible.
            Transform cam = GetCurrentUsableFixedPoint();
            transform.position = cam != null ? cam.position : GetOrbitPosition(currentTarget);
            transform.LookAt(currentTarget.position + Vector3.up * 2f);
            return;
        }

        // Chase cam: smoothly trail behind the target car and look at it.
        Vector3 targetPos = currentTarget.position
                            - currentTarget.forward * FollowDistance
                            + Vector3.up * FollowHeight;

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, SmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        transform.LookAt(currentTarget.position + Vector3.up * 2f);
    }

    private void UpdateTarget()
    {
        if (ScoreManager == null) return;

        List<CarIdentity> ranked = ScoreManager.GetRankedCars();
        if (ranked.Count == 0) return;

        // FixedPointsOnLeader always looks at the leader; cycleIndex selects the camera, not the car.
        if (Mode == FollowMode.FixedPointsOnLeader)
        {
            Transform leader = ranked[0].transform;
            if (leader != null) currentTarget = leader;
            return;
        }

        // ChaseTopN — FollowCount == 1: always the leader. FollowCount > 1: cycle through the top-N
        // field, clamped to the number of cars racing so a small grid never lands on an empty slot.
        int poolSize = Mathf.Clamp(FollowCount, 1, ranked.Count);
        int index = poolSize > 1 ? cycleIndex % poolSize : 0;

        Transform target = ranked[index].transform;
        if (target != null)
            currentTarget = target;
    }

    // A fixed camera counts only once it has actually been positioned in the scene. Setup Track
    // spawns FixedCam_F1..F9 at the origin as placeholders, so ignore any still sitting there —
    // otherwise the "cut" would jump between identical origin shots and look frozen.
    private static bool IsUsable(FixedCameraPoint p)
    {
        return p != null && p.transform.position.sqrMagnitude > 0.25f;
    }

    // Returns the current placed fixed camera, or null when fewer than two are placed (in which
    // case the caller orbits the leader instead — a single or zero placed cam can't "switch").
    private Transform GetCurrentUsableFixedPoint()
    {
        if (!HasFixedPoints) return null;

        int usable = 0;
        for (int i = 0; i < FixedPoints.Length; i++)
            if (IsUsable(FixedPoints[i])) usable++;
        if (usable < 2) return null;

        int target = cycleIndex % usable;
        int seen = 0;
        for (int i = 0; i < FixedPoints.Length; i++)
        {
            if (!IsUsable(FixedPoints[i])) continue;
            if (seen == target) return FixedPoints[i].transform;
            seen++;
        }
        return null;
    }

    // Fallback broadcast rig: four evenly-spaced positions orbiting the leader (reusing the chase
    // distance/height), so each cut shows the leader from a distinctly different angle.
    private Vector3 GetOrbitPosition(Transform target)
    {
        const int shots = 4;
        int i = cycleIndex % shots;
        float angle = i * (360f / shots);
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.back;
        return target.position + dir * FollowDistance + Vector3.up * FollowHeight;
    }
}
