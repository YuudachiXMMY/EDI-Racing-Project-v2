using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a winner overlay with top 3 standings when the race finishes.
/// Assignable via Inspector.
/// </summary>
public class RaceFinishPanel : MonoBehaviour
{
    [Header("References")]
    public RaceManager RaceManager;
    public ScoreManager ScoreManager;

    private GameObject panel;
    private Text resultsText;

    private void OnEnable()
    {
        if (RaceManager != null)
            RaceManager.OnRaceFinished += ShowFinish;
    }

    private void OnDisable()
    {
        if (RaceManager != null)
            RaceManager.OnRaceFinished -= ShowFinish;
    }

    private void ShowFinish(CarIdentity winner)
    {
        if (panel != null)
        {
            panel.SetActive(true);
            UpdateResults(winner);
            return;
        }

        BuildPanel(winner);
    }

    private void BuildPanel(CarIdentity winner)
    {
        // Screen-space canvas for the overlay
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        // Semi-transparent background — center of screen
        panel = new GameObject("FinishOverlay");
        panel.transform.SetParent(parent, false);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.75f);

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.2f, 0.2f);
        rt.anchorMax = new Vector2(0.8f, 0.8f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Results text
        GameObject textObj = new GameObject("ResultsText");
        textObj.transform.SetParent(panel.transform, false);

        resultsText = textObj.AddComponent<Text>();
        resultsText.fontSize = 28;
        resultsText.alignment = TextAnchor.MiddleCenter;
        resultsText.color = Color.white;
        resultsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resultsText.supportRichText = true;
        resultsText.horizontalOverflow = HorizontalWrapMode.Overflow;
        resultsText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(20, 20);
        textRt.offsetMax = new Vector2(-20, -20);

        UpdateResults(winner);
    }

    private void UpdateResults(CarIdentity winner)
    {
        if (resultsText == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=42><color=yellow>RACE FINISHED!</color></size>");
        sb.AppendLine();
        sb.AppendLine($"<size=34><color=yellow>Winner: {winner.TeamName}</color></size>");
        sb.AppendLine();

        if (ScoreManager != null)
        {
            List<CarIdentity> ranked = ScoreManager.GetRankedCars();
            int count = Mathf.Min(ranked.Count, 5);
            sb.AppendLine("<size=24>── Final Standings ──</size>");
            sb.AppendLine();
            for (int i = 0; i < count; i++)
            {
                var car = ranked[i];
                string color = i == 0 ? "yellow" : i == 1 ? "#C0C0C0" : i == 2 ? "#CD7F32" : "white";
                sb.AppendLine($"<color={color}>{i + 1}. {car.TeamName}  (Lap {car.CurrentLap})</color>");
            }
        }

        resultsText.text = sb.ToString();
    }

    /// <summary>
    /// Hides the panel. Called on race reset via OnStateChanged.
    /// </summary>
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
