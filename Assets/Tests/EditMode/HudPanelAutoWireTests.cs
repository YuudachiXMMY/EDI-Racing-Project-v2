using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression tests for the HUD panel data-source auto-wire fix (commit a103209).
/// The panels are made visible by RaceUI wiring, but read their content from a
/// separate manager reference (ScoreManager / EventManager). When a scene shipped
/// with that reference unset, the panel appeared but stayed permanently empty.
/// The fix adds a defensive auto-resolve in each panel's Start(); these tests drive
/// Start() directly (private, so via SendMessage — EditMode does not auto-run it) and
/// assert the manager gets resolved from the scene.
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
    public void EventPanel_Start_ResolvesEventManager_WhenUnset()
    {
        var mgrObj = NewObject("EventManager");
        var eventManager = mgrObj.AddComponent<EventManager>();

        var panelObj = NewObject("EventPanel");
        var panel = panelObj.AddComponent<EventPanel>();
        panel.EventManager = null;

        // Schedule is null, so BuildEventRows early-returns after the auto-wire — safe.
        panel.SendMessage("Start");

        Assert.IsNotNull(panel.EventManager, "EventManager should be auto-resolved when left unset.");
        Assert.AreSame(eventManager, panel.EventManager, "Should resolve the EventManager present in the scene.");
    }

    [Test]
    public void EventPanel_Start_ResolvesEventManager_OnInactiveObject()
    {
        // FindObjectsInactive.Include is what lets the panel find a manager that
        // lives on a disabled GameObject — the exact reason the fix passes that flag.
        var mgrObj = NewObject("EventManager (inactive)", active: false);
        var eventManager = mgrObj.AddComponent<EventManager>();

        var panelObj = NewObject("EventPanel");
        var panel = panelObj.AddComponent<EventPanel>();
        panel.EventManager = null;

        panel.SendMessage("Start");

        Assert.IsNotNull(panel.EventManager, "Manager on an inactive object should still be resolved.");
        Assert.AreSame(eventManager, panel.EventManager);
    }

    [Test]
    public void EventPanel_Start_NoManagerInScene_LeavesNull_AndDoesNotThrow()
    {
        var panelObj = NewObject("EventPanel");
        var panel = panelObj.AddComponent<EventPanel>();
        panel.EventManager = null;

        Assert.DoesNotThrow(() => panel.SendMessage("Start"),
            "With no EventManager in the scene, Start must not throw (BuildEventRows early-returns on null).");
        Assert.IsNull(panel.EventManager, "No manager in scene → reference stays null, panel stays safely empty.");
    }
}
