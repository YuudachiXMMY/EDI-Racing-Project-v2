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
}
