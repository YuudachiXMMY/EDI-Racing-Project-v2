using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Professor-only live race-control menu (formerly a list of one button per pre-baked rule).
/// Six primary actions — Name Length, Male, Color Boost, Color Penalty, Function Boost,
/// Function Penalty — each open a fade-in secondary menu where the professor picks the exact
/// parameter (a number, accelerate/decelerate, a colour, or a function). The chosen action is
/// built by <see cref="EventActionBuilder"/> and applied via <see cref="EventManager.TriggerRule"/>.
/// Primary actions are also reachable by digit keys 1-6; Snow/Night fire directly on keys 9/0.
///
/// The whole menu is built in code (only EventManager is serialized) so a merge can't ship a
/// half-wired panel — mirrors the runtime-UI approach in RaceUI.CreateTouchButton. The class name
/// and the ContentParent/EventRowPrefab fields are kept so existing RaceUI/TrackSetupEditor wiring
/// and the HUD visibility tests continue to work unchanged.
/// </summary>
public class EventPanel : MonoBehaviour
{
    [Header("References")]
    public EventManager EventManager;

    [Header("UI (legacy fields — set by TrackSetupEditor)")]
    [Tooltip("Parent for the primary action buttons. Auto-created under this panel if unset.")]
    public Transform ContentParent;

    [Tooltip("Unused since the menu rewrite; kept so existing scene wiring still compiles.")]
    public GameObject EventRowPrefab;

    private CanvasGroup overlayGroup;
    private Transform overlayContent;
    private Text overlayTitle;
    private InputField nameLengthField;
    private Coroutine fadeCoroutine;
    private bool built;

    // Unity 6 ships the legacy dynamic font as "LegacyRuntime.ttf" (old "Arial.ttf" builtin).
    private static Font UiFont =>
        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

    private void Awake()
    {
        // Defensive auto-wire (mirrors the old EventPanel): a scene may ship with EventManager unset.
        if (EventManager == null)
            EventManager = FindFirstObjectByType<EventManager>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        BuildMenu();
    }

    private void Update()
    {
        if (!built || EventManager == null || !EventManager.IsActive) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        // Don't hijack digit keys while the professor is typing a name-length threshold.
        bool typing = nameLengthField != null && nameLengthField.isFocused;
        if (!typing)
        {
            if (kb[Key.Digit1].wasPressedThisFrame) ShowNameLength();
            else if (kb[Key.Digit2].wasPressedThisFrame) ShowMale();
            else if (kb[Key.Digit3].wasPressedThisFrame) ShowColor(true);
            else if (kb[Key.Digit4].wasPressedThisFrame) ShowColor(false);
            else if (kb[Key.Digit5].wasPressedThisFrame) ShowFunction(true);
            else if (kb[Key.Digit6].wasPressedThisFrame) ShowFunction(false);
        }

        // Weather fires directly (no submenu), even while a submenu is open.
        if (kb[Key.Digit9].wasPressedThisFrame) EventManager.TriggerRule(EventActionBuilder.Snow());
        if (kb[Key.Digit0].wasPressedThisFrame) EventManager.TriggerRule(EventActionBuilder.Night());
    }

    // ── Build ─────────────────────────────────────────────

    private void BuildMenu()
    {
        Transform primaryParent = ContentParent != null ? ContentParent : CreatePrimaryColumn();

        CreateButton(primaryParent, "NameLength", "[1] Name Length", ShowNameLength);
        CreateButton(primaryParent, "Male", "[2] Male", ShowMale);
        CreateButton(primaryParent, "ColorBoost", "[3] Color Boost", () => ShowColor(true));
        CreateButton(primaryParent, "ColorPenalty", "[4] Color Penalty", () => ShowColor(false));
        CreateButton(primaryParent, "FunctionBoost", "[5] Function Boost", () => ShowFunction(true));
        CreateButton(primaryParent, "FunctionPenalty", "[6] Function Penalty", () => ShowFunction(false));

        var hint = CreateText(primaryParent, "WeatherHint", "9 = Snow     0 = Night", 12, TextAnchor.MiddleCenter);
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        BuildOverlay();
        built = true;
    }

