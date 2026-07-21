using System;
using NUnit.Framework;

[TestFixture]
public class NetCarDataTests
{
    [Test]
    public void FromCarData_PreservesTeamName()
    {
        var car = new CarData("Alpha", Array.Empty<AttributeEntry>());
        var net = NetCarData.FromCarData(car);

        Assert.AreEqual("Alpha", net.teamName);
    }

    [Test]
    public void FromCarData_ConvertsAttributes()
    {
        var car = new CarData("A", new AttributeEntry[]
        {
            new AttributeEntry { Key = "color", Value = "blue" },
            new AttributeEntry { Key = "score", Value = "5" }
        });

        var net = NetCarData.FromCarData(car);

        Assert.AreEqual(2, net.attrs.Length);
        Assert.AreEqual("color", net.attrs[0].k);
        Assert.AreEqual("blue", net.attrs[0].v);
        Assert.AreEqual("score", net.attrs[1].k);
        Assert.AreEqual("5", net.attrs[1].v);
    }

    [Test]
    public void FromCarData_EmptyAttributes_ReturnsEmptyArray()
    {
        var car = new CarData("A", Array.Empty<AttributeEntry>());
        var net = NetCarData.FromCarData(car);

        Assert.IsNotNull(net.attrs);
        Assert.AreEqual(0, net.attrs.Length);
    }

    [Test]
    public void FromCarData_NullAttributes_ReturnsEmptyArray()
    {
        var car = new CarData("A", (AttributeEntry[])null);
        var net = NetCarData.FromCarData(car);

        Assert.IsNotNull(net.attrs);
        Assert.AreEqual(0, net.attrs.Length);
    }

    [Test]
    public void ToCarData_RestoresTeamName()
    {
        var net = new NetCarData { teamName = "Beta", attrs = Array.Empty<NetAttribute>() };
        var car = net.ToCarData();

        Assert.AreEqual("Beta", car.TeamName);
    }

    [Test]
    public void ToCarData_RestoresAttributes()
    {
        var net = new NetCarData
        {
            teamName = "A",
            attrs = new NetAttribute[]
            {
                new NetAttribute { k = "lang", v = "en" }
            }
        };

        var car = net.ToCarData();

        Assert.AreEqual(1, car.Attributes.Length);
        Assert.AreEqual("lang", car.Attributes[0].Key);
        Assert.AreEqual("en", car.Attributes[0].Value);
    }

    [Test]
    public void ToCarData_NullAttrs_ReturnsEmptyAttributes()
    {
        var net = new NetCarData { teamName = "A", attrs = null };
        var car = net.ToCarData();

        Assert.IsNotNull(car.Attributes);
        Assert.AreEqual(0, car.Attributes.Length);
    }

    [Test]
    public void RoundTrip_PreservesAllData()
    {
        var original = new CarData("Gamma", new AttributeEntry[]
        {
            new AttributeEntry { Key = "color", Value = "red" },
            new AttributeEntry { Key = "speed", Value = "10" },
            new AttributeEntry { Key = "lang", Value = "French" }
        });

        var restored = NetCarData.FromCarData(original).ToCarData();

        Assert.AreEqual(original.TeamName, restored.TeamName);
        Assert.AreEqual(original.Attributes.Length, restored.Attributes.Length);
        for (int i = 0; i < original.Attributes.Length; i++)
        {
            Assert.AreEqual(original.Attributes[i].Key, restored.Attributes[i].Key);
            Assert.AreEqual(original.Attributes[i].Value, restored.Attributes[i].Value);
        }
    }
}
