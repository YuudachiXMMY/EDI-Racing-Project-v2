using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Parses CSV with header row into CarData list.
/// First column is always TeamName; remaining columns become attributes.
/// WebGL-compatible: accepts string content, no file I/O.
/// </summary>
public static class CsvParser
{
    public static List<CarData> Parse(string csvContent)
    {
        var cars = new List<CarData>();
        if (string.IsNullOrEmpty(csvContent)) return cars;

        var lines = csvContent.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length < 2) return cars; // need header + at least one data row

        // Parse header row
        string[] headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        if (headers.Length == 0) return cars;

        // Data rows
        for (int row = 1; row < lines.Length; row++)
        {
            string[] columns = lines[row].Split(',');
            if (columns.Length == 0) continue;

            string teamName = columns[0].Trim();
            if (string.IsNullOrEmpty(teamName)) continue;

            var attributes = new List<AttributeEntry>();
            for (int col = 1; col < headers.Length && col < columns.Length; col++)
            {
                string key = headers[col];
                if (string.IsNullOrEmpty(key)) continue;
                string value = columns[col].Trim();
                attributes.Add(new AttributeEntry { Key = key, Value = value });
            }

            cars.Add(new CarData(teamName, attributes.ToArray()));
        }

        return cars;
    }
}
