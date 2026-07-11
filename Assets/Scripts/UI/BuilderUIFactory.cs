using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Static factory methods for creating runtime UI elements.
/// Used by all builder panels to construct UI without prefabs.
/// Mirrors RuntimeSetup.cs UI factory helpers.
/// </summary>
public static class BuilderUIFactory
{
    private static Font cachedFont;

    private static Font GetFont()
    {
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    public static GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        Color? bgColor = null)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image bg = obj.AddComponent<Image>();
        bg.color = bgColor ?? new Color(0, 0, 0, 0.6f);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0, 1);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return obj;
    }

    public static Text CreateText(Transform parent, string name, string content,
        int fontSize, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Text text = obj.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.font = GetFont();
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return text;
    }

    public static Button CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        Color? bgColor = null)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image bg = obj.AddComponent<Image>();
        bg.color = bgColor ?? new Color(0.2f, 0.2f, 0.2f, 0.9f);

        Button btn = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
        btn.colors = colors;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        // Label text fills the button
        CreateText(obj.transform, "Label", label, 14, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return btn;
    }

    public static InputField CreateInputField(Transform parent, string name, string placeholder,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        InputField.ContentType contentType = InputField.ContentType.Standard)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        // Text child
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        Text textComp = textObj.AddComponent<Text>();
        textComp.font = GetFont();
        textComp.fontSize = 14;
        textComp.color = Color.white;
        textComp.alignment = TextAnchor.MiddleLeft;
        textComp.supportRichText = false;
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(5, 0);
        textRt.offsetMax = new Vector2(-5, 0);

        // Placeholder child
        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(obj.transform, false);
        Text phText = phObj.AddComponent<Text>();
        phText.font = GetFont();
        phText.fontSize = 14;
        phText.fontStyle = FontStyle.Italic;
        phText.color = new Color(0.5f, 0.5f, 0.5f);
        phText.alignment = TextAnchor.MiddleLeft;
        phText.text = placeholder;
        RectTransform phRt = phObj.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(5, 0);
        phRt.offsetMax = new Vector2(-5, 0);

        InputField input = obj.AddComponent<InputField>();
        input.textComponent = textComp;
        input.placeholder = phText;
        input.contentType = contentType;

        return input;
    }

    public static Dropdown CreateDropdown(Transform parent, string name, string[] options,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        // Label text
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(obj.transform, false);
        Text labelText = labelObj.AddComponent<Text>();
        labelText.font = GetFont();
        labelText.fontSize = 14;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(5, 0);
        labelRt.offsetMax = new Vector2(-25, 0);

        // Arrow indicator
        GameObject arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(obj.transform, false);
        Text arrowText = arrowObj.AddComponent<Text>();
        arrowText.font = GetFont();
        arrowText.fontSize = 14;
        arrowText.color = Color.white;
        arrowText.alignment = TextAnchor.MiddleCenter;
        arrowText.text = "v";
        RectTransform arrowRt = arrowObj.GetComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(1, 0);
        arrowRt.anchorMax = new Vector2(1, 1);
        arrowRt.offsetMin = new Vector2(-25, 0);
        arrowRt.offsetMax = Vector2.zero;

        // Template for dropdown items
        GameObject templateObj = new GameObject("Template");
        templateObj.transform.SetParent(obj.transform, false);
        Image templateBg = templateObj.AddComponent<Image>();
        templateBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        RectTransform templateRt = templateObj.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0, 0);
        templateRt.anchorMax = new Vector2(1, 0);
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.offsetMin = Vector2.zero;
        templateRt.offsetMax = Vector2.zero;
        templateRt.sizeDelta = new Vector2(0, 150);
        ScrollRect scroll = templateObj.AddComponent<ScrollRect>();

        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(templateObj.transform, false);
        viewportObj.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 1f);
        viewportObj.AddComponent<Mask>().showMaskGraphic = true;
        RectTransform viewportRt = viewportObj.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        scroll.viewport = viewportRt;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRt = contentObj.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;
        scroll.content = contentRt;

        // Item template
        GameObject itemObj = new GameObject("Item");
        itemObj.transform.SetParent(contentObj.transform, false);
        RectTransform itemRt = itemObj.AddComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0, 0.5f);
        itemRt.anchorMax = new Vector2(1, 0.5f);
        itemRt.sizeDelta = new Vector2(0, 28);
        Toggle itemToggle = itemObj.AddComponent<Toggle>();

        // Item label
        GameObject itemLabelObj = new GameObject("Item Label");
        itemLabelObj.transform.SetParent(itemObj.transform, false);
        Text itemLabel = itemLabelObj.AddComponent<Text>();
        itemLabel.font = GetFont();
        itemLabel.fontSize = 14;
        itemLabel.color = Color.white;
        itemLabel.alignment = TextAnchor.MiddleLeft;
        RectTransform itemLabelRt = itemLabelObj.GetComponent<RectTransform>();
        itemLabelRt.anchorMin = Vector2.zero;
        itemLabelRt.anchorMax = Vector2.one;
        itemLabelRt.offsetMin = new Vector2(5, 0);
        itemLabelRt.offsetMax = new Vector2(-5, 0);

        itemToggle.graphic = null;
        itemToggle.targetGraphic = null;

        templateObj.SetActive(false);

        Dropdown dropdown = obj.AddComponent<Dropdown>();
        dropdown.captionText = labelText;
        dropdown.template = templateRt;
        dropdown.itemText = itemLabel;

        // Populate options
        dropdown.ClearOptions();
        if (options != null && options.Length > 0)
        {
            var optList = new System.Collections.Generic.List<Dropdown.OptionData>();
            foreach (var opt in options)
                optList.Add(new Dropdown.OptionData(opt));
            dropdown.AddOptions(optList);
        }

        return dropdown;
    }

    public static Toggle CreateToggle(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        // Background box
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(obj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.5f);
        bgRt.anchorMax = new Vector2(0, 0.5f);
        bgRt.pivot = new Vector2(0, 0.5f);
        bgRt.sizeDelta = new Vector2(20, 20);
        bgRt.anchoredPosition = Vector2.zero;

        // Checkmark
        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform, false);
        Text checkText = checkObj.AddComponent<Text>();
        checkText.font = GetFont();
        checkText.text = "X";
        checkText.fontSize = 14;
        checkText.color = Color.white;
        checkText.alignment = TextAnchor.MiddleCenter;
        RectTransform checkRt = checkObj.GetComponent<RectTransform>();
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = Vector2.zero;

        // Label
        CreateText(obj.transform, "Label", label, 14, TextAnchor.MiddleLeft,
            new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(25, 0), Vector2.zero);

        Toggle toggle = obj.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkText;

        return toggle;
    }

    public static ScrollRect CreateScrollView(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        // Viewport with mask
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(obj.transform, false);
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;

        // Content with vertical layout
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.padding = new RectOffset(5, 5, 5, 5);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ScrollRect component
        ScrollRect scroll = obj.AddComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.viewport = vpRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        return scroll;
    }

    /// <summary>
    /// Creates a horizontal row container with LayoutElement preferred height.
    /// </summary>
    public static GameObject CreateRow(Transform parent, string name, float height = 30f)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, height);

        HorizontalLayoutGroup hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.padding = new RectOffset(5, 5, 2, 2);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.flexibleWidth = 1f;

        return obj;
    }

    /// <summary>
    /// Creates a UI element with LayoutElement for use inside layout groups.
    /// </summary>
    public static LayoutElement AddLayoutElement(GameObject obj, float minWidth = -1f,
        float preferredWidth = -1f, float flexibleWidth = -1f)
    {
        LayoutElement le = obj.GetComponent<LayoutElement>();
        if (le == null) le = obj.AddComponent<LayoutElement>();
        if (minWidth >= 0) le.minWidth = minWidth;
        if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
        if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
        return le;
    }
}