    // Fallback primary column when TrackSetupEditor did not provide a ContentParent.
    private Transform CreatePrimaryColumn()
    {
        var go = new GameObject("Primary", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(5f, 5f); rt.offsetMax = new Vector2(-5f, -5f);

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        return go.transform;
    }

    private void BuildOverlay()
    {
        var go = new GameObject("SecondaryMenu",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);

        overlayGroup = go.GetComponent<CanvasGroup>();

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f; vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;

        overlayTitle = CreateText(go.transform, "Title", "", 14, TextAnchor.UpperCenter);

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(go.transform, false);
        var cvlg = contentGO.AddComponent<VerticalLayoutGroup>();
        cvlg.spacing = 5f;
        cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
        cvlg.childControlWidth = true; cvlg.childControlHeight = true;
        overlayContent = contentGO.transform;

        CreateButton(go.transform, "Cancel", "Cancel", HideOverlay);

        HideOverlayImmediate();
    }

    // ── Secondary menus ───────────────────────────────────

    private void ShowNameLength()
    {
        ClearOverlayContent();
        overlayTitle.text = "Name Length — slow cars whose name is longer than N (-15)";
        nameLengthField = CreateIntInputField(overlayContent);
        CreateButton(overlayContent, "Confirm", "Confirm (-15)", () =>
        {
            if (nameLengthField != null && int.TryParse(nameLengthField.text, out int n))
            {
                EventManager.TriggerRule(EventActionBuilder.NameLengthPenalty(n));
                HideOverlay();
            }
        });
        FadeInOverlay();
    }

    private void ShowMale()
    {
        ClearOverlayContent();
        overlayTitle.text = "Male — cars with the male feature";
        CreateButton(overlayContent, "Accelerate", "Accelerate (+20)", () => Apply(EventActionBuilder.Male(true)));
        CreateButton(overlayContent, "Decelerate", "Decelerate (-15)", () => Apply(EventActionBuilder.Male(false)));
        FadeInOverlay();
    }

    private void ShowColor(bool boost)
    {
        ClearOverlayContent();
        overlayTitle.text = boost
            ? "Color Boost — accelerate a colour (+20)"
            : "Color Penalty — decelerate a colour (-15)";
        foreach (var c in EventActionBuilder.Colors)
        {
            var color = c; // capture per iteration
            CreateButton(overlayContent, $"Color_{color.Label}", color.Label,
                () => Apply(EventActionBuilder.Color(color.Index, boost)));
        }
        FadeInOverlay();
    }

    private void ShowFunction(bool boost)
    {
        ClearOverlayContent();
        overlayTitle.text = boost
            ? "Function Boost — accelerate a feature (+20)"
            : "Function Penalty — decelerate a feature (-15)";
        foreach (var f in EventActionBuilder.Functions)
        {
            var fn = f; // capture per iteration
            CreateButton(overlayContent, $"Fn_{fn.Label}", fn.Label,
                () => Apply(EventActionBuilder.Function(fn.Tag, boost)));
        }
        FadeInOverlay();
    }

    private void Apply(EventRule rule)
    {
        if (EventManager != null) EventManager.TriggerRule(rule);
        HideOverlay();
    }

    // ── Overlay fade ──────────────────────────────────────

    private void FadeInOverlay()
    {
        overlayGroup.blocksRaycasts = true;
        overlayGroup.interactable = true;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTo(1f, 0.2f));
    }

    private void HideOverlay()
    {
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;
        nameLengthField = null;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTo(0f, 0.15f));
    }

    private void HideOverlayImmediate()
    {
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;
    }

    // Uses unscaledDeltaTime so the fade runs while the race is paused (RaceControlPanel pattern).
    private IEnumerator FadeTo(float target, float duration)
    {
        float start = overlayGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        overlayGroup.alpha = target;
    }

    private void ClearOverlayContent()
    {
        nameLengthField = null;
        if (overlayContent == null) return;
        for (int i = overlayContent.childCount - 1; i >= 0; i--)
            Destroy(overlayContent.GetChild(i).gameObject);
    }

    // ── Runtime UGUI helpers (mirror RaceUI.CreateTouchButton) ─────────────

    private Button CreateButton(Transform parent, string name, string label, UnityAction onClick)
    {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 30f;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var rt = txtGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var t = txtGO.AddComponent<Text>();
        t.text = label;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 15;
        t.color = Color.white;
        t.font = UiFont;
        return btn;
    }

    private Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 26f;

        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.font = UiFont;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private InputField CreateIntInputField(Transform parent)
    {
        var go = new GameObject("NameLengthInput",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 32f;

        go.GetComponent<Image>().color = Color.white;

        var input = go.AddComponent<InputField>();

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6f, 4f); trt.offsetMax = new Vector2(-6f, -4f);

        var txt = txtGO.AddComponent<Text>();
        txt.font = UiFont;
        txt.color = Color.black;
        txt.fontSize = 16;
        txt.supportRichText = false;

        input.textComponent = txt;
        input.contentType = InputField.ContentType.IntegerNumber;
        input.text = "10";
        return input;
    }
}
