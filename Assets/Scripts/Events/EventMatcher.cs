using System;
using System.Linq;

/// <summary>
/// Determines whether a car is affected by a given event.
/// Pure static utility — no MonoBehaviour, no state.
/// </summary>
public static class EventMatcher
{
    public static bool IsAffected(RaceEventConfig config, CarIdentity car)
    {
        switch (config.EventType)
        {
            case RaceEventType.NameLengthPenalty:
                return car.TeamName.Length > config.NameLengthThreshold;

            case RaceEventType.ColorBoost:
            case RaceEventType.ColorPenalty:
                return car.ColorIndex == config.TargetColorIndex;

            case RaceEventType.FunctionBoost:
            case RaceEventType.FunctionPenalty:
                if (string.IsNullOrEmpty(config.TargetFunction)) return false;
                string target = config.TargetFunction.Trim().ToLower();
                return car.Functions != null
                    && car.Functions.Any(f => f.Equals(target, StringComparison.OrdinalIgnoreCase));

            case RaceEventType.SnowWeather:
            case RaceEventType.NightWeather:
                return true;

            default:
                return false;
        }
    }
}
