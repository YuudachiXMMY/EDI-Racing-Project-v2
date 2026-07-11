using System;

/// <summary>
/// Built-in survey configuration templates for common EDI themes.
/// Provides starting points so professors don't build from scratch.
/// </summary>
public static class SurveyTemplates
{
    public static readonly string[] TemplateNames = { "V1 Parity", "Accessibility", "Diversity" };

    public static SurveyConfig GetTemplate(string name)
    {
        switch (name)
        {
            case "V1 Parity": return V1Parity();
            case "Accessibility": return AccessibilitySurvey();
            case "Diversity": return DiversitySurvey();
            default: return null;
        }
    }

    /// <summary>
    /// Reproduces the original ENGG*1100 setup.
    /// No questions or mappings (data comes from CSV import).
    /// 7 default event rules matching EventSchedule defaults.
    /// </summary>
    public static SurveyConfig V1Parity()
    {
        return new SurveyConfig
        {
            ConfigName = "V1 Parity",
            Description = "Original ENGG*1100 configuration. Import data via CSV with columns: teamName, colorIndex, functions.",
            CreatedAt = DateTime.Now.ToString("o"),
            Questions = Array.Empty<SurveyQuestion>(),
            Mappings = Array.Empty<AttributeMapping>(),
            Rules = new SavedEventRule[]
            {
                new SavedEventRule
                {
                    DisplayName = "Name Length Penalty",
                    AttributeName = "teamName",
                    Operator = (int)ComparisonOperator.LengthGreaterThan,
                    CompareValue = "10",
                    SpeedDelta = -10f,
                    Duration = 8f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Color Boost (Blue)",
                    AttributeName = "colorIndex",
                    Operator = (int)ComparisonOperator.Equals,
                    CompareValue = "3",
                    SpeedDelta = 15f,
                    Duration = 6f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Color Penalty (Red)",
                    AttributeName = "colorIndex",
                    Operator = (int)ComparisonOperator.Equals,
                    CompareValue = "2",
                    SpeedDelta = -12f,
                    Duration = 8f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Function Boost (Password)",
                    AttributeName = "functions",
                    Operator = (int)ComparisonOperator.Contains,
                    CompareValue = "password",
                    SpeedDelta = 10f,
                    Duration = 6f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Function Penalty (Face Recog)",
                    AttributeName = "functions",
                    Operator = (int)ComparisonOperator.Contains,
                    CompareValue = "facerecog",
                    SpeedDelta = -10f,
                    Duration = 8f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Snow Weather",
                    AttributeName = "",
                    Operator = (int)ComparisonOperator.All,
                    CompareValue = "",
                    SpeedDelta = -8f,
                    Duration = 12f,
                    Weather = (int)WeatherType.Snow,
                    AllowRepeat = true
                },
                new SavedEventRule
                {
                    DisplayName = "Night Weather",
                    AttributeName = "",
                    Operator = (int)ComparisonOperator.All,
                    CompareValue = "",
                    SpeedDelta = -5f,
                    Duration = 15f,
                    Weather = (int)WeatherType.Night,
                    AllowRepeat = true
                }
            }
        };
    }

    /// <summary>
    /// Accessibility-themed survey for demonstrating how disability
    /// and assistive technology access create unequal outcomes.
    /// </summary>
    public static SurveyConfig AccessibilitySurvey()
    {
        return new SurveyConfig
        {
            ConfigName = "Accessibility",
            Description = "Demonstrates how disability status and assistive technology access affect outcomes.",
            CreatedAt = DateTime.Now.ToString("o"),
            Questions = new SurveyQuestion[]
            {
                new SurveyQuestion
                {
                    Id = "disability",
                    Text = "Do you have a disability that affects your daily activities?",
                    Type = (int)QuestionType.MultipleChoice,
                    Options = new[] { "No", "Yes - Physical", "Yes - Cognitive", "Yes - Sensory", "Prefer not to say" },
                    Required = true
                },
                new SurveyQuestion
                {
                    Id = "assistive_tech",
                    Text = "Do you use assistive technology (screen reader, hearing aid, mobility device, etc.)?",
                    Type = (int)QuestionType.MultipleChoice,
                    Options = new[] { "No", "Yes" },
                    Required = true
                },
                new SurveyQuestion
                {
                    Id = "accommodation_ease",
                    Text = "How easy is it for you to get accommodations when needed? (1=very difficult, 10=very easy)",
                    Type = (int)QuestionType.Numeric,
                    MinValue = 1f,
                    MaxValue = 10f,
                    Required = true
                }
            },
            Mappings = new AttributeMapping[]
            {
                new AttributeMapping
                {
                    QuestionId = "disability",
                    AttributeName = "disability",
                    DefaultValue = "none",
                    TransformType = "lookup",
                    LookupEntries = new AttributeEntry[]
                    {
                        new AttributeEntry { Key = "No", Value = "none" },
                        new AttributeEntry { Key = "Yes - Physical", Value = "physical" },
                        new AttributeEntry { Key = "Yes - Cognitive", Value = "cognitive" },
                        new AttributeEntry { Key = "Yes - Sensory", Value = "sensory" },
                        new AttributeEntry { Key = "Prefer not to say", Value = "undisclosed" }
                    }
                },
                new AttributeMapping
                {
                    QuestionId = "assistive_tech",
                    AttributeName = "assistive_tech",
                    DefaultValue = "no",
                    TransformType = "lookup",
                    LookupEntries = new AttributeEntry[]
                    {
                        new AttributeEntry { Key = "No", Value = "no" },
                        new AttributeEntry { Key = "Yes", Value = "yes" }
                    }
                },
                new AttributeMapping
                {
                    QuestionId = "accommodation_ease",
                    AttributeName = "accommodation_ease",
                    DefaultValue = "5",
                    TransformType = "numeric",
                    LookupEntries = Array.Empty<AttributeEntry>()
                }
            },
            Rules = new SavedEventRule[]
            {
                new SavedEventRule
                {
                    DisplayName = "Inaccessible Building",
                    AttributeName = "disability",
                    Operator = (int)ComparisonOperator.NotEquals,
                    CompareValue = "none",
                    SpeedDelta = -12f,
                    Duration = 8f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Tech Upgrade",
                    AttributeName = "assistive_tech",
                    Operator = (int)ComparisonOperator.Equals,
                    CompareValue = "yes",
                    SpeedDelta = 10f,
                    Duration = 6f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Accommodation Barrier",
                    AttributeName = "accommodation_ease",
                    Operator = (int)ComparisonOperator.LessThan,
                    CompareValue = "5",
                    SpeedDelta = -8f,
                    Duration = 10f,
                    Weather = (int)WeatherType.Snow,
                    AllowRepeat = false
                }
            }
        };
    }

