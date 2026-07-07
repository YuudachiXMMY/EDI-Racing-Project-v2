using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public EventManager EventManager;
    public WeatherEffect WeatherEffect;

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

        if (EventManager != null)
        {
            EventManager.RegisterCars(spawnedCars);
            EventManager.Activate();
            EventManager.OnEventTriggered += OnEventTriggered;
        }

        raceStarted = true;
        Debug.Log($"[RaceManager] Race started with {spawnedCars.Count} cars");
    }

    private void OnEventTriggered(RaceEventConfig config, int affectedCount)
    {
        if (WeatherEffect == null) return;
        if (config.EventType == RaceEventType.SnowWeather)
            WeatherEffect.ActivateSnow(config.Duration);
        else if (config.EventType == RaceEventType.NightWeather)
            WeatherEffect.ActivateNight(config.Duration);
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
        if (raceStarted && Keyboard.current != null && Keyboard.current[Key.F1].wasPressedThisFrame)
            Debug.Log("[Scoreboard]\n" + ScoreManager.GetScoreboardText());
    }
}
