using NUnit.Framework;
using UnityEngine.InputSystem;

[TestFixture]
public class EventRuleKeysTests
{
    private static SavedEventRule Rule(string name) => new SavedEventRule
    {
        DisplayName = name,
        AttributeName = "",
        Operator = 0,
        CompareValue = "",
        SpeedDelta = 0f,
        Duration = 1f,
        Weather = 0,
        AllowRepeat = false
    };

    [Test]
    public void DigitKeys_ContainsNineDigitsInOrder()
    {
        Assert.AreEqual(9, EventRuleKeys.DigitKeys.Length);
        Assert.AreEqual(Key.Digit1, EventRuleKeys.DigitKeys[0]);
        Assert.AreEqual(Key.Digit9, EventRuleKeys.DigitKeys[8]);
    }

    [Test]
    public void AssignKeys_AssignsDigit1ToNInOrder()
    {
        var rules = new[] { Rule("a"), Rule("b"), Rule("c") };
        var result = EventRuleKeys.AssignKeys(rules);

        Assert.AreEqual(3, result.Length);
        Assert.AreEqual(Key.Digit1, result[0].TriggerKey);
        Assert.AreEqual(Key.Digit2, result[1].TriggerKey);
        Assert.AreEqual(Key.Digit3, result[2].TriggerKey);
        Assert.AreEqual("a", result[0].DisplayName);
    }

    [Test]
    public void AssignKeys_MoreRulesThanKeys_TruncatesToNine()
    {
        var rules = new SavedEventRule[12];
        for (int i = 0; i < rules.Length; i++) rules[i] = Rule($"r{i}");

        var result = EventRuleKeys.AssignKeys(rules);

        Assert.AreEqual(9, result.Length);
        Assert.AreEqual(Key.Digit9, result[8].TriggerKey);
    }

    [Test]
    public void AssignKeys_Null_ReturnsEmpty()
    {
        Assert.AreEqual(0, EventRuleKeys.AssignKeys(null).Length);
    }

    [Test]
    public void AssignKeys_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(0, EventRuleKeys.AssignKeys(new SavedEventRule[0]).Length);
    }
}
