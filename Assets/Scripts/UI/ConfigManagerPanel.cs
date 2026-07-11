using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay panel for loading saved configs or selecting templates.
/// Opens from SetupScreen, passes selected config to SurveyConfigManager.
/// </summary>
public class ConfigManagerPanel : MonoBehaviour
{
    [Header("References")]
    public SurveyConfigManager ConfigManager;
    public SurveyBuilderPanel BuilderPanel;
    public SetupScreen SetupScreen;

    private Transform listContent;
    private Text titleText;
    private readonly List<GameObject> listItems = new List<GameObject>();

    private void Awake()
    {
        BuildUI();
        gameObject.SetActive(false);
    }

    public void ShowLoadPanel()
    {
        gameObject.SetActive(true);
        if (titleText != null) titleText.text = "Load Configuration";
        RebuildList(false);
    }

    public void ShowTemplatePanel()
    {
        gameObject.SetActive(true);
        if (titleText != null) titleText.text = "Select Template";
        RebuildList(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void BuildUI()
    {
        RectTransform rt = gameObject.GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.2f, 0.15f);
        rt.anchorMax = new Vector2(0.8f, 0.85f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.14f, 0.97f);

        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 5;
        vlg.padding = new RectOffset(15, 15, 10, 10);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // Header row
        GameObject header = BuilderUIFactory.CreateRow(transform, "Header", 35f);

        titleText = BuilderUIFactory.CreateText(header.transform, "Title", "Load Configuration",
            18, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(titleText.gameObject, flexibleWidth: 1f);

        Button closeBtn = BuilderUIFactory.CreateButton(header.transform, "CloseBtn", "X",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.5f, 0.15f, 0.15f, 0.9f));
        BuilderUIFactory.AddLayoutElement(closeBtn.gameObject, preferredWidth: 30f);
        closeBtn.onClick.AddListener(Hide);

        // Scroll list
        ScrollRect scroll = BuilderUIFactory.CreateScrollView(transform, "ConfigList",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement scrollLe = scroll.gameObject.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.flexibleWidth = 1f;

        listContent = scroll.content;
    }

    private void RebuildList(bool showTemplates)
    {
        // Clear existing items
        foreach (var item in listItems)
            if (item != null) Destroy(item);
        listItems.Clear();

        if (ConfigManager == null) return;

        if (showTemplates)
        {
            string[] names = ConfigManager.GetTemplateNames();
            foreach (string name in names)
                AddListItem(name, () => OnSelectTemplate(name));
        }
        else
        {
            string[] paths = ConfigManager.GetSavedConfigPaths();
            foreach (string path in paths)
            {
                string displayName = Path.GetFileNameWithoutExtension(path);
                string capturedPath = path;
                AddListItem(displayName, () => OnSelectSaved(capturedPath));
            }

            if (paths.Length == 0)
            {
                AddListItem("(No saved configs found)", null);
            }
        }
    }

    private void AddListItem(string label, Action onClick)
    {
        GameObject row = BuilderUIFactory.CreateRow(listContent, $"Item_{listItems.Count}", 35f);

        if (onClick != null)
        {
            Button btn = BuilderUIFactory.CreateButton(row.transform, "SelectBtn", label,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.18f, 0.22f, 0.3f, 0.9f));
            BuilderUIFactory.AddLayoutElement(btn.gameObject, flexibleWidth: 1f);
            btn.onClick.AddListener(() => onClick());
        }
        else
        {
            BuilderUIFactory.CreateText(row.transform, "Label", label,
                14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BuilderUIFactory.AddLayoutElement(row.transform.GetChild(0).gameObject, flexibleWidth: 1f);
        }

        listItems.Add(row);
    }

    private void OnSelectSaved(string path)
    {
        if (ConfigManager == null) return;
        SurveyConfig config = ConfigManager.LoadConfig(path);
        if (config != null)
        {
            ConfigManager.SetActiveConfig(config);
            Hide();
            if (SetupScreen != null) SetupScreen.RefreshActiveConfigDisplay();
        }
    }

    private void OnSelectTemplate(string templateName)
    {
        if (ConfigManager == null) return;
        SurveyConfig config = ConfigManager.LoadTemplate(templateName);
        if (config != null)
        {
            ConfigManager.SetActiveConfig(config);
            Hide();
            if (SetupScreen != null) SetupScreen.RefreshActiveConfigDisplay();
        }
    }
}
