using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Covers the on/off flip of car name labels exposed by <see cref="CarLabelSpawner.ToggleLabels"/>
/// and <see cref="CarLabelSpawner.SetLabelsVisible"/>. Labels default to visible; a toggle flips the
/// flag. Pinned so a refactor can't silently change the default or the toggle direction.
/// </summary>
[TestFixture]
public class CarLabelSpawnerToggleTests
{
    private CarLabelSpawner spawner;

    [SetUp]
    public void SetUp()
    {
        var go = new GameObject("CarLabelSpawner_Test");
        spawner = go.AddComponent<CarLabelSpawner>();
    }

    [TearDown]
    public void TearDown()
    {
        if (spawner != null) Object.DestroyImmediate(spawner.gameObject);
    }

    [Test]
    public void LabelsVisible_DefaultsToTrue()
    {
        Assert.IsTrue(spawner.LabelsVisible);
    }

    [Test]
    public void ToggleLabels_FromDefault_HidesLabels()
    {
        spawner.ToggleLabels();
        Assert.IsFalse(spawner.LabelsVisible);
    }

    [Test]
    public void ToggleLabels_Twice_ReturnsToVisible()
    {
        spawner.ToggleLabels();
        spawner.ToggleLabels();
        Assert.IsTrue(spawner.LabelsVisible);
    }

    [Test]
    public void SetLabelsVisible_False_HidesLabels()
    {
        spawner.SetLabelsVisible(false);
        Assert.IsFalse(spawner.LabelsVisible);
    }
}
