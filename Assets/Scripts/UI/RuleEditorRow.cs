using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editable row for a single event rule within the builder.
/// Supports all ComparisonOperator and WeatherType values.
/// </summary>
public class RuleEditorRow : MonoBehaviour
{
    private InputField displayNameField;
    private InputField attributeNameField;
    private Dropdown operatorDropdown;
    private InputField compareValueField;
    private InputField speedDeltaField;
    private InputField durationField;
    private Dropdown weatherDropdown;
    private Toggle allowRepeatToggle;
    private GameObject conditionSection;

    private int rowIndex;
    private Action<int> onDelete;

    private static readonly string[] OperatorNames = Enum.GetNames(typeof(ComparisonOperator));
    private static readonly string[] WeatherNames = Enum.GetNames(typeof(WeatherType));

    public void Build(Transform parent, SavedEventRule data, int index, Action<int> deleteCallback)
    {
        rowIndex = index;
        onDelete = deleteCallback;

        transform.SetParent(parent, false);

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

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

        // Row 1: Display Name + Delete
        GameObject row1 = BuilderUIFactory.CreateRow(transform, "Row1", 30f);

        BuilderUIFactory.CreateText(row1.transform, "NameLabel", "Rule:",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row1.transform.GetChild(0).gameObject, preferredWidth: 40f);

        displayNameField = BuilderUIFactory.CreateInputField(row1.transform, "DisplayName",
            "Rule name...", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        displayNameField.text = data.DisplayName ?? "";
        BuilderUIFactory.AddLayoutElement(displayNameField.gameObject, flexibleWidth: 1f);

        Button delBtn = BuilderUIFactory.CreateButton(row1.transform, "DelBtn", "X",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.6f, 0.15f, 0.15f, 0.9f));
        BuilderUIFactory.AddLayoutElement(delBtn.gameObject, preferredWidth: 25f);
        delBtn.onClick.AddListener(() => onDelete?.Invoke(rowIndex));

        // Row 2: Condition (Attribute, Operator, Value)
        conditionSection = BuilderUIFactory.CreateRow(transform, "Row2", 30f);

        BuilderUIFactory.CreateText(conditionSection.transform, "IfLabel", "If",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(conditionSection.transform.GetChild(0).gameObject, preferredWidth: 20f);

        attributeNameField = BuilderUIFactory.CreateInputField(conditionSection.transform, "AttrName",
            "attribute", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        attributeNameField.text = data.AttributeName ?? "";
        BuilderUIFactory.AddLayoutElement(attributeNameField.gameObject, flexibleWidth: 1f);

        operatorDropdown = BuilderUIFactory.CreateDropdown(conditionSection.transform, "OperatorDD",
            OperatorNames, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        operatorDropdown.value = Mathf.Clamp(data.Operator, 0, OperatorNames.Length - 1);
        BuilderUIFactory.AddLayoutElement(operatorDropdown.gameObject, preferredWidth: 130f);

        compareValueField = BuilderUIFactory.CreateInputField(conditionSection.transform, "CompareVal",
            "value", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        compareValueField.text = data.CompareValue ?? "";
        BuilderUIFactory.AddLayoutElement(compareValueField.gameObject, flexibleWidth: 1f);

        // Row 3: Effect (Speed, Duration, Weather, Repeat)
        GameObject row3 = BuilderUIFactory.CreateRow(transform, "Row3", 28f);

        BuilderUIFactory.CreateText(row3.transform, "SpeedLabel", "Speed:",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row3.transform.GetChild(0).gameObject, preferredWidth: 45f);

        speedDeltaField = BuilderUIFactory.CreateInputField(row3.transform, "SpeedDelta",
            "-10", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        speedDeltaField.text = data.SpeedDelta.ToString();
        BuilderUIFactory.AddLayoutElement(speedDeltaField.gameObject, preferredWidth: 55f);

        BuilderUIFactory.CreateText(row3.transform, "DurLabel", "Dur(s):",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row3.transform.GetChild(2).gameObject, preferredWidth: 50f);

        durationField = BuilderUIFactory.CreateInputField(row3.transform, "Duration",
            "8", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        durationField.text = data.Duration.ToString();
        BuilderUIFactory.AddLayoutElement(durationField.gameObject, preferredWidth: 45f);

        BuilderUIFactory.CreateText(row3.transform, "WLabel", "Weather:",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row3.transform.GetChild(4).gameObject, preferredWidth: 60f);

        weatherDropdown = BuilderUIFactory.CreateDropdown(row3.transform, "WeatherDD",
            WeatherNames, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        weatherDropdown.value = Mathf.Clamp(data.Weather, 0, WeatherNames.Length - 1);
        BuilderUIFactory.AddLayoutElement(weatherDropdown.gameObject, preferredWidth: 80f);

        allowRepeatToggle = BuilderUIFactory.CreateToggle(row3.transform, "Repeat", "Repeat",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        allowRepeatToggle.isOn = data.AllowRepeat;
        BuilderUIFactory.AddLayoutElement(allowRepeatToggle.gameObject, preferredWidth: 80f);

        // Toggle condition visibility when "All" operator is selected
        operatorDropdown.onValueChanged.AddListener(OnOperatorChanged);
        OnOperatorChanged(operatorDropdown.value);
    }

    private void OnOperatorChanged(int index)
    {
        bool isAll = index == (int)ComparisonOperator.All;
        if (attributeNameField != null) attributeNameField.interactable = !isAll;
        if (compareValueField != null) compareValueField.interactable = !isAll;
    }

    public SavedEventRule ToRule()
    {
        float speed = 0f;
        if (speedDeltaField != null) float.TryParse(speedDeltaField.text, out speed);

        float duration = 8f;
        if (durationField != null) float.TryParse(durationField.text, out duration);

        return new SavedEventRule
        {
            DisplayName = displayNameField != null ? displayNameField.text : "",
            AttributeName = attributeNameField != null ? attributeNameField.text : "",
            Operator = operatorDropdown != null ? operatorDropdown.value : 0,
            CompareValue = compareValueField != null ? compareValueField.text : "",
            SpeedDelta = speed,
            Duration = duration,
            Weather = weatherDropdown != null ? weatherDropdown.value : 0,
            AllowRepeat = allowRepeatToggle != null && allowRepeatToggle.isOn
        };
    }

    public void UpdateIndex(int newIndex)
    {
        rowIndex = newIndex;
    }
}
