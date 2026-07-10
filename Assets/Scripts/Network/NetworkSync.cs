using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Synchronizes race state over the network.
/// Professor: broadcasts car positions, events, leaderboard at regular intervals.
/// Student: receives state and interpolates car positions.
/// </summary>
public class NetworkSync : MonoBehaviour
{
    [Header("References")]
    public RaceManager RaceManager;
    public NetworkManager NetworkManager;
    public ScoreManager ScoreManager;

    [Header("Sync Settings")]
    [Tooltip("How often to broadcast car positions (seconds)")]
    public float StateUpdateInterval = 0.1f;

    [Tooltip("How often to broadcast leaderboard (seconds)")]
    public float LeaderboardInterval = 0.5f;

    [Tooltip("Interpolation speed for student-side car movement")]
    public float InterpolationSpeed = 15f;

    // Professor-side timers
    private float stateTimer;
    private float leaderboardTimer;

    // Student-side interpolation targets
    private List<GameObject> remoteCars;
    private Vector3[] targetPositions;
    private float[] targetRotationsY;

    private void Start()
    {
        if (NetworkManager == null) return;
        NetworkManager.OnMessageReceived += HandleGameMessage;

        if (RaceManager != null)
        {
            RaceManager.OnStateChanged += OnStateChanged;
        }

        if (RaceManager != null && RaceManager.EventManager != null)
        {
            RaceManager.EventManager.OnEventTriggered += OnEventTriggered;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager != null)
            NetworkManager.OnMessageReceived -= HandleGameMessage;

        if (RaceManager != null)
            RaceManager.OnStateChanged -= OnStateChanged;

        if (RaceManager != null && RaceManager.EventManager != null)
            RaceManager.EventManager.OnEventTriggered -= OnEventTriggered;
    }

    private void Update()
    {
        if (NetworkManager == null || !NetworkManager.IsConnected) return;

        if (NetworkManager.IsHost)
            UpdateHost();
        else
            UpdateStudent();
    }

    // ── Professor (Host) ──────────────────────────────────

    private void UpdateHost()
    {
        if (RaceManager == null || RaceManager.CurrentState != GameState.Racing) return;
        var cars = RaceManager.SpawnedCars;
        if (cars == null || cars.Count == 0) return;

        stateTimer += Time.deltaTime;
        if (stateTimer >= StateUpdateInterval)
        {
            stateTimer = 0f;
            BroadcastStateUpdate(cars);
        }

        leaderboardTimer += Time.deltaTime;
        if (leaderboardTimer >= LeaderboardInterval)
        {
            leaderboardTimer = 0f;
            BroadcastLeaderboard();
        }
    }

