/// <summary>
/// The 7 event types matching v1 parity.
/// Each event modifies car speeds based on different criteria.
/// </summary>
public enum RaceEventType
{
    NameLengthPenalty,   // Cars with team name > threshold chars get penalized
    ColorBoost,          // Cars matching target colorIndex get speed boost
    ColorPenalty,        // Cars matching target colorIndex get speed penalty
    FunctionBoost,       // Cars with target function tag get speed boost
    FunctionPenalty,     // Cars with target function tag get speed penalty
    SnowWeather,         // All cars slow down (global weather)
    NightWeather         // All cars slow down (global weather, different magnitude)
}
