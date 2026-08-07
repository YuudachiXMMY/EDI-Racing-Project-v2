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
///   • Normal     — the scene-authored top-left HUD panel, top <see cref="MaxRows"/> (15).
///   • Enlarged   — large single-column panel, top <see cref="EnlargedMaxRows"/> (20).
///   • Fullscreen — full-screen panel, the whole field up to <see cref="FullscreenMaxRows"/> (60)
///                  laid out in <see cref="ColumnCount"/> (3) rank-ordered columns.
///
/// Rows are pooled and re-parented between one column (Normal/Enlarged) and three columns
/// (Fullscreen) via a HorizontalLayoutGroup of VerticalLayoutGroups, so column widths split
/// evenly with no pixel math. The panel's own RectTransform / background Image / "Title" label /
/// Content parent are reconfigured at runtime, so no scene wiring is required — the Normal layout
/// captured at Start is restored exactly when cycling back.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    /// <summary>Projector-visibility presets cycled by <see cref="ToggleKey"/>.</summary>
    public enum DisplayMode { Normal, Enlarged, Fullscreen }

    /// <summary>
    /// Raised whenever the display mode changes; the argument is true when the new mode is
    /// <see cref="DisplayMode.Fullscreen"/>. RaceUI subscribes to this to hide the EventPanel
    /// behind the full-screen leaderboard and restore it once the leaderboard shrinks back.
    /// The leaderboard itself stays deliberately unaware of the EventPanel.
    /// </summary>
    public event System.Action<bool> OnFullscreenChanged;

    /// <summary>Columns used by the Fullscreen layout (rank-ordered, filled top-to-bottom).</summary>
    public const int ColumnCount = 3;

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

    [Header("Row counts per mode")]
    [Tooltip("Normal mode: top N rows (single column)")]
    public int MaxRows = 15;

    [Tooltip("Enlarged mode: top N rows (single column)")]
    public int EnlargedMaxRows = 20;

    [Tooltip("Fullscreen mode: whole field, up to N rows across three columns")]
    public int FullscreenMaxRows = 60;

    [Header("Display Modes (Tab to cycle)")]
    [Tooltip("Key that cycles Normal → Enlarged → Fullscreen leaderboard sizes")]
    public Key ToggleKey = Key.Tab;

    [Tooltip("Row font size in Normal / Enlarged / Fullscreen")]
    public int NormalRowFontSize = 16;
    public int EnlargedRowFontSize = 30;
    public int FullscreenRowFontSize = 28;

    [Tooltip("Title font size in Enlarged / Fullscreen (Normal keeps the scene value)")]
    public int EnlargedTitleFontSize = 40;
    public int FullscreenTitleFontSize = 48;

    private readonly List<GameObject> rowPool = new List<GameObject>();
    private float timer;

    // Row height as a multiple of the row font size, used in the zoomed modes so each row is tall
    // enough for its enlarged text (the row prefab's LayoutElement pins height to 25px and its Text
    // clips vertical overflow — at the larger fonts that clips the whole line, which is why a zoomed
    // leaderboard would render blank without this).
    private const float RowHeightFactor = 1.4f;

    // --- Display-mode state -------------------------------------------------
    private DisplayMode currentMode = DisplayMode.Normal;
    private int currentRowFontSize;
    private float currentRowHeight;

    // Re-parenting rows across columns is only needed when the mode or the visible-row count
    // changes; this signature detects that so per-tick refreshes just restyle in place.
    private int lastLayoutSignature = -1;

    private RectTransform panelRect;
    private Image background;
    private Text titleText;
    private RectTransform contentRect;

    // Column containers built under ContentParent. columns[0] is used alone in Normal/Enlarged;
    // all three are used in Fullscreen.
    private readonly RectTransform[] columns = new RectTransform[ColumnCount];

    // Normal-layout snapshot captured at Start, restored verbatim when cycling back to Normal.
    private Vector2 normAnchorMin, normAnchorMax, normPivot, normOffsetMin, normOffsetMax;
    private Vector2 normContentOffsetMin, normContentOffsetMax;
    private float normBgAlpha;
    private int normTitleFontSize;
    // Row prefab defaults, captured from the pool so Normal restores the shipped look exactly.
    private float normRowPreferredHeight = 25f;
    private VerticalWrapMode normRowVerticalOverflow = VerticalWrapMode.Truncate;

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
        BuildColumns();

        // Pre-instantiate row pool, sized to the largest mode. Rows start in column 0 and are
        // re-parented per mode by the refresh.
        int poolSize = Mathf.Max(Mathf.Max(MaxRows, EnlargedMaxRows), FullscreenMaxRows);
        Transform rowParent = columns[0] != null ? columns[0] : ContentParent;
        for (int i = 0; i < poolSize; i++)
        {
            GameObject row = Instantiate(RowPrefab, rowParent);
            row.SetActive(false);
            rowPool.Add(row);
        }

        CaptureRowDefaults();
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

    // Build a HorizontalLayoutGroup of ColumnCount VerticalLayoutGroups that fills ContentParent.
    // The scene's own VerticalLayoutGroup on ContentParent is disabled so it doesn't fight this.
    // childForceExpandWidth splits the columns evenly (no pixel math); rows keep natural, top-
    // aligned heights within each column.
    private void BuildColumns()
    {
        if (ContentParent == null) return;

        var sceneVlg = ContentParent.GetComponent<VerticalLayoutGroup>();
        if (sceneVlg != null) sceneVlg.enabled = false;

        var rootGO = new GameObject("ColumnsRoot", typeof(RectTransform));
        var rootRT = rootGO.GetComponent<RectTransform>();
        rootRT.SetParent(ContentParent, false);
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        var hlg = rootGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.UpperLeft;

        for (int c = 0; c < ColumnCount; c++)
        {
            var colGO = new GameObject("Column" + c, typeof(RectTransform));
            var colRT = colGO.GetComponent<RectTransform>();
            colRT.SetParent(rootRT, false);

            var vlg = colGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            columns[c] = colRT;
        }
    }

    // Snapshot the row prefab's height / overflow from the first pooled instance so Normal restores
    // the exact shipped look, while the zoomed modes can safely grow both to fit larger fonts.
    private void CaptureRowDefaults()
    {
        if (rowPool.Count == 0) return;

        var le = rowPool[0].GetComponent<LayoutElement>();
        if (le != null && le.preferredHeight > 0f) normRowPreferredHeight = le.preferredHeight;

        var text = rowPool[0].GetComponent<Text>();
        if (text != null) normRowVerticalOverflow = text.verticalOverflow;

        currentRowHeight = normRowPreferredHeight;
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

    /// <summary>Rows actually drawn for the current mode.</summary>
    private int EffectiveMaxRows => currentMode switch
    {
        DisplayMode.Enlarged => EnlargedMaxRows,
        DisplayMode.Fullscreen => FullscreenMaxRows,
        _ => MaxRows
    };

    /// <summary>Columns used for the current mode (Fullscreen spreads across three).</summary>
    private int ActiveColumnCount => currentMode == DisplayMode.Fullscreen ? ColumnCount : 1;

    /// <summary>
    /// Switch projector size. Reconfigures the panel layout/fonts and refreshes immediately so the
    /// row count, columns and font update on the same frame instead of after the next 0.5s tick.
    /// </summary>
    public void SetDisplayMode(DisplayMode mode)
    {
        currentMode = mode;
        ApplyModeLayout();
        lastLayoutSignature = -1; // force a re-parent/re-distribute on the next render
        RefreshLeaderboard();

        // Notify listeners (RaceUI) so the EventPanel can hide behind the full-screen leaderboard
        // and reappear when it shrinks back. Fired on every mode change; the handler is idempotent.
        OnFullscreenChanged?.Invoke(currentMode == DisplayMode.Fullscreen);
    }

    private void ApplyModeLayout()
    {
        switch (currentMode)
        {
            case DisplayMode.Enlarged:
                currentRowFontSize = EnlargedRowFontSize;
                currentRowHeight = EnlargedRowFontSize * RowHeightFactor;
                // Tall single-column panel on the left of the screen — readable from the back of
                // the room without covering the whole race view.
                ApplyStretchRect(new Vector2(0.03f, 0.03f), new Vector2(0.42f, 0.97f));
                ApplyContentInsets(new Vector2(12f, 12f), new Vector2(-12f, -(EnlargedTitleFontSize + 20f)));
                SetBackgroundAlpha(0.85f);
                SetTitleFontSize(EnlargedTitleFontSize);
                break;

            case DisplayMode.Fullscreen:
                currentRowFontSize = FullscreenRowFontSize;
                currentRowHeight = FullscreenRowFontSize * RowHeightFactor;
                ApplyStretchRect(Vector2.zero, Vector2.one);
                ApplyContentInsets(new Vector2(40f, 20f), new Vector2(-40f, -(FullscreenTitleFontSize + 28f)));
                SetBackgroundAlpha(0.95f);
                SetTitleFontSize(FullscreenTitleFontSize);
                break;

            default: // Normal — restore the exact scene-authored layout captured at Start.
                currentRowFontSize = NormalRowFontSize;
                currentRowHeight = normRowPreferredHeight;
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
        bool replace = BeginLayout(displayCount, out int rowsPerColumn);

        for (int i = 0; i < rowPool.Count; i++)
        {
            if (i < displayCount)
            {
                if (replace) PlaceRow(i, rowsPerColumn);
                var car = ranked[i];
                StyleRow(i, $"{i + 1}. [{car.CurrentLap}] {car.TeamName}", i);
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
        bool replace = BeginLayout(displayCount, out int rowsPerColumn);

        for (int i = 0; i < rowPool.Count; i++)
        {
            if (i < displayCount)
            {
                if (replace) PlaceRow(i, rowsPerColumn);
                var entry = rankings[i];
                StyleRow(i, $"{entry.rank}. [{entry.lap}] {entry.name}", i);
            }
            else
            {
                rowPool[i].SetActive(false);
            }
        }
    }

    // Decide the column layout for this many rows. Activates the columns in use, returns the rows
    // per column, and reports whether rows need re-parenting (mode or count changed since last time).
    private bool BeginLayout(int displayCount, out int rowsPerColumn)
    {
        int cols = ActiveColumnCount;
        rowsPerColumn = Mathf.Max(1, Mathf.CeilToInt(displayCount / (float)cols));

        int signature = ((int)currentMode * 1000000) + displayCount;
        if (signature == lastLayoutSignature) return false;
        lastLayoutSignature = signature;

        for (int c = 0; c < ColumnCount; c++)
        {
            if (columns[c] != null)
                columns[c].gameObject.SetActive(c < cols);
        }
        return true;
    }

    // Column-major placement: rows 0..rpc-1 fill column 0 (ranks 1-20), the next rpc fill column 1,
    // and so on, so each column stays rank-ordered top-to-bottom.
    private void PlaceRow(int index, int rowsPerColumn)
    {
        int col = Mathf.Min(index / rowsPerColumn, ColumnCount - 1);
        int slot = index - (col * rowsPerColumn);

        Transform parent = columns[col] != null ? columns[col] : ContentParent;
        Transform rowTf = rowPool[index].transform;
        if (rowTf.parent != parent) rowTf.SetParent(parent, false);
        rowTf.SetSiblingIndex(slot);
    }

    private void StyleRow(int index, string label, int rankZeroBased)
    {
        GameObject row = rowPool[index];
        row.SetActive(true);

        var text = row.GetComponent<Text>();
        if (text != null)
        {
            text.text = label;
            text.fontSize = currentRowFontSize;
            // Zoomed rows must not clip: the prefab clips vertical overflow, which hides an
            // enlarged line entirely. Normal keeps the prefab's original overflow behaviour.
            text.verticalOverflow = currentMode == DisplayMode.Normal
                ? normRowVerticalOverflow
                : VerticalWrapMode.Overflow;
            // Highlight top 3
            text.color = LeaderboardFormatter.RankColor(rankZeroBased);
        }

        // Grow the row's laid-out height with the font so the VerticalLayoutGroup gives each line
        // enough space (and rows don't overlap) in the projector modes.
        var le = row.GetComponent<LayoutElement>();
        if (le != null) le.preferredHeight = currentRowHeight;
    }
}
