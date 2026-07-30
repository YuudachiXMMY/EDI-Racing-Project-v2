using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class LeaderboardFormatterTests
{
    [Test]
    public void RankColor_First_ReturnsGold()
    {
        Assert.AreEqual(LeaderboardFormatter.Gold, LeaderboardFormatter.RankColor(0));
    }

    [Test]
    public void RankColor_Second_ReturnsSilver()
    {
        Assert.AreEqual(LeaderboardFormatter.Silver, LeaderboardFormatter.RankColor(1));
    }

    [Test]
    public void RankColor_Third_ReturnsBronze()
    {
        Assert.AreEqual(LeaderboardFormatter.Bronze, LeaderboardFormatter.RankColor(2));
    }

    [Test]
    public void RankColor_FourthOrLower_ReturnsWhite()
    {
        Assert.AreEqual(Color.white, LeaderboardFormatter.RankColor(3));
        Assert.AreEqual(Color.white, LeaderboardFormatter.RankColor(99));
    }

    [Test]
    public void RankHex_MatchesLegacyRichTextStrings()
    {
        Assert.AreEqual("yellow", LeaderboardFormatter.RankHex(0));
        Assert.AreEqual("#C0C0C0", LeaderboardFormatter.RankHex(1));
        Assert.AreEqual("#CD7F32", LeaderboardFormatter.RankHex(2));
        Assert.AreEqual("white", LeaderboardFormatter.RankHex(3));
    }

    [Test]
    public void Gold_MatchesLegacyRgb()
    {
        Assert.AreEqual(new Color(1f, 0.84f, 0f), LeaderboardFormatter.Gold);
    }
}
