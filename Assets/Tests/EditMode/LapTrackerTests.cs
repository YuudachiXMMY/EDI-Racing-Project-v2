using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class LapTrackerTests
{
    private GameObject trackerObj;
    private LapTracker tracker;
    private GameObject carObj;
    private CarIdentity car;

    [SetUp]
    public void SetUp()
    {
        trackerObj = new GameObject("LapTracker");
        tracker = trackerObj.AddComponent<LapTracker>();

        // Set private totalCheckpoints via reflection (Start() uses FindObjectsByType which
        // won't find anything in EditMode)
        var field = typeof(LapTracker).GetField("totalCheckpoints", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(tracker, 4); // 4 checkpoints per lap

        carObj = new GameObject("TestCar");
        car = carObj.AddComponent<CarIdentity>();
        car.Initialize(new CarData("Alpha", Array.Empty<AttributeEntry>()));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(carObj);
        Object.DestroyImmediate(trackerObj);
    }

    [Test]
    public void OnCarPassedCheckpoint_CorrectIndex_IncrementsProgress()
    {
        tracker.OnCarPassedCheckpoint(car, 0);

        Assert.AreEqual(1, car.TotalCheckpointsPassed);
        Assert.AreEqual(1, car.CurrentCheckpointIndex);
    }

    [Test]
    public void OnCarPassedCheckpoint_WrongIndex_Ignored()
    {
        // Car expects checkpoint 0, but we pass checkpoint 2
        tracker.OnCarPassedCheckpoint(car, 2);

        Assert.AreEqual(0, car.TotalCheckpointsPassed);
        Assert.AreEqual(0, car.CurrentCheckpointIndex);
    }

    [Test]
    public void OnCarPassedCheckpoint_SequentialCheckpoints_AllCounted()
    {
        tracker.OnCarPassedCheckpoint(car, 0);
        tracker.OnCarPassedCheckpoint(car, 1);
        tracker.OnCarPassedCheckpoint(car, 2);

        Assert.AreEqual(3, car.TotalCheckpointsPassed);
        Assert.AreEqual(3, car.CurrentCheckpointIndex);
    }

    [Test]
    public void OnCarPassedCheckpoint_CompleteLap_IncrementsLap()
    {
        // Pass all 4 checkpoints
        for (int i = 0; i < 4; i++)
            tracker.OnCarPassedCheckpoint(car, i);

        Assert.AreEqual(1, car.CurrentLap);
        Assert.AreEqual(4, car.TotalCheckpointsPassed);
    }

    [Test]
    public void OnCarPassedCheckpoint_MultipleLaps_WrapsCorrectly()
    {
        // Complete 2 full laps (8 checkpoints total, 4 per lap)
        for (int lap = 0; lap < 2; lap++)
            for (int cp = 0; cp < 4; cp++)
                tracker.OnCarPassedCheckpoint(car, cp);

        Assert.AreEqual(2, car.CurrentLap);
        Assert.AreEqual(8, car.TotalCheckpointsPassed);
    }

    [Test]
    public void OnLapCompleted_EventFires_OnLapBoundary()
    {
        int lapCompletedCount = 0;
        CarIdentity completedCar = null;
        tracker.OnLapCompleted += c => { lapCompletedCount++; completedCar = c; };

        // Pass checkpoints 0-3 (one full lap)
        for (int i = 0; i < 4; i++)
            tracker.OnCarPassedCheckpoint(car, i);

        Assert.AreEqual(1, lapCompletedCount);
        Assert.AreEqual(car, completedCar);
    }

    [Test]
    public void OnLapCompleted_DoesNotFire_MidLap()
    {
        int lapCompletedCount = 0;
        tracker.OnLapCompleted += c => lapCompletedCount++;

        // Pass only 3 of 4 checkpoints
        for (int i = 0; i < 3; i++)
            tracker.OnCarPassedCheckpoint(car, i);

        Assert.AreEqual(0, lapCompletedCount);
    }

    [Test]
    public void OnCheckpointPassed_EventFires_OnValidCheckpoint()
    {
        int passedCount = 0;
        tracker.OnCheckpointPassed += (c, idx) => passedCount++;

        tracker.OnCarPassedCheckpoint(car, 0);
        tracker.OnCarPassedCheckpoint(car, 1);

        Assert.AreEqual(2, passedCount);
    }

    [Test]
    public void OnCheckpointPassed_DoesNotFire_OnInvalidCheckpoint()
    {
        int passedCount = 0;
        tracker.OnCheckpointPassed += (c, idx) => passedCount++;

        tracker.OnCarPassedCheckpoint(car, 3); // wrong, expects 0

        Assert.AreEqual(0, passedCount);
    }

    [Test]
    public void CheckpointTime_ResetsOnValidCheckpoint()
    {
        car.CheckpointTime = 15f;

        tracker.OnCarPassedCheckpoint(car, 0);

        Assert.AreEqual(0f, car.CheckpointTime);
    }
}
