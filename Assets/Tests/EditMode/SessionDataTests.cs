using System;
using NUnit.Framework;

[TestFixture]
public class SessionDataTests
{
    // --- CarResult.ColorIndex ---

    [Test]
    public void CarResult_ColorIndex_ReturnsZero_WhenAttributesNull()
    {
        var result = new CarResult { Attributes = null };
        Assert.AreEqual(0, result.ColorIndex);
    }

    [Test]
    public void CarResult_ColorIndex_ReturnsZero_WhenNoColorIndexAttribute()
    {
        var result = new CarResult
        {
            Attributes = new[] { new AttributeEntry { Key = "team", Value = "A" } }
        };
        Assert.AreEqual(0, result.ColorIndex);
    }

    [Test]
    public void CarResult_ColorIndex_ReturnsValue_WhenPresent()
    {
        var result = new CarResult
        {
            Attributes = new[] { new AttributeEntry { Key = "colorIndex", Value = "3" } }
        };
        Assert.AreEqual(3, result.ColorIndex);
    }

    [Test]
    public void CarResult_ColorIndex_IsCaseInsensitive()
    {
        var result = new CarResult
        {
            Attributes = new[] { new AttributeEntry { Key = "COLORINDEX", Value = "5" } }
        };
        Assert.AreEqual(5, result.ColorIndex);
    }

    [Test]
    public void CarResult_ColorIndex_ReturnsZero_WhenValueNotParseable()
    {
        var result = new CarResult
        {
            Attributes = new[] { new AttributeEntry { Key = "colorIndex", Value = "notanumber" } }
        };
        Assert.AreEqual(0, result.ColorIndex);
    }

    [Test]
    public void CarResult_ColorIndex_ReturnsFirst_WhenMultiplePresent()
    {
        var result = new CarResult
        {
            Attributes = new[]
            {
                new AttributeEntry { Key = "colorIndex", Value = "2" },
                new AttributeEntry { Key = "colorIndex", Value = "7" }
            }
        };
        Assert.AreEqual(2, result.ColorIndex);
    }

    // --- RaceResults defaults ---

    [Test]
    public void RaceResults_DefaultRankings_IsEmpty()
    {
        var results = new RaceResults();
        Assert.IsNotNull(results.Rankings);
        Assert.AreEqual(0, results.Rankings.Length);
    }

    [Test]
    public void RaceResults_DefaultEventLog_IsEmpty()
    {
        var results = new RaceResults();
        Assert.IsNotNull(results.EventLog);
        Assert.AreEqual(0, results.EventLog.Length);
    }

    [Test]
    public void RaceResults_DefaultTotalRaceTime_IsZero()
    {
        var results = new RaceResults();
        Assert.AreEqual(0f, results.TotalRaceTime);
    }

    // --- SessionData defaults ---

    [Test]
    public void SessionData_DefaultCars_IsEmpty()
    {
        var session = new SessionData();
        Assert.IsNotNull(session.Cars);
        Assert.AreEqual(0, session.Cars.Length);
    }

    [Test]
    public void SessionData_DefaultEvents_IsEmpty()
    {
        var session = new SessionData();
        Assert.IsNotNull(session.Events);
        Assert.AreEqual(0, session.Events.Length);
    }

    [Test]
    public void SessionData_DefaultSessionName_IsEmpty()
    {
        var session = new SessionData();
        Assert.AreEqual("", session.SessionName);
    }

    // --- EventLogEntry ---

    [Test]
    public void EventLogEntry_FieldsAreAccessible()
    {
        var entry = new EventLogEntry
        {
            Timestamp = 12.5f,
            EventName = "Snow",
            AffectedCount = 3,
            TotalCars = 8
        };
        Assert.AreEqual(12.5f, entry.Timestamp);
        Assert.AreEqual("Snow", entry.EventName);
        Assert.AreEqual(3, entry.AffectedCount);
        Assert.AreEqual(8, entry.TotalCars);
    }

    // --- SavedRuleCondition ---

    [Test]
    public void SavedRuleCondition_StoresOperatorAsInt()
    {
        var cond = new SavedRuleCondition
        {
            AttributeName = "color",
            Operator = (int)ComparisonOperator.Equals,
            CompareValue = "red"
        };
        Assert.AreEqual((int)ComparisonOperator.Equals, cond.Operator);
        Assert.AreEqual("color", cond.AttributeName);
    }
}
