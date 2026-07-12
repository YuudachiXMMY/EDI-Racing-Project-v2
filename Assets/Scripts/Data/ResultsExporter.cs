using System.Collections.Generic;
using System.Text;

/// <summary>
/// Formats race results as CSV strings for export.
/// Dynamically includes all car attribute columns.
/// WebGL-compatible: produces string content, no file I/O.
/// </summary>
public static class ResultsExporter
{
    public static string ExportRankingsCsv(RaceResults results, SurveyConfig config)
    {
        var sb = new StringBuilder();
        if (config != null)
        {
            sb.AppendLine($"# Survey: {config.ConfigName}");
            int qCount = config.Questions != null ? config.Questions.Length : 0;
            int rCount = config.Rules != null ? config.Rules.Length : 0;
            sb.AppendLine($"# Questions: {qCount}, Rules: {rCount}");
            sb.AppendLine();
        }
        sb.Append(ExportRankingsCsv(results));
        return sb.ToString();
    }

    public static string ExportRankingsCsv(RaceResults results)
    {
        if (results.Rankings == null || results.Rankings.Length == 0)
            return "Rank,TeamName,LapsCompleted,CheckpointsPassed,Time\n";

        // Collect all unique attribute keys across all cars (preserve insertion order)
        var allKeys = new List<string>();
        foreach (var car in results.Rankings)
        {
            if (car.Attributes == null) continue;
            foreach (var attr in car.Attributes)
                if (!string.IsNullOrEmpty(attr.Key) && !allKeys.Contains(attr.Key))
                    allKeys.Add(attr.Key);
        }

        var sb = new StringBuilder();

        // Header
        sb.Append("Rank,TeamName");
        foreach (var key in allKeys)
            sb.Append($",{EscapeCsv(key)}");
        sb.AppendLine(",LapsCompleted,CheckpointsPassed,Time");

        // Data rows
        foreach (var car in results.Rankings)
        {
            sb.Append($"{car.Rank},{EscapeCsv(car.TeamName)}");
            foreach (var key in allKeys)
            {
                string val = "";
                if (car.Attributes != null)
                {
                    for (int i = 0; i < car.Attributes.Length; i++)
                    {
                        if (string.Equals(car.Attributes[i].Key, key, System.StringComparison.OrdinalIgnoreCase))
                        {
                            val = car.Attributes[i].Value;
                            break;
                        }
                    }
                }
                sb.Append($",{EscapeCsv(val)}");
            }
            sb.AppendLine($",{car.LapsCompleted},{car.CheckpointsPassed},{car.TotalTime:F2}");
        }
        return sb.ToString();
    }

    public static string ExportEventLogCsv(RaceResults results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,EventName,AffectedCount,TotalCars");
        if (results.EventLog == null) return sb.ToString();

        foreach (var entry in results.EventLog)
            sb.AppendLine($"{entry.Timestamp:F2},{EscapeCsv(entry.EventName)},{entry.AffectedCount},{entry.TotalCars}");

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
