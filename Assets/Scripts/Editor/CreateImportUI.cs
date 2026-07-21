using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// One-shot editor utility: creates the "Import JSON" button and panel
/// on SetupScreen, then wires the references. Run via menu once and delete this file.
/// Menu: EDI Racing > Create Import JSON UI
/// </summary>
public static class CreateImportUI
{
    [MenuItem("EDI Racing/Create Import JSON UI")]
    public static void Execute()
    {
        var setupScreen = Object.FindFirstObjectByType<SetupScreen>();
        if (setupScreen == null)
        {
            Debug.LogError("[CreateImportUI] No SetupScreen found in scene.");
            return;
        }

        // Clean up if already exists
        if (setupScreen.ImportJsonButton != null)
            Object.DestroyImmediate(setupScreen.ImportJsonButton.gameObject);
        if (setupScreen.ImportPanel != null)
            Object.DestroyImmediate(setupScreen.ImportPanel);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // --- Import JSON Button ---
        var btnObj = new GameObject("ImportJsonBtn");
        btnObj.transform.SetParent(setupScreen.transform, false);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.15f, 0.4f, 0.9f);
        btnImg.raycastTarget = true;
        var btn = btnObj.AddComponent<Button>();
        var btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0);
        btnRt.anchorMax = new Vector2(0.5f, 0);
        btnRt.sizeDelta = new Vector2(200, 35);
        btnRt.anchoredPosition = new Vector2(0, 50);

        var btnLabel = new GameObject("Label");
        btnLabel.transform.SetParent(btnObj.transform, false);
        var btnText = btnLabel.AddComponent<Text>();
        btnText.text = "Import JSON";
        btnText.font = font;
        btnText.fontSize = 16;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        var btnLabelRt = btnLabel.GetComponent<RectTransform>();
        btnLabelRt.anchorMin = Vector2.zero;
        btnLabelRt.anchorMax = Vector2.one;
        btnLabelRt.offsetMin = Vector2.zero;
        btnLabelRt.offsetMax = Vector2.zero;

        setupScreen.ImportJsonButton = btn;

        // --- Import Panel (hidden by default) ---
        var panelObj = new GameObject("ImportPanel");
        panelObj.transform.SetParent(setupScreen.transform, false);
        var panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.08f, 0.97f);
        panelImg.raycastTarget = true;
        var panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.15f, 0.15f);
        panelRt.anchorMax = new Vector2(0.85f, 0.85f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        setupScreen.ImportPanel = panelObj;

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleText = titleObj.AddComponent<Text>();
        titleText.text = "Paste Web App JSON Export";
        titleText.font = font;
        titleText.fontSize = 18;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        var titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.88f);
        titleRt.anchorMax = new Vector2(1, 1f);
        titleRt.offsetMin = new Vector2(10, 0);
        titleRt.offsetMax = new Vector2(-10, -5);

        // --- InputField ---
        var inputObj = new GameObject("JsonInputField");
        inputObj.transform.SetParent(panelObj.transform, false);
        var inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        inputBg.raycastTarget = true;
        var inputRt = inputObj.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.05f, 0.2f);
        inputRt.anchorMax = new Vector2(0.95f, 0.85f);
        inputRt.offsetMin = Vector2.zero;
        inputRt.offsetMax = Vector2.zero;

        // Text child (displays typed text)
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(inputObj.transform, false);
        var textComp = textObj.AddComponent<Text>();
        textComp.font = font;
        textComp.fontSize = 14;
        textComp.color = Color.white;
        textComp.alignment = TextAnchor.UpperLeft;
        textComp.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComp.verticalOverflow = VerticalWrapMode.Overflow;
        textComp.supportRichText = false;
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(5, 2);
        textRt.offsetMax = new Vector2(-5, -2);

        // Placeholder child
        var phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(inputObj.transform, false);
        var phText = phObj.AddComponent<Text>();
        phText.font = font;
        phText.fontSize = 14;
        phText.fontStyle = FontStyle.Italic;
        phText.color = new Color(0.5f, 0.5f, 0.5f);
        phText.alignment = TextAnchor.UpperLeft;
        phText.horizontalOverflow = HorizontalWrapMode.Wrap;
        phText.text = "Paste JSON here...";
        var phRt = phObj.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(5, 2);
        phRt.offsetMax = new Vector2(-5, -2);

        // InputField component
        var inputField = inputObj.AddComponent<InputField>();
        inputField.textComponent = textComp;
        inputField.placeholder = phText;
        inputField.lineType = InputField.LineType.MultiLineNewline;
        inputField.interactable = true;

        setupScreen.JsonInputField = inputField;

        // --- Confirm Import Button ---
        var confirmObj = new GameObject("ConfirmImportBtn");
        confirmObj.transform.SetParent(panelObj.transform, false);
        var confirmImg = confirmObj.AddComponent<Image>();
        confirmImg.color = new Color(0.1f, 0.35f, 0.1f, 0.95f);
        confirmImg.raycastTarget = true;
        var confirmBtn = confirmObj.AddComponent<Button>();
        var confirmRt = confirmObj.GetComponent<RectTransform>();
        confirmRt.anchorMin = new Vector2(0.5f, 0);
        confirmRt.anchorMax = new Vector2(0.5f, 0);
        confirmRt.sizeDelta = new Vector2(180, 40);
        confirmRt.anchoredPosition = new Vector2(0, 30);

        var confirmLabel = new GameObject("Label");
        confirmLabel.transform.SetParent(confirmObj.transform, false);
        var confirmText = confirmLabel.AddComponent<Text>();
        confirmText.text = "Confirm Import";
        confirmText.font = font;
        confirmText.fontSize = 16;
        confirmText.alignment = TextAnchor.MiddleCenter;
        confirmText.color = Color.white;
        var confirmLabelRt = confirmLabel.GetComponent<RectTransform>();
        confirmLabelRt.anchorMin = Vector2.zero;
        confirmLabelRt.anchorMax = Vector2.one;
        confirmLabelRt.offsetMin = Vector2.zero;
        confirmLabelRt.offsetMax = Vector2.zero;

        setupScreen.ConfirmImportButton = confirmBtn;

        // Hide panel by default
        panelObj.SetActive(false);

        EditorUtility.SetDirty(setupScreen);
        Debug.Log("[CreateImportUI] Import JSON UI created and wired successfully.");
    }
}
