using NUnit.Framework;
using UnityEngine.InputSystem;

[TestFixture]
public class SavedEventRuleTests
{
    [Test]
    public void FromRule_PreservesAllFields()
    {
        var rule = new EventRule
        {
            DisplayName = "Test Event",
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = "3",
            SpeedDelta = -10f,
            Duration = 8f,
            Weather = WeatherType.Snow,
            AllowRepeat = true,
            TriggerKey = Key.Digit1,
            HasBeenTriggered = true
        };

        var saved = SavedEventRule.FromRule(rule);

        Assert.AreEqual("Test Event", saved.DisplayName);
        Assert.AreEqual("colorIndex", saved.AttributeName);
        Assert.AreEqual((int)ComparisonOperator.Equals, saved.Operator);
        Assert.AreEqual("3", saved.CompareValue);
        Assert.AreEqual(-10f, saved.SpeedDelta);
        Assert.AreEqual(8f, saved.Duration);
        Assert.AreEqual((int)WeatherType.Snow, saved.Weather);
        Assert.IsTrue(saved.AllowRepeat);
    }

    [Test]
    public void FromRule_NullStrings_DefaultToEmpty()
    {
        var rule = new EventRule
        {
            DisplayName = null,
            AttributeName = null,
            CompareValue = null
        };

        var saved = SavedEventRule.FromRule(rule);

        Assert.AreEqual("", saved.DisplayName);
        Assert.AreEqual("", saved.AttributeName);
        Assert.AreEqual("", saved.CompareValue);
    }

    [Test]
    public void ToRule_RestoresEnumValues()
    {
        var saved = new SavedEventRule
        {
            DisplayName = "Snow",
            AttributeName = "",
            Operator = (int)ComparisonOperator.All,
            CompareValue = "",
            SpeedDelta = -8f,
            Duration = 12f,
            Weather = (int)WeatherType.Snow,
            AllowRepeat = true
        };

        var rule = saved.ToRule(Key.Digit6);

        Assert.AreEqual(ComparisonOperator.All, rule.Operator);
        Assert.AreEqual(WeatherType.Snow, rule.Weather);
        Assert.AreEqual(Key.Digit6, rule.TriggerKey);
    }

    [Test]
    public void ToRule_ResetsHasBeenTriggered()
    {
        var saved = SavedEventRule.FromRule(new EventRule { HasBeenTriggered = true });
        var rule = saved.ToRule(Key.Digit1);

        Assert.IsFalse(rule.HasBeenTriggered);
    }

    [Test]
    public void RoundTrip_PreservesAllData()
    {
        var original = new EventRule
        {
            DisplayName = "Boost",
            AttributeName = "functions",
            Operator = ComparisonOperator.Contains,
            CompareValue = "password",
            SpeedDelta = 10f,
            Duration = 6f,
            Weather = WeatherType.Night,
            AllowRepeat = false,
            TriggerKey = Key.Digit4
        };

        var restored = SavedEventRule.FromRule(original).ToRule(Key.Digit9);

        Assert.AreEqual(original.DisplayName, restored.DisplayName);
        Assert.AreEqual(original.AttributeName, restored.AttributeName);
        Assert.AreEqual(original.Operator, restored.Operator);
        Assert.AreEqual(original.CompareValue, restored.CompareValue);
        Assert.AreEqual(original.SpeedDelta, restored.SpeedDelta);
        Assert.AreEqual(original.Duration, restored.Duration);
        Assert.AreEqual(original.Weather, restored.Weather);
        Assert.AreEqual(original.AllowRepeat, restored.AllowRepeat);
        // TriggerKey is reassigned, not preserved
        Assert.AreEqual(Key.Digit9, restored.TriggerKey);
    }

    // --- Compound condition serialization tests ---

    [Test]
    public void FromRule_WithConditions_PreservesAll()
    {
        var rule = new EventRule
        {
            DisplayName = "Compound",
            Logic = LogicOperator.And,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "color", Operator = ComparisonOperator.Equals, CompareValue = "3" },
                new RuleCondition { AttributeName = "score", Operator = ComparisonOperator.GreaterThan, CompareValue = "5" }
            },
            SpeedDelta = -15f,
            Duration = 10f
        };

        var saved = SavedEventRule.FromRule(rule);

        Assert.AreEqual((int)LogicOperator.And, saved.Logic);
        Assert.IsNotNull(saved.Conditions);
        Assert.AreEqual(2, saved.Conditions.Length);
        Assert.AreEqual("color", saved.Conditions[0].AttributeName);
        Assert.AreEqual((int)ComparisonOperator.Equals, saved.Conditions[0].Operator);
        Assert.AreEqual("3", saved.Conditions[0].CompareValue);
        Assert.AreEqual("score", saved.Conditions[1].AttributeName);
        Assert.AreEqual((int)ComparisonOperator.GreaterThan, saved.Conditions[1].Operator);
    }

    [Test]
    public void ToRule_WithConditions_RestoresCorrectly()
    {
        var saved = new SavedEventRule
        {
            DisplayName = "Compound",
            Logic = (int)LogicOperator.Or,
            Conditions = new[]
            {
                new SavedRuleCondition { AttributeName = "lang", Operator = (int)ComparisonOperator.NotEquals, CompareValue = "en" }
            },
            SpeedDelta = -8f,
            Duration = 6f
        };

        var rule = saved.ToRule(Key.Digit1);

        Assert.AreEqual(LogicOperator.Or, rule.Logic);
        Assert.IsNotNull(rule.Conditions);
        Assert.AreEqual(1, rule.Conditions.Length);
        Assert.AreEqual("lang", rule.Conditions[0].AttributeName);
        Assert.AreEqual(ComparisonOperator.NotEquals, rule.Conditions[0].Operator);
        Assert.AreEqual("en", rule.Conditions[0].CompareValue);
    }

    [Test]
    public void RoundTrip_CompoundRule_Preserves()
    {
        var original = new EventRule
        {
            DisplayName = "Complex",
            Logic = LogicOperator.And,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "a", Operator = ComparisonOperator.Equals, CompareValue = "1" },
                new RuleCondition { AttributeName = "b", Operator = ComparisonOperator.Contains, CompareValue = "x" }
            },
            SpeedDelta = -20f,
            Duration = 12f,
            Weather = WeatherType.Snow,
            AllowRepeat = true
        };

        var restored = SavedEventRule.FromRule(original).ToRule(Key.Digit3);

        Assert.AreEqual(original.Logic, restored.Logic);
        Assert.AreEqual(original.Conditions.Length, restored.Conditions.Length);
        for (int i = 0; i < original.Conditions.Length; i++)
        {
            Assert.AreEqual(original.Conditions[i].AttributeName, restored.Conditions[i].AttributeName);
            Assert.AreEqual(original.Conditions[i].Operator, restored.Conditions[i].Operator);
            Assert.AreEqual(original.Conditions[i].CompareValue, restored.Conditions[i].CompareValue);
        }
        Assert.AreEqual(original.SpeedDelta, restored.SpeedDelta);
        Assert.AreEqual(original.Weather, restored.Weather);
    }

    [Test]
    public void FromRule_NullConditions_StaysNull()
    {
        var rule = new EventRule { DisplayName = "Simple", Conditions = null };
        var saved = SavedEventRule.FromRule(rule);
        Assert.IsNull(saved.Conditions);
    }
}
