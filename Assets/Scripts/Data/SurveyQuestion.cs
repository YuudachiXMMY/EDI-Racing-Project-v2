using System;

/// <summary>
/// Types of survey questions supported by the config system.
/// </summary>
public enum QuestionType
{
    Text,            // Free-form text input
    MultipleChoice,  // Select from predefined options
    Numeric,         // Number within a range (slider or input)
    MultiSelect      // Select multiple from predefined options
}

/// <summary>
/// A single survey question definition.
/// Stored in SurveyConfig and rendered by the survey UI (Phase 4/5).
/// </summary>
[Serializable]
public struct SurveyQuestion
{
    public string Id;
    public string Text;
    public int Type; // (int)QuestionType — JsonUtility compat
    public string[] Options; // for MultipleChoice
    public float MinValue;   // for Numeric
    public float MaxValue;   // for Numeric
    public bool Required;

    public QuestionType QuestionType
    {
        get => (QuestionType)Type;
        set => Type = (int)value;
    }
}
