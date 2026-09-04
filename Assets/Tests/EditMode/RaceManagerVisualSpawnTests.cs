using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression tests for the student (visual-only) race-start path. The student spectator's
/// auto broadcast camera (SpectatorCamera AutoTopCars / AutoAllCams) ranks cars via
/// ScoreManager.GetRankedCars(); if RaceManager.LoadAndStartRaceVisualOnly does not register the
/// spawned visual cars, the student's ScoreManager stays empty, ranking returns [], and the auto
/// camera never acquires a target — so the "Auto Cam" button appears dead. These tests pin that the
/// visual-spawn path registers cars, ranks them by the network-synced TotalCheckpointsPassed, and is
/// safe to call twice (reconnect / re-sent race_start).
/// </summary>
[TestFixture]
public class RaceManagerVisualSpawnTests
{
    private GameObject raceManagerObj;
    private RaceManager raceManager;
    private CarSpawner carSpawner;
    private ScoreManager scoreManager;
    private RaceConfig config;
    private GameObject carPrefab;
    private readonly List<GameObject> spawnedForCleanup = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        // RaceConfig is a ScriptableObject; CreateInstance keeps the CarScale / Trail defaults.
        config = ScriptableObject.CreateInstance<RaceConfig>();

        // A stub car prefab carrying CarIdentity, mirroring the production visual-car prefabs so
        // SpawnVisualCars' GetComponent<CarIdentity>() path is exercised realistically.
        carPrefab = new GameObject("CarPrefabStub");
        carPrefab.AddComponent<CarIdentity>();

        raceManagerObj = new GameObject("RaceManager");
        raceManager = raceManagerObj.AddComponent<RaceManager>();
        carSpawner = raceManagerObj.AddComponent<CarSpawner>();
        scoreManager = raceManagerObj.AddComponent<ScoreManager>();

        carSpawner.CarPrefabs = new[] { carPrefab };
        carSpawner.Config = config;
        carSpawner.SpawnPoint = null; // falls back to Vector3.zero in SpawnVisualCars

        raceManager.CarSpawner = carSpawner;
        raceManager.ScoreManager = scoreManager;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in spawnedForCleanup)
            if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        spawnedForCleanup.Clear();

        if (carPrefab != null) UnityEngine.Object.DestroyImmediate(carPrefab);
        if (raceManagerObj != null) UnityEngine.Object.DestroyImmediate(raceManagerObj);
        if (config != null) UnityEngine.Object.DestroyImmediate(config);
    }

    private static List<CarData> MakeCars(params string[] names)
    {
        var list = new List<CarData>();
        foreach (var n in names)
            list.Add(new CarData(n, Array.Empty<AttributeEntry>()));
        return list;
    }

    // Track the cars a call spawned so TearDown can destroy them.
    private void TrackSpawned()
    {
        if (raceManager.SpawnedCars != null)
            spawnedForCleanup.AddRange(raceManager.SpawnedCars);
    }

    [Test]
    public void LoadAndStartRaceVisualOnly_RegistersCarsInScoreManager()
    {
        // Arrange
        var cars = MakeCars("Alpha", "Bravo", "Charlie");

        // Act
        raceManager.LoadAndStartRaceVisualOnly(cars);
        TrackSpawned();

        // Assert: every spawned visual car is registered so ranking is non-empty (the fix).
        Assert.AreEqual(cars.Count, scoreManager.GetRankedCars().Count);
    }

    [Test]
    public void LoadAndStartRaceVisualOnly_RankingReflectsSyncedCheckpoints()
    {
        // Arrange
        raceManager.LoadAndStartRaceVisualOnly(MakeCars("Alpha", "Bravo", "Charlie"));
        TrackSpawned();

        // Simulate NetworkSync.HandleStateUpdate writing progress onto the spawned CarIdentity objects.
        foreach (var car in raceManager.SpawnedCars)
        {
            var id = car.GetComponent<CarIdentity>();
            id.TotalCheckpointsPassed = id.TeamName == "Bravo" ? 9
                                       : id.TeamName == "Charlie" ? 5
                                       : 2;
        }

        // Act
        var ranked = scoreManager.GetRankedCars();

        // Assert: the auto camera would trace the true leader (most checkpoints).
        Assert.AreEqual("Bravo", ranked[0].TeamName);
        Assert.AreEqual("Charlie", ranked[1].TeamName);
        Assert.AreEqual("Alpha", ranked[2].TeamName);
    }

    [Test]
    public void LoadAndStartRaceVisualOnly_CalledTwice_DoesNotDuplicateRoster()
    {
        // Arrange + Act: a reconnect / re-sent race_start calls the visual path a second time.
        raceManager.LoadAndStartRaceVisualOnly(MakeCars("Alpha", "Bravo"));
        TrackSpawned();
        raceManager.LoadAndStartRaceVisualOnly(MakeCars("Alpha", "Bravo"));
        TrackSpawned();

        // Assert: Clear() before registering keeps the roster at the current field size, not doubled.
        Assert.AreEqual(2, scoreManager.GetRankedCars().Count);
    }

    [Test]
    public void LoadAndStartRaceVisualOnly_NullScoreManager_DoesNotThrow()
    {
        // Arrange: a stripped scene without a wired ScoreManager must not crash the visual spawn.
        raceManager.ScoreManager = null;

        // Act + Assert
        Assert.DoesNotThrow(() => raceManager.LoadAndStartRaceVisualOnly(MakeCars("Alpha", "Bravo")));
        TrackSpawned();
        Assert.AreEqual(2, raceManager.SpawnedCars.Count);
    }
}
