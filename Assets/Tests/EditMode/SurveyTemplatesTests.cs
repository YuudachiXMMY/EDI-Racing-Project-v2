using System;
using NUnit.Framework;

[TestFixture]
public class SurveyTemplatesTests
{
    [Test]
    public void TemplateNames_ContainsAllFourTemplates()
    {
        var names = SurveyTemplates.TemplateNames;

        Assert.AreEqual(4, names.Length);
        Assert.Contains("V1 Parity", names);
        Assert.Contains("Accessibility", names);
        Assert.Contains("Diversity", names);
        Assert.Contains("ENGG*1100 Survey", names);
    }

    [Test]
    public void GetTemplate_V1Parity_ReturnsValidConfig()
    {
        var config = SurveyTemplates.GetTemplate("V1 Parity");

        Assert.IsNotNull(config);
        Assert.AreEqual("V1 Parity", config.ConfigName);
        Assert.AreEqual(0, config.Questions.Length);
        Assert.AreEqual(0, config.Mappings.Length);
        Assert.AreEqual(8, config.Rules.Length);
    }

    [Test]
    public void GetTemplate_Accessibility_ReturnsCorrectCounts()
    {
        var config = SurveyTemplates.GetTemplate("Accessibility");

        Assert.IsNotNull(config);
        Assert.AreEqual("Accessibility", config.ConfigName);
        Assert.AreEqual(3, config.Questions.Length);
        Assert.AreEqual(3, config.Mappings.Length);
        Assert.AreEqual(3, config.Rules.Length);
    }

    [Test]
    public void GetTemplate_Diversity_ReturnsCorrectCounts()
    {
        var config = SurveyTemplates.GetTemplate("Diversity");

        Assert.IsNotNull(config);
        Assert.AreEqual("Diversity", config.ConfigName);
        Assert.AreEqual(3, config.Questions.Length);
        Assert.AreEqual(3, config.Mappings.Length);
        Assert.AreEqual(4, config.Rules.Length);
    }

    [Test]
    public void GetTemplate_ENGG1100_ReturnsCorrectCounts()
    {
        var config = SurveyTemplates.GetTemplate("ENGG*1100 Survey");

        Assert.IsNotNull(config);
        Assert.AreEqual("ENGG*1100 Survey", config.ConfigName);
        Assert.AreEqual(14, config.Questions.Length);
        Assert.AreEqual(8, config.Mappings.Length);
        Assert.AreEqual(8, config.Rules.Length);
    }

    [Test]
    public void GetTemplate_UnknownName_ReturnsNull()
    {
        Assert.IsNull(SurveyTemplates.GetTemplate("Nonexistent"));
    }

    [Test]
    public void AllTemplates_RulesHaveValidOperators()
    {
        int maxOperator = Enum.GetValues(typeof(ComparisonOperator)).Length;
        foreach (var name in SurveyTemplates.TemplateNames)
        {
            var config = SurveyTemplates.GetTemplate(name);
            foreach (var rule in config.Rules)
            {
                Assert.IsTrue(rule.Operator >= 0 && rule.Operator < maxOperator,
                    $"Template '{name}' rule '{rule.DisplayName}' has invalid operator {rule.Operator}");
            }
        }
    }

    [Test]
    public void AllTemplates_MappingsHaveNonEmptyFields()
    {
        foreach (var name in SurveyTemplates.TemplateNames)
        {
            var config = SurveyTemplates.GetTemplate(name);
            foreach (var mapping in config.Mappings)
            {
                Assert.IsFalse(string.IsNullOrEmpty(mapping.QuestionId),
                    $"Template '{name}' has mapping with empty QuestionId");
                Assert.IsFalse(string.IsNullOrEmpty(mapping.AttributeName),
                    $"Template '{name}' has mapping with empty AttributeName");
            }
        }
    }

    [Test]
    public void AllTemplates_HaveNonEmptyConfigName()
    {
        foreach (var name in SurveyTemplates.TemplateNames)
        {
            var config = SurveyTemplates.GetTemplate(name);
            Assert.IsFalse(string.IsNullOrEmpty(config.ConfigName),
                $"Template '{name}' has empty ConfigName");
        }
    }
}