    /// <summary>
    /// General diversity survey demonstrating how identity factors
    /// create systemic advantages and barriers.
    /// </summary>
    public static SurveyConfig DiversitySurvey()
    {
        return new SurveyConfig
        {
            ConfigName = "Diversity",
            Description = "Demonstrates how identity factors (language, first-generation status) create systemic barriers.",
            CreatedAt = DateTime.Now.ToString("o"),
            Questions = new SurveyQuestion[]
            {
                new SurveyQuestion
                {
                    Id = "primary_language",
                    Text = "What is your primary language?",
                    Type = (int)QuestionType.Text,
                    Options = Array.Empty<string>(),
                    Required = true
                },
                new SurveyQuestion
                {
                    Id = "first_gen",
                    Text = "Are you a first-generation university student?",
                    Type = (int)QuestionType.MultipleChoice,
                    Options = new[] { "No", "Yes" },
                    Required = true
                },
                new SurveyQuestion
                {
                    Id = "work_hours",
                    Text = "How many hours per week do you work outside of school? (0-40)",
                    Type = (int)QuestionType.Numeric,
                    MinValue = 0f,
                    MaxValue = 40f,
                    Required = false
                }
            },
            Mappings = new AttributeMapping[]
            {
                new AttributeMapping
                {
                    QuestionId = "primary_language",
                    AttributeName = "language",
                    DefaultValue = "English",
                    TransformType = "direct",
                    LookupEntries = Array.Empty<AttributeEntry>()
                },
                new AttributeMapping
                {
                    QuestionId = "first_gen",
                    AttributeName = "first_gen",
                    DefaultValue = "no",
                    TransformType = "lookup",
                    LookupEntries = new AttributeEntry[]
                    {
                        new AttributeEntry { Key = "No", Value = "no" },
                        new AttributeEntry { Key = "Yes", Value = "yes" }
                    }
                },
                new AttributeMapping
                {
                    QuestionId = "work_hours",
                    AttributeName = "work_hours",
                    DefaultValue = "0",
                    TransformType = "numeric",
                    LookupEntries = Array.Empty<AttributeEntry>()
                }
            },
            Rules = new SavedEventRule[]
            {
                new SavedEventRule
                {
                    DisplayName = "Language Barrier",
                    AttributeName = "language",
                    Operator = (int)ComparisonOperator.NotEquals,
                    CompareValue = "English",
                    SpeedDelta = -10f,
                    Duration = 8f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "First-Gen Headwind",
                    AttributeName = "first_gen",
                    Operator = (int)ComparisonOperator.Equals,
                    CompareValue = "yes",
                    SpeedDelta = -8f,
                    Duration = 10f,
                    Weather = (int)WeatherType.Snow,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Work Fatigue",
                    AttributeName = "work_hours",
                    Operator = (int)ComparisonOperator.GreaterThan,
                    CompareValue = "20",
                    SpeedDelta = -6f,
                    Duration = 12f,
                    Weather = (int)WeatherType.Night,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Mentorship Program",
                    AttributeName = "first_gen",
                    Operator = (int)ComparisonOperator.Equals,
                    CompareValue = "yes",
                    SpeedDelta = 12f,
                    Duration = 6f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                }
            }
        };
    }
}
