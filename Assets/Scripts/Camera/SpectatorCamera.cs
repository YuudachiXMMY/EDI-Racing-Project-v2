using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Auto-follow camera for student spectator view.
/// Tracks the current race leader with smooth transitions.
/// When <see cref="FollowCount"/> > 1 it becomes an auto-switching chase cam that
/// cycles through the top-N ranked cars (professor "Auto Cam" mode).
/// </summary>
public class SpectatorCamera : MonoBehaviour
{
    [Header("References")]
    public ScoreManager ScoreManager;

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
    [Tooltip("How many top-ranked cars to cycle through. 1 = follow the leader only; " +
             "set to 3 for the professor's auto-switching top-3 chase cam.")]
    public int FollowCount = 1;

    [Tooltip("Seconds to hold on each car before switching to the next (only used when FollowCount > 1)")]
    public float CycleInterval = 4f;

    private Transform currentTarget;
    private Vector3 velocity;
    private float leaderCheckTimer;
    private int cycleIndex;

    // Reset the cycle whenever the camera is (re)enabled so it always starts from the leader
    // and re-picks a target immediately instead of drifting from a stale one.
    private void OnEnable()
    {
        cycleIndex = 0;
        leaderCheckTimer = 0f;
        currentTarget = null;
    }

    private void LateUpdate()
    {
        // Cycle faster through the top-N field in auto-switch mode; otherwise just re-check the leader.
        float switchInterval = FollowCount > 1 ? CycleInterval : LeaderCheckInterval;

        leaderCheckTimer += Time.unscaledDeltaTime;
        bool timedSwitch = leaderCheckTimer >= switchInterval;
        if (timedSwitch || currentTarget == null)
        {
            leaderCheckTimer = 0f;
            // Only advance the cycle on a genuine timed switch — a null target (first frame or a
            // despawned car) should re-pick the current slot, not skip ahead, so auto-switch always
            // opens on the leader.
            if (timedSwitch && FollowCount > 1) cycleIndex++;
            UpdateTarget();
        }

        if (currentTarget == null) return;

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

        // FollowCount == 1: always the leader. FollowCount > 1: cycle through the top-N field,
        // clamped to the number of cars actually racing so a small grid never lands on an empty slot.
        int poolSize = Mathf.Clamp(FollowCount, 1, ranked.Count);
        int index = poolSize > 1 ? cycleIndex % poolSize : 0;

        Transform target = ranked[index].transform;
        if (target != null)
            currentTarget = target;
    }
}
