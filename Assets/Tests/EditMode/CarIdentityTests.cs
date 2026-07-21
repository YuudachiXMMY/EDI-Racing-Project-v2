using System;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CarIdentityTests
{
    private GameObject testObj;
    private CarIdentity identity;

    [SetUp]
    public void SetUp()
    {
        testObj = new GameObject("TestCar");
        identity = testObj.AddComponent<CarIdentity>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(testObj);
    }

    [Test]
    public void Initialize_SetsTeamName()
    {
        identity.Initialize(new CarData("Alpha", new AttributeEntry[]
        {
            new AttributeEntry { Key = "color", Value = "red" }
        }));

        Assert.AreEqual("Alpha", identity.TeamName);
    }

    [Test]
    public void Initialize_ClonesAttributes()
    {
        var attrs = new AttributeEntry[] { new AttributeEntry { Key = "x", Value = "1" } };
        var data = new CarData("A", attrs);

        identity.Initialize(data);

        Assert.AreEqual(1, identity.Attributes.Length);
        Assert.AreEqual("x", identity.Attributes[0].Key);
        // Verify it's a clone, not the same reference
        Assert.AreNotSame(attrs, identity.Attributes);
    }

    [Test]
    public void Initialize_ResetsProgressFields()
    {
        identity.CurrentCheckpointIndex = 5;
        identity.TotalCheckpointsPassed = 10;
        identity.CurrentLap = 3;
        identity.CheckpointTime = 99f;
        identity.IsOwnCar = true;

        identity.Initialize(new CarData("A", Array.Empty<AttributeEntry>()));

        Assert.AreEqual(0, identity.CurrentCheckpointIndex);
        Assert.AreEqual(0, identity.TotalCheckpointsPassed);
        Assert.AreEqual(0, identity.CurrentLap);
        Assert.AreEqual(0f, identity.CheckpointTime);
        Assert.IsFalse(identity.IsOwnCar);
    }

    [Test]
    public void Initialize_NullAttributes_SetsEmptyArray()
    {
        identity.Initialize(new CarData("A", (AttributeEntry[])null));

        Assert.IsNotNull(identity.Attributes);
        Assert.AreEqual(0, identity.Attributes.Length);
    }

    [Test]
    public void GetAttribute_ExistingKey_ReturnsValue()
    {
        identity.Initialize(new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "lang", Value = "French" }
        }));

        Assert.AreEqual("French", identity.GetAttribute("lang"));
    }

    [Test]
    public void GetAttribute_CaseInsensitive()
    {
        identity.Initialize(new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "Color", Value = "blue" }
        }));

        Assert.AreEqual("blue", identity.GetAttribute("color"));
    }

    [Test]
    public void GetAttribute_Missing_ReturnsDefault()
    {
        identity.Initialize(new CarData("A", Array.Empty<AttributeEntry>()));

        Assert.AreEqual("", identity.GetAttribute("missing"));
        Assert.AreEqual("fallback", identity.GetAttribute("missing", "fallback"));
    }

    [Test]
    public void GetIntAttribute_ValidInt_ReturnsValue()
    {
        identity.Initialize(new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "score", Value = "42" }
        }));

        Assert.AreEqual(42, identity.GetIntAttribute("score"));
    }

    [Test]
    public void GetIntAttribute_Invalid_ReturnsDefault()
    {
        identity.Initialize(new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "score", Value = "abc" }
        }));

        Assert.AreEqual(0, identity.GetIntAttribute("score"));
    }

    [Test]
    public void HasAttribute_Present_ReturnsTrue()
    {
        identity.Initialize(new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "x", Value = "1" }
        }));

        Assert.IsTrue(identity.HasAttribute("x"));
    }

    [Test]
    public void HasAttribute_Absent_ReturnsFalse()
    {
        identity.Initialize(new CarData("A", Array.Empty<AttributeEntry>()));

        Assert.IsFalse(identity.HasAttribute("x"));
    }

    [Test]
    public void ColorIndex_BackwardCompat()
    {
        identity.Initialize(new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "colorIndex", Value = "3" }
        }));

        Assert.AreEqual(3, identity.ColorIndex);
    }

    [Test]
    public void Functions_BackwardCompat_SlashSeparated()
    {
        identity.Initialize(new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "functions", Value = "password/glasses" }
        }));

        var funcs = identity.Functions;

        Assert.AreEqual(2, funcs.Length);
        Assert.Contains("password", funcs);
        Assert.Contains("glasses", funcs);
    }
}
