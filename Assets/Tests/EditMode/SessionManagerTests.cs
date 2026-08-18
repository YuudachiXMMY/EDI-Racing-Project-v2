using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SessionManagerTests
{
    private GameObject obj;
    private SessionManager manager;
    private string tempDir;

    [SetUp]
    public void SetUp()
    {
        obj = new GameObject("SessionManager");
        manager = obj.AddComponent<SessionManager>();
        tempDir = Path.Combine(Path.GetTempPath(), "EDIRacingTest_" + Guid.NewGuid().ToString("N"));
        manager.SaveFolder = tempDir;
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(obj);
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }

    [Test]
    public void SaveSession_CreatesJsonFile()
    {
        var session = new SessionData { SessionName = "Test" };

        string path = manager.SaveSession(session);

        Assert.IsTrue(File.Exists(path));
        Assert.IsTrue(path.EndsWith(".json"));
    }

    [Test]
    public void SaveSession_JsonContainsSessionName()
    {
        var session = new SessionData { SessionName = "MyRace" };

        string path = manager.SaveSession(session);
        string content = File.ReadAllText(path);

        Assert.IsTrue(content.Contains("MyRace"));
    }

    [Test]
    public void LoadSession_ReturnsNull_WhenPathEmpty()
    {
        var result = manager.LoadSession("");
        Assert.IsNull(result);
    }

    [Test]
    public void LoadSession_ReturnsNull_WhenFileNotFound()
    {
        var result = manager.LoadSession("/nonexistent/path.json");
        Assert.IsNull(result);
    }

    [Test]
    public void SaveAndLoad_RoundTrip_PreservesSessionName()
    {
        var session = new SessionData { SessionName = "RoundTrip" };

        string path = manager.SaveSession(session);
        var loaded = manager.LoadSession(path);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("RoundTrip", loaded.SessionName);
    }

    [Test]
    public void SaveAndLoad_RoundTrip_PreservesCars()
    {
        var session = new SessionData
        {
            SessionName = "WithCars",
            Cars = new[]
            {
                new CarData("Alpha", new[] { new AttributeEntry { Key = "color", Value = "red" } }),
                new CarData("Beta", Array.Empty<AttributeEntry>())
            }
        };

        string path = manager.SaveSession(session);
        var loaded = manager.LoadSession(path);

        Assert.AreEqual(2, loaded.Cars.Length);
        Assert.AreEqual("Alpha", loaded.Cars[0].TeamName);
    }

    [Test]
    public void FindLatestSession_ReturnsNull_WhenNoDirectory()
    {
        var result = manager.FindLatestSession();
        Assert.IsNull(result);
    }

    [Test]
    public void FindLatestSession_ReturnsNull_WhenDirectoryEmpty()
    {
        Directory.CreateDirectory(tempDir);
        var result = manager.FindLatestSession();
        Assert.IsNull(result);
    }

    [Test]
    public void FindLatestSession_ReturnsMostRecent()
    {
        manager.SaveSession(new SessionData { SessionName = "First" });
        System.Threading.Thread.Sleep(50);
        string secondPath = manager.SaveSession(new SessionData { SessionName = "Second" });

        string latest = manager.FindLatestSession();

        Assert.AreEqual(secondPath, latest);
    }

    [Test]
    public void GetSavedSessionPaths_ReturnsEmpty_WhenNoDirectory()
    {
        var paths = manager.GetSavedSessionPaths();
        Assert.IsNotNull(paths);
        Assert.AreEqual(0, paths.Length);
    }

    [Test]
    public void GetSavedSessionPaths_ReturnsSavedFiles()
    {
        manager.SaveSession(new SessionData { SessionName = "A" });
        manager.SaveSession(new SessionData { SessionName = "B" });

        var paths = manager.GetSavedSessionPaths();

        Assert.AreEqual(2, paths.Length);
    }

    [Test]
    public void ExportResults_CreatesFile()
    {
        var results = new RaceResults
        {
            Rankings = new[]
            {
                new CarResult { Rank = 1, TeamName = "Winner", LapsCompleted = 3 }
            },
            TotalRaceTime = 60f
        };

        string path = manager.ExportResults(results);

        Assert.IsNotNull(path);
        // In Editor mode, ExportResults writes to file (not WebGL download)
        Assert.IsTrue(path.Contains("results_"));
    }
}
