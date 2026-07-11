using System;

/// <summary>
/// Maps a survey question response to a car attribute.
/// TransformType controls how the raw response becomes an attribute value:
///   "direct"  — raw response text becomes the attribute value
///   "lookup"  — map specific responses to values via LookupEntries
///   "numeric" — parse as number, pass through as-is
/// </summary>
[Serializable]
public struct AttributeMapping
{
    public string QuestionId;
    public string AttributeName;
    public string DefaultValue;
    public string TransformType;

    /// <summary>
    /// For "lookup" transform: maps response text (Key) to attribute value (Value).
    /// Reuses AttributeEntry from CarData.cs for serialization consistency.
    /// </summary>
    public AttributeEntry[] LookupEntries;
}
