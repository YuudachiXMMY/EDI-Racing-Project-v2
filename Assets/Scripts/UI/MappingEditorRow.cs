using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editable row for a single attribute mapping within the builder.
/// Maps a question response to a car attribute with optional transform.
/// </summary>
public class MappingEditorRow : MonoBehaviour
{
    private Dropdown questionDropdown;
    private InputField attributeNameField;
    private Dropdown transformDropdown;
    private InputField defaultValueField;
    private InputField lookupField;
    private GameObject lookupSection;

    private int rowIndex;
    private string[] currentQuestionIds;
    private Action<int> onDelete;

    private static readonly string[] TransformTypes = { "direct", "lookup", "numeric" };

    public void Build(Transform parent, AttributeMapping data, int index,
        string[] questionIds, Action<int> deleteCallback)
    {
        rowIndex = index;
        currentQuestionIds = questionIds ?? Array.Empty<string>();
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

        // Row 1: Question dropdown -> Attribute name
        GameObject row1 = BuilderUIFactory.CreateRow(transform, "Row1", 30f);

        BuilderUIFactory.CreateText(row1.transform, "QLabel", "Question:",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row1.transform.GetChild(0).gameObject, preferredWidth: 65f);

        questionDropdown = BuilderUIFactory.CreateDropdown(row1.transform, "QuestionDD",
            currentQuestionIds, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(questionDropdown.gameObject, preferredWidth: 150f);
        SetDropdownToValue(questionDropdown, currentQuestionIds, data.QuestionId);

        BuilderUIFactory.CreateText(row1.transform, "Arrow", "->",
            14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row1.transform.GetChild(2).gameObject, preferredWidth: 25f);

        BuilderUIFactory.CreateText(row1.transform, "AttrLabel", "Attr:",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row1.transform.GetChild(3).gameObject, preferredWidth: 35f);

        attributeNameField = BuilderUIFactory.CreateInputField(row1.transform, "AttrName",
            "attribute_name", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        attributeNameField.text = data.AttributeName ?? "";
        BuilderUIFactory.AddLayoutElement(attributeNameField.gameObject, flexibleWidth: 1f);

        Button delBtn = BuilderUIFactory.CreateButton(row1.transform, "DelBtn", "X",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.6f, 0.15f, 0.15f, 0.9f));
        BuilderUIFactory.AddLayoutElement(delBtn.gameObject, preferredWidth: 25f);
        delBtn.onClick.AddListener(() => onDelete?.Invoke(rowIndex));

        // Row 2: Transform type + Default value + Lookup entries
        GameObject row2 = BuilderUIFactory.CreateRow(transform, "Row2", 28f);

        BuilderUIFactory.CreateText(row2.transform, "TransLabel", "Transform:",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row2.transform.GetChild(0).gameObject, preferredWidth: 70f);

        transformDropdown = BuilderUIFactory.CreateDropdown(row2.transform, "TransformDD",
            TransformTypes, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(transformDropdown.gameObject, preferredWidth: 90f);
        SetDropdownToValue(transformDropdown, TransformTypes, data.TransformType ?? "direct");

        BuilderUIFactory.CreateText(row2.transform, "DefLabel", "Default:",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(row2.transform.GetChild(2).gameObject, preferredWidth: 55f);

        defaultValueField = BuilderUIFactory.CreateInputField(row2.transform, "DefaultVal",
            "default", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        defaultValueField.text = data.DefaultValue ?? "";
        BuilderUIFactory.AddLayoutElement(defaultValueField.gameObject, flexibleWidth: 1f);

        // Lookup section (Row 3)
        lookupSection = BuilderUIFactory.CreateRow(transform, "LookupRow", 28f);

        BuilderUIFactory.CreateText(lookupSection.transform, "LookupLabel", "Lookup (key=val|...):",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(lookupSection.transform.GetChild(0).gameObject, preferredWidth: 140f);

        lookupField = BuilderUIFactory.CreateInputField(lookupSection.transform, "LookupInput",
            "Yes=yes|No=no", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(lookupField.gameObject, flexibleWidth: 1f);

        if (data.LookupEntries != null && data.LookupEntries.Length > 0)
        {
            var parts = new List<string>();
            foreach (var entry in data.LookupEntries)
                parts.Add($"{entry.Key}={entry.Value}");
            lookupField.text = string.Join("|", parts);
        }

        transformDropdown.onValueChanged.AddListener(OnTransformChanged);
        OnTransformChanged(transformDropdown.value);
    }

    private void OnTransformChanged(int index)
    {
        string transformType = index < TransformTypes.Length ? TransformTypes[index] : "direct";
        if (lookupSection != null)
            lookupSection.SetActive(transformType == "lookup");
    }

    public AttributeMapping ToMapping()
    {
        var mapping = new AttributeMapping
        {
            QuestionId = GetSelectedQuestionId(),
            AttributeName = attributeNameField != null ? attributeNameField.text : "",
            DefaultValue = defaultValueField != null ? defaultValueField.text : "",
            TransformType = transformDropdown != null && transformDropdown.value < TransformTypes.Length
                ? TransformTypes[transformDropdown.value] : "direct",
            LookupEntries = Array.Empty<AttributeEntry>()
        };

        if (mapping.TransformType == "lookup" && lookupField != null && !string.IsNullOrEmpty(lookupField.text))
        {
            var entries = new List<AttributeEntry>();
            string[] pairs = lookupField.text.Split('|');
            foreach (string pair in pairs)
            {
                int eqIdx = pair.IndexOf('=');
                if (eqIdx > 0)
                {
                    entries.Add(new AttributeEntry
                    {
                        Key = pair.Substring(0, eqIdx).Trim(),
                        Value = pair.Substring(eqIdx + 1).Trim()
                    });
                }
            }
            mapping.LookupEntries = entries.ToArray();
        }

        return mapping;
    }

    public void UpdateQuestionOptions(string[] questionIds)
    {
        currentQuestionIds = questionIds ?? Array.Empty<string>();
        if (questionDropdown == null) return;

        string currentVal = GetSelectedQuestionId();
        questionDropdown.ClearOptions();
        var opts = new List<Dropdown.OptionData>();
        foreach (var id in currentQuestionIds)
            opts.Add(new Dropdown.OptionData(id));
        questionDropdown.AddOptions(opts);

        SetDropdownToValue(questionDropdown, currentQuestionIds, currentVal);
    }

    public void UpdateIndex(int newIndex)
    {
        rowIndex = newIndex;
    }

    private string GetSelectedQuestionId()
    {
        if (questionDropdown == null || currentQuestionIds.Length == 0) return "";
        int idx = questionDropdown.value;
        return idx < currentQuestionIds.Length ? currentQuestionIds[idx] : "";
    }

    private static void SetDropdownToValue(Dropdown dd, string[] options, string target)
    {
        if (dd == null || options == null || string.IsNullOrEmpty(target)) return;
        for (int i = 0; i < options.Length; i++)
        {
            if (string.Equals(options[i], target, StringComparison.OrdinalIgnoreCase))
            {
                dd.value = i;
                return;
            }
        }
    }
}
