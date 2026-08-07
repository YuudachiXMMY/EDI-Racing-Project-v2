using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
///
/// Display size: press <see cref="ToggleKey"/> (Tab) to cycle Normal → Enlarged → Fullscreen.
/// The two zoomed modes grow the panel, background, and fonts so the leaderboard is legible on
/// a projector, and trim the list to the top <see cref="ZoomedMaxRows"/> (default 10). The panel's
/// own RectTransform / background Image / "Title" label / Content parent are reconfigured at
/// runtime, so no scene wiring is required — the Normal layout captured at Start is restored
/// exactly when cycling back.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    /// <summary>Projector-visibility presets cycled by <see cref="ToggleKey"/>.</summary>
    public enum DisplayMode { Normal, Enlarged, Fullscreen }

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

    [Tooltip("Maximum rows to display in Normal mode")]
    public int MaxRows = 15;

    [Header("Display Modes (Tab to cycle)")]
    [Tooltip("Key that cycles Normal → Enlarged → Fullscreen leaderboard sizes")]
    public Key ToggleKey = Key.Tab;

    [Tooltip("Rows shown in the Enlarged / Fullscreen projector modes (top N)")]
    public int ZoomedMaxRows = 10;

    [Tooltip("Row font size in Normal / Enlarged / Fullscreen")]
    public int NormalRowFontSize = 16;
    public int EnlargedRowFontSize = 34;
    public int FullscreenRowFontSize = 48;

    [Tooltip("Title font size in Enlarged / Fullscreen (Normal keeps the scene value)")]
    public int EnlargedTitleFontSize = 44;
    public int FullscreenTitleFontSize = 64;

    private readonly List<GameObject> rowPool = new List<GameObject>();
    private float timer;

    // --- Display-mode state -------------------------------------------------
    private DisplayMode currentMode = DisplayMode.Normal;
    private int currentRowFontSize;

    private RectTransform panelRect;
    private Image background;
    private Text titleText;
    private RectTransform contentRect;

    // Normal-layout snapshot captured at Start, restored verbatim when cycling back to Normal.
    private Vector2 normAnchorMin, normAnchorMax, normPivot, normOffsetMin, normOffsetMax;
    private Vector2 normContentOffsetMin, normContentOffsetMax;
    private float normBgAlpha;
    private int normTitleFontSize;

    private void Start()
    {
        // Defensive auto-wire: SceneWiring/TrackSetupEditor do not re-assign these on an
        // already-existing panel, so a scene can ship with them unset — which leaves the
        // leaderboard visible but permanently empty (the refresh early-returns on null).
        if (ScoreManager == null)
            ScoreManager = FindFirstObjectByType<ScoreManager>(FindObjectsInactive.Include);

        if (NetworkSync == null)
            NetworkSync = FindFirstObjectByType<NetworkSync>(FindObjectsInactive.Include);

        CacheDisplayModeTargets();

        // Pre-instantiate row pool. Size it to the largest mode so a zoomed mode that shows more
        // rows than Normal (if someone raises ZoomedMaxRows) never indexes past the pool.
        int poolSize = Mathf.Max(MaxRows, ZoomedMaxRows);
        for (int i = 0; i < poolSize; i++)
        {
            GameObject row = Instantiate(RowPrefab, ContentParent);
            row.SetActive(false);
            rowPool.Add(row);
        }
    }

    // Resolve and snapshot the objects the zoom modes mutate. All are optional — the panel still
    // shows rows if a scene is missing the Title / background, the zoom just won't restyle them.
    private void CacheDisplayModeTargets()
    {
        panelRect = GetComponent<RectTransform>();
        background = GetComponent<Image>();

        Transform titleTf = transform.Find("Title");
        if (titleTf != null) titleText = titleTf.GetComponent<Text>();

        contentRect = ContentParent as RectTransform;

        if (panelRect != null)
        {
            normAnchorMin = panelRect.anchorMin;
            normAnchorMax = panelRect.anchorMax;
            normPivot = panelRect.pivot;
            normOffsetMin = panelRect.offsetMin;
            normOffsetMax = panelRect.offsetMax;
        }
        if (background != null) normBgAlpha = background.color.a;
        if (titleText != null) normTitleFontSize = titleText.fontSize;
        if (contentRect != null)
        {
            normContentOffsetMin = contentRect.offsetMin;
            normContentOffsetMax = contentRect.offsetMax;
        }

        currentRowFontSize = NormalRowFontSize;
    }

    private void Update()
    {
        HandleToggleInput();

        timer += Time.unscaledDeltaTime;
        if (timer < UpdateInterval) return;
        timer = 0f;

        RefreshLeaderboard();
    }

    private void HandleToggleInput()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[ToggleKey].wasPressedThisFrame)
            SetDisplayMode(NextMode(currentMode));
    }

    /// <summary>Pure cycle order: Normal → Enlarged → Fullscreen → Normal.</summary>
    public static DisplayMode NextMode(DisplayMode mode) => mode switch
    {
        DisplayMode.Normal => DisplayMode.Enlarged,
        DisplayMode.Enlarged => DisplayMode.Fullscreen,
        _ => DisplayMode.Normal
    };

    /// <summary>Rows actually drawn: full <see cref="MaxRows"/> in Normal, trimmed to the top
    /// <see cref="ZoomedMaxRows"/> in the projector modes.</summary>
    private int EffectiveMaxRows => currentMode == DisplayMode.Normal ? MaxRows : ZoomedMaxRows;

    /// <summary>
    /// Switch projector size. Reconfigures the panel layout/fonts and refreshes immediately so the
    /// row count and font update on the same frame instead of after the next 0.5s tick.
    /// </summary>
    public void SetDisplayMode(DisplayMode mode)
    {
        currentMode = mode;
        ApplyModeLayout();
        RefreshLeaderboard();
    }

    private void ApplyModeLayout()
    {
        switch (currentMode)
        {
            case DisplayMode.Enlarged:
                currentRowFontSize = EnlargedRowFontSize;
                // Tall panel on the left half of the screen — big enough to read from the back of
                // the room without covering the whole race view.
                ApplyStretchRect(new Vector2(0.03f, 0.08f), new Vector2(0.45f, 0.95f));
                ApplyContentInsets(new Vector2(12f, 12f), new Vector2(-12f, -(EnlargedTitleFontSize + 24f)));
                SetBackgroundAlpha(0.85f);
                SetTitleFontSize(EnlargedTitleFontSize);
                break;

            case DisplayMode.Fullscreen:
                currentRowFontSize = FullscreenRowFontSize;
                ApplyStretchRect(Vector2.zero, Vector2.one);
                ApplyContentInsets(new Vector2(48f, 24f), new Vector2(-48f, -(FullscreenTitleFontSize + 36f)));
                SetBackgroundAlpha(0.95f);
                SetTitleFontSize(FullscreenTitleFontSize);
                break;

            default: // Normal — restore the exact scene-authored layout captured at Start.
                currentRowFontSize = NormalRowFontSize;
                RestoreNormalLayout();
                break;
        }
    }

    private void ApplyStretchRect(Vector2 anchorMin, Vector2 anchorMax)
    {
        if (panelRect == null) return;
        panelRect.anchorMin = anchorMin;
        panelRect.anchorMax = anchorMax;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
    }

    private void ApplyContentInsets(Vector2 offsetMin, Vector2 offsetMax)
    {
        if (contentRect == null) return;
        contentRect.offsetMin = offsetMin;
        contentRect.offsetMax = offsetMax;
    }

    private void RestoreNormalLayout()
    {
        if (panelRect != null)
        {
            panelRect.anchorMin = normAnchorMin;
            panelRect.anchorMax = normAnchorMax;
            panelRect.pivot = normPivot;
            panelRect.offsetMin = normOffsetMin;
            panelRect.offsetMax = normOffsetMax;
        }
        if (contentRect != null)
        {
            contentRect.offsetMin = normContentOffsetMin;
            contentRect.offsetMax = normContentOffsetMax;
        }
        SetBackgroundAlpha(normBgAlpha);
        SetTitleFontSize(normTitleFontSize);
    }

    private void SetBackgroundAlpha(float alpha)
    {
        if (background == null) return;
        Color c = background.color;
        c.a = alpha;
        background.color = c;
    }

    private void SetTitleFontSize(int size)
    {
        if (titleText != null) titleText.fontSize = size;
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
        int displayCount = Mathf.Min(Mathf.Min(ranked.Count, EffectiveMaxRows), rowPool.Count);

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
        int displayCount = rankings != null
            ? Mathf.Min(Mathf.Min(rankings.Length, EffectiveMaxRows), rowPool.Count)
            : 0;

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
            text.fontSize = currentRowFontSize;
            // Highlight top 3
            text.color = LeaderboardFormatter.RankColor(rankZeroBased);
        }
    }
}
