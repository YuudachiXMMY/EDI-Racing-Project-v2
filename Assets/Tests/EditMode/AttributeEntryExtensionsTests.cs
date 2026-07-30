using NUnit.Framework;

[TestFixture]
public class AttributeEntryExtensionsTests
{
    private static AttributeEntry[] Sample() => new[]
    {
        new AttributeEntry { Key = "colorIndex", Value = "3" },
        new AttributeEntry { Key = "functions", Value = "password/facerecog" }
    };

    [Test]
    public void Get_ExistingKeyCaseInsensitive_ReturnsValue()
    {
        Assert.AreEqual("3", Sample().Get("COLORINDEX"));
    }

    [Test]
    public void Get_MissingKey_ReturnsProvidedDefault()
    {
        Assert.AreEqual("fallback", Sample().Get("missing", "fallback"));
    }

    [Test]
    public void Get_MissingKey_NoDefault_ReturnsNull()
    {
        Assert.IsNull(Sample().Get("missing"));
    }

    [Test]
    public void Get_NullArray_ReturnsDefault()
    {
        AttributeEntry[] entries = null;
        Assert.AreEqual("d", entries.Get("colorIndex", "d"));
    }

    [Test]
    public void Get_NullOrEmptyKey_ReturnsDefault()
    {
        Assert.AreEqual("d", Sample().Get("", "d"));
        Assert.AreEqual("d", Sample().Get(null, "d"));
    }

    [Test]
    public void Has_ExistingKey_ReturnsTrue()
    {
        Assert.IsTrue(Sample().Has("functions"));
    }

    [Test]
    public void Has_MissingKey_ReturnsFalse()
    {
        Assert.IsFalse(Sample().Has("missing"));
    }

    [Test]
    public void Has_NullArray_ReturnsFalse()
    {
        AttributeEntry[] entries = null;
        Assert.IsFalse(entries.Has("colorIndex"));
    }
}
