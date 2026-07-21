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
}
