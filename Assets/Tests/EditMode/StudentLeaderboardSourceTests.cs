using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Regression tests for the student-view leaderboard fix.
///
/// Bug: on a student (network client), cars are spawned visual-only and are never
/// registered with ScoreManager, so the local ranking is always empty. The panel read
/// ScoreManager directly, so the student leaderboard rendered nothing while the host's
/// worked fine. The fix routes student clients to the authoritative leaderboard the host
/// broadcasts (NetworkSync.LatestLeaderboard).
///
/// These tests drive the network render path directly with synthetic entries (no live
/// socket) and verify the NetworkSync reference is auto-wired, protecting the previously
/// dead code path that now backs the student view.
/// </summary>
[TestFixture]
public class StudentLeaderboardSourceTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    private GameObject NewObject(string name, bool active = true)
    {
        var obj = new GameObject(name);
        obj.SetActive(active);
        spawned.Add(obj);
        return obj;
    }

    // Builds a started panel whose row pool is Text-bearing rows, so assertions can read
    // each row's text/visibility. ScoreManager/NetworkSync are left for the caller to set.
    private LeaderboardPanel NewStartedPanel(int maxRows)
    {
        var panelObj = NewObject("LeaderboardPanel");
        var panel = panelObj.AddComponent<LeaderboardPanel>();

        var rowPrefab = NewObject("Row");
        rowPrefab.AddComponent<Text>();
        panel.RowPrefab = rowPrefab;
        panel.ContentParent = panelObj.transform;
        panel.MaxRows = maxRows;

        // Drive the private lifecycle hook via reflection, not Component.SendMessage: in Unity 6.3
        // EditMode, SendMessage("Start") trips a native 'ShouldRunBehaviour()' assertion that the
        // Test Framework reports as an unhandled log failure. Reflection runs Start() directly.
        InvokePrivate(panel, "Start");
        return panel;
    }

    private static void InvokePrivate(object target, string method, params object[] args)
    {
        var mi = target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, $"Expected private method '{method}' to exist.");
        try { mi.Invoke(target, args); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }

    private List<GameObject> RowPool(LeaderboardPanel panel)
    {
        var fi = typeof(LeaderboardPanel).GetField("rowPool",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (List<GameObject>)fi.GetValue(panel);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
                Object.DestroyImmediate(spawned[i]);
        }
        spawned.Clear();
    }

    [Test]
    public void LeaderboardPanel_Start_ResolvesNetworkSync_WhenUnset()
    {
        // Arrange
        var syncObj = NewObject("NetworkSync");
        var sync = syncObj.AddComponent<NetworkSync>();

        var panelObj = NewObject("LeaderboardPanel");
        var panel = panelObj.AddComponent<LeaderboardPanel>();
        var rowPrefab = NewObject("Row");
        rowPrefab.AddComponent<Text>();
        panel.RowPrefab = rowPrefab;
        panel.ContentParent = panelObj.transform;
        panel.MaxRows = 1;
        panel.NetworkSync = null;

        // Act
        // Drive the private lifecycle hook via reflection, not Component.SendMessage: in Unity 6.3
        // EditMode, SendMessage("Start") trips a native 'ShouldRunBehaviour()' assertion that the
        // Test Framework reports as an unhandled log failure. Reflection runs Start() directly.
        InvokePrivate(panel, "Start");

        // Assert
        Assert.IsNotNull(panel.NetworkSync, "NetworkSync should be auto-resolved when left unset.");
        Assert.AreSame(sync, panel.NetworkSync, "Should resolve the NetworkSync present in the scene.");
    }

    [Test]
    public void RenderNetworkEntries_PopulatesRows_FromBroadcastRankings()
    {
        // Arrange
        var panel = NewStartedPanel(maxRows: 5);
        var rankings = new[]
        {
            new LeaderboardEntry { rank = 1, name = "Red Team",  lap = 3, cp = 12 },
            new LeaderboardEntry { rank = 2, name = "Blue Team", lap = 2, cp = 9 },
        };

        // Act
        InvokePrivate(panel, "RenderNetworkEntries", new object[] { rankings });

        // Assert
        var rows = RowPool(panel);
        Assert.IsTrue(rows[0].activeSelf, "Row 0 should be shown for the leader.");
        Assert.IsTrue(rows[1].activeSelf, "Row 1 should be shown for the runner-up.");
        Assert.AreEqual("1. [3] Red Team", rows[0].GetComponent<Text>().text);
        Assert.AreEqual("2. [2] Blue Team", rows[1].GetComponent<Text>().text);
        for (int i = 2; i < rows.Count; i++)
            Assert.IsFalse(rows[i].activeSelf, $"Row {i} beyond the ranking count must be hidden.");
    }

    [Test]
    public void RenderNetworkEntries_NullRankings_HidesAllRows_AndDoesNotThrow()
    {
        // Arrange — a student that has joined but before the first leaderboard broadcast arrives.
        var panel = NewStartedPanel(maxRows: 3);

        // Act / Assert
        Assert.DoesNotThrow(() =>
            InvokePrivate(panel, "RenderNetworkEntries", new object[] { null }),
            "Null rankings (no broadcast yet) must not throw.");

        var rows = RowPool(panel);
        foreach (var row in rows)
            Assert.IsFalse(row.activeSelf, "With no rankings, every row must be hidden.");
    }

    [Test]
    public void RenderNetworkEntries_ClampsToMaxRows()
    {
        // Arrange
        var panel = NewStartedPanel(maxRows: 2);
        var rankings = new[]
        {
            new LeaderboardEntry { rank = 1, name = "A", lap = 1, cp = 3 },
            new LeaderboardEntry { rank = 2, name = "B", lap = 1, cp = 2 },
            new LeaderboardEntry { rank = 3, name = "C", lap = 1, cp = 1 },
        };

        // Act
        InvokePrivate(panel, "RenderNetworkEntries", new object[] { rankings });

        // Assert — the row pool is pre-sized to the largest display mode (Fullscreen), so its count
        // is not MaxRows. What clamps to MaxRows is how many rows are *shown*: in Normal mode only
        // MaxRows entries are displayed, so the 3rd broadcast entry ("C") must be hidden.
        var rows = RowPool(panel);
        int shown = rows.FindAll(r => r.activeSelf).Count;
        Assert.AreEqual(2, shown, "Only MaxRows entries are shown even when more are broadcast.");
        Assert.IsTrue(rows[0].activeSelf);
        Assert.IsTrue(rows[1].activeSelf);
        Assert.IsFalse(rows[2].activeSelf, "The 3rd entry, beyond MaxRows, must be clamped out.");
        Assert.AreEqual("1. [1] A", rows[0].GetComponent<Text>().text);
        Assert.AreEqual("2. [1] B", rows[1].GetComponent<Text>().text);
    }
}
