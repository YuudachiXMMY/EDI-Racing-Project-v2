using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Runtime state for a spawned race car.
/// Initialized from CarData, stores dynamic attributes and tracks race progress.
/// </summary>
public class CarIdentity : MonoBehaviour
{
    [Header("Identity")]
    public string TeamName;
    public AttributeEntry[] Attributes;

    [Header("Player Ownership")]
    public bool IsOwnCar;

    [Header("Race Progress")]
    public int CurrentCheckpointIndex;
    public int TotalCheckpointsPassed;
    public int CurrentLap;
    public float CheckpointTime;

    [Header("Lap Timing")]
    // Fastest completed lap for this car (seconds). 0 = no lap completed yet.
    public float BestLapTime;
    // Sum of all completed lap times (seconds); AverageLapTime = AccumulatedLapTime / CompletedLaps.
    public float AccumulatedLapTime;
    // Number of laps for which a time was recorded (mirrors CurrentLap under normal play).
    public int CompletedLaps;
    // Time.time when the current lap started. Seed to the race start time (not 0), or the
    // first lap time would be the whole elapsed clock. Set by RaceManager on race start.
    public float LastLapStartTime;

    public void Initialize(CarData data)
    {
        TeamName = data.TeamName;
        Attributes = data.Attributes != null
            ? (AttributeEntry[])data.Attributes.Clone()
            : Array.Empty<AttributeEntry>();
        IsOwnCar = false;
        CurrentCheckpointIndex = 0;
        TotalCheckpointsPassed = 0;
        CurrentLap = 0;
        CheckpointTime = 0f;
        BestLapTime = 0f;
        AccumulatedLapTime = 0f;
        CompletedLaps = 0;
        LastLapStartTime = 0f;
    }

    /// <summary>
    /// Records the lap that just completed at <paramref name="now"/> (Time.time): accumulates
    /// its duration, updates the best lap, and resets the lap-start marker for the next lap.
    /// </summary>
    public void RecordLap(float now)
    {
        float lap = now - LastLapStartTime;
        if (lap > 0f)
        {
            AccumulatedLapTime += lap;
            if (BestLapTime <= 0f || lap < BestLapTime) BestLapTime = lap;
        }
        LastLapStartTime = now;
        CompletedLaps++;
    }

    // --- Attribute Accessors (mirror CarData) ---

    public string GetAttribute(string key, string defaultValue = "")
    {
        return Attributes.Get(key, defaultValue);
    }

    public int GetIntAttribute(string key, int defaultValue = 0)
    {
        string val = GetAttribute(key, null);
        if (val != null && int.TryParse(val, out int result)) return result;
        return defaultValue;
    }

    public bool HasAttribute(string key)
    {
        return Attributes.Has(key);
    }

    // --- Backward-Compatible Accessors ---

    public int ColorIndex => GetIntAttribute("colorIndex", 0);

    public string[] Functions
    {
        get
        {
            string val = GetAttribute("functions", "");
            if (string.IsNullOrEmpty(val)) return Array.Empty<string>();
            return val.Split('/').Select(f => f.Trim().ToLower()).Where(f => f.Length > 0).ToArray();
        }
    }

    private void Update()
    {
        CheckpointTime += Time.deltaTime;
    }
}
