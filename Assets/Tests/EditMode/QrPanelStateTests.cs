using NUnit.Framework;

[TestFixture]
public class QrPanelStateTests
{
    [Test]
    public void PixelSize_Small_Returns256()
    {
        Assert.AreEqual(256, QrPanelState.PixelSize(QrPanelState.QrSize.Small));
    }

    [Test]
    public void PixelSize_Large_Returns512()
    {
        Assert.AreEqual(512, QrPanelState.PixelSize(QrPanelState.QrSize.Large));
    }

    [Test]
    public void NextSize_Small_ReturnsLarge()
    {
        Assert.AreEqual(QrPanelState.QrSize.Large, QrPanelState.NextSize(QrPanelState.QrSize.Small));
    }

    [Test]
    public void NextSize_Large_ReturnsSmall()
    {
        Assert.AreEqual(QrPanelState.QrSize.Small, QrPanelState.NextSize(QrPanelState.QrSize.Large));
    }

    [Test]
    public void VisibilityLabel_Visible_SaysHide()
    {
        Assert.AreEqual("隐藏二维码", QrPanelState.VisibilityLabel(true));
    }

    [Test]
    public void VisibilityLabel_Hidden_SaysShow()
    {
        Assert.AreEqual("显示二维码", QrPanelState.VisibilityLabel(false));
    }

    [Test]
    public void SizeLabel_Small_SaysSmall()
    {
        Assert.AreEqual("尺寸: 小", QrPanelState.SizeLabel(QrPanelState.QrSize.Small));
    }

    [Test]
    public void SizeLabel_Large_SaysLarge()
    {
        Assert.AreEqual("尺寸: 大", QrPanelState.SizeLabel(QrPanelState.QrSize.Large));
    }
}
