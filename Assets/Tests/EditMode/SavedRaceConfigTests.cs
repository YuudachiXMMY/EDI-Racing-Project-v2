using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SavedRaceConfigTests
{
    private RaceConfig sourceConfig;
    private RaceConfig targetConfig;

    [SetUp]
    public void SetUp()
    {
        sourceConfig = ScriptableObject.CreateInstance<RaceConfig>();
        targetConfig = ScriptableObject.CreateInstance<RaceConfig>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(sourceConfig);
        Object.DestroyImmediate(targetConfig);
    }

    [Test]
    public void FromScriptableObject_CapturesAllFields()
    {
        sourceConfig.DefaultSpeed = 55f;
        sourceConfig.AngularSpeed = 900f;
        sourceConfig.Acceleration = 70f;
        sourceConfig.TotalLaps = 5;
        sourceConfig.CarScale = 3f;

        var saved = SavedRaceConfig.FromScriptableObject(sourceConfig);

        Assert.AreEqual(55f, saved.DefaultSpeed);
        Assert.AreEqual(900f, saved.AngularSpeed);
        Assert.AreEqual(70f, saved.Acceleration);
        Assert.AreEqual(5, saved.TotalLaps);
        Assert.AreEqual(3f, saved.CarScale);
    }

    [Test]
    public void ApplyTo_RestoresAllFields()
    {
        var saved = new SavedRaceConfig
        {
            DefaultSpeed = 100f,
            AngularSpeed = 500f,
            Acceleration = 30f,
            TotalLaps = 10,
            CarScale = 1.5f
        };

        saved.ApplyTo(targetConfig);

        Assert.AreEqual(100f, targetConfig.DefaultSpeed);
        Assert.AreEqual(500f, targetConfig.AngularSpeed);
        Assert.AreEqual(30f, targetConfig.Acceleration);
        Assert.AreEqual(10, targetConfig.TotalLaps);
        Assert.AreEqual(1.5f, targetConfig.CarScale);
    }

    [Test]
    public void RoundTrip_AllValuesMatch()
    {
        sourceConfig.DefaultSpeed = 77f;
        sourceConfig.AngularSpeed = 1200f;
        sourceConfig.Acceleration = 85f;
        sourceConfig.TotalLaps = 7;
        sourceConfig.CarScale = 2.2f;

        var saved = SavedRaceConfig.FromScriptableObject(sourceConfig);
        saved.ApplyTo(targetConfig);

        Assert.AreEqual(sourceConfig.DefaultSpeed, targetConfig.DefaultSpeed);
        Assert.AreEqual(sourceConfig.AngularSpeed, targetConfig.AngularSpeed);
        Assert.AreEqual(sourceConfig.Acceleration, targetConfig.Acceleration);
        Assert.AreEqual(sourceConfig.TotalLaps, targetConfig.TotalLaps);
        Assert.AreEqual(sourceConfig.CarScale, targetConfig.CarScale);
    }
}
