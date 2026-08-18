using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

[TestFixture]
public class DefaultEventRulesTests
{
    [Test]
    public void BaseSaved_HasEightRulesInclSunset()
    {
        var rules = DefaultEventRules.BaseSaved();

        Assert.AreEqual(8, rules.Length);
        Assert.AreEqual("Sunset Weather", rules[7].DisplayName);
        Assert.AreEqual((int)WeatherType.Sunset, rules[7].Weather);
    }

    [Test]
    public void BaseRuntime_HasEightRulesWithDigitKeys()
    {
        var rules = DefaultEventRules.BaseRuntime();

        Assert.AreEqual(8, rules.Length);
        Assert.AreEqual(Key.Digit1, rules[0].TriggerKey);
        Assert.AreEqual(Key.Digit8, rules[7].TriggerKey);
        Assert.AreEqual(WeatherType.Sunset, rules[7].Weather);
    }

    [Test]
    public void BaseRuntime_MatchesEventScheduleDefault()
    {
        var schedule = ScriptableObject.CreateInstance<EventSchedule>();
        try
        {
            var runtime = DefaultEventRules.BaseRuntime();

            Assert.AreEqual(schedule.Events.Length, runtime.Length);
            for (int i = 0; i < runtime.Length; i++)
            {
                Assert.AreEqual(schedule.Events[i].DisplayName, runtime[i].DisplayName, $"rule {i} DisplayName");
                Assert.AreEqual(schedule.Events[i].SpeedDelta, runtime[i].SpeedDelta, $"rule {i} SpeedDelta");
                Assert.AreEqual(schedule.Events[i].Weather, runtime[i].Weather, $"rule {i} Weather");
                Assert.AreEqual(schedule.Events[i].TriggerKey, runtime[i].TriggerKey, $"rule {i} TriggerKey");
            }
        }
        finally
        {
            Object.DestroyImmediate(schedule);
        }
    }
}
