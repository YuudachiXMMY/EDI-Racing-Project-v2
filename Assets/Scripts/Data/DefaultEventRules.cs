/// <summary>
/// Single source of truth for the default event rule set, shared by
/// EventSchedule (runtime EventRule[] with TriggerKeys) and the built-in
/// survey templates (SavedEventRule[] without TriggerKeys).
///
/// 8 rules including the Sunset weather event. Per the 2026-07-30 decision,
/// the survey templates (V1 Parity, ENGG*1100) now also include Sunset, so
/// both projections derive from the same 8-rule base. Adding Sunset to the
/// templates intentionally changes the race they generate (one extra sunset
/// event) — the corresponding SurveyTemplatesTests assert 8 rules.
/// </summary>
public static class DefaultEventRules
{
    /// <summary>The 8 shared base rules as SavedEventRule[] (no TriggerKey; int-cast enums).</summary>
    public static SavedEventRule[] BaseSaved()
    {
        return new SavedEventRule[]
        {
            new SavedEventRule { DisplayName = "Name Length Penalty", AttributeName = "teamName", Operator = (int)ComparisonOperator.LengthGreaterThan, CompareValue = "10", SpeedDelta = -10f, Duration = 8f, Weather = (int)WeatherType.None, AllowRepeat = false },
            new SavedEventRule { DisplayName = "Color Boost (Blue)", AttributeName = "colorIndex", Operator = (int)ComparisonOperator.Equals, CompareValue = "3", SpeedDelta = 15f, Duration = 6f, Weather = (int)WeatherType.None, AllowRepeat = false },
            new SavedEventRule { DisplayName = "Color Penalty (Red)", AttributeName = "colorIndex", Operator = (int)ComparisonOperator.Equals, CompareValue = "2", SpeedDelta = -12f, Duration = 8f, Weather = (int)WeatherType.None, AllowRepeat = false },
            new SavedEventRule { DisplayName = "Function Boost (Password)", AttributeName = "functions", Operator = (int)ComparisonOperator.Contains, CompareValue = "password", SpeedDelta = 10f, Duration = 6f, Weather = (int)WeatherType.None, AllowRepeat = false },
            new SavedEventRule { DisplayName = "Function Penalty (Face Recog)", AttributeName = "functions", Operator = (int)ComparisonOperator.Contains, CompareValue = "facerecog", SpeedDelta = -10f, Duration = 8f, Weather = (int)WeatherType.None, AllowRepeat = false },
            new SavedEventRule { DisplayName = "Snow Weather", AttributeName = "", Operator = (int)ComparisonOperator.All, CompareValue = "", SpeedDelta = -8f, Duration = 12f, Weather = (int)WeatherType.Snow, AllowRepeat = true },
            new SavedEventRule { DisplayName = "Night Weather", AttributeName = "", Operator = (int)ComparisonOperator.All, CompareValue = "", SpeedDelta = -5f, Duration = 15f, Weather = (int)WeatherType.Night, AllowRepeat = true },
            new SavedEventRule { DisplayName = "Sunset Weather", AttributeName = "", Operator = (int)ComparisonOperator.All, CompareValue = "", SpeedDelta = -3f, Duration = 20f, Weather = (int)WeatherType.Sunset, AllowRepeat = true },
        };
    }

    /// <summary>
    /// The full 8 default rules as runtime EventRule[] incl. Sunset, with Digit1..8
    /// trigger keys — reproduces the historical EventSchedule default verbatim.
    /// </summary>
    public static EventRule[] BaseRuntime()
    {
        return EventRuleKeys.AssignKeys(BaseSaved());
    }
}
