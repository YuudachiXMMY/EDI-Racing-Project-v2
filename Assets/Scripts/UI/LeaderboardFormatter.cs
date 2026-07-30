using UnityEngine;

/// <summary>
/// Rank-based medal colors shared by leaderboard and finish UI.
/// Single source of truth for the gold/silver/bronze colors previously
/// duplicated across LeaderboardPanel, RaceFinishPanel, CarLabelSpawner,
/// and NetworkSync. Only colors are centralized — each caller keeps its own
/// row-text format string (they differ intentionally).
/// </summary>
public static class LeaderboardFormatter
{
    public static readonly Color Gold = new Color(1f, 0.84f, 0f);
    public static readonly Color Silver = new Color(0.75f, 0.75f, 0.75f);
    public static readonly Color Bronze = new Color(0.8f, 0.5f, 0.2f);

    /// <summary>Rank-based color: 1st gold, 2nd silver, 3rd bronze, else white. (0-based index)</summary>
    public static Color RankColor(int rankZeroBased) => rankZeroBased switch
    {
        0 => Gold,
        1 => Silver,
        2 => Bronze,
        _ => Color.white
    };

    /// <summary>Rich-text color name/hex for rank (RaceFinishPanel style). (0-based index)</summary>
    public static string RankHex(int rankZeroBased) => rankZeroBased switch
    {
        0 => "yellow",
        1 => "#C0C0C0",
        2 => "#CD7F32",
        _ => "white"
    };
}
