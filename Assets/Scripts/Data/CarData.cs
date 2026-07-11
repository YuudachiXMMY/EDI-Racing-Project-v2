using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Serializable key-value pair for car attributes.
/// Used instead of Dictionary for JsonUtility compatibility.
/// </summary>
[Serializable]
public struct AttributeEntry
{
    public string Key;
    public string Value;
}

/// <summary>
/// Immutable data representing a car parsed from survey CSV.
/// Supports arbitrary attributes via AttributeEntry array.
/// Backward-compatible accessors for v1 fields (ColorIndex, Functions).
/// </summary>
[Serializable]
public struct CarData
{
    public string TeamName;
    public AttributeEntry[] Attributes;

    public CarData(string teamName, AttributeEntry[] attributes)
    {
        TeamName = teamName;
        Attributes = attributes ?? Array.Empty<AttributeEntry>();
    }

    public CarData(string teamName, Dictionary<string, string> attributes)
    {
        TeamName = teamName;
        Attributes = attributes != null
            ? attributes.Select(kv => new AttributeEntry { Key = kv.Key, Value = kv.Value }).ToArray()
            : Array.Empty<AttributeEntry>();
    }

    // --- Generic Accessors ---

    public string GetAttribute(string key, string defaultValue = "")
    {
        if (Attributes == null) return defaultValue;
        for (int i = 0; i < Attributes.Length; i++)
            if (string.Equals(Attributes[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return Attributes[i].Value;
        return defaultValue;
    }

    public int GetIntAttribute(string key, int defaultValue = 0)
    {
        string val = GetAttribute(key, null);
        if (val != null && int.TryParse(val, out int result)) return result;
        return defaultValue;
    }

    public float GetFloatAttribute(string key, float defaultValue = 0f)
    {
        string val = GetAttribute(key, null);
        if (val != null && float.TryParse(val, out float result)) return result;
        return defaultValue;
    }

    public bool HasAttribute(string key)
    {
        if (Attributes == null) return false;
        for (int i = 0; i < Attributes.Length; i++)
            if (string.Equals(Attributes[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public string[] GetAttributeKeys()
    {
        if (Attributes == null) return Array.Empty<string>();
        return Attributes.Select(a => a.Key).ToArray();
    }

    public Dictionary<string, string> ToDictionary()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Attributes != null)
            foreach (var attr in Attributes)
                if (!string.IsNullOrEmpty(attr.Key))
                    dict[attr.Key] = attr.Value;
        return dict;
    }

    // --- Backward-Compatible Accessors (v1 parity) ---

    public int ColorIndex => GetIntAttribute("colorIndex", 0);

    public string[] Functions
    {
        get
        {
            string val = GetAttribute("functions", "");
            if (string.IsNullOrEmpty(val)) return Array.Empty<string>();
            return val.Split('/').Select(f => f.Trim().ToLower()).Where(f => f.Length > 0).ToArray();
        }
    }
}
