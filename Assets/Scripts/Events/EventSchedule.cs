using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pre-configured list of events for a race session.
/// Create via Assets > Create > EDI Racing > Event Schedule.
/// Professor sets up events here before starting the race.
/// </summary>
[CreateAssetMenu(fileName = "EventSchedule", menuName = "EDI Racing/Event Schedule")]
public class EventSchedule : ScriptableObject
{
    [Tooltip("List of events configured for this race session")]
    public RaceEventConfig[] Events = new RaceEventConfig[]
    {
        new RaceEventConfig
        {
            EventType = RaceEventType.NameLengthPenalty,
            DisplayName = "Name Length Penalty",
            SpeedDelta = -10f,
            Duration = 8f,
            NameLengthThreshold = 10,
            TriggerKey = Key.Digit1,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.ColorBoost,
            DisplayName = "Color Boost (Blue)",
            SpeedDelta = 15f,
            Duration = 6f,
            TargetColorIndex = 3,
            TriggerKey = Key.Digit2,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.ColorPenalty,
            DisplayName = "Color Penalty (Red)",
            SpeedDelta = -12f,
            Duration = 8f,
            TargetColorIndex = 2,
            TriggerKey = Key.Digit3,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.FunctionBoost,
            DisplayName = "Function Boost (Password)",
            SpeedDelta = 10f,
            Duration = 6f,
            TargetFunction = "password",
            TriggerKey = Key.Digit4,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.FunctionPenalty,
            DisplayName = "Function Penalty (Face Recog)",
            SpeedDelta = -10f,
            Duration = 8f,
            TargetFunction = "facerecog",
            TriggerKey = Key.Digit5,
            AllowRepeat = false
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.SnowWeather,
            DisplayName = "Snow Weather",
            SpeedDelta = -8f,
            Duration = 12f,
            TriggerKey = Key.Digit6,
            AllowRepeat = true
        },
        new RaceEventConfig
        {
            EventType = RaceEventType.NightWeather,
            DisplayName = "Night Weather",
            SpeedDelta = -5f,
            Duration = 15f,
            TriggerKey = Key.Digit7,
            AllowRepeat = true
        }
    };

    /// <summary>
    /// Reset all runtime state (HasBeenTriggered flags).
    /// Call at race start.
    /// </summary>
    public void ResetRuntimeState()
    {
        for (int i = 0; i < Events.Length; i++)
        {
            Events[i].HasBeenTriggered = false;
        }
    }
}
