using System;
using NUnit.Framework;

[TestFixture]
public class JsonImporterTests
{
    [Test]
    public void Parse_ValidJson_ReturnsCorrectCarsAndRules()
    {
        string json = @"{
            ""configName"": ""Test Survey"",
            ""carData"": [
                { ""teamName"": ""Alpha"", ""attributes"": [{ ""key"": ""color"", ""value"": ""blue"" }] },
                { ""teamName"": ""Beta"", ""attributes"": [{ ""key"": ""speed"", ""value"": ""5"" }] }
            ],
            ""eventRules"": [
                { ""DisplayName"": ""Rule1"", ""AttributeName"": ""color"", ""Operator"": 0, ""CompareValue"": ""blue"", ""SpeedDelta"": -10.0, ""Duration"": 8.0, ""Weather"": 0, ""AllowRepeat"": false }
            ]
        }";

        var result = JsonImporter.Parse(json);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("Test Survey", result.ConfigName);
        Assert.AreEqual(2, result.Cars.Count);
        Assert.AreEqual("Alpha", result.Cars[0].TeamName);
        Assert.AreEqual("blue", result.Cars[0].GetAttribute("color"));
        Assert.AreEqual("Beta", result.Cars[1].TeamName);
        Assert.AreEqual("5", result.Cars[1].GetAttribute("speed"));
        Assert.AreEqual(1, result.EventRules.Length);
        Assert.AreEqual("Rule1", result.EventRules[0].DisplayName);
        Assert.AreEqual(-10f, result.EventRules[0].SpeedDelta);
    }

    [Test]
    public void Parse_EmptyCarData_ReturnsSuccessWithZeroCars()
    {
        string json = @"{ ""configName"": ""Empty"", ""carData"": [], ""eventRules"": [] }";

        var result = JsonImporter.Parse(json);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.Cars.Count);
        Assert.AreEqual(0, result.EventRules.Length);
    }

    [Test]
    public void Parse_NullJson_ReturnsError()
    {
        var result = JsonImporter.Parse(null);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.Error);
    }

    [Test]
    public void Parse_EmptyString_ReturnsError()
    {
        var result = JsonImporter.Parse("");

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.Error);
    }

    [Test]
    public void Parse_MalformedJson_ReturnsError()
    {
        var result = JsonImporter.Parse("{ invalid json }}}");

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.Error);
    }

    [Test]
    public void Parse_MultipleAttributes_AllMapped()
    {
        string json = @"{
            ""configName"": ""Multi"",
            ""carData"": [{
                ""teamName"": ""Gamma"",
                ""attributes"": [
                    { ""key"": ""disability"", ""value"": ""physical"" },
                    { ""key"": ""assistive_tech"", ""value"": ""yes"" },
                    { ""key"": ""accommodation_ease"", ""value"": ""3"" }
                ]
            }],
            ""eventRules"": []
        }";

        var result = JsonImporter.Parse(json);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.Cars.Count);
        Assert.AreEqual(3, result.Cars[0].Attributes.Length);
        Assert.AreEqual("physical", result.Cars[0].GetAttribute("disability"));
        Assert.AreEqual("yes", result.Cars[0].GetAttribute("assistive_tech"));
        Assert.AreEqual("3", result.Cars[0].GetAttribute("accommodation_ease"));
    }

    [Test]
    public void Parse_EventRuleOperatorAndWeather_CorrectIntValues()
    {
        string json = @"{
            ""configName"": ""Rules"",
            ""carData"": [],
            ""eventRules"": [{
                ""DisplayName"": ""Snow Event"",
                ""AttributeName"": """",
                ""Operator"": 8,
                ""CompareValue"": """",
                ""SpeedDelta"": -8.0,
                ""Duration"": 12.0,
                ""Weather"": 1,
                ""AllowRepeat"": true
            }]
        }";

        var result = JsonImporter.Parse(json);

        Assert.IsTrue(result.Success);
        var rule = result.EventRules[0];
        Assert.AreEqual(8, rule.Operator); // ComparisonOperator.All
        Assert.AreEqual(1, rule.Weather);  // WeatherType.Snow
        Assert.IsTrue(rule.AllowRepeat);
    }

    [Test]
    public void Parse_EmptyTeamName_SkipsEntry()
    {
        string json = @"{
            ""configName"": ""Skip"",
            ""carData"": [
                { ""teamName"": """", ""attributes"": [] },
                { ""teamName"": ""Valid"", ""attributes"": [] }
            ],
            ""eventRules"": []
        }";

        var result = JsonImporter.Parse(json);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.Cars.Count);
        Assert.AreEqual("Valid", result.Cars[0].TeamName);
    }
}
