using System;

/// <summary>
/// Complete survey configuration: questions, attribute mappings, and event rules.
/// Serialized as JSON for save/load/share. Loaded at runtime by SurveyConfigManager.
/// </summary>
[Serializable]
public class SurveyConfig
{
    public string ConfigName = "";
    public string Description = "";
    public string CreatedAt = "";
    public string Version = "1.0";

    public SurveyQuestion[] Questions = Array.Empty<SurveyQuestion>();
    public AttributeMapping[] Mappings = Array.Empty<AttributeMapping>();
    public SavedEventRule[] Rules = Array.Empty<SavedEventRule>();
}
