using NUnit.Framework;

[TestFixture]
public class JoinToastTextTests
{
    [Test]
    public void Format_NamedTeamWithCount_QuotesNameAndAppendsTotal()
    {
        Assert.AreEqual("'Alice' joined  (3 total)", JoinToastText.Format("Alice", 3));
    }

    [Test]
    public void Format_BlankTeamName_FallsBackToAStudent()
    {
        // Anonymous spectators (auto-join, empty teamName) must not render "'' joined".
        Assert.AreEqual("A student joined  (5 total)", JoinToastText.Format("", 5));
    }

    [Test]
    public void Format_WhitespaceTeamName_FallsBackToAStudent()
    {
        Assert.AreEqual("A student joined  (1 total)", JoinToastText.Format("   ", 1));
    }

    [Test]
    public void Format_NullTeamName_FallsBackToAStudent()
    {
        Assert.AreEqual("A student joined  (2 total)", JoinToastText.Format(null, 2));
    }

    [Test]
    public void Format_TrimsSurroundingWhitespaceFromName()
    {
        Assert.AreEqual("'Bob' joined  (4 total)", JoinToastText.Format("  Bob  ", 4));
    }

    [Test]
    public void Format_NonPositiveCount_OmitsTotalSuffix()
    {
        // A zero/unknown count must not show a misleading "(0 total)".
        Assert.AreEqual("'Cara' joined", JoinToastText.Format("Cara", 0));
        Assert.AreEqual("A student joined", JoinToastText.Format(null, -1));
    }
}
