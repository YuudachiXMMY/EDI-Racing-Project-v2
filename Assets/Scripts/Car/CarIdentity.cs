using UnityEngine;

/// <summary>
/// Runtime state for a spawned race car.
/// Initialized from CarData, tracks race progress.
/// </summary>
public class CarIdentity : MonoBehaviour
{
    [Header("Identity")]
    public string TeamName;
    public int ColorIndex;
    public string[] Functions;

    [Header("Race Progress")]
    public int CurrentCheckpointIndex;
    public int TotalCheckpointsPassed;
    public int CurrentLap;
    public float CheckpointTime;

    public void Initialize(CarData data)
    {
        TeamName = data.TeamName;
        ColorIndex = data.ColorIndex;
        Functions = data.Functions;
        CurrentCheckpointIndex = 0;
        TotalCheckpointsPassed = 0;
        CurrentLap = 0;
        CheckpointTime = 0f;
    }

    private void Update()
    {
        CheckpointTime += Time.deltaTime;
    }
}
