using NUnit.Framework;

/// <summary>
/// Covers the Tab-driven display-mode cycle order (Normal → Enlarged → Fullscreen → Normal)
/// exposed by <see cref="LeaderboardPanel.NextMode"/>. The layout/font mutation itself needs a
/// live RectTransform, but the cycle order is pure and worth pinning so a future refactor can't
/// silently drop a mode or reorder the sequence.
/// </summary>
[TestFixture]
public class LeaderboardDisplayModeTests
{
    [Test]
    public void NextMode_Normal_ReturnsEnlarged()
    {
        Assert.AreEqual(LeaderboardPanel.DisplayMode.Enlarged,
            LeaderboardPanel.NextMode(LeaderboardPanel.DisplayMode.Normal));
    }

    [Test]
    public void NextMode_Enlarged_ReturnsFullscreen()
    {
        Assert.AreEqual(LeaderboardPanel.DisplayMode.Fullscreen,
            LeaderboardPanel.NextMode(LeaderboardPanel.DisplayMode.Enlarged));
    }

    [Test]
    public void NextMode_Fullscreen_WrapsBackToNormal()
    {
        Assert.AreEqual(LeaderboardPanel.DisplayMode.Normal,
            LeaderboardPanel.NextMode(LeaderboardPanel.DisplayMode.Fullscreen));
    }

    [Test]
    public void NextMode_ThreePresses_ReturnsToStart()
    {
        var mode = LeaderboardPanel.DisplayMode.Normal;
        mode = LeaderboardPanel.NextMode(mode);
        mode = LeaderboardPanel.NextMode(mode);
        mode = LeaderboardPanel.NextMode(mode);
        Assert.AreEqual(LeaderboardPanel.DisplayMode.Normal, mode);
    }
}
