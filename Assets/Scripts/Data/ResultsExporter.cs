using System.Text;

/// <summary>
/// Formats race results as CSV strings for export.
/// WebGL-compatible: produces string content, no file I/O.
/// </summary>
public static class ResultsExporter
{
    public static string ExportRankingsCsv(RaceResults results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rank,TeamName,ColorIndex,LapsCompleted,CheckpointsPassed,Time");
        if (results.Rankings == null) return sb.ToString();

        foreach (var car in results.Rankings)
        {
            sb.AppendLine($"{car.Rank},{EscapeCsv(car.TeamName)},{car.ColorIndex},{car.LapsCompleted},{car.CheckpointsPassed},{car.TotalTime:F2}");
        }
        return sb.ToString();
    }

    public static string ExportEventLogCsv(RaceResults results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,EventName,AffectedCount,TotalCars");
        if (results.EventLog == null) return sb.ToString();

        foreach (var entry in results.EventLog)
        {
            sb.AppendLine($"{entry.Timestamp:F2},{EscapeCsv(entry.EventName)},{entry.AffectedCount},{entry.TotalCars}");
        }
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\""))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
