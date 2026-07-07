using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Serializable data containers for session JSON persistence.
/// Used by SessionManager to save/load race sessions.
/// </summary>

/// <summary>
/// Top-level session container. Captures all race inputs and outputs.
/// </summary>
[Serializable]
public class SessionData
{
    public string SessionName = "";
    public string CreatedAt = "";
    public CarData[] Cars = Array.Empty<CarData>();
    public SavedEventConfig[] Events = Array.Empty<SavedEventConfig>();
    public SavedRaceConfig RaceSettings;
    public RaceResults Results = new RaceResults();
}

/// <summary>
/// Snapshot of RaceConfig ScriptableObject values.
/// ScriptableObjects serialize as asset references, not inline values,
/// so we capture the numeric parameters into a plain struct.
/// </summary>
[Serializable]
public struct SavedRaceConfig
{
    public float DefaultSpeed;
    public float AngularSpeed;
    public float Acceleration;
    public int TotalLaps;
    public float CarScale;

    public static SavedRaceConfig FromScriptableObject(RaceConfig config)
    {
        return new SavedRaceConfig
        {
            DefaultSpeed = config.DefaultSpeed,
            AngularSpeed = config.AngularSpeed,
            Acceleration = config.Acceleration,
            TotalLaps = config.TotalLaps,
            CarScale = config.CarScale
        };
    }

    public void ApplyTo(RaceConfig config)
    {
        config.DefaultSpeed = DefaultSpeed;
        config.AngularSpeed = AngularSpeed;
        config.Acceleration = Acceleration;
        config.TotalLaps = TotalLaps;
        config.CarScale = CarScale;
    }
}

/// <summary>
/// Serializable copy of RaceEventConfig without runtime state (HasBeenTriggered)
/// and without Key binding (UI concern, not data).
/// </summary>
[Serializable]
public struct SavedEventConfig
{
    public int EventType;
    public string DisplayName;
    public float SpeedDelta;
    public float Duration;
    public int TargetColorIndex;
    public string TargetFunction;
    public int NameLengthThreshold;
    public bool AllowRepeat;

    public static SavedEventConfig FromConfig(RaceEventConfig config)
    {
        return new SavedEventConfig
        {
            EventType = (int)config.EventType,
            DisplayName = config.DisplayName ?? "",
            SpeedDelta = config.SpeedDelta,
            Duration = config.Duration,
            TargetColorIndex = config.TargetColorIndex,
            TargetFunction = config.TargetFunction ?? "",
            NameLengthThreshold = config.NameLengthThreshold,
            AllowRepeat = config.AllowRepeat
        };
    }

    public RaceEventConfig ToConfig(Key triggerKey)
    {
        return new RaceEventConfig
        {
            EventType = (RaceEventType)EventType,
            DisplayName = DisplayName,
            SpeedDelta = SpeedDelta,
            Duration = Duration,
            TargetColorIndex = TargetColorIndex,
            TargetFunction = TargetFunction,
            NameLengthThreshold = NameLengthThreshold,
            TriggerKey = triggerKey,
            AllowRepeat = AllowRepeat,
            HasBeenTriggered = false
        };
    }
}

/// <summary>
/// Race outcome data. Collected on-demand or at race completion.
/// </summary>
[Serializable]
public class RaceResults
{
    public CarResult[] Rankings = Array.Empty<CarResult>();
    public EventLogEntry[] EventLog = Array.Empty<EventLogEntry>();
    public float TotalRaceTime;
}

/// <summary>
/// Individual car's standing at time of collection.
/// </summary>
[Serializable]
public struct CarResult
{
    public int Rank;
    public string TeamName;
    public int ColorIndex;
    public int LapsCompleted;
    public int CheckpointsPassed;
    public float TotalTime;
}

/// <summary>
/// Record of a single triggered event during a race.
/// </summary>
[Serializable]
public struct EventLogEntry
{
    public float Timestamp;
    public string EventName;
    public int AffectedCount;
    public int TotalCars;
}
