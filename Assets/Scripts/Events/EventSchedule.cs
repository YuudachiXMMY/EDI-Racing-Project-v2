using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pre-configured list of event rules for a race session.
/// Create via Assets > Create > EDI Racing > Event Schedule.
/// Professor sets up rules here before starting the race.
/// </summary>
[CreateAssetMenu(fileName = "EventSchedule", menuName = "EDI Racing/Event Schedule")]
public class EventSchedule : ScriptableObject
{
    [Tooltip("List of event rules configured for this race session")]
    public EventRule[] Events = new EventRule[]
    {
        new EventRule
        {
            DisplayName = "Name Length Penalty",
            AttributeName = "teamName",
            Operator = ComparisonOperator.LengthGreaterThan,
            CompareValue = "10",
            SpeedDelta = -10f,
            Duration = 8f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit1,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Color Boost (Blue)",
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = "3",
            SpeedDelta = 15f,
            Duration = 6f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit2,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Color Penalty (Red)",
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = "2",
            SpeedDelta = -12f,
            Duration = 8f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit3,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Function Boost (Password)",
            AttributeName = "functions",
            Operator = ComparisonOperator.Contains,
            CompareValue = "password",
            SpeedDelta = 10f,
            Duration = 6f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit4,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Function Penalty (Face Recog)",
            AttributeName = "functions",
            Operator = ComparisonOperator.Contains,
            CompareValue = "facerecog",
            SpeedDelta = -10f,
            Duration = 8f,
            Weather = WeatherType.None,
            TriggerKey = Key.Digit5,
            AllowRepeat = false
        },
        new EventRule
        {
            DisplayName = "Snow Weather",
            AttributeName = "",
            Operator = ComparisonOperator.All,
            CompareValue = "",
            SpeedDelta = -8f,
            Duration = 12f,
            Weather = WeatherType.Snow,
            TriggerKey = Key.Digit6,
            AllowRepeat = true
        },
        new EventRule
        {
            DisplayName = "Night Weather",
            AttributeName = "",
            Operator = ComparisonOperator.All,
            CompareValue = "",
            SpeedDelta = -5f,
            Duration = 15f,
            Weather = WeatherType.Night,
            TriggerKey = Key.Digit7,
            AllowRepeat = true
        },
        new EventRule
        {
            DisplayName = "Sunset Weather",
            AttributeName = "",
            Operator = ComparisonOperator.All,
            CompareValue = "",
            SpeedDelta = -3f,
            Duration = 20f,
            Weather = WeatherType.Sunset,
            TriggerKey = Key.Digit8,
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
