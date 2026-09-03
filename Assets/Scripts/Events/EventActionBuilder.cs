/// <summary>
/// Pure static factory for the professor's parameterized race events. Centralizes every fixed
/// constant (boost/penalty magnitude, effect duration, the colour and function pick-lists, and
/// the weather values) so nothing is hardcoded in the UI. Each method returns an EventRule the
/// UI hands to EventManager.TriggerRule — the rules reuse the existing RuleEngine operators
/// (Equals on colorIndex, Contains on functions, LengthGreaterThan on teamName, All for weather).
///
/// Colour → colorIndex contract matches the ENGG*1100 template lookup
/// (Green=0, Black=1, Red=2, Blue=3, White=4). Function label → tag matches the tags the
/// web-app post-processing writes into the "functions" attribute.
/// </summary>
public static class EventActionBuilder
{
    /// <summary>Fixed speed added to matched cars on a boost.</summary>
    public const float BoostDelta = 20f;

    /// <summary>Fixed speed removed from matched cars on a penalty (stored negative).</summary>
    public const float PenaltyDelta = -15f;

    /// <summary>How long every boost/penalty lasts, in seconds.</summary>
    public const float EffectDuration = 10f;

    /// <summary>Snow weather effect values (mirrors DefaultEventRules).</summary>
    public const float SnowDelta = -8f;
    public const float SnowDuration = 12f;

    /// <summary>Night weather effect values (mirrors DefaultEventRules).</summary>
    public const float NightDelta = -5f;
    public const float NightDuration = 15f;

    /// <summary>
    /// Function pick-list for the Function Boost/Penalty menus. Display label → the tag the
    /// web-app writes into the car's "functions" attribute. (Male is a separate menu.)
    /// </summary>
    public static readonly (string Label, string Tag)[] Functions =
    {
        ("Facial", "facerecog"),
        ("Glasses", "glasses"),
        ("Language", "language"),
        ("Password", "password"),
        ("Distance", "distance"),
    };

    /// <summary>Colour pick-list for the Color Boost/Penalty menus. Display label → colorIndex.</summary>
    public static readonly (string Label, int Index)[] Colors =
    {
        ("Blue", 3),
        ("Red", 2),
        ("Black", 1),
        ("White", 4),
        ("Green", 0),
    };

    /// <summary>Cars whose team name is longer than <paramref name="threshold"/> are slowed.</summary>
    public static EventRule NameLengthPenalty(int threshold)
    {
        return new EventRule
        {
            DisplayName = $"Name Length > {threshold}",
            AttributeName = "teamName",
            Operator = ComparisonOperator.LengthGreaterThan,
            CompareValue = threshold.ToString(),
            SpeedDelta = PenaltyDelta,
            Duration = EffectDuration,
            Weather = WeatherType.None,
            AllowRepeat = true,
        };
    }

    /// <summary>Cars with the "male" function tag are accelerated or decelerated.</summary>
    public static EventRule Male(bool accelerate)
    {
        return FunctionTag("male", accelerate, accelerate ? "Male Boost" : "Male Penalty");
    }

    /// <summary>Cars carrying <paramref name="tag"/> in their functions are accelerated (boost) or decelerated.</summary>
    public static EventRule Function(string tag, bool boost)
    {
        return FunctionTag(tag, boost, $"Function {(boost ? "Boost" : "Penalty")} ({tag})");
    }

    private static EventRule FunctionTag(string tag, bool boost, string displayName)
    {
        return new EventRule
        {
            DisplayName = displayName,
            AttributeName = "functions",
            Operator = ComparisonOperator.Contains,
            CompareValue = tag,
            SpeedDelta = boost ? BoostDelta : PenaltyDelta,
            Duration = EffectDuration,
            Weather = WeatherType.None,
            AllowRepeat = true,
        };
    }

    /// <summary>Cars of the given colorIndex are accelerated (boost) or decelerated (penalty).</summary>
    public static EventRule Color(int colorIndex, bool boost)
    {
        return new EventRule
        {
            DisplayName = $"Color {(boost ? "Boost" : "Penalty")} ({colorIndex})",
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = colorIndex.ToString(),
            SpeedDelta = boost ? BoostDelta : PenaltyDelta,
            Duration = EffectDuration,
            Weather = WeatherType.None,
            AllowRepeat = true,
        };
    }

    /// <summary>Snow weather: slows all cars and triggers the snow VFX (via OnEventTriggered).</summary>
    public static EventRule Snow()
    {
        return new EventRule
        {
            DisplayName = "Snow Weather",
            AttributeName = "",
            Operator = ComparisonOperator.All,
            CompareValue = "",
            SpeedDelta = SnowDelta,
            Duration = SnowDuration,
            Weather = WeatherType.Snow,
            AllowRepeat = true,
        };
    }

    /// <summary>Night weather: slows all cars and triggers the night VFX (via OnEventTriggered).</summary>
    public static EventRule Night()
    {
        return new EventRule
        {
            DisplayName = "Night Weather",
            AttributeName = "",
            Operator = ComparisonOperator.All,
            CompareValue = "",
            SpeedDelta = NightDelta,
            Duration = NightDuration,
            Weather = WeatherType.Night,
            AllowRepeat = true,
        };
    }
}
