using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression tests for the HUD panel data-source auto-wire fix (commit a103209).
/// The panels are made visible by RaceUI wiring, but read their content from a
/// separate manager reference (ScoreManager / EventManager). When a scene shipped
/// with that reference unset, the panel appeared but stayed permanently empty.
/// The fix adds a defensive auto-resolve in each panel's lifecycle (LeaderboardPanel /
/// RaceControlPanel in Start(); EventPanel in Awake(), because its OnEnable subscribes to the
/// manager and runs before Start). These tests drive the relevant hook directly (private, so via
/// SendMessage — EditMode does not auto-run it) and assert the manager gets resolved from the scene.
/// </summary>
[TestFixture]
public class HudPanelAutoWireTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    private GameObject NewObject(string name, bool active = true)
    {
        var obj = new GameObject(name);
        obj.SetActive(active);
        spawned.Add(obj);
        return obj;
    }

    [TearDown]
    public void TearDown()
    {
        // Destroy panels before managers so panel OnDisable never touches a
        // freed manager; Unity's overloaded null-check guards it either way.
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
                Object.DestroyImmediate(spawned[i]);
        }
        spawned.Clear();
    }

    [Test]
    public void LeaderboardPanel_Start_ResolvesScoreManager_WhenUnset()
    {
        var scoreObj = NewObject("ScoreManager");
        var scoreManager = scoreObj.AddComponent<ScoreManager>();

        var panelObj = NewObject("LeaderboardPanel");
        var panel = panelObj.AddComponent<LeaderboardPanel>();
        // Give Start()'s row-pool loop a prefab + parent so it does not throw; the
        // rows become children of the panel and are cleaned up with it.
        panel.RowPrefab = NewObject("Row");
        panel.ContentParent = panelObj.transform;
        panel.MaxRows = 1;
        panel.ScoreManager = null;

        panel.SendMessage("Start");

        Assert.IsNotNull(panel.ScoreManager, "ScoreManager should be auto-resolved when left unset.");
        Assert.AreSame(scoreManager, panel.ScoreManager, "Should resolve the ScoreManager present in the scene.");
    }

    [Test]
    public void EventPanel_Awake_ResolvesEventManager_WhenUnset()
    {
        var mgrObj = NewObject("EventManager");
        var eventManager = mgrObj.AddComponent<EventManager>();

        var panelObj = NewObject("EventPanel");
        var panel = panelObj.AddComponent<EventPanel>();
        panel.EventManager = null;

        // The auto-wire lives in Awake (not Start): OnEnable subscribes to OnEventTriggered and
        // runs BEFORE Start, so wiring in Start would make the first OnEnable miss the manager and
        // never subscribe. Driving Awake is what the runtime lifecycle does before OnEnable.
        panel.SendMessage("Awake");

        Assert.IsNotNull(panel.EventManager, "EventManager should be auto-resolved when left unset.");
        Assert.AreSame(eventManager, panel.EventManager, "Should resolve the EventManager present in the scene.");
    }

    [Test]
    public void EventPanel_Awake_ResolvesEventManager_OnInactiveObject()
    {
        // FindObjectsInactive.Include is what lets the panel find a manager that
        // lives on a disabled GameObject — the exact reason the fix passes that flag.
        var mgrObj = NewObject("EventManager (inactive)", active: false);
        var eventManager = mgrObj.AddComponent<EventManager>();

        var panelObj = NewObject("EventPanel");
        var panel = panelObj.AddComponent<EventPanel>();
        panel.EventManager = null;

        panel.SendMessage("Awake");

        Assert.IsNotNull(panel.EventManager, "Manager on an inactive object should still be resolved.");
        Assert.AreSame(eventManager, panel.EventManager);
    }

    [Test]
    public void EventPanel_AwakeThenStart_NoManagerInScene_LeavesNull_AndDoesNotThrow()
    {
        var panelObj = NewObject("EventPanel");
        var panel = panelObj.AddComponent<EventPanel>();
        panel.EventManager = null;

        // Drive the real lifecycle order: Awake (auto-wire) then Start (BuildEventRows). With no
        // manager in the scene neither must throw — the wire leaves null and BuildEventRows
        // early-returns on null.
        Assert.DoesNotThrow(() =>
        {
            panel.SendMessage("Awake");
            panel.SendMessage("Start");
        }, "With no EventManager in the scene, Awake+Start must not throw.");
        Assert.IsNull(panel.EventManager, "No manager in scene → reference stays null, panel stays safely empty.");
    }

    [Test]
    public void RaceControlPanel_Start_ResolvesRaceManager_WhenUnset()
    {
        // Regression: the scene shipped RaceControlPanel.RaceManager as {fileID: 0}, so
        // TogglePause/SaveSession/ExportResults all early-returned on the null RaceManager
        // and the Pause button silently did nothing. The fix auto-resolves it in Start().
        var rmObj = NewObject("RaceManager");
        var raceManager = rmObj.AddComponent<RaceManager>();

        var panelObj = NewObject("RaceControlPanel");
        var panel = panelObj.AddComponent<RaceControlPanel>();
        panel.RaceManager = null;

        panel.SendMessage("Start");

        Assert.IsNotNull(panel.RaceManager, "RaceManager should be auto-resolved when left unset.");
        Assert.AreSame(raceManager, panel.RaceManager, "Should resolve the RaceManager present in the scene.");
    }

    [Test]
    public void RaceControlPanel_Start_ResolvesRaceManager_OnInactiveObject()
    {
        var rmObj = NewObject("RaceManager (inactive)", active: false);
        var raceManager = rmObj.AddComponent<RaceManager>();

        var panelObj = NewObject("RaceControlPanel");
        var panel = panelObj.AddComponent<RaceControlPanel>();
        panel.RaceManager = null;

        panel.SendMessage("Start");

        Assert.IsNotNull(panel.RaceManager, "RaceManager on an inactive object should still be resolved.");
        Assert.AreSame(raceManager, panel.RaceManager);
    }
}
