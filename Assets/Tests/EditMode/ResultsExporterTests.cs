using System;
using NUnit.Framework;

[TestFixture]
public class ResultsExporterTests
{
    [Test]
    public void ExportRankingsCsv_EmptyResults_ReturnsHeaderOnly()
    {
        var results = new RaceResults { Rankings = Array.Empty<CarResult>() };
        string csv = ResultsExporter.ExportRankingsCsv(results);

        Assert.IsTrue(csv.StartsWith("Rank,TeamName"));
        Assert.IsTrue(csv.Contains("LapsCompleted"));
    }

    [Test]
    public void ExportRankingsCsv_SingleCar_CorrectFormat()
    {
        var results = new RaceResults
        {
            Rankings = new CarResult[]
            {
                new CarResult
                {
                    Rank = 1, TeamName = "Alpha",
                    Attributes = new AttributeEntry[] { new AttributeEntry { Key = "color", Value = "red" } },
                    LapsCompleted = 3, CheckpointsPassed = 12, TotalTime = 2.0f,
                    ElapsedTime = 45.5f, BestLapTime = 12.25f, AverageLapTime = 15.167f
                }
            }
        };

        string csv = ResultsExporter.ExportRankingsCsv(results);
        string[] lines = csv.Split('\n');

        Assert.IsTrue(lines[0].Contains("color"));
        Assert.IsTrue(lines[0].Contains("TotalTime,BestLap,AvgLap"));
        Assert.IsTrue(lines[1].Contains("Alpha"));
        Assert.IsTrue(lines[1].Contains("red"));
        // Millisecond (F3) precision for all three time columns; ElapsedTime surfaces as TotalTime.
        Assert.IsTrue(lines[1].EndsWith("3,12,45.500,12.250,15.167"));
    }

    [Test]
    public void ExportRankingsCsv_EmptyResults_HeaderHasTimeColumns()
    {
        var results = new RaceResults { Rankings = Array.Empty<CarResult>() };
        string csv = ResultsExporter.ExportRankingsCsv(results);

        Assert.AreEqual("Rank,TeamName,LapsCompleted,CheckpointsPassed,TotalTime,BestLap,AvgLap\n", csv);
    }

    [Test]
    public void ExportRankingsCsv_MultipleCars_DynamicAttributeColumns()
    {
        var results = new RaceResults
        {
            Rankings = new CarResult[]
            {
                new CarResult
                {
                    Rank = 1, TeamName = "A",
                    Attributes = new AttributeEntry[]
                    {
                        new AttributeEntry { Key = "lang", Value = "en" },
                        new AttributeEntry { Key = "score", Value = "8" }
                    },
                    LapsCompleted = 3, CheckpointsPassed = 10, TotalTime = 30f
                },
                new CarResult
                {
                    Rank = 2, TeamName = "B",
                    Attributes = new AttributeEntry[]
                    {
                        new AttributeEntry { Key = "lang", Value = "fr" },
                        new AttributeEntry { Key = "score", Value = "5" }
                    },
                    LapsCompleted = 2, CheckpointsPassed = 8, TotalTime = 35f
                }
            }
        };

        string csv = ResultsExporter.ExportRankingsCsv(results);

        Assert.IsTrue(csv.Contains("lang"));
        Assert.IsTrue(csv.Contains("score"));
        Assert.IsTrue(csv.Contains("en"));
        Assert.IsTrue(csv.Contains("fr"));
    }

    [Test]
    public void ExportRankingsCsv_CarsWithDifferentAttributes_UnionOfAllKeys()
    {
        var results = new RaceResults
        {
            Rankings = new CarResult[]
            {
                new CarResult
                {
                    Rank = 1, TeamName = "A",
                    Attributes = new AttributeEntry[] { new AttributeEntry { Key = "x", Value = "1" } },
                    LapsCompleted = 1, CheckpointsPassed = 1, TotalTime = 10f
                },
                new CarResult
                {
                    Rank = 2, TeamName = "B",
                    Attributes = new AttributeEntry[] { new AttributeEntry { Key = "y", Value = "2" } },
                    LapsCompleted = 1, CheckpointsPassed = 1, TotalTime = 11f
                }
            }
        };

        string csv = ResultsExporter.ExportRankingsCsv(results);
        string header = csv.Split('\n')[0];

        Assert.IsTrue(header.Contains("x"));
        Assert.IsTrue(header.Contains("y"));
    }

    [Test]
    public void ExportRankingsCsv_ValueWithComma_IsEscaped()
    {
        var results = new RaceResults
        {
            Rankings = new CarResult[]
            {
                new CarResult
                {
                    Rank = 1, TeamName = "Team,A",
                    Attributes = Array.Empty<AttributeEntry>(),
                    LapsCompleted = 1, CheckpointsPassed = 1, TotalTime = 10f
                }
            }
        };

        string csv = ResultsExporter.ExportRankingsCsv(results);

        Assert.IsTrue(csv.Contains("\"Team,A\""));
    }

    [Test]
    public void ExportEventLogCsv_NoEvents_ReturnsHeaderOnly()
    {
        var results = new RaceResults { EventLog = Array.Empty<EventLogEntry>() };
        string csv = ResultsExporter.ExportEventLogCsv(results);

        Assert.IsTrue(csv.StartsWith("Timestamp,EventName"));
        string[] lines = csv.Trim().Split('\n');
        Assert.AreEqual(1, lines.Length);
    }

    [Test]
    public void ExportEventLogCsv_MultipleEvents_CorrectFormat()
    {
        var results = new RaceResults
        {
            EventLog = new EventLogEntry[]
            {
                new EventLogEntry { Timestamp = 5.5f, EventName = "Snow", AffectedCount = 3, TotalCars = 10 },
                new EventLogEntry { Timestamp = 12.0f, EventName = "Boost", AffectedCount = 5, TotalCars = 10 }
            }
        };

        string csv = ResultsExporter.ExportEventLogCsv(results);
        string[] lines = csv.Trim().Split('\n');

        Assert.AreEqual(3, lines.Length); // header + 2 events
        Assert.IsTrue(lines[1].Contains("Snow"));
        Assert.IsTrue(lines[2].Contains("Boost"));
    }
}
