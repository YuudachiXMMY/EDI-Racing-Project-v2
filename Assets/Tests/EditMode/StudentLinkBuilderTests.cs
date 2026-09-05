using NUnit.Framework;

[TestFixture]
public class StudentLinkBuilderTests
{
    [Test]
    public void BuildJoinLink_Normal_ComposesJoinLandingRoute()
    {
        // Lowercase input → uppercase output (matches web-app buildJoinLandingUrl + server room lookup).
        Assert.AreEqual("https://host.example/#/join/A1B2C3",
            StudentLinkBuilder.BuildJoinLink("https://host.example", "a1b2c3"));
    }

    [Test]
    public void BuildJoinLink_TrailingSlashOrigin_NoDoubleSlash()
    {
        Assert.AreEqual("https://host.example/#/join/R1",
            StudentLinkBuilder.BuildJoinLink("https://host.example/", "r1"));
    }

    [Test]
    public void BuildJoinLink_EmptyOrigin_ReturnsEmpty()
    {
        Assert.AreEqual("", StudentLinkBuilder.BuildJoinLink("", "R1"));
    }

    [Test]
    public void BuildJoinLink_EmptyRoom_ReturnsEmpty()
    {
        Assert.AreEqual("", StudentLinkBuilder.BuildJoinLink("https://host.example", ""));
    }
}
