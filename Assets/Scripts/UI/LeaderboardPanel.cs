using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Real-time leaderboard showing ranked cars.
/// Updates every 0.5s to avoid GC pressure. Uses object pooling for rows.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    [Header("References")]
    public ScoreManager ScoreManager;

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

    private void RefreshLeaderboard()
    {
        if (ScoreManager == null) return;

        List<CarIdentity> ranked = ScoreManager.GetRankedCars();
        int displayCount = Mathf.Min(ranked.Count, MaxRows);

        for (int i = 0; i < rowPool.Count; i++)
        {
            if (i < displayCount)
            {
                rowPool[i].SetActive(true);
                var text = rowPool[i].GetComponent<Text>();
                if (text != null)
                {
                    var car = ranked[i];
                    text.text = $"{i + 1}. [{car.CurrentLap}] {car.TeamName}";

                    // Highlight top 3
                    if (i == 0) text.color = new Color(1f, 0.84f, 0f); // gold
                    else if (i == 1) text.color = new Color(0.75f, 0.75f, 0.75f); // silver
                    else if (i == 2) text.color = new Color(0.8f, 0.5f, 0.2f); // bronze
                    else text.color = Color.white;
                }
            }
            else
            {
                rowPool[i].SetActive(false);
            }
        }
    }
}
