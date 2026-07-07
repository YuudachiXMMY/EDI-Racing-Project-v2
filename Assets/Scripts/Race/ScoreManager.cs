using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Ranks cars by race progress: most checkpoints passed, then least time.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    private readonly List<CarIdentity> cars = new List<CarIdentity>();

    public void RegisterCar(CarIdentity car)
    {
        cars.Add(car);
    }

    public List<CarIdentity> GetRankedCars()
    {
        return cars
            .OrderByDescending(c => c.TotalCheckpointsPassed)
            .ThenBy(c => c.CheckpointTime)
            .ToList();
    }

    public string GetScoreboardText()
    {
        var ranked = GetRankedCars();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < ranked.Count; i++)
        {
            var c = ranked[i];
            sb.AppendLine($"({i + 1}) [{c.CurrentLap}] {c.TeamName} - {c.CheckpointTime:F1}s");
        }
        return sb.ToString();
    }

    public void Clear()
    {
        cars.Clear();
    }

    public RaceResults CollectResults(List<EventLogEntry> eventLog, float raceTime)
    {
        var ranked = GetRankedCars();
        var rankings = new CarResult[ranked.Count];
        for (int i = 0; i < ranked.Count; i++)
        {
            var c = ranked[i];
            rankings[i] = new CarResult
            {
                Rank = i + 1,
                TeamName = c.TeamName,
                ColorIndex = c.ColorIndex,
                LapsCompleted = c.CurrentLap,
                CheckpointsPassed = c.TotalCheckpointsPassed,
                TotalTime = c.CheckpointTime
            };
        }
        return new RaceResults
        {
            Rankings = rankings,
            EventLog = eventLog != null ? eventLog.ToArray() : Array.Empty<EventLogEntry>(),
            TotalRaceTime = raceTime
        };
    }
}
