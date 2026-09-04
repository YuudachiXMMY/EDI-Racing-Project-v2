/// <summary>
/// Pure size/visibility state for the host-screen join-QR panel. UnityEngine-free so the size-cycle
/// and pixel-size mapping are EditMode-testable without a live scene (mirrors StudentLinkBuilder /
/// LeaderboardFormatter). The MonoBehaviour (SetupScreen) holds the current values and applies the
/// results to UGUI.
/// </summary>
public static class QrPanelState
{
    public enum QrSize { Small, Large }

    // Pixel edge length rendered into the Texture2D AND used as the RawImage sizeDelta. Rendering at
    // the display size (not upscaling a small texture) keeps QR modules crisp when scanned.
    public static int PixelSize(QrSize size) => size == QrSize.Large ? 512 : 256;

    // The size button cycles Small <-> Large.
    public static QrSize NextSize(QrSize size) => size == QrSize.Small ? QrSize.Large : QrSize.Small;

    // Button labels reflect the ACTION the button performs / the current size.
    public static string SizeLabel(QrSize size) => size == QrSize.Large ? "尺寸: 大" : "尺寸: 小";
    public static string VisibilityLabel(bool visible) => visible ? "隐藏二维码" : "显示二维码";
}
