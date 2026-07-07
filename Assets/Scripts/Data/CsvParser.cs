using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Parses v1-format vehicleGroupData.csv into CarData list.
/// WebGL-compatible: accepts string content, no file I/O.
/// </summary>
public static class CsvParser
{
    public static List<CarData> Parse(string csvContent)
    {
        var cars = new List<CarData>();
        if (string.IsNullOrEmpty(csvContent)) return cars;

        var lines = csvContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var columns = trimmed.Split(',');
            if (columns.Length < 2) continue;

            string teamName = columns[0].Trim();
            if (string.IsNullOrEmpty(teamName)) continue;

            if (!int.TryParse(columns[1].Trim(), out int colorIndex))
                colorIndex = 0;

            string[] functions = columns.Length > 2 && !string.IsNullOrEmpty(columns[2])
                ? columns[2].Split('/').Select(f => f.Trim().ToLower()).Where(f => f.Length > 0).ToArray()
                : System.Array.Empty<string>();

            cars.Add(new CarData(teamName, colorIndex, functions));
        }
        return cars;
    }
}
