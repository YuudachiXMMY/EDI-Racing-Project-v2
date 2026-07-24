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
    public string SurveyConfigName = "";
    public SurveyConfig SurveyConfig;
    public CarData[] Cars = Array.Empty<CarData>();
    public SavedEventRule[] Events = Array.Empty<SavedEventRule>();
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
/// Serializable copy of RuleCondition for JSON persistence.
/// </summary>
[Serializable]
public struct SavedRuleCondition
{
    public string AttributeName;
    public int Operator;
    public string CompareValue;
}

/// <summary>
/// Serializable copy of EventRule without runtime state (HasBeenTriggered)
/// and without Key binding (UI concern, not data).
/// </summary>
[Serializable]
public struct SavedEventRule
{
    public string DisplayName;
    public int Logic;
    public SavedRuleCondition[] Conditions;
    public string AttributeName;
    public int Operator;
    public string CompareValue;
    public float SpeedDelta;
    public float Duration;
    public int Weather;
    public bool AllowRepeat;

    public static SavedEventRule FromRule(EventRule rule)
    {
        SavedRuleCondition[] savedConditions = null;
        if (rule.Conditions != null && rule.Conditions.Length > 0)
        {
            savedConditions = new SavedRuleCondition[rule.Conditions.Length];
            for (int i = 0; i < rule.Conditions.Length; i++)
            {
                savedConditions[i] = new SavedRuleCondition
                {
                    AttributeName = rule.Conditions[i].AttributeName ?? "",
                    Operator = (int)rule.Conditions[i].Operator,
                    CompareValue = rule.Conditions[i].CompareValue ?? ""
                };
            }
        }

        return new SavedEventRule
        {
            DisplayName = rule.DisplayName ?? "",
            Logic = (int)rule.Logic,
            Conditions = savedConditions,
            AttributeName = rule.AttributeName ?? "",
            Operator = (int)rule.Operator,
            CompareValue = rule.CompareValue ?? "",
            SpeedDelta = rule.SpeedDelta,
            Duration = rule.Duration,
            Weather = (int)rule.Weather,
            AllowRepeat = rule.AllowRepeat
        };
    }

    public EventRule ToRule(Key triggerKey)
    {
        RuleCondition[] ruleConditions = null;
        if (Conditions != null && Conditions.Length > 0)
        {
            ruleConditions = new RuleCondition[Conditions.Length];
            for (int i = 0; i < Conditions.Length; i++)
            {
                ruleConditions[i] = new RuleCondition
                {
                    AttributeName = Conditions[i].AttributeName,
                    Operator = (ComparisonOperator)Conditions[i].Operator,
                    CompareValue = Conditions[i].CompareValue
                };
            }
        }

        return new EventRule
        {
            DisplayName = DisplayName,
            Logic = (LogicOperator)Logic,
            Conditions = ruleConditions,
            AttributeName = AttributeName,
            Operator = (ComparisonOperator)Operator,
            CompareValue = CompareValue,
            SpeedDelta = SpeedDelta,
            Duration = Duration,
            Weather = (WeatherType)Weather,
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
    public AttributeEntry[] Attributes;
    public int LapsCompleted;
    public int CheckpointsPassed;
    public float TotalTime;

    public int ColorIndex
    {
        get
        {
            if (Attributes == null) return 0;
            for (int i = 0; i < Attributes.Length; i++)
                if (string.Equals(Attributes[i].Key, "colorIndex", System.StringComparison.OrdinalIgnoreCase))
                    if (int.TryParse(Attributes[i].Value, out int val)) return val;
            return 0;
        }
    }
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
