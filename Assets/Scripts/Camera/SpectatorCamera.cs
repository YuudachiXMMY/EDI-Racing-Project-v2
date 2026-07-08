using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Auto-follow camera for student spectator view.
/// Tracks the current race leader with smooth transitions.
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

    private Transform currentTarget;
    private Vector3 velocity;
    private float leaderCheckTimer;

    private void LateUpdate()
    {
        leaderCheckTimer += Time.unscaledDeltaTime;
        if (leaderCheckTimer >= LeaderCheckInterval || currentTarget == null)
        {
            leaderCheckTimer = 0f;
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

        // Follow the leader (first in ranked list)
        Transform leader = ranked[0].transform;
        if (leader != null)
            currentTarget = leader;
    }
}
