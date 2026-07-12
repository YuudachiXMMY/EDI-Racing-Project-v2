using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main orchestrator: loads CSV data, spawns cars, manages race lifecycle.
/// Keyboard shortcuts: T=scoreboard, P=save, L=load, X=export.
/// Exposes events and public API for UI integration (Phase 4).
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

    [Header("Session")]
    public SessionManager SessionManager;

    [Header("Network (Optional)")]
    public NetworkSync NetworkSync;

    [Header("Survey (Optional)")]
    public SurveyConfigManager SurveyConfigManager;

    private List<GameObject> spawnedCars;
    private bool raceStarted;
    private bool raceFinished;
    private float raceStartTime;
    private readonly List<EventLogEntry> eventLog = new List<EventLogEntry>();

    // --- Phase 4: UI Integration ---
    public GameState CurrentState { get; private set; } = GameState.Setup;
    public event Action<GameState> OnStateChanged;
    public event Action<CarIdentity> OnRaceFinished;
    public List<GameObject> SpawnedCars => spawnedCars;

    private void SetState(GameState state)
    {
        CurrentState = state;
        OnStateChanged?.Invoke(state);
    }

    private void Start()
    {
        SetState(GameState.Setup);

        // Auto-start when no SetupScreen is present in the scene
        if (FindFirstObjectByType<SetupScreen>() == null && DefaultCsvData != null)
        {
            LoadAndStartRace(DefaultCsvData.text);
        }
    }

    public void LoadAndStartRace(string csvContent)
    {
        var carDataList = CsvParser.Parse(csvContent);
        Debug.Log($"[RaceManager] Parsed {carDataList.Count} cars from CSV");
        LoadAndStartRace(carDataList);
    }

    public void LoadAndStartRace(List<CarData> carDataList)
    {
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

        if (WeatherEffect != null)
            WeatherEffect.StartCycle();

        eventLog.Clear();
        raceStartTime = Time.time;
        raceFinished = false;
        raceStarted = true;
        SetState(GameState.Racing);
        Debug.Log($"[RaceManager] Race started with {spawnedCars.Count} cars");

        // Broadcast to students if hosting
        if (NetworkSync != null)
            NetworkSync.BroadcastRaceStart(carDataList);
    }

    public void ResetRace()
    {
        LapTracker.OnLapCompleted -= OnCarCompletedLap;
        if (EventManager != null)
        {
            EventManager.OnEventTriggered -= OnEventTriggered;
            EventManager.ClearRegisteredCars();
        }

        if (WeatherEffect != null)
            WeatherEffect.ResetAll();

        if (spawnedCars != null)
        {
            foreach (var car in spawnedCars)
                if (car != null) Destroy(car);
            spawnedCars.Clear();
        }

        ScoreManager.Clear();
        eventLog.Clear();
        raceStarted = false;
        raceFinished = false;
        SetState(GameState.Setup);
    }

    // --- Phase 4: Public API for UI ---

    public void PauseRace()
    {
        if (!raceStarted || raceFinished) return;
        Time.timeScale = 0f;
        SetState(GameState.Paused);
        Debug.Log("[RaceManager] Race paused");
    }

    public void ResumeRace()
    {
        if (CurrentState != GameState.Paused) return;
        Time.timeScale = 1f;
        SetState(GameState.Racing);
        Debug.Log("[RaceManager] Race resumed");
    }

    public void SaveCurrentSession()
    {
        if (SessionManager == null || !raceStarted) return;
        var session = BuildSessionData();
        SessionManager.SaveSession(session);
    }

    public void ExportCurrentResults()
    {
        if (SessionManager == null || !raceStarted) return;
        var results = ScoreManager.CollectResults(eventLog, Time.time - raceStartTime);
        var config = SurveyConfigManager != null ? SurveyConfigManager.ActiveConfig : null;
        SessionManager.ExportResults(results, config);
    }

    public void LoadFromSession(SessionData session)
    {
        LoadSession(session);
    }

    /// <summary>
    /// Student-side: spawns visual-only cars (no NavMesh/AI) for network sync.
    /// </summary>
    public void LoadAndStartRaceVisualOnly(List<CarData> carDataList)
    {
        spawnedCars = CarSpawner.SpawnVisualCars(carDataList);
        raceStarted = true;
        raceFinished = false;
        SetState(GameState.Racing);
        Debug.Log($"[RaceManager] Visual-only race started with {spawnedCars.Count} cars");
    }

    private void OnEventTriggered(EventRule rule, int affectedCount)
    {
        eventLog.Add(new EventLogEntry
        {
            Timestamp = Time.time - raceStartTime,
            EventName = rule.DisplayName,
            AffectedCount = affectedCount,
            TotalCars = spawnedCars != null ? spawnedCars.Count : 0
        });

        if (WeatherEffect == null) return;
        switch (rule.Weather)
        {
            case WeatherType.Snow:
                WeatherEffect.ActivateSnow(rule.Duration);
                break;
            case WeatherType.Night:
                WeatherEffect.ActivateNight(rule.Duration);
                break;
            case WeatherType.Sunset:
                WeatherEffect.ActivateSunset(rule.Duration);
                break;
        }
    }

    private void OnCarCompletedLap(CarIdentity car)
    {
        Debug.Log($"[Race] {car.TeamName} completed lap {car.CurrentLap}");
        if (car.CurrentLap >= Config.TotalLaps)
        {
            Debug.Log($"[Race] {car.TeamName} FINISHED!");
            if (!raceFinished)
            {
                raceFinished = true;
                Debug.Log($"[RaceManager] Race complete! Winner: {car.TeamName}");
                OnRaceFinished?.Invoke(car);
                SetState(GameState.Finished);
            }
        }
    }

    private SessionData BuildSessionData()
    {
        var cars = Array.Empty<CarData>();
        if (spawnedCars != null)
        {
            var carList = new List<CarData>();
            foreach (var car in spawnedCars)
            {
                if (car == null) continue;
                var id = car.GetComponent<CarIdentity>();
                if (id != null)
                    carList.Add(new CarData(id.TeamName, id.Attributes != null
                        ? (AttributeEntry[])id.Attributes.Clone()
                        : Array.Empty<AttributeEntry>()));
            }
            cars = carList.ToArray();
        }

        var events = Array.Empty<SavedEventRule>();
        if (EventManager != null && EventManager.Schedule != null)
        {
            events = new SavedEventRule[EventManager.Schedule.Events.Length];
            for (int i = 0; i < events.Length; i++)
                events[i] = SavedEventRule.FromRule(EventManager.Schedule.Events[i]);
        }

        string surveyConfigName = "";
        SurveyConfig surveyConfig = null;
        if (SurveyConfigManager != null && SurveyConfigManager.ActiveConfig != null)
        {
            surveyConfigName = SurveyConfigManager.ActiveConfig.ConfigName ?? "";
            surveyConfig = SurveyConfigManager.ActiveConfig;
        }

        return new SessionData
        {
            SessionName = "Race Session",
            CreatedAt = DateTime.Now.ToString("o"),
            SurveyConfigName = surveyConfigName,
            SurveyConfig = surveyConfig,
            Cars = cars,
            Events = events,
            RaceSettings = SavedRaceConfig.FromScriptableObject(Config),
            Results = ScoreManager.CollectResults(eventLog, Time.time - raceStartTime)
        };
    }

    private void LoadSession(SessionData session)
    {
        ResetRace();

        session.RaceSettings.ApplyTo(Config);

        // Restore SurveyConfig if present (takes priority over raw event list)
        if (session.SurveyConfig != null && SurveyConfigManager != null)
        {
            SurveyConfigManager.SetActiveConfig(session.SurveyConfig);
            if (EventManager != null && EventManager.Schedule != null)
                SurveyConfigManager.ApplyRulesToSchedule(EventManager.Schedule);
        }
        else if (EventManager != null && EventManager.Schedule != null && session.Events.Length > 0)
        {
            int count = Mathf.Min(session.Events.Length, EventManager.Schedule.Events.Length);
            for (int i = 0; i < count; i++)
            {
                Key key = EventManager.Schedule.Events[i].TriggerKey;
                EventManager.Schedule.Events[i] = session.Events[i].ToRule(key);
            }
        }

        var carDataList = new List<CarData>(session.Cars);
        LoadAndStartRace(carDataList);
    }

    // Debug keys: T=scoreboard, P=save, L=load, X=export results
    private void Update()
    {
        if (Keyboard.current == null) return;

        if (raceStarted && Keyboard.current[Key.T].wasPressedThisFrame)
            Debug.Log("[Scoreboard]\n" + ScoreManager.GetScoreboardText());

        if (SessionManager == null) return;

        if (raceStarted && Keyboard.current[Key.P].wasPressedThisFrame)
        {
            var session = BuildSessionData();
            SessionManager.SaveSession(session);
        }

        if (Keyboard.current[Key.L].wasPressedThisFrame)
        {
            string path = SessionManager.FindLatestSession();
            if (path != null)
            {
                var session = SessionManager.LoadSession(path);
                if (session != null)
                    LoadSession(session);
            }
            else
            {
                Debug.LogWarning("[RaceManager] No saved sessions found");
            }
        }

        if (raceStarted && Keyboard.current[Key.X].wasPressedThisFrame)
        {
            var results = ScoreManager.CollectResults(eventLog, Time.time - raceStartTime);
            var config = SurveyConfigManager != null ? SurveyConfigManager.ActiveConfig : null;
            SessionManager.ExportResults(results, config);
        }
    }
}
