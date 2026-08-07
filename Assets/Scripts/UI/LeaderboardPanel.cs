using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Real-time leaderboard showing ranked cars.
/// Updates every 0.5s to avoid GC pressure. Uses object pooling for rows.
///
/// Data source depends on role:
///   • Host / offline — reads the local ScoreManager (cars are registered and scored here).
///   • Student (network client) — reads the authoritative leaderboard the host broadcasts
///     over the network (NetworkSync.LatestLeaderboard). Student cars are spawned
///     visual-only and are never registered with ScoreManager, so its local ranking is
///     always empty — the leaderboard MUST come from the network instead.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    [Header("References")]
    public ScoreManager ScoreManager;

    [Tooltip("Source of the networked leaderboard on student clients. Auto-resolved if unset.")]
    public NetworkSync NetworkSync;

    [Header("UI Elements")]
    [Tooltip("Parent transform for leaderboard row items")]
    public Transform ContentParent;

    [Tooltip("Prefab for a single leaderboard row (Text component required)")]
    public GameObject RowPrefab;

    [Header("Settings")]
    [Tooltip("Update interval in seconds")]
    public float UpdateInterval = 0.5f;

    [Tooltip("Maximum rows to display")]
    public int MaxRows = 15;

    private readonly List<GameObject> rowPool = new List<GameObject>();
    private float timer;

    private void Start()
    {
        // Defensive auto-wire: SceneWiring/TrackSetupEditor do not re-assign these on an
        // already-existing panel, so a scene can ship with them unset — which leaves the
        // leaderboard visible but permanently empty (the refresh early-returns on null).
        if (ScoreManager == null)
            ScoreManager = FindFirstObjectByType<ScoreManager>(FindObjectsInactive.Include);

        if (NetworkSync == null)
            NetworkSync = FindFirstObjectByType<NetworkSync>(FindObjectsInactive.Include);

        // Pre-instantiate row pool
        for (int i = 0; i < MaxRows; i++)
        {
            GameObject row = Instantiate(RowPrefab, ContentParent);
            row.SetActive(false);
            rowPool.Add(row);
        }
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < UpdateInterval) return;
        timer = 0f;

        RefreshLeaderboard();
    }

    /// <summary>
    /// True when running as a connected network client that is not the host — the only
    /// role whose cars are not locally scored, so the leaderboard must come from the network.
    /// </summary>
    private bool IsStudentClient =>
        NetworkSync != null
        && NetworkSync.NetworkManager != null
        && NetworkSync.NetworkManager.IsConnected
        && !NetworkSync.NetworkManager.IsHost;

    private void RefreshLeaderboard()
    {
        if (IsStudentClient)
            RefreshFromNetwork();
        else
            RefreshFromScoreManager();
    }

    private void RefreshFromScoreManager()
    {
        if (ScoreManager == null) return;

        List<CarIdentity> ranked = ScoreManager.GetRankedCars();
        int displayCount = Mathf.Min(ranked.Count, MaxRows);

        for (int i = 0; i < rowPool.Count; i++)
        {
            if (i < displayCount)
            {
                var car = ranked[i];
                SetRow(i, $"{i + 1}. [{car.CurrentLap}] {car.TeamName}", i);
            }
            else
            {
                rowPool[i].SetActive(false);
            }
        }
    }

    private void RefreshFromNetwork()
    {
        RenderNetworkEntries(NetworkSync.LatestLeaderboard);
    }

    // Split from RefreshFromNetwork so the render logic is unit-testable without a live
    // NetworkManager connection: it takes the entries directly instead of reading the source.
    private void RenderNetworkEntries(LeaderboardEntry[] rankings)
    {
        int displayCount = rankings != null ? Mathf.Min(rankings.Length, MaxRows) : 0;

        for (int i = 0; i < rowPool.Count; i++)
        {
            if (i < displayCount)
            {
                var entry = rankings[i];
                SetRow(i, $"{entry.rank}. [{entry.lap}] {entry.name}", i);
            }
            else
            {
                rowPool[i].SetActive(false);
            }
        }
    }

    private void SetRow(int index, string label, int rankZeroBased)
    {
        rowPool[index].SetActive(true);
        var text = rowPool[index].GetComponent<Text>();
        if (text != null)
        {
            text.text = label;
            // Highlight top 3
            text.color = LeaderboardFormatter.RankColor(rankZeroBased);
        }
    }
}