    private void BroadcastStateUpdate(List<GameObject> cars)
    {
        var msg = new StateUpdateMessage();
        msg.t = Time.time;

        var states = new CarNetState[cars.Count];
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i] == null) continue;
            var t = cars[i].transform;
            var id = cars[i].GetComponent<CarIdentity>();
            states[i] = new CarNetState
            {
                i = i,
                px = t.position.x,
                py = t.position.y,
                pz = t.position.z,
                ry = t.eulerAngles.y,
                l = id != null ? id.CurrentLap : 0,
                c = id != null ? id.TotalCheckpointsPassed : 0
            };
        }
        msg.cars = states;
        NetworkManager.Send(JsonUtility.ToJson(msg));
    }

    private void BroadcastLeaderboard()
    {
        if (ScoreManager == null) return;
        var ranked = ScoreManager.GetRankedCars();
        var msg = new LeaderboardMessage();
        int count = Mathf.Min(ranked.Count, 15);
        msg.rankings = new LeaderboardEntry[count];
        for (int i = 0; i < count; i++)
        {
            msg.rankings[i] = new LeaderboardEntry
            {
                rank = i + 1,
                name = ranked[i].TeamName,
                lap = ranked[i].CurrentLap,
                cp = ranked[i].TotalCheckpointsPassed
            };
        }
        NetworkManager.Send(JsonUtility.ToJson(msg));
    }

    public void BroadcastRaceStart(List<CarData> carDataList)
    {
        if (NetworkManager == null || !NetworkManager.IsHost) return;
        var msg = new RaceStartMessage();
        msg.cars = new NetCarData[carDataList.Count];
        for (int i = 0; i < carDataList.Count; i++)
            msg.cars[i] = NetCarData.FromCarData(carDataList[i]);
        NetworkManager.Send(JsonUtility.ToJson(msg));
    }

    private void OnStateChanged(GameState state)
    {
        if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
        var msg = new GameStateMessage { state = state.ToString() };
        NetworkManager.Send(JsonUtility.ToJson(msg));
    }

    private void OnEventTriggered(RaceEventConfig config, int affectedCount)
    {
        if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost) return;
        var cars = RaceManager.SpawnedCars;
        var msg = new EventTriggeredMessage
        {
            name = config.DisplayName,
            affected = affectedCount,
            total = cars != null ? cars.Count : 0
        };
        NetworkManager.Send(JsonUtility.ToJson(msg));
    }

    // ── Student ───────────────────────────────────────────

    private void UpdateStudent()
    {
        if (remoteCars == null || targetPositions == null) return;

        float t = Time.deltaTime * InterpolationSpeed;
        for (int i = 0; i < remoteCars.Count; i++)
        {
            if (remoteCars[i] == null) continue;
            var tr = remoteCars[i].transform;
            tr.position = Vector3.Lerp(tr.position, targetPositions[i], t);
            var angles = tr.eulerAngles;
            angles.y = Mathf.LerpAngle(angles.y, targetRotationsY[i], t);
            tr.eulerAngles = angles;
        }
    }

    private void HandleGameMessage(string json)
    {
        var baseMsg = JsonUtility.FromJson<NetworkMessage>(json);

        switch (baseMsg.type)
        {
            case "race_start":
                HandleRaceStart(json);
                break;
            case "state_update":
                HandleStateUpdate(json);
                break;
            case "game_state":
                HandleGameState(json);
                break;
            case "event_triggered":
                HandleEventTriggered(json);
                break;
            case "leaderboard":
                HandleLeaderboard(json);
                break;
            case "race_end":
                HandleRaceEnd();
                break;
            case "room_closed":
                HandleRoomClosed();
                break;
        }
    }

    private void HandleRaceStart(string json)
    {
        var msg = JsonUtility.FromJson<RaceStartMessage>(json);
        var carDataList = new List<CarData>();
        foreach (var nc in msg.cars)
            carDataList.Add(nc.ToCarData());

        if (RaceManager != null)
            RaceManager.LoadAndStartRaceVisualOnly(carDataList);

        // Cache car references for interpolation
        remoteCars = RaceManager != null ? RaceManager.SpawnedCars : null;
        if (remoteCars != null)
        {
            targetPositions = new Vector3[remoteCars.Count];
            targetRotationsY = new float[remoteCars.Count];
            for (int i = 0; i < remoteCars.Count; i++)
            {
                if (remoteCars[i] != null)
                {
                    targetPositions[i] = remoteCars[i].transform.position;
                    targetRotationsY[i] = remoteCars[i].transform.eulerAngles.y;
                }
            }
        }

        Debug.Log($"[NetworkSync] Race started with {carDataList.Count} cars (visual-only)");
    }

    private void HandleStateUpdate(string json)
    {
        if (remoteCars == null || targetPositions == null) return;

        var msg = JsonUtility.FromJson<StateUpdateMessage>(json);
        foreach (var cs in msg.cars)
        {
            if (cs.i < 0 || cs.i >= remoteCars.Count) continue;
            targetPositions[cs.i] = new Vector3(cs.px, cs.py, cs.pz);
            targetRotationsY[cs.i] = cs.ry;

            // Update CarIdentity progress for labels
            if (remoteCars[cs.i] != null)
            {
                var id = remoteCars[cs.i].GetComponent<CarIdentity>();
                if (id != null)
                {
                    id.CurrentLap = cs.l;
                    id.TotalCheckpointsPassed = cs.c;
                }
            }
        }
    }

    private void HandleGameState(string json)
    {
        var msg = JsonUtility.FromJson<GameStateMessage>(json);
        if (Enum.TryParse<GameState>(msg.state, out var state) && RaceManager != null)
        {
            if (state == GameState.Paused)
                Time.timeScale = 0f;
            else if (state == GameState.Racing)
                Time.timeScale = 1f;
        }
    }

    private void HandleEventTriggered(string json)
    {
        var msg = JsonUtility.FromJson<EventTriggeredMessage>(json);
        Debug.Log($"[NetworkSync] Event: {msg.name} ({msg.affected}/{msg.total} cars)");
    }

    // Student-side leaderboard data for UI
    public LeaderboardEntry[] LatestLeaderboard { get; private set; }
    public event Action<LeaderboardEntry[]> OnLeaderboardReceived;

    private void HandleLeaderboard(string json)
    {
        var msg = JsonUtility.FromJson<LeaderboardMessage>(json);
        LatestLeaderboard = msg.rankings;
        OnLeaderboardReceived?.Invoke(msg.rankings);
    }

    private void HandleRaceEnd()
    {
        Debug.Log("[NetworkSync] Race ended");
    }

    private void HandleRoomClosed()
    {
        Debug.Log("[NetworkSync] Room closed by professor");
        remoteCars = null;
        targetPositions = null;
        targetRotationsY = null;
    }
}
