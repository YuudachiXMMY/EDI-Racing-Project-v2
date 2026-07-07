using System;

/// <summary>
/// Immutable data representing a car parsed from survey CSV.
/// Format: teamName,colorIndex,functionList (slash-separated)
/// </summary>
[Serializable]
public struct CarData
{
    public string TeamName;
    public int ColorIndex;     // 0=green, 1=black, 2=red, 3=blue, 4=white
    public string[] Functions; // e.g. ["facerecog","glasses","male"]

    public CarData(string teamName, int colorIndex, string[] functions)
    {
        TeamName = teamName;
        ColorIndex = colorIndex;
        Functions = functions ?? Array.Empty<string>();
    }
}
