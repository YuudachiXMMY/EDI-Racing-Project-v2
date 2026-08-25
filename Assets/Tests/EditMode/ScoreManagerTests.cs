using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class ScoreManagerTests
{
    private GameObject managerObj;
    private ScoreManager scoreManager;
    private List<GameObject> carObjects;

    [SetUp]
    public void SetUp()
    {
        managerObj = new GameObject("ScoreManager");
        scoreManager = managerObj.AddComponent<ScoreManager>();
        carObjects = new List<GameObject>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in carObjects)
            UnityEngine.Object.DestroyImmediate(obj);
        UnityEngine.Object.DestroyImmediate(managerObj);
    }

    private CarIdentity CreateCar(string name, int checkpoints, float time, int lap = 0)
    {
        var obj = new GameObject(name);
        carObjects.Add(obj);
        var identity = obj.AddComponent<CarIdentity>();
        identity.Initialize(new CarData(name, Array.Empty<AttributeEntry>()));
        identity.TotalCheckpointsPassed = checkpoints;
        identity.CheckpointTime = time;
        identity.CurrentLap = lap;
        return identity;
    }

    [Test]
    public void GetRankedCars_OrdersByCheckpointsDescending()
    {
        var car1 = CreateCar("Slow", 5, 10f);
        var car2 = CreateCar("Fast", 10, 20f);
        var car3 = CreateCar("Mid", 7, 15f);

        scoreManager.RegisterCar(car1);
        scoreManager.RegisterCar(car2);
        scoreManager.RegisterCar(car3);

        var ranked = scoreManager.GetRankedCars();

        Assert.AreEqual("Fast", ranked[0].TeamName);
        Assert.AreEqual("Mid", ranked[1].TeamName);
        Assert.AreEqual("Slow", ranked[2].TeamName);
    }

    [Test]
    public void GetRankedCars_TiedCheckpoints_FurtherIntoSegmentWins()
    {
        // CheckpointTime resets to 0 at each checkpoint then counts up, so among
        // cars tied on checkpoint count the one with the LARGER CheckpointTime has
        // travelled further toward the next checkpoint and is physically ahead.
        // Arrange
        var carBehind = CreateCar("JustPassedCheckpoint", 10, 5f);
        var carAhead = CreateCar("NearNextCheckpoint", 10, 25f);

        scoreManager.RegisterCar(carBehind);
        scoreManager.RegisterCar(carAhead);

        // Act
        var ranked = scoreManager.GetRankedCars();

        // Assert
        Assert.AreEqual("NearNextCheckpoint", ranked[0].TeamName);
        Assert.AreEqual("JustPassedCheckpoint", ranked[1].TeamName);
    }

    [Test]
    public void GetRankedCars_LeaderJustPassedCheckpoint_StillRanksFirst()
    {
        // Regression: the actual race leader (most checkpoints passed) must rank #1
        // even immediately after crossing a checkpoint, when its CheckpointTime has
        // just reset to ~0 while a trailing car's timer is still large. Previously the
        // tiebreaker's direction was inverted, so this leader dropped below the trailer.
        // Arrange
        var leader = CreateCar("Leader", 11, 0.1f, lap: 2);   // one more checkpoint = ahead
        var trailer = CreateCar("Trailer", 10, 28f, lap: 2);  // fewer checkpoints, large timer

        scoreManager.RegisterCar(trailer);
        scoreManager.RegisterCar(leader);

        // Act
        var ranked = scoreManager.GetRankedCars();

        // Assert
        Assert.AreEqual("Leader", ranked[0].TeamName);
        Assert.AreEqual("Trailer", ranked[1].TeamName);
    }

    [Test]
    public void GetScoreboardText_ContainsTeamNames()
    {
        var car = CreateCar("Alpha", 5, 10f, 1);
        scoreManager.RegisterCar(car);

        string text = scoreManager.GetScoreboardText();

        Assert.IsTrue(text.Contains("Alpha"));
    }

    [Test]
    public void Clear_EmptiesCarList()
    {
        scoreManager.RegisterCar(CreateCar("A", 1, 1f));
        scoreManager.RegisterCar(CreateCar("B", 2, 2f));

        scoreManager.Clear();

        Assert.AreEqual(0, scoreManager.GetRankedCars().Count);
    }

    [Test]
    public void CollectResults_ProducesCorrectRanks()
    {
        scoreManager.RegisterCar(CreateCar("First", 10, 20f, 2));
        scoreManager.RegisterCar(CreateCar("Second", 5, 15f, 1));

        var results = scoreManager.CollectResults(new List<EventLogEntry>(), 60f);

        Assert.AreEqual(2, results.Rankings.Length);
        Assert.AreEqual(1, results.Rankings[0].Rank);
        Assert.AreEqual("First", results.Rankings[0].TeamName);
        Assert.AreEqual(2, results.Rankings[1].Rank);
        Assert.AreEqual("Second", results.Rankings[1].TeamName);
        Assert.AreEqual(60f, results.TotalRaceTime);
    }

    [Test]
    public void CollectResults_EmptyEventLog_ProducesEmptyArray()
    {
        scoreManager.RegisterCar(CreateCar("A", 1, 1f));

        var results = scoreManager.CollectResults(new List<EventLogEntry>(), 10f);

        Assert.IsNotNull(results.EventLog);
        Assert.AreEqual(0, results.EventLog.Length);
    }

    [Test]
    public void CollectResults_NullEventLog_ProducesEmptyArray()
    {
        scoreManager.RegisterCar(CreateCar("A", 1, 1f));

        var results = scoreManager.CollectResults(null, 10f);

        Assert.IsNotNull(results.EventLog);
        Assert.AreEqual(0, results.EventLog.Length);
    }

    [Test]
    public void CollectResults_PopulatesMillisecondTimeFields()
    {
        // Arrange: a car with two completed laps (best 12s, total 27s).
        var car = CreateCar("Racer", 8, 3f, lap: 2);
        car.BestLapTime = 12f;
        car.AccumulatedLapTime = 27f;
        car.CompletedLaps = 2;
        scoreManager.RegisterCar(car);

        // Act
        var results = scoreManager.CollectResults(new List<EventLogEntry>(), 90f);

        // Assert: ElapsedTime is the whole-race time; average = accumulated / laps.
        var r = results.Rankings[0];
        Assert.AreEqual(90f, r.ElapsedTime);
        Assert.AreEqual(12f, r.BestLapTime);
        Assert.AreEqual(13.5f, r.AverageLapTime, 0.0001f);
    }

    [Test]
    public void CollectResults_ZeroLaps_AverageLapTimeIsZero()
    {
        var car = CreateCar("NoLaps", 0, 1f, lap: 0); // CompletedLaps defaults to 0
        scoreManager.RegisterCar(car);

        var results = scoreManager.CollectResults(new List<EventLogEntry>(), 5f);

        Assert.AreEqual(0f, results.Rankings[0].AverageLapTime);
        Assert.AreEqual(0f, results.Rankings[0].BestLapTime);
    }

    [Test]
    public void CarIdentity_RecordLap_TracksBestAndAccumulated()
    {
        // Arrange: race started at t=0 (LastLapStartTime seeded to 0 by Initialize).
        var obj = new GameObject("LapCar");
        carObjects.Add(obj);
        var car = obj.AddComponent<CarIdentity>();
        car.Initialize(new CarData("LapCar", Array.Empty<AttributeEntry>()));

        // Act: lap 1 ends at t=10 (10s), lap 2 ends at t=25 (15s).
        car.RecordLap(10f);
        car.RecordLap(25f);

        // Assert
        Assert.AreEqual(2, car.CompletedLaps);
        Assert.AreEqual(10f, car.BestLapTime, 0.0001f);       // faster of 10s / 15s
        Assert.AreEqual(25f, car.AccumulatedLapTime, 0.0001f); // 10 + 15
    }

    [Test]
    public void CollectResults_PreservesCarAttributes()
    {
        var obj = new GameObject("WithAttrs");
        carObjects.Add(obj);
        var identity = obj.AddComponent<CarIdentity>();
        identity.Initialize(new CarData("Team", new AttributeEntry[]
        {
            new AttributeEntry { Key = "color", Value = "blue" }
        }));
        identity.TotalCheckpointsPassed = 1;

        scoreManager.RegisterCar(identity);

        var results = scoreManager.CollectResults(new List<EventLogEntry>(), 5f);

        Assert.AreEqual(1, results.Rankings[0].Attributes.Length);
        Assert.AreEqual("color", results.Rankings[0].Attributes[0].Key);
    }
}
