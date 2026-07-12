using System;
using NUnit.Framework;

[TestFixture]
public class SurveyResponseMapperTests
{
    [Test]
    public void MapResponses_NoMappings_ReturnsEmptyAttributes()
    {
        var responses = new AttributeEntry[] { new AttributeEntry { Key = "q1", Value = "hello" } };
        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, Array.Empty<AttributeMapping>());

        Assert.AreEqual("Team1", result.TeamName);
        Assert.AreEqual(0, result.Attributes.Length);
    }

    [Test]
    public void MapResponses_DirectTransform_PassesThroughValue()
    {
        var responses = new AttributeEntry[] { new AttributeEntry { Key = "q1", Value = "French" } };
        var mappings = new AttributeMapping[]
        {
            new AttributeMapping { QuestionId = "q1", AttributeName = "language", TransformType = "direct", DefaultValue = "" }
        };

        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, mappings);

        Assert.AreEqual("French", result.GetAttribute("language"));
    }

    [Test]
    public void MapResponses_LookupTransform_MapsCorrectly()
    {
        var responses = new AttributeEntry[] { new AttributeEntry { Key = "q1", Value = "Yes" } };
        var mappings = new AttributeMapping[]
        {
            new AttributeMapping
            {
                QuestionId = "q1", AttributeName = "first_gen", TransformType = "lookup",
                DefaultValue = "no",
                LookupEntries = new AttributeEntry[]
                {
                    new AttributeEntry { Key = "No", Value = "no" },
                    new AttributeEntry { Key = "Yes", Value = "yes" }
                }
            }
        };

        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, mappings);

        Assert.AreEqual("yes", result.GetAttribute("first_gen"));
    }

    [Test]
    public void MapResponses_LookupTransform_UnknownValue_UsesDefault()
    {
        var responses = new AttributeEntry[] { new AttributeEntry { Key = "q1", Value = "Maybe" } };
        var mappings = new AttributeMapping[]
        {
            new AttributeMapping
            {
                QuestionId = "q1", AttributeName = "status", TransformType = "lookup",
                DefaultValue = "unknown",
                LookupEntries = new AttributeEntry[]
                {
                    new AttributeEntry { Key = "Yes", Value = "yes" },
                    new AttributeEntry { Key = "No", Value = "no" }
                }
            }
        };

        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, mappings);

        Assert.AreEqual("unknown", result.GetAttribute("status"));
    }

    [Test]
    public void MapResponses_NumericTransform_ValidNumber_Passes()
    {
        var responses = new AttributeEntry[] { new AttributeEntry { Key = "q1", Value = "7.5" } };
        var mappings = new AttributeMapping[]
        {
            new AttributeMapping { QuestionId = "q1", AttributeName = "score", TransformType = "numeric", DefaultValue = "0" }
        };

        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, mappings);

        Assert.AreEqual("7.5", result.GetAttribute("score"));
    }

    [Test]
    public void MapResponses_NumericTransform_InvalidNumber_UsesDefault()
    {
        var responses = new AttributeEntry[] { new AttributeEntry { Key = "q1", Value = "abc" } };
        var mappings = new AttributeMapping[]
        {
            new AttributeMapping { QuestionId = "q1", AttributeName = "score", TransformType = "numeric", DefaultValue = "0" }
        };

        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, mappings);

        Assert.AreEqual("0", result.GetAttribute("score"));
    }

    [Test]
    public void MapResponses_MissingResponse_UsesDefaultValue()
    {
        var responses = Array.Empty<AttributeEntry>();
        var mappings = new AttributeMapping[]
        {
            new AttributeMapping { QuestionId = "q1", AttributeName = "color", TransformType = "direct", DefaultValue = "blue" }
        };

        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, mappings);

        Assert.AreEqual("blue", result.GetAttribute("color"));
    }

    [Test]
    public void MapResponses_MultipleMappings_AllApplied()
    {
        var responses = new AttributeEntry[]
        {
            new AttributeEntry { Key = "q1", Value = "English" },
            new AttributeEntry { Key = "q2", Value = "5" }
        };
        var mappings = new AttributeMapping[]
        {
            new AttributeMapping { QuestionId = "q1", AttributeName = "language", TransformType = "direct", DefaultValue = "" },
            new AttributeMapping { QuestionId = "q2", AttributeName = "score", TransformType = "numeric", DefaultValue = "0" }
        };

        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, mappings);

        Assert.AreEqual("English", result.GetAttribute("language"));
        Assert.AreEqual("5", result.GetAttribute("score"));
        Assert.AreEqual(2, result.Attributes.Length);
    }

    [Test]
    public void MapResponses_CaseInsensitiveQuestionIdMatch()
    {
        var responses = new AttributeEntry[] { new AttributeEntry { Key = "Q1", Value = "hello" } };
        var mappings = new AttributeMapping[]
        {
            new AttributeMapping { QuestionId = "q1", AttributeName = "greeting", TransformType = "direct", DefaultValue = "" }
        };

        CarData result = SurveyResponseMapper.MapResponses("Team1", responses, mappings);

        Assert.AreEqual("hello", result.GetAttribute("greeting"));
    }
}
