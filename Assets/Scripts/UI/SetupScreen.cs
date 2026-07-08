using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-race setup overlay. Shown during GameState.Setup.
/// Allows starting race with default CSV data or loading a saved session.
/// </summary>
public class SetupScreen : MonoBehaviour
{
    [Header("References")]
    public RaceManager RaceManager;

    [Header("UI Elements")]
    public Button StartDefaultButton;
    public Button LoadSessionButton;
    public Text InfoText;

    private void Start()
    {
        if (StartDefaultButton != null)
            StartDefaultButton.onClick.AddListener(StartWithDefaultData);
        if (LoadSessionButton != null)
            LoadSessionButton.onClick.AddListener(LoadLatestSession);

        if (InfoText != null)
            InfoText.text = "Ready to start race.";
    }

    private void StartWithDefaultData()
    {
        if (RaceManager == null) return;

        if (RaceManager.DefaultCsvData != null)
        {
            RaceManager.LoadAndStartRace(RaceManager.DefaultCsvData.text);
            gameObject.SetActive(false);
        }
        else
        {
            if (InfoText != null)
                InfoText.text = "No default CSV data assigned.";
        }
    }

    private void LoadLatestSession()
    {
        if (RaceManager == null || RaceManager.SessionManager == null) return;

        string path = RaceManager.SessionManager.FindLatestSession();
        if (path != null)
        {
            var session = RaceManager.SessionManager.LoadSession(path);
            if (session != null)
            {
                RaceManager.LoadFromSession(session);
                gameObject.SetActive(false);
            }
        }
        else
        {
            if (InfoText != null)
                InfoText.text = "No saved sessions found.";
        }
    }
}
