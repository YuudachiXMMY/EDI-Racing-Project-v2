using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class EventScheduleTests
{
    private EventSchedule schedule;

    [SetUp]
    public void SetUp()
    {
        schedule = ScriptableObject.CreateInstance<EventSchedule>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(schedule);
    }

    [Test]
    public void Default_HasEightEvents()
    {
        Assert.AreEqual(8, schedule.Events.Length);
    }

    [Test]
    public void ResetRuntimeState_ClearsAllHasBeenTriggered()
    {
        for (int i = 0; i < schedule.Events.Length; i++)
            schedule.Events[i].HasBeenTriggered = true;

        schedule.ResetRuntimeState();

        for (int i = 0; i < schedule.Events.Length; i++)
            Assert.IsFalse(schedule.Events[i].HasBeenTriggered,
                $"Event {i} ({schedule.Events[i].DisplayName}) was not reset");
    }

    [Test]
    public void ResetRuntimeState_OnAlreadyClean_NoError()
    {
        schedule.ResetRuntimeState();

        for (int i = 0; i < schedule.Events.Length; i++)
            Assert.IsFalse(schedule.Events[i].HasBeenTriggered);
    }

    [Test]
    public void Default_AllEventsHaveDisplayName()
    {
        for (int i = 0; i < schedule.Events.Length; i++)
            Assert.IsFalse(string.IsNullOrEmpty(schedule.Events[i].DisplayName),
                $"Event {i} has no DisplayName");
    }

    [Test]
    public void Default_AllEventsHaveDuration()
    {
        for (int i = 0; i < schedule.Events.Length; i++)
            Assert.Greater(schedule.Events[i].Duration, 0f,
                $"Event {i} ({schedule.Events[i].DisplayName}) has zero duration");
    }

    [Test]
    public void Default_AllEventsHaveUniqueTriggerKeys()
    {
        var keys = new System.Collections.Generic.HashSet<UnityEngine.InputSystem.Key>();
        for (int i = 0; i < schedule.Events.Length; i++)
        {
            Assert.IsTrue(keys.Add(schedule.Events[i].TriggerKey),
                $"Event {i} ({schedule.Events[i].DisplayName}) has duplicate key {schedule.Events[i].TriggerKey}");
        }
    }

    [Test]
    public void Default_HasBeenTriggered_AllFalse()
    {
        for (int i = 0; i < schedule.Events.Length; i++)
            Assert.IsFalse(schedule.Events[i].HasBeenTriggered);
    }

    [Test]
    public void EmptySchedule_ResetRuntimeState_NoError()
    {
        schedule.Events = new EventRule[0];
        Assert.DoesNotThrow(() => schedule.ResetRuntimeState());
    }

    [Test]
    public void Default_WeatherEvents_HaveAllowRepeatTrue()
    {
        for (int i = 0; i < schedule.Events.Length; i++)
        {
            if (schedule.Events[i].Weather != WeatherType.None)
                Assert.IsTrue(schedule.Events[i].AllowRepeat,
                    $"Weather event {schedule.Events[i].DisplayName} should allow repeat");
        }
    }
}
