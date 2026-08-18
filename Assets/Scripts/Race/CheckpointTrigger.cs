using UnityEngine;

/// <summary>
/// Placed on checkpoint colliders along the track.
/// Detects car passage and reports to LapTracker.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Index of this checkpoint in the track sequence (0-based)")]
    public int CheckpointIndex;

    private LapTracker lapTracker;

    private void Start()
    {
        lapTracker = FindFirstObjectByType<LapTracker>();
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var carIdentity = other.GetComponentInParent<CarIdentity>();
        if (carIdentity == null) return;
        lapTracker?.OnCarPassedCheckpoint(carIdentity, CheckpointIndex);
    }
}
