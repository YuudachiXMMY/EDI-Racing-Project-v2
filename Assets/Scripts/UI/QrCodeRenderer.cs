using UnityEngine;
using QRCoder;

/// <summary>
/// Renders a payload string into a crisp black-on-white QR Texture2D for display in a UGUI RawImage.
/// WebGL-safe: uses only QRCoder's generator core (no System.Drawing) plus Unity's Texture2D.
/// FilterMode.Point keeps the modules sharp when the RawImage is scaled. Caller owns the returned
/// texture (destroy it before replacing to avoid a WebGL memory leak). Mirrors the procedural
/// Texture2D idiom in WeatherEffect.CreateSnowflakeTexture.
/// </summary>
public static class QrCodeRenderer
{
    // pixelSize = target texture edge length (e.g. QrPanelState.PixelSize(size)).
    public static Texture2D Render(string payload, int pixelSize)
    {
        if (string.IsNullOrEmpty(payload) || pixelSize <= 0) return null;

        using (var generator = new QRCodeGenerator())
        {
            // ECC level Q (~25% recovery) is a good projector-scan default.
            QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            // ModuleMatrix already includes the 4-module quiet-zone border — do not add another.
            var matrix = data.ModuleMatrix;
            int modules = matrix.Count;
            if (modules <= 0) return null;

            var tex = new Texture2D(pixelSize, pixelSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[pixelSize * pixelSize];
            Color32 black = new Color32(0, 0, 0, 255);
            Color32 white = new Color32(255, 255, 255, 255);

            for (int y = 0; y < pixelSize; y++)
            {
                // Texture2D origin is bottom-left; flip Y so the QR isn't mirrored vertically.
                int my = modules - 1 - (y * modules / pixelSize);
                var row = matrix[my];
                int rowBase = y * pixelSize;
                for (int x = 0; x < pixelSize; x++)
                {
                    int mx = x * modules / pixelSize;
                    pixels[rowBase + x] = row[mx] ? black : white;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
