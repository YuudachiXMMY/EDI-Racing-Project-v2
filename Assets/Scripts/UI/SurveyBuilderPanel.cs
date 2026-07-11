using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main tabbed editor for survey configs. Manages Questions, Mappings, and Rules tabs.
/// Professor creates/edits full SurveyConfig here, then saves via SurveyConfigManager.
/// </summary>
public class SurveyBuilderPanel : MonoBehaviour
{
    [Header("References")]
    public SurveyConfigManager ConfigManager;
    public SetupScreen SetupScreen;

    private const int MaxQuestions = 20;
    private const int MaxMappings = 20;
    private const int MaxRules = 9;

    // UI state
    private InputField configNameField;
    private Text statusText;
    private TabButton[] tabs;
    private ScrollRect questionsScroll;
    private ScrollRect mappingsScroll;
    private ScrollRect rulesScroll;
    private Text questionsWarning;
    private Text mappingsWarning;
    private Text rulesWarning;

    // Row tracking
    private readonly List<QuestionEditorRow> questionRows = new List<QuestionEditorRow>();
    private readonly List<MappingEditorRow> mappingRows = new List<MappingEditorRow>();
    private readonly List<RuleEditorRow> ruleRows = new List<RuleEditorRow>();

    private SurveyConfig editingConfig;

    private void Awake()
    {
        BuildUI();
        gameObject.SetActive(false);
    }

    public void Show(SurveyConfig config)
    {
        editingConfig = config ?? new SurveyConfig
        {
            ConfigName = "",
            Description = "",
            CreatedAt = DateTime.Now.ToString("o"),
            Version = "1.0",
            Questions = Array.Empty<SurveyQuestion>(),
            Mappings = Array.Empty<AttributeMapping>(),
            Rules = Array.Empty<SavedEventRule>()
        };

        gameObject.SetActive(true);
        PopulateFromConfig(editingConfig);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        if (SetupScreen != null)
        {
            SetupScreen.gameObject.SetActive(true);
            SetupScreen.RefreshActiveConfigDisplay();
        }
    }

