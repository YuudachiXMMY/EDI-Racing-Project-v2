using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Professor-only controls: Pause/Resume toggle, Save Session, Export Results, and the
/// Auto Cam toggle (auto-switching top-3 chase camera). Status text provides feedback and
/// fades after 3 seconds.
/// </summary>
public class RaceControlPanel : MonoBehaviour
{
    [Header("References")]
    public RaceManager RaceManager;
    [Tooltip("Drives the Auto Cam button. Auto-resolved if unset.")]
    public CameraManager CameraManager;
    [Tooltip("Drives the Names toggle button. Auto-resolved if unset.")]
    public CarLabelSpawner CarLabelSpawner;

    [Header("UI Elements")]
    public Button PauseResumeButton;
    public Text PauseResumeLabel;
    public Button SaveButton;
    public Button ExportButton;
    public Button EndRaceButton;
    public Button AutoCamButton;
    public Text AutoCamLabel;
    public Button ToggleNamesButton;
    public Text ToggleNamesLabel;
    public Text StatusText;

    private bool isPaused;
    private bool hasStarted;
    private Coroutine statusFadeCoroutine;

    private void Start()
    {
        // Defensive auto-wire: RaceManager is a unique scene singleton. Its serialized
        // reference here has been lost to {fileID: 0} before (the panel's own button refs
        // survive serialization, but the cross-object RaceManager link can drop), which
        // silently breaks Pause/Save/Export because every handler early-returns on a null
        // RaceManager. Re-resolve by type when unset. Mirrors RaceUI.ResolveMissingReferences.
        if (RaceManager == null)
            RaceManager = FindFirstObjectByType<RaceManager>(FindObjectsInactive.Include);
        if (CameraManager == null)
            CameraManager = FindFirstObjectByType<CameraManager>(FindObjectsInactive.Include);
        if (CarLabelSpawner == null)
            CarLabelSpawner = FindFirstObjectByType<CarLabelSpawner>(FindObjectsInactive.Include);

        if (PauseResumeButton != null)
            PauseResumeButton.onClick.AddListener(TogglePause);
        if (SaveButton != null)
            SaveButton.onClick.AddListener(SaveSession);
        if (ExportButton != null)
            ExportButton.onClick.AddListener(ExportResults);
        if (EndRaceButton != null)
            EndRaceButton.onClick.AddListener(EndRace);
        if (AutoCamButton != null)
            AutoCamButton.onClick.AddListener(ToggleAutoCam);
        if (ToggleNamesButton != null)
            ToggleNamesButton.onClick.AddListener(ToggleNames);

        if (StatusText != null)
            StatusText.text = "";

        // The race now starts Paused (RaceManager.LoadAndStartRace pauses on entry). Track the
        // state here so the button opens showing "Start" — otherwise isPaused stays false and the
        // professor's first click re-pauses instead of resuming (a dead click).
        if (RaceManager != null)
            RaceManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (RaceManager != null)
            RaceManager.OnStateChanged -= HandleStateChanged;
    }

    // Keep the Pause/Resume toggle in sync with the authoritative RaceManager state, whatever the
    // pause source (start-paused, this button, or a future one). Only Racing/Paused are relevant;
    // Setup and Finished leave the label as-is.
    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Paused)
        {
            isPaused = true;
            // Before the race has ever been resumed it opens Paused, so the button reads "Start"
            // (the professor's first click begins the action). Once the race has run, any later
            // pause is a mid-race pause and the button reads "Resume".
            if (PauseResumeLabel != null) PauseResumeLabel.text = hasStarted ? "Resume" : "Start";
        }
        else if (state == GameState.Racing)
        {
            isPaused = false;
            hasStarted = true;
            if (PauseResumeLabel != null) PauseResumeLabel.text = "Pause";
        }
    }

    private void ToggleNames()
    {
        if (CarLabelSpawner == null) return;
        CarLabelSpawner.ToggleLabels();
        bool on = CarLabelSpawner.LabelsVisible;
        if (ToggleNamesLabel != null) ToggleNamesLabel.text = on ? "Names: On" : "Names: Off";
        ShowStatus(on ? "Car names shown" : "Car names hidden");
    }

    private void ToggleAutoCam()
    {
        if (CameraManager == null) return;
        CameraManager.ToggleAutoSwitch();

        // The button never turns Auto Cam off — it flips between the two auto modes (Esc/F1-F9 exit).
        if (CameraManager.CurrentMode == CameraManager.CameraMode.AutoAllCams)
        {
            if (AutoCamLabel != null) AutoCamLabel.text = "Auto: All Cam";
            ShowStatus("Auto camera: all cams on leader");
        }
        else
        {
            if (AutoCamLabel != null) AutoCamLabel.text = "Auto: Top 3";
            ShowStatus("Auto camera: following top 3");
        }
    }

    private void TogglePause()
    {
        if (RaceManager == null) return;

        if (isPaused)
        {
            RaceManager.ResumeRace();
            isPaused = false;
            if (PauseResumeLabel != null) PauseResumeLabel.text = "Pause";
        }
        else
        {
            RaceManager.PauseRace();
            isPaused = true;
            if (PauseResumeLabel != null) PauseResumeLabel.text = "Resume";
        }
    }

    private void SaveSession()
    {
        if (RaceManager == null) return;
        RaceManager.SaveCurrentSession();
        ShowStatus("Session saved!");
    }

    private void ExportResults()
    {
        if (RaceManager == null) return;
        RaceManager.ExportCurrentResults();
        ShowStatus("Results exported!");
    }

    private void EndRace()
    {
        if (RaceManager == null) return;
        RaceManager.EndRace();
        ShowStatus("Race ended — results sent");
    }

    private void ShowStatus(string message)
    {
        if (StatusText == null) return;
        StatusText.text = message;
        StatusText.color = Color.white;

        if (statusFadeCoroutine != null)
            StopCoroutine(statusFadeCoroutine);
        statusFadeCoroutine = StartCoroutine(FadeStatus());
    }

    private IEnumerator FadeStatus()
    {
        yield return new WaitForSecondsRealtime(2f);

        float elapsed = 0f;
        Color startColor = StatusText.color;
        while (elapsed < 1f)
        {
            elapsed += Time.unscaledDeltaTime;
            StatusText.color = Color.Lerp(startColor, Color.clear, elapsed);
            yield return null;
        }
        StatusText.text = "";
    }
}
