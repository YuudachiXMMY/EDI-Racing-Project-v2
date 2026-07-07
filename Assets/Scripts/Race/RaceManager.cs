using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main orchestrator: loads CSV data, spawns cars, manages race lifecycle.
/// Press F1 during play mode to print debug scoreboard.
/// </summary>
public class RaceManager : MonoBehaviour
{
    [Header("References")]
    public CarSpawner CarSpawner;
    public LapTracker LapTracker;
    public ScoreManager ScoreManager;
    public RaceConfig Config;

    [Header("Data")]
    public TextAsset DefaultCsvData;

    private List<GameObject> spawnedCars;
    private bool raceStarted;

    private void Start()
    {
        if (DefaultCsvData != null)
        {
            LoadAndStartRace(DefaultCsvData.text);
        }
    }

    public void LoadAndStartRace(string csvContent)
    {
        var carDataList = CsvParser.Parse(csvContent);
        Debug.Log($"[RaceManager] Parsed {carDataList.Count} cars from CSV");

        spawnedCars = CarSpawner.SpawnCars(carDataList);

        foreach (var car in spawnedCars)
        {
            var identity = car.GetComponent<CarIdentity>();
            ScoreManager.RegisterCar(identity);
        }

        LapTracker.OnLapCompleted += OnCarCompletedLap;
        raceStarted = true;
        Debug.Log($"[RaceManager] Race started with {spawnedCars.Count} cars");
    }

    private void OnCarCompletedLap(CarIdentity car)
    {
        Debug.Log($"[Race] {car.TeamName} completed lap {car.CurrentLap}");
        if (car.CurrentLap >= Config.TotalLaps)
            Debug.Log($"[Race] {car.TeamName} FINISHED!");
    }

    // Temporary debug output — replaced by UI in Phase 4
    private void Update()
    {
        if (raceStarted && Input.GetKeyDown(KeyCode.F1))
            Debug.Log("[Scoreboard]\n" + ScoreManager.GetScoreboardText());
    }
}
