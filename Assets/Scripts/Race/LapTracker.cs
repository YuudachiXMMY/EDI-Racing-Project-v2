using UnityEngine;
using System;

/// <summary>
/// Tracks checkpoint progress and lap completion for all cars.
/// Enforces sequential checkpoint passage to prevent shortcuts.
/// Auto-detects checkpoint count from scene at startup.
/// </summary>
public class LapTracker : MonoBehaviour
{
    private int totalCheckpoints;

    public event Action<CarIdentity> OnLapCompleted;
    public event Action<CarIdentity, int> OnCheckpointPassed;

    private void Start()
    {
        var checkpoints = FindObjectsByType<CheckpointTrigger>(FindObjectsSortMode.None);
        totalCheckpoints = checkpoints.Length;
        if (totalCheckpoints == 0)
            Debug.LogError("[LapTracker] No CheckpointTriggers found in scene!");
        else
            Debug.Log($"[LapTracker] Detected {totalCheckpoints} checkpoints");
    }

    public void OnCarPassedCheckpoint(CarIdentity car, int checkpointIndex)
    {
        int expectedIndex = car.CurrentCheckpointIndex % totalCheckpoints;
        if (checkpointIndex != expectedIndex) return;

        car.TotalCheckpointsPassed++;
        car.CurrentCheckpointIndex++;
        car.CheckpointTime = 0f;

        OnCheckpointPassed?.Invoke(car, checkpointIndex);

        if (car.CurrentCheckpointIndex % totalCheckpoints == 0)
        {
            car.CurrentLap++;
            OnLapCompleted?.Invoke(car);
        }
    }
}
