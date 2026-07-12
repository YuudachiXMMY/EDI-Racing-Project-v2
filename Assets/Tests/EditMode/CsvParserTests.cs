using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class CsvParserTests
{
    [Test]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        List<CarData> result = CsvParser.Parse("");
        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void Parse_NullString_ReturnsEmptyList()
    {
        List<CarData> result = CsvParser.Parse(null);
        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void Parse_HeaderOnly_ReturnsEmptyList()
    {
        List<CarData> result = CsvParser.Parse("teamName,colorIndex,functions");
        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void Parse_ValidCsvWithHeaders_ReturnsCorrectCarData()
    {
        string csv = "teamName,colorIndex,functions\nAlpha,2,password/glasses";
        List<CarData> result = CsvParser.Parse(csv);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Alpha", result[0].TeamName);
        Assert.AreEqual("2", result[0].GetAttribute("colorIndex"));
        Assert.AreEqual("password/glasses", result[0].GetAttribute("functions"));
    }

    [Test]
    public void Parse_MultipleRows_ParsesAllCars()
    {
        string csv = "teamName,color\nAlpha,red\nBeta,blue\nGamma,green";
        List<CarData> result = CsvParser.Parse(csv);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Alpha", result[0].TeamName);
        Assert.AreEqual("Beta", result[1].TeamName);
        Assert.AreEqual("Gamma", result[2].TeamName);
        Assert.AreEqual("blue", result[1].GetAttribute("color"));
    }

    [Test]
    public void Parse_FewerColumnsThanHeaders_MapsAvailableOnly()
    {
        string csv = "teamName,a,b,c\nAlpha,1";
        List<CarData> result = CsvParser.Parse(csv);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("1", result[0].GetAttribute("a"));
        Assert.AreEqual("", result[0].GetAttribute("b"));
    }

    [Test]
    public void Parse_EmptyTeamName_SkipsRow()
    {
        string csv = "teamName,score\n,100\nBeta,200";
        List<CarData> result = CsvParser.Parse(csv);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Beta", result[0].TeamName);
    }

    [Test]
    public void Parse_WhitespaceHandling_TrimsValues()
    {
        string csv = "teamName , color \n  Alpha  , red  ";
        List<CarData> result = CsvParser.Parse(csv);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Alpha", result[0].TeamName);
        Assert.AreEqual("red", result[0].GetAttribute("color"));
    }

    [Test]
    public void Parse_ManyColumns_CreatesAllAttributes()
    {
        string csv = "teamName,a,b,c,d,e,f,g,h,i,j\nAlpha,1,2,3,4,5,6,7,8,9,10";
        List<CarData> result = CsvParser.Parse(csv);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(10, result[0].Attributes.Length);
        Assert.AreEqual("10", result[0].GetAttribute("j"));
    }

    [Test]
    public void Parse_V1Format_WorksWithOldColumns()
    {
        string csv = "teamName,colorIndex,functions\nTeamA,3,password/glasses/facerecog";
        List<CarData> result = CsvParser.Parse(csv);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(3, result[0].ColorIndex);
        Assert.Contains("password", result[0].Functions);
        Assert.Contains("glasses", result[0].Functions);
        Assert.Contains("facerecog", result[0].Functions);
    }
}
