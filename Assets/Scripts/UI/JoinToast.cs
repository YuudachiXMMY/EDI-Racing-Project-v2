using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left transient "student joined" toast. ONE reusable label that shows the most recent
/// join and auto-dissolves a few seconds later. Coalescing (a single label whose fade timer resets
/// on every new message) is deliberate and load-bearing: with up to ~500 students joining in a
/// burst, spawning a toast object per join would flood the HUD and thrash the GC. One label that
/// just refreshes its text stays O(1) no matter the join rate.
///
/// Self-bootstrapping: if <see cref="Group"/>/<see cref="Label"/> are left unassigned, the first
/// <see cref="Show"/> builds a screen-space overlay canvas + a bottom-left label entirely in code,
/// so the toast needs zero scene wiring (WebGL-safe). Assign the fields in the Editor to skin it.
///
/// Place on a standalone, always-active GameObject (see <see cref="CreateDefault"/>) rather than on
/// a panel that hides itself — the fade runs as a coroutine and needs a live host to tick.
/// </summary>
public class JoinToast : MonoBehaviour
{
    [Header("Optional scene refs (auto-built if empty)")]
    public CanvasGroup Group;
    public Text Label;

    [Header("Timing")]
    [Tooltip("Seconds the toast stays fully opaque before it begins to fade.")]
    public float DisplaySeconds = 3f;
    [Tooltip("Seconds the fade-out takes once the hold expires.")]
    public float FadeSeconds = 0.75f;

    private Coroutine fade;

    /// <summary>Show a message bottom-left and (re)start the auto-dissolve timer.</summary>
    public void Show(string message)
    {
        EnsureUi();
        if (Label != null) Label.text = message;
        if (Group != null) Group.alpha = 1f;

        // A coroutine needs a live host. If this component is somehow inactive, leave the toast
        // visible rather than silently dropping it (better a lingering label than none).
        if (!isActiveAndEnabled) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // Unscaled time so a paused or slow-mo race clock never freezes the toast.
        float held = 0f;
        while (held < DisplaySeconds)
        {
            held += Time.unscaledDeltaTime;
            yield return null;
        }

        float dur = Mathf.Max(0.0001f, FadeSeconds);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            if (Group != null) Group.alpha = Mathf.Clamp01(1f - elapsed / dur);
            yield return null;
        }

        if (Group != null) Group.alpha = 0f;
        fade = null;
    }

    // Build a screen-space overlay canvas + a single bottom-left label, all in code, so the toast
    // works with no scene setup. Parented under this component's GameObject to share its lifetime.
    private void EnsureUi()
    {
        if (Group != null && Label != null) return;

        var canvasGo = new GameObject("JoinToastCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // above the race HUD

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var labelGo = new GameObject("Label", typeof(CanvasGroup), typeof(Text), typeof(Outline));
        labelGo.transform.SetParent(canvasGo.transform, false);

        var rt = labelGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;   // pin to bottom-left corner
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(32f, 32f);
        rt.sizeDelta = new Vector2(720f, 64f);

        Label = labelGo.GetComponent<Text>();
        Label.font = LoadUiFont();
        Label.fontSize = 28;
        Label.alignment = TextAnchor.LowerLeft;
        Label.horizontalOverflow = HorizontalWrapMode.Overflow;
        Label.verticalOverflow = VerticalWrapMode.Overflow;
        Label.color = Color.white;
        Label.raycastTarget = false; // never intercept clicks — it's a passive notice

        var outline = labelGo.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f); // legibility over any background
        outline.effectDistance = new Vector2(2f, -2f);

        Group = labelGo.GetComponent<CanvasGroup>();
        Group.alpha = 0f;
        Group.interactable = false;
        Group.blocksRaycasts = false;
    }

    // Unity 6 ships the legacy dynamic font as "LegacyRuntime.ttf" (the old "Arial.ttf" builtin was
    // removed). Fall back to the old name for safety; a null font just renders nothing, never throws.
    private static Font LoadUiFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    /// <summary>
    /// Spawn a standalone, always-active toast host. Callers with no scene-assigned toast use this so
    /// the label survives a panel (e.g. the Setup screen) deactivating itself when the race starts.
    /// </summary>
    public static JoinToast CreateDefault()
    {
        var go = new GameObject("JoinToast");
        return go.AddComponent<JoinToast>();
    }
}
