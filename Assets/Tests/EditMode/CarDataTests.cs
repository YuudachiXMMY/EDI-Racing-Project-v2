using System;
using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class CarDataTests
{
    // --- Constructor ---

    [Test]
    public void Constructor_WithAttributes_SetsFieldsCorrectly()
    {
        var attrs = new AttributeEntry[] { new AttributeEntry { Key = "color", Value = "red" } };
        var car = new CarData("Alpha", attrs);

        Assert.AreEqual("Alpha", car.TeamName);
        Assert.AreEqual(1, car.Attributes.Length);
    }

    [Test]
    public void Constructor_NullAttributes_DefaultsToEmptyArray()
    {
        var car = new CarData("Alpha", (AttributeEntry[])null);

        Assert.IsNotNull(car.Attributes);
        Assert.AreEqual(0, car.Attributes.Length);
    }

    [Test]
    public void Constructor_WithDictionary_ConvertsToAttributeEntries()
    {
        var dict = new Dictionary<string, string> { { "lang", "en" }, { "score", "5" } };
        var car = new CarData("Beta", dict);

        Assert.AreEqual("Beta", car.TeamName);
        Assert.AreEqual(2, car.Attributes.Length);
        Assert.AreEqual("en", car.GetAttribute("lang"));
        Assert.AreEqual("5", car.GetAttribute("score"));
    }

    [Test]
    public void Constructor_NullDictionary_DefaultsToEmptyArray()
    {
        var car = new CarData("Alpha", (Dictionary<string, string>)null);

        Assert.IsNotNull(car.Attributes);
        Assert.AreEqual(0, car.Attributes.Length);
    }

    // --- GetAttribute ---

    [Test]
    public void GetAttribute_ExistingKey_ReturnsValue()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "color", Value = "blue" } });

        Assert.AreEqual("blue", car.GetAttribute("color"));
    }

    [Test]
    public void GetAttribute_MissingKey_ReturnsDefault()
    {
        var car = new CarData("A", Array.Empty<AttributeEntry>());

        Assert.AreEqual("", car.GetAttribute("nonexistent"));
        Assert.AreEqual("fallback", car.GetAttribute("nonexistent", "fallback"));
    }

    [Test]
    public void GetAttribute_CaseInsensitive_MatchesKey()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "Color", Value = "red" } });

        Assert.AreEqual("red", car.GetAttribute("color"));
        Assert.AreEqual("red", car.GetAttribute("COLOR"));
    }

    [Test]
    public void GetAttribute_NullAttributes_ReturnsDefault()
    {
        var car = new CarData();
        car.TeamName = "A";
        // Attributes is null by default for uninitialized struct

        Assert.AreEqual("def", car.GetAttribute("any", "def"));
    }

    // --- GetIntAttribute ---

    [Test]
    public void GetIntAttribute_ValidInt_ReturnsValue()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "score", Value = "42" } });

        Assert.AreEqual(42, car.GetIntAttribute("score"));
    }

    [Test]
    public void GetIntAttribute_InvalidString_ReturnsDefault()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "score", Value = "abc" } });

        Assert.AreEqual(0, car.GetIntAttribute("score"));
        Assert.AreEqual(99, car.GetIntAttribute("score", 99));
    }

    [Test]
    public void GetIntAttribute_MissingKey_ReturnsDefault()
    {
        var car = new CarData("A", Array.Empty<AttributeEntry>());

        Assert.AreEqual(0, car.GetIntAttribute("missing"));
    }

    // --- GetFloatAttribute ---

    [Test]
    public void GetFloatAttribute_ValidFloat_ReturnsValue()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "speed", Value = "3.14" } });

        Assert.AreEqual(3.14f, car.GetFloatAttribute("speed"), 0.01f);
    }

    [Test]
    public void GetFloatAttribute_InvalidString_ReturnsDefault()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "speed", Value = "fast" } });

        Assert.AreEqual(0f, car.GetFloatAttribute("speed"));
        Assert.AreEqual(1.5f, car.GetFloatAttribute("speed", 1.5f));
    }

    // --- HasAttribute ---

    [Test]
    public void HasAttribute_Present_ReturnsTrue()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "color", Value = "red" } });

        Assert.IsTrue(car.HasAttribute("color"));
        Assert.IsTrue(car.HasAttribute("COLOR"));
    }

    [Test]
    public void HasAttribute_Absent_ReturnsFalse()
    {
        var car = new CarData("A", Array.Empty<AttributeEntry>());

        Assert.IsFalse(car.HasAttribute("color"));
    }

    [Test]
    public void HasAttribute_NullAttributes_ReturnsFalse()
    {
        var car = new CarData();

        Assert.IsFalse(car.HasAttribute("any"));
    }

    // --- ToDictionary ---

    [Test]
    public void ToDictionary_NormalCase_ReturnsAllEntries()
    {
        var car = new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "lang", Value = "en" },
            new AttributeEntry { Key = "score", Value = "5" }
        });

        var dict = car.ToDictionary();

        Assert.AreEqual(2, dict.Count);
        Assert.AreEqual("en", dict["lang"]);
        Assert.AreEqual("5", dict["score"]);
    }

    [Test]
    public void ToDictionary_EmptyAttributes_ReturnsEmptyDict()
    {
        var car = new CarData("A", Array.Empty<AttributeEntry>());

        Assert.AreEqual(0, car.ToDictionary().Count);
    }

    // --- GetAttributeKeys ---

    [Test]
    public void GetAttributeKeys_ReturnsAllKeys()
    {
        var car = new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "a", Value = "1" },
            new AttributeEntry { Key = "b", Value = "2" }
        });

        var keys = car.GetAttributeKeys();

        Assert.AreEqual(2, keys.Length);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Test]
    public void GetAttributeKeys_NullAttributes_ReturnsEmptyArray()
    {
        var car = new CarData();

        Assert.AreEqual(0, car.GetAttributeKeys().Length);
    }

    // --- Backward Compat: ColorIndex ---

    [Test]
    public void ColorIndex_WithAttribute_ReturnsIntValue()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "colorIndex", Value = "3" } });

        Assert.AreEqual(3, car.ColorIndex);
    }

    [Test]
    public void ColorIndex_WithoutAttribute_ReturnsZero()
    {
        var car = new CarData("A", Array.Empty<AttributeEntry>());

        Assert.AreEqual(0, car.ColorIndex);
    }

    // --- Backward Compat: Functions ---

    [Test]
    public void Functions_SlashSeparated_ReturnsParsedArray()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "functions", Value = "password/glasses/facerecog" } });

        var funcs = car.Functions;

        Assert.AreEqual(3, funcs.Length);
        Assert.Contains("password", funcs);
        Assert.Contains("glasses", funcs);
        Assert.Contains("facerecog", funcs);
    }

    [Test]
    public void Functions_EmptyValue_ReturnsEmptyArray()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "functions", Value = "" } });

        Assert.AreEqual(0, car.Functions.Length);
    }

    [Test]
    public void Functions_MissingAttribute_ReturnsEmptyArray()
    {
        var car = new CarData("A", Array.Empty<AttributeEntry>());

        Assert.AreEqual(0, car.Functions.Length);
    }

    [Test]
    public void Functions_TrimsAndLowercases()
    {
        var car = new CarData("A", new AttributeEntry[] { new AttributeEntry { Key = "functions", Value = " Password / Glasses " } });

        var funcs = car.Functions;

        Assert.Contains("password", funcs);
        Assert.Contains("glasses", funcs);
    }
}
