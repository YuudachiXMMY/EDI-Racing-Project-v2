using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editable row for a single survey question within the builder.
/// Built programmatically, supports all 3 question types.
/// </summary>
public class QuestionEditorRow : MonoBehaviour
{
    private InputField questionTextField;
    private Dropdown typeDropdown;
    private Toggle requiredToggle;
    private InputField optionsField;
    private InputField minField;
    private InputField maxField;
    private GameObject optionsSection;
    private GameObject numericSection;
    private Text indexLabel;

    private int rowIndex;
    private string questionId;
    private Action<int> onDelete;
    private Action<int> onMoveUp;
    private Action<int> onMoveDown;

    public void Build(Transform parent, SurveyQuestion data, int index,
        Action<int> deleteCallback, Action<int> moveUpCallback, Action<int> moveDownCallback)
    {
        rowIndex = index;
        questionId = string.IsNullOrEmpty(data.Id) ? $"q_{index}" : data.Id;
        onDelete = deleteCallback;
        onMoveUp = moveUpCallback;
        onMoveDown = moveDownCallback;

        transform.SetParent(parent, false);

        // Container with background
        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        RectTransform rt = gameObject.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 0); // controlled by layout

        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.padding = new RectOffset(8, 8, 4, 4);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement le = gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        // Row 1: Index + Question text + Type dropdown + Buttons
        GameObject row1 = BuilderUIFactory.CreateRow(transform, "Row1", 30f);

        indexLabel = BuilderUIFactory.CreateText(row1.transform, "Index", $"Q{index + 1}:",
            14, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(indexLabel.gameObject, preferredWidth: 35f);

        questionTextField = BuilderUIFactory.CreateInputField(row1.transform, "QuestionText",
            "Enter question text...",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        questionTextField.text = data.Text ?? "";
        BuilderUIFactory.AddLayoutElement(questionTextField.gameObject, flexibleWidth: 1f);

        string[] typeNames = Enum.GetNames(typeof(QuestionType));
        typeDropdown = BuilderUIFactory.CreateDropdown(row1.transform, "TypeDropdown", typeNames,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        typeDropdown.value = data.Type;
        BuilderUIFactory.AddLayoutElement(typeDropdown.gameObject, preferredWidth: 120f);

        // Control buttons
        Button upBtn = BuilderUIFactory.CreateButton(row1.transform, "UpBtn", "^",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(upBtn.gameObject, preferredWidth: 25f);
        upBtn.onClick.AddListener(() => onMoveUp?.Invoke(rowIndex));

        Button downBtn = BuilderUIFactory.CreateButton(row1.transform, "DownBtn", "v",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(downBtn.gameObject, preferredWidth: 25f);
        downBtn.onClick.AddListener(() => onMoveDown?.Invoke(rowIndex));

        Button delBtn = BuilderUIFactory.CreateButton(row1.transform, "DelBtn", "X",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.6f, 0.15f, 0.15f, 0.9f));
        BuilderUIFactory.AddLayoutElement(delBtn.gameObject, preferredWidth: 25f);
        delBtn.onClick.AddListener(() => onDelete?.Invoke(rowIndex));

        // Row 2: Required toggle + type-specific fields
        GameObject row2 = BuilderUIFactory.CreateRow(transform, "Row2", 28f);

        requiredToggle = BuilderUIFactory.CreateToggle(row2.transform, "Required", "Required",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        requiredToggle.isOn = data.Required;
        BuilderUIFactory.AddLayoutElement(requiredToggle.gameObject, preferredWidth: 100f);

        // Options section (MultipleChoice)
        optionsSection = new GameObject("OptionsSection");
        optionsSection.transform.SetParent(row2.transform, false);
        optionsSection.AddComponent<RectTransform>();
        BuilderUIFactory.AddLayoutElement(optionsSection, flexibleWidth: 1f);

        BuilderUIFactory.CreateText(optionsSection.transform, "OptionsLabel", "Options (| separated):",
            12, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0.35f, 1), Vector2.zero, Vector2.zero);

        optionsField = BuilderUIFactory.CreateInputField(optionsSection.transform, "OptionsInput",
            "Option1|Option2|Option3",
            new Vector2(0.36f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        if (data.Options != null && data.Options.Length > 0)
            optionsField.text = string.Join("|", data.Options);

        // Numeric section
        numericSection = new GameObject("NumericSection");
        numericSection.transform.SetParent(row2.transform, false);
        numericSection.AddComponent<RectTransform>();
        BuilderUIFactory.AddLayoutElement(numericSection, flexibleWidth: 1f);

        BuilderUIFactory.CreateText(numericSection.transform, "MinLabel", "Min:",
            12, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0.1f, 1), Vector2.zero, Vector2.zero);

        minField = BuilderUIFactory.CreateInputField(numericSection.transform, "MinInput", "0",
            new Vector2(0.11f, 0), new Vector2(0.35f, 1), Vector2.zero, Vector2.zero,
            InputField.ContentType.DecimalNumber);
        minField.text = data.MinValue.ToString();

        BuilderUIFactory.CreateText(numericSection.transform, "MaxLabel", "Max:",
            12, TextAnchor.MiddleLeft, new Vector2(0.4f, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);

        maxField = BuilderUIFactory.CreateInputField(numericSection.transform, "MaxInput", "10",
            new Vector2(0.51f, 0), new Vector2(0.75f, 1), Vector2.zero, Vector2.zero,
            InputField.ContentType.DecimalNumber);
        maxField.text = data.MaxValue.ToString();

        // Type change listener
        typeDropdown.onValueChanged.AddListener(OnTypeChanged);
        OnTypeChanged(data.Type);
    }

    private void OnTypeChanged(int typeIndex)
    {
        QuestionType qt = (QuestionType)typeIndex;
        if (optionsSection != null) optionsSection.SetActive(qt == QuestionType.MultipleChoice);
        if (numericSection != null) numericSection.SetActive(qt == QuestionType.Numeric);
    }

    public SurveyQuestion ToQuestion()
    {
        var q = new SurveyQuestion
        {
            Id = questionId,
            Text = questionTextField != null ? questionTextField.text : "",
            Type = typeDropdown != null ? typeDropdown.value : 0,
            Required = requiredToggle != null && requiredToggle.isOn,
            MinValue = 0f,
            MaxValue = 10f,
            Options = Array.Empty<string>()
        };

        if (q.QuestionType == QuestionType.MultipleChoice && optionsField != null)
        {
            string raw = optionsField.text;
            if (!string.IsNullOrEmpty(raw))
                q.Options = raw.Split('|');
        }

        if (q.QuestionType == QuestionType.Numeric)
        {
            if (minField != null && float.TryParse(minField.text, out float min))
                q.MinValue = min;
            if (maxField != null && float.TryParse(maxField.text, out float max))
                q.MaxValue = max;
        }

        return q;
    }

    public void UpdateIndex(int newIndex)
    {
        rowIndex = newIndex;
        if (indexLabel != null) indexLabel.text = $"Q{newIndex + 1}:";
    }

    public string QuestionId => questionId;
    public string QuestionDisplayText => questionTextField != null ? questionTextField.text : questionId;
}
