using NUnit.Framework;

[TestFixture]
public class StudentLinkBuilderTests
{
    [Test]
    public void BuildJoinLink_Normal_ComposesSurveyJoinRoute()
    {
        Assert.AreEqual("https://host.example/survey/#/join/A1B2C3",
            StudentLinkBuilder.BuildJoinLink("https://host.example", "A1B2C3"));
    }

    [Test]
    public void BuildJoinLink_TrailingSlashOrigin_NoDoubleSlash()
    {
        Assert.AreEqual("https://host.example/survey/#/join/R1",
            StudentLinkBuilder.BuildJoinLink("https://host.example/", "R1"));
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
