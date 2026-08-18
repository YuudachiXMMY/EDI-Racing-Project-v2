using NUnit.Framework;
using UnityEngine.InputSystem;

[TestFixture]
public class EventRuleTests
{
    [Test]
    public void EventRule_HasBeenTriggered_DefaultsFalse()
    {
        var rule = new EventRule();
        Assert.IsFalse(rule.HasBeenTriggered);
    }

    [Test]
    public void EventRule_AllowRepeat_DefaultsFalse()
    {
        var rule = new EventRule();
        Assert.IsFalse(rule.AllowRepeat);
    }

    [Test]
    public void EventRule_SpeedDelta_DefaultsZero()
    {
        var rule = new EventRule();
        Assert.AreEqual(0f, rule.SpeedDelta);
    }

    [Test]
    public void EventRule_Duration_DefaultsZero()
    {
        var rule = new EventRule();
        Assert.AreEqual(0f, rule.Duration);
    }

    [Test]
    public void EventRule_Conditions_DefaultsNull()
    {
        var rule = new EventRule();
        Assert.IsNull(rule.Conditions);
    }

    [Test]
    public void EventRule_Weather_DefaultsNone()
    {
        var rule = new EventRule();
        Assert.AreEqual(default(WeatherType), rule.Weather);
    }

    [Test]
    public void RuleCondition_FieldsAssignable()
    {
        var cond = new RuleCondition
        {
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = "3"
        };
        Assert.AreEqual("colorIndex", cond.AttributeName);
        Assert.AreEqual(ComparisonOperator.Equals, cond.Operator);
        Assert.AreEqual("3", cond.CompareValue);
    }

    [Test]
    public void LogicOperator_And_HasValueZero()
    {
        Assert.AreEqual(0, (int)LogicOperator.And);
    }

    [Test]
    public void LogicOperator_Or_HasValueOne()
    {
        Assert.AreEqual(1, (int)LogicOperator.Or);
    }

    [Test]
    public void EventRule_CanSetCompoundConditions()
    {
        var rule = new EventRule
        {
            DisplayName = "Test Compound",
            Logic = LogicOperator.And,
            Conditions = new[]
            {
                new RuleCondition
                {
                    AttributeName = "colorIndex",
                    Operator = ComparisonOperator.Equals,
                    CompareValue = "1"
                },
                new RuleCondition
                {
                    AttributeName = "functions",
                    Operator = ComparisonOperator.Contains,
                    CompareValue = "password"
                }
            },
            SpeedDelta = -5f,
            Duration = 10f
        };

        Assert.AreEqual(2, rule.Conditions.Length);
        Assert.AreEqual(LogicOperator.And, rule.Logic);
        Assert.AreEqual(-5f, rule.SpeedDelta);
    }
}
