using System;

/// <summary>
/// Built-in survey configuration templates for common EDI themes.
/// Provides starting points so professors don't build from scratch.
/// </summary>
public static class SurveyTemplates
{
    public static readonly string[] TemplateNames = { "V1 Parity", "Accessibility", "Diversity", "ENGG*1100 Survey" };

    public static SurveyConfig GetTemplate(string name)
    {
        switch (name)
        {
            case "V1 Parity": return V1Parity();
            case "Accessibility": return AccessibilitySurvey();
            case "Diversity": return DiversitySurvey();
            case "ENGG*1100 Survey": return ENGG1100Survey();
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
            Rules = DefaultEventRules.BaseSaved()
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

    /// <summary>
    /// Replicates the original ENGG*1100 MS Forms questionnaire.
    /// 14 questions, color/numeric mappings, V1 event rules.
    /// Average-threshold post-processing is handled server-side by the web-app.
    /// </summary>
    public static SurveyConfig ENGG1100Survey()
    {
        return new SurveyConfig
        {
            ConfigName = "ENGG*1100 Survey",
            Description = "Replicates the original ENGG*1100 MS Forms questionnaire. Collects team data, computes average-threshold tags (DataTool.py algorithm), and exports as Excel or game CSV.",
            CreatedAt = DateTime.Now.ToString("o"),
            Questions = new SurveyQuestion[]
            {
                new SurveyQuestion { Id = "team_name", Text = "Name your autonomous vehicle team.\n[Letters and numbers only, NO space] (e.g. Apollo3)", Type = (int)QuestionType.Text, Options = Array.Empty<string>(), Required = true },
                new SurveyQuestion { Id = "color", Text = "Choose the colour for your autonomous vehicle.", Type = (int)QuestionType.MultipleChoice, Options = new[] { "Blue", "Red", "Black", "White", "Green" }, Required = true },
                new SurveyQuestion { Id = "member_count", Text = "How many members in the team?", Type = (int)QuestionType.Numeric, MinValue = 1f, MaxValue = 20f, Required = true },
                new SurveyQuestion { Id = "member_names", Text = "Please enter the first names of the members of your group, separated by commas. (e.g. Steve, Albert, Hussein, Wael). This information will only be used to distribute prizes after", Type = (int)QuestionType.Text, Options = Array.Empty<string>(), Required = true },
                new SurveyQuestion { Id = "facial_count", Text = "How many members in your team use a facial recognition function on their phones/PCs?", Type = (int)QuestionType.Numeric, MinValue = 0f, MaxValue = 20f, Required = true },
                new SurveyQuestion { Id = "glasses_count", Text = "How many members in your team wear glasses or contact lenses?", Type = (int)QuestionType.Numeric, MinValue = 0f, MaxValue = 20f, Required = true },
                new SurveyQuestion { Id = "language_count", Text = "How many different languages overall are spoken in your team?", Type = (int)QuestionType.Numeric, MinValue = 0f, MaxValue = 50f, Required = true },
                new SurveyQuestion { Id = "male_count", Text = "How many members in the group identify themselves as male?", Type = (int)QuestionType.Numeric, MinValue = 0f, MaxValue = 20f, Required = true },
                new SurveyQuestion { Id = "pwd_count", Text = "How many members in the team has their PPW* 5 characters or more?", Type = (int)QuestionType.Numeric, MinValue = 0f, MaxValue = 20f, Required = true },
                new SurveyQuestion { Id = "distance_km", Text = "Consider all members, whose hometown (attended their high school) is the furthest from the University of Guelph? Enter the distance in kilometers below.", Type = (int)QuestionType.Numeric, MinValue = 0f, MaxValue = 99999f, Required = true },
                new SurveyQuestion { Id = "vehicle_type", Text = "What type of vehicle would your team prefer to ride in?", Type = (int)QuestionType.MultipleChoice, Options = new[] { "Convertible", "Hatchback", "Pickup truck", "Sedan", "SUV", "Van" }, Required = true },
                new SurveyQuestion { Id = "entertainment", Text = "What type of entertainment system do you utilize the most?", Type = (int)QuestionType.MultipleChoice, Options = new[] { "Bluetooth", "CD player", "AUX connected devices", "Apple CarPlay", "Android AutoCar" }, Required = true },
                new SurveyQuestion { Id = "driving_experience", Text = "What is the cumulative driving experience of your team (in years)?", Type = (int)QuestionType.Numeric, MinValue = 0f, MaxValue = 200f, Required = false },
                new SurveyQuestion { Id = "car_features", Text = "Rank the following advanced in-car features in terms of importance to your team. (Select up to 3)", Type = (int)QuestionType.MultiSelect, Options = new[] { "Heads-up display", "Automatic High Beams", "Electronic Door Handles", "Do-It-All Touchscreens", "Camera Vision", "Lane-Keep Assist", "Full-self driving" }, MaxValue = 3f, Required = false },
            },
            Mappings = new AttributeMapping[]
            {
                new AttributeMapping { QuestionId = "color", AttributeName = "colorIndex", DefaultValue = "0", TransformType = "lookup", LookupEntries = new AttributeEntry[] { new AttributeEntry { Key = "Green", Value = "0" }, new AttributeEntry { Key = "Black", Value = "1" }, new AttributeEntry { Key = "Red", Value = "2" }, new AttributeEntry { Key = "Blue", Value = "3" }, new AttributeEntry { Key = "White", Value = "4" } } },
                new AttributeMapping { QuestionId = "facial_count", AttributeName = "facial_count", DefaultValue = "0", TransformType = "numeric", LookupEntries = Array.Empty<AttributeEntry>() },
                new AttributeMapping { QuestionId = "glasses_count", AttributeName = "glasses_count", DefaultValue = "0", TransformType = "numeric", LookupEntries = Array.Empty<AttributeEntry>() },
                new AttributeMapping { QuestionId = "language_count", AttributeName = "language_count", DefaultValue = "0", TransformType = "numeric", LookupEntries = Array.Empty<AttributeEntry>() },
                new AttributeMapping { QuestionId = "male_count", AttributeName = "male_count", DefaultValue = "0", TransformType = "numeric", LookupEntries = Array.Empty<AttributeEntry>() },
                new AttributeMapping { QuestionId = "pwd_count", AttributeName = "pwd_count", DefaultValue = "0", TransformType = "numeric", LookupEntries = Array.Empty<AttributeEntry>() },
                new AttributeMapping { QuestionId = "distance_km", AttributeName = "distance_km", DefaultValue = "0", TransformType = "numeric", LookupEntries = Array.Empty<AttributeEntry>() },
            },
            Rules = DefaultEventRules.BaseSaved()
        };
    }
}
