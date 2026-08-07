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
        // LeaderCheckInterval. FixedPointsOnLeader with no points falls back to leader-follow.
        bool cycling = (Mode == FollowMode.ChaseTopN && FollowCount > 1)
                       || (Mode == FollowMode.FixedPointsOnLeader && HasFixedPoints);
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

        if (Mode == FollowMode.FixedPointsOnLeader && HasFixedPoints)
        {
            // Broadcast cut: snap to the current fixed camera, then continuously aim at the leader.
            Transform cam = GetCurrentFixedPoint();
            if (cam != null) transform.position = cam.position;
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

    private Transform GetCurrentFixedPoint()
    {
        if (!HasFixedPoints) return null;
        FixedCameraPoint point = FixedPoints[cycleIndex % FixedPoints.Length];
        return point != null ? point.transform : null;
    }
}
