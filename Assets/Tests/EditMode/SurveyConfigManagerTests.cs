using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

[TestFixture]
public class SurveyConfigManagerTests
{
    private GameObject managerObj;
    private SurveyConfigManager configManager;
    private EventSchedule schedule;

    [SetUp]
    public void SetUp()
    {
        managerObj = new GameObject("SurveyConfigManager");
        configManager = managerObj.AddComponent<SurveyConfigManager>();

        schedule = ScriptableObject.CreateInstance<EventSchedule>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(managerObj);
        Object.DestroyImmediate(schedule);
    }

    [Test]
    public void SetActiveConfig_StoresReference()
    {
        var config = new SurveyConfig { ConfigName = "Test" };

        configManager.SetActiveConfig(config);

        Assert.AreEqual("Test", configManager.ActiveConfig.ConfigName);
    }

    [Test]
    public void SetActiveConfig_Null_ClearsConfig()
    {
        configManager.SetActiveConfig(new SurveyConfig { ConfigName = "X" });
        configManager.SetActiveConfig(null);

        Assert.IsNull(configManager.ActiveConfig);
    }

    [Test]
    public void ApplyRulesToSchedule_MapsRulesToEvents()
    {
        var config = new SurveyConfig
        {
            ConfigName = "Test",
            Rules = new SavedEventRule[]
            {
                new SavedEventRule
                {
                    DisplayName = "Rule A",
                    AttributeName = "color",
                    Operator = (int)ComparisonOperator.Equals,
                    CompareValue = "blue",
                    SpeedDelta = -5f,
                    Duration = 4f,
                    Weather = (int)WeatherType.None,
                    AllowRepeat = false
                },
                new SavedEventRule
                {
                    DisplayName = "Rule B",
                    AttributeName = "",
                    Operator = (int)ComparisonOperator.All,
                    CompareValue = "",
                    SpeedDelta = -8f,
                    Duration = 10f,
                    Weather = (int)WeatherType.Snow,
                    AllowRepeat = true
                }
            }
        };

        configManager.SetActiveConfig(config);
        configManager.ApplyRulesToSchedule(schedule);

        Assert.AreEqual(2, schedule.Events.Length);
        Assert.AreEqual("Rule A", schedule.Events[0].DisplayName);
        Assert.AreEqual(Key.Digit1, schedule.Events[0].TriggerKey);
        Assert.AreEqual("Rule B", schedule.Events[1].DisplayName);
        Assert.AreEqual(Key.Digit2, schedule.Events[1].TriggerKey);
    }

    [Test]
    public void ApplyRulesToSchedule_MaxNineRules()
    {
        var rules = new SavedEventRule[12];
        for (int i = 0; i < 12; i++)
            rules[i] = new SavedEventRule { DisplayName = $"Rule{i}" };

        configManager.SetActiveConfig(new SurveyConfig { ConfigName = "Big", Rules = rules });
        configManager.ApplyRulesToSchedule(schedule);

        Assert.AreEqual(9, schedule.Events.Length);
    }

    [Test]
    public void ApplyRulesToSchedule_NoActiveConfig_NoError()
    {
        int originalCount = schedule.Events.Length;

        Assert.DoesNotThrow(() => configManager.ApplyRulesToSchedule(schedule));

        Assert.AreEqual(originalCount, schedule.Events.Length);
    }

    [Test]
    public void ApplyRulesToSchedule_EmptyRules_NoError()
    {
        configManager.SetActiveConfig(new SurveyConfig { ConfigName = "Empty", Rules = Array.Empty<SavedEventRule>() });
        int originalCount = schedule.Events.Length;

        Assert.DoesNotThrow(() => configManager.ApplyRulesToSchedule(schedule));

        Assert.AreEqual(originalCount, schedule.Events.Length);
    }

    [Test]
    public void GetTemplateNames_ReturnsAllFour()
    {
        var names = configManager.GetTemplateNames();

        Assert.AreEqual(4, names.Length);
    }

    [Test]
    public void LoadTemplate_ValidName_ReturnsConfig()
    {
        var config = configManager.LoadTemplate("V1 Parity");

        Assert.IsNotNull(config);
        Assert.AreEqual("V1 Parity", config.ConfigName);
    }

    [Test]
    public void LoadTemplate_InvalidName_ReturnsNull()
    {
        var config = configManager.LoadTemplate("Nonexistent");

        Assert.IsNull(config);
    }
}
