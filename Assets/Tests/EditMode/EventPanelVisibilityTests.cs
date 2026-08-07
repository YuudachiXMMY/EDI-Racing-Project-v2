using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Covers the rule that a full-screen leaderboard hides the professor's EventPanel and that the
/// panel comes back once the leaderboard shrinks again. Two layers are pinned:
///   1. <see cref="RaceUI.ShouldShowEventPanel"/> — the pure visibility decision.
///   2. <see cref="LeaderboardPanel.SetDisplayMode"/> — the OnFullscreenChanged notification that
///      drives that decision, asserted true only when the new mode is Fullscreen.
/// </summary>
[TestFixture]
public class EventPanelVisibilityTests
{
    // --- Pure decision --------------------------------------------------------

    [Test]
    public void ShouldShowEventPanel_Racing_NotFullscreen_Professor_ReturnsTrue()
    {
        Assert.IsTrue(RaceUI.ShouldShowEventPanel(isProfessor: true, isRacing: true, leaderboardFullscreen: false));
    }

    [Test]
    public void ShouldShowEventPanel_Racing_Fullscreen_Professor_ReturnsFalse()
    {
        // The whole point: full-screen leaderboard covers the view, so the EventPanel is hidden.
        Assert.IsFalse(RaceUI.ShouldShowEventPanel(isProfessor: true, isRacing: true, leaderboardFullscreen: true));
    }

    [Test]
    public void ShouldShowEventPanel_ToggleFullscreenOff_RestoresPanel()
    {
        // Enter fullscreen → hidden; leave fullscreen → shown again (same racing/professor state).
        Assert.IsFalse(RaceUI.ShouldShowEventPanel(true, true, leaderboardFullscreen: true),
            "Entering fullscreen must hide the EventPanel.");
        Assert.IsTrue(RaceUI.ShouldShowEventPanel(true, true, leaderboardFullscreen: false),
            "Toggling fullscreen off must show the EventPanel back.");
    }

    [Test]
    public void ShouldShowEventPanel_NotRacing_ReturnsFalse_EvenWhenNotFullscreen()
    {
        Assert.IsFalse(RaceUI.ShouldShowEventPanel(isProfessor: true, isRacing: false, leaderboardFullscreen: false));
    }

    [Test]
    public void ShouldShowEventPanel_Student_ReturnsFalse()
    {
        Assert.IsFalse(RaceUI.ShouldShowEventPanel(isProfessor: false, isRacing: true, leaderboardFullscreen: false));
    }

    // --- Notification from the leaderboard ------------------------------------

    [Test]
    public void SetDisplayMode_Fullscreen_RaisesOnFullscreenChanged_True()
    {
        var panelObj = new GameObject("LeaderboardPanel");
        try
        {
            var panel = panelObj.AddComponent<LeaderboardPanel>();
            bool? lastFullscreen = null;
            panel.OnFullscreenChanged += v => lastFullscreen = v;

            // SetDisplayMode's layout/refresh helpers all null-guard, so no Start() is needed.
            panel.SetDisplayMode(LeaderboardPanel.DisplayMode.Fullscreen);

            Assert.IsTrue(lastFullscreen.HasValue, "OnFullscreenChanged should fire on a mode change.");
            Assert.IsTrue(lastFullscreen.Value, "Fullscreen mode must report fullscreen = true.");
        }
        finally
        {
            Object.DestroyImmediate(panelObj);
        }
    }

    [Test]
    public void SetDisplayMode_NonFullscreen_RaisesOnFullscreenChanged_False()
    {
        var panelObj = new GameObject("LeaderboardPanel");
        try
        {
            var panel = panelObj.AddComponent<LeaderboardPanel>();
            bool? lastFullscreen = null;
            panel.OnFullscreenChanged += v => lastFullscreen = v;

            panel.SetDisplayMode(LeaderboardPanel.DisplayMode.Fullscreen);
            panel.SetDisplayMode(LeaderboardPanel.DisplayMode.Enlarged);

            Assert.IsTrue(lastFullscreen.HasValue);
            Assert.IsFalse(lastFullscreen.Value, "Leaving fullscreen must report fullscreen = false.");
        }
        finally
        {
            Object.DestroyImmediate(panelObj);
        }
    }
}
