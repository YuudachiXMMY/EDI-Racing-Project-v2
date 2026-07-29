using NUnit.Framework;

[TestFixture]
public class HostLaunchParamsTests
{
    [Test]
    public void ParseHash_NormalHostLaunch_ExtractsAllParams()
    {
        // Arrange
        string url = "https://edi-racing.example/#role=host&token=abc.def&survey=5";

        // Act
        var p = HostLaunchParams.ParseHash(url);

        // Assert
        Assert.AreEqual("host", p["role"]);
        Assert.AreEqual("abc.def", p["token"]);
        Assert.AreEqual("5", p["survey"]);
    }

    [Test]
    public void ParseHash_EmptyString_ReturnsEmptyMap()
    {
        var p = HostLaunchParams.ParseHash("");
        Assert.AreEqual(0, p.Count);
    }

    [Test]
    public void ParseHash_NullInput_ReturnsEmptyMap()
    {
        var p = HostLaunchParams.ParseHash(null);
        Assert.AreEqual(0, p.Count);
    }

    [Test]
    public void ParseHash_NoHashFragment_ReturnsEmptyMap()
    {
        var p = HostLaunchParams.ParseHash("https://edi-racing.example/");
        Assert.AreEqual(0, p.Count);
    }

    [Test]
    public void ParseHash_ValueContainsEquals_KeepsEverythingAfterFirstEquals()
    {
        // Base64 padding uses '=', so a value may legitimately contain '='.
        var p = HostLaunchParams.ParseHash("#token=ab=cd");
        Assert.AreEqual("ab=cd", p["token"]);
    }

    [Test]
    public void ParseHash_LeadingQuestionMark_IsTolerated()
    {
        var p = HostLaunchParams.ParseHash("#?role=host&survey=1");
        Assert.AreEqual("host", p["role"]);
        Assert.AreEqual("1", p["survey"]);
    }

    [Test]
    public void ParseHash_PercentEncodedValue_IsDecoded()
    {
        var p = HostLaunchParams.ParseHash("#name=a%20b");
        Assert.AreEqual("a b", p["name"]);
    }

    [Test]
    public void ParseHash_ValuelessFragment_IsSkipped()
    {
        var p = HostLaunchParams.ParseHash("#role=host&garbage&survey=2");
        Assert.AreEqual("host", p["role"]);
        Assert.AreEqual("2", p["survey"]);
        Assert.IsFalse(p.ContainsKey("garbage"));
    }
}
