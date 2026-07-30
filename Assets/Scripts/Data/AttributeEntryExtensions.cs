using System;

/// <summary>
/// Extension methods for AttributeEntry[] — case-insensitive key lookup.
/// Single source of truth for the linear-scan lookup previously duplicated
/// across CarData, CarIdentity, SurveyResponseMapper, ResultsExporter, and
/// SessionData (8 sites). This is the project's first extension method.
/// </summary>
public static class AttributeEntryExtensions
{
    /// <summary>Case-insensitive lookup by Key. Returns the matching Value, or defaultValue if absent.</summary>
    public static string Get(this AttributeEntry[] entries, string key, string defaultValue = null)
    {
        if (entries == null || string.IsNullOrEmpty(key)) return defaultValue;
        for (int i = 0; i < entries.Length; i++)
            if (string.Equals(entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return entries[i].Value;
        return defaultValue;
    }

    /// <summary>True if an entry with the given key exists and has a non-null value.</summary>
    public static bool Has(this AttributeEntry[] entries, string key) =>
        entries.Get(key, null) != null;
}