    private void BuildUI()
    {
        // Full-screen overlay
        RectTransform rt = gameObject.GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(40, 30);
        rt.offsetMax = new Vector2(-40, -30);

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        VerticalLayoutGroup mainVlg = gameObject.AddComponent<VerticalLayoutGroup>();
        mainVlg.spacing = 5;
        mainVlg.padding = new RectOffset(10, 10, 10, 10);
        mainVlg.childForceExpandWidth = true;
        mainVlg.childForceExpandHeight = false;
        mainVlg.childControlWidth = true;
        mainVlg.childControlHeight = true;

        // Top bar: Title + Config Name + Save + Back
        GameObject topBar = BuilderUIFactory.CreateRow(transform, "TopBar", 36f);

        BuilderUIFactory.CreateText(topBar.transform, "Title", "SURVEY BUILDER",
            18, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(topBar.transform.GetChild(0).gameObject, preferredWidth: 160f);

        BuilderUIFactory.CreateText(topBar.transform, "NameLabel", "Name:",
            14, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(topBar.transform.GetChild(1).gameObject, preferredWidth: 45f);

        configNameField = BuilderUIFactory.CreateInputField(topBar.transform, "ConfigName",
            "My Survey Config", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(configNameField.gameObject, flexibleWidth: 1f);

        Button saveBtn = BuilderUIFactory.CreateButton(topBar.transform, "SaveBtn", "Save",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.15f, 0.45f, 0.15f, 0.9f));
        BuilderUIFactory.AddLayoutElement(saveBtn.gameObject, preferredWidth: 60f);
        saveBtn.onClick.AddListener(SaveConfig);

        Button backBtn = BuilderUIFactory.CreateButton(topBar.transform, "BackBtn", "Back",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(backBtn.gameObject, preferredWidth: 60f);
        backBtn.onClick.AddListener(Hide);

        // Status text row
        GameObject statusRow = BuilderUIFactory.CreateRow(transform, "StatusRow", 20f);
        statusText = BuilderUIFactory.CreateText(statusRow.transform, "Status", "",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(statusText.gameObject, flexibleWidth: 1f);
        statusText.color = Color.yellow;

        // Tab buttons row
        GameObject tabRow = BuilderUIFactory.CreateRow(transform, "TabRow", 32f);
        tabs = new TabButton[3];

        tabs[0] = CreateTabButton(tabRow.transform, "Questions");
        tabs[1] = CreateTabButton(tabRow.transform, "Mappings");
        tabs[2] = CreateTabButton(tabRow.transform, "Rules");

        // Tab content area (fills remaining space)
        GameObject contentArea = new GameObject("ContentArea");
        contentArea.transform.SetParent(transform, false);
        contentArea.AddComponent<RectTransform>();
        LayoutElement contentLe = contentArea.AddComponent<LayoutElement>();
        contentLe.flexibleHeight = 1f;
        contentLe.flexibleWidth = 1f;

        // Questions tab content
        GameObject questionsTab = CreateTabContent(contentArea.transform, "QuestionsContent");
        tabs[0].TabContent = questionsTab;
        questionsScroll = BuildScrollWithAddButton(questionsTab.transform, "Add Question", OnAddQuestion);
        questionsWarning = CreateWarningText(questionsTab.transform);

        // Mappings tab content
        GameObject mappingsTab = CreateTabContent(contentArea.transform, "MappingsContent");
        tabs[1].TabContent = mappingsTab;
        mappingsScroll = BuildScrollWithAddButton(mappingsTab.transform, "Add Mapping", OnAddMapping);
        mappingsWarning = CreateWarningText(mappingsTab.transform);

        // Rules tab content
        GameObject rulesTab = CreateTabContent(contentArea.transform, "RulesContent");
        tabs[2].TabContent = rulesTab;
        rulesScroll = BuildScrollWithAddButton(rulesTab.transform, "Add Rule", OnAddRule);
        rulesWarning = CreateWarningText(rulesTab.transform);

        // Wire tab buttons
        tabs[0].Button.onClick.AddListener(() => TabButton.SelectTab(tabs, 0));
        tabs[1].Button.onClick.AddListener(() => TabButton.SelectTab(tabs, 1));
        tabs[2].Button.onClick.AddListener(() => TabButton.SelectTab(tabs, 2));

        TabButton.SelectTab(tabs, 0);
    }

    private TabButton CreateTabButton(Transform parent, string label)
    {
        Button btn = BuilderUIFactory.CreateButton(parent, $"Tab_{label}", label,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(btn.gameObject, flexibleWidth: 1f);

        TabButton tb = btn.gameObject.AddComponent<TabButton>();
        tb.Button = btn;
        tb.Label = btn.GetComponentInChildren<Text>();
        return tb;
    }

    private GameObject CreateTabContent(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return obj;
    }

    private ScrollRect BuildScrollWithAddButton(Transform parent, string addLabel, UnityEngine.Events.UnityAction onClick)
    {
        // Add button at top
        GameObject addRow = new GameObject("AddRow");
        addRow.transform.SetParent(parent, false);
        RectTransform addRt = addRow.AddComponent<RectTransform>();
        addRt.anchorMin = new Vector2(0, 1);
        addRt.anchorMax = new Vector2(1, 1);
        addRt.pivot = new Vector2(0, 1);
        addRt.offsetMin = new Vector2(5, -35);
        addRt.offsetMax = new Vector2(-5, -5);

        Button addBtn = BuilderUIFactory.CreateButton(addRow.transform, "AddBtn", $"+ {addLabel}",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.35f, 0.2f, 0.9f));
        RectTransform btnRt = addBtn.GetComponent<RectTransform>();
        btnRt.anchorMin = Vector2.zero;
        btnRt.anchorMax = new Vector2(0.2f, 1f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;
        addBtn.onClick.AddListener(onClick);

        // Scroll view below
        ScrollRect scroll = BuilderUIFactory.CreateScrollView(parent, "Scroll",
            new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(0, 0), new Vector2(0, -40));

        return scroll;
    }

    private Text CreateWarningText(Transform parent)
    {
        Text t = BuilderUIFactory.CreateText(parent, "Warning", "",
            12, TextAnchor.MiddleRight,
            new Vector2(0.5f, 1), new Vector2(1, 1),
            new Vector2(0, -35), new Vector2(-5, -5));
        t.color = new Color(1f, 0.6f, 0.2f);
        return t;
    }

    // --- Population ---

    private void PopulateFromConfig(SurveyConfig config)
    {
        if (configNameField != null) configNameField.text = config.ConfigName ?? "";

        ClearAllRows();

        if (config.Questions != null)
            foreach (var q in config.Questions) AddQuestionRow(q);

        if (config.Mappings != null)
            foreach (var m in config.Mappings) AddMappingRow(m);

        if (config.Rules != null)
            foreach (var r in config.Rules) AddRuleRow(r);

        UpdateWarnings();
        SetStatus("");
    }

    // --- Question Management ---

    private void OnAddQuestion()
    {
        if (questionRows.Count >= MaxQuestions)
        {
            SetStatus($"Maximum {MaxQuestions} questions reached.");
            return;
        }

        AddQuestionRow(new SurveyQuestion
        {
            Id = $"q_{questionRows.Count}",
            Text = "",
            Type = (int)QuestionType.Text,
            Options = Array.Empty<string>(),
            Required = true
        });
        UpdateWarnings();
    }

    private void AddQuestionRow(SurveyQuestion data)
    {
        int index = questionRows.Count;
        GameObject rowObj = new GameObject($"Question_{index}");
        QuestionEditorRow row = rowObj.AddComponent<QuestionEditorRow>();
        row.Build(questionsScroll.content, data, index, OnDeleteQuestion, OnMoveQuestionUp, OnMoveQuestionDown);
        questionRows.Add(row);
    }

    private void OnDeleteQuestion(int index)
    {
        if (index < 0 || index >= questionRows.Count) return;
        Destroy(questionRows[index].gameObject);
        questionRows.RemoveAt(index);
        ReindexQuestions();
        RefreshMappingDropdowns();
        UpdateWarnings();
    }

    private void OnMoveQuestionUp(int index)
    {
        if (index <= 0 || index >= questionRows.Count) return;
        SwapRows(questionRows, index, index - 1);
        ReindexQuestions();
    }

    private void OnMoveQuestionDown(int index)
    {
        if (index < 0 || index >= questionRows.Count - 1) return;
        SwapRows(questionRows, index, index + 1);
        ReindexQuestions();
    }

    private void ReindexQuestions()
    {
        for (int i = 0; i < questionRows.Count; i++)
        {
            questionRows[i].UpdateIndex(i);
            questionRows[i].transform.SetSiblingIndex(i);
        }
    }

    // --- Mapping Management ---

    private void OnAddMapping()
    {
        if (mappingRows.Count >= MaxMappings)
        {
            SetStatus($"Maximum {MaxMappings} mappings reached.");
            return;
        }

        AddMappingRow(new AttributeMapping
        {
            QuestionId = "",
            AttributeName = "",
            DefaultValue = "",
            TransformType = "direct",
            LookupEntries = Array.Empty<AttributeEntry>()
        });
        UpdateWarnings();
    }

    private void AddMappingRow(AttributeMapping data)
    {
        int index = mappingRows.Count;
        GameObject rowObj = new GameObject($"Mapping_{index}");
        MappingEditorRow row = rowObj.AddComponent<MappingEditorRow>();
        row.Build(mappingsScroll.content, data, index, GetQuestionIds(), OnDeleteMapping);
        mappingRows.Add(row);
    }

    private void OnDeleteMapping(int index)
    {
        if (index < 0 || index >= mappingRows.Count) return;
        Destroy(mappingRows[index].gameObject);
        mappingRows.RemoveAt(index);
        for (int i = 0; i < mappingRows.Count; i++)
            mappingRows[i].UpdateIndex(i);
        UpdateWarnings();
    }

    private void RefreshMappingDropdowns()
    {
        string[] ids = GetQuestionIds();
        foreach (var row in mappingRows)
            row.UpdateQuestionOptions(ids);
    }

    // --- Rule Management ---

    private void OnAddRule()
    {
        if (ruleRows.Count >= MaxRules)
        {
            SetStatus($"Maximum {MaxRules} rules (keyboard keys 1-9).");
            return;
        }

        AddRuleRow(new SavedEventRule
        {
            DisplayName = "",
            AttributeName = "",
            Operator = (int)ComparisonOperator.Equals,
            CompareValue = "",
            SpeedDelta = -10f,
            Duration = 8f,
            Weather = (int)WeatherType.None,
            AllowRepeat = false
        });
        UpdateWarnings();
    }

    private void AddRuleRow(SavedEventRule data)
    {
        int index = ruleRows.Count;
        GameObject rowObj = new GameObject($"Rule_{index}");
        RuleEditorRow row = rowObj.AddComponent<RuleEditorRow>();
        row.Build(rulesScroll.content, data, index, OnDeleteRule);
        ruleRows.Add(row);
    }

    private void OnDeleteRule(int index)
    {
        if (index < 0 || index >= ruleRows.Count) return;
        Destroy(ruleRows[index].gameObject);
        ruleRows.RemoveAt(index);
        for (int i = 0; i < ruleRows.Count; i++)
            ruleRows[i].UpdateIndex(i);
        UpdateWarnings();
    }

    // --- Save / Collect ---

    private void SaveConfig()
    {
        if (ConfigManager == null)
        {
            SetStatus("Error: No SurveyConfigManager available.");
            return;
        }

        string name = configNameField != null ? configNameField.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Please enter a config name.");
            return;
        }

        SurveyConfig config = CollectConfig();
        config.ConfigName = name;

        ConfigManager.SaveConfig(config);
        ConfigManager.SetActiveConfig(config);
        SetStatus($"Saved: {name}");
    }

    public SurveyConfig CollectConfig()
    {
        var config = new SurveyConfig
        {
            ConfigName = configNameField != null ? configNameField.text : "",
            Description = "",
            CreatedAt = DateTime.Now.ToString("o"),
            Version = "1.0"
        };

        // Collect questions
        var questions = new List<SurveyQuestion>();
        foreach (var row in questionRows)
            questions.Add(row.ToQuestion());
        config.Questions = questions.ToArray();

        // Collect mappings
        var mappings = new List<AttributeMapping>();
        foreach (var row in mappingRows)
            mappings.Add(row.ToMapping());
        config.Mappings = mappings.ToArray();

        // Collect rules
        var rules = new List<SavedEventRule>();
        foreach (var row in ruleRows)
            rules.Add(row.ToRule());
        config.Rules = rules.ToArray();

        return config;
    }

    // --- Helpers ---

    private string[] GetQuestionIds()
    {
        var ids = new List<string>();
        foreach (var row in questionRows)
            ids.Add(row.QuestionId);
        return ids.ToArray();
    }

    private void ClearAllRows()
    {
        foreach (var row in questionRows) if (row != null) Destroy(row.gameObject);
        foreach (var row in mappingRows) if (row != null) Destroy(row.gameObject);
        foreach (var row in ruleRows) if (row != null) Destroy(row.gameObject);
        questionRows.Clear();
        mappingRows.Clear();
        ruleRows.Clear();
    }

    private void UpdateWarnings()
    {
        if (questionsWarning != null)
            questionsWarning.text = questionRows.Count >= MaxQuestions ? $"Max {MaxQuestions} reached" : "";
        if (mappingsWarning != null)
            mappingsWarning.text = mappingRows.Count >= MaxMappings ? $"Max {MaxMappings} reached" : "";
        if (rulesWarning != null)
            rulesWarning.text = ruleRows.Count >= MaxRules ? $"Max {MaxRules} (keys 1-9)" : "";
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private static void SwapRows<T>(List<T> list, int a, int b) where T : MonoBehaviour
    {
        T temp = list[a];
        list[a] = list[b];
        list[b] = temp;
    }
}
