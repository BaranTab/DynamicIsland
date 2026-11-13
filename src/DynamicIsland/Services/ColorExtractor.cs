using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland.Services;

/// <summary>
/// Extracts the dominant, "vivid" colour from album art.
/// Saturated, bright pixels get more weight; greys are almost ignored.
/// </summary>
public static class ColorExtractor
{
    public static Color? FromImageBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 32; // A tiny thumbnail is enough and fast
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();

            var bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight;
            var pixels = new byte[w * h * 4];
            bgra.CopyPixels(pixels, w * 4, 0);

            double r = 0, g = 0, b = 0, total = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                double pb = pixels[i] / 255.0, pg = pixels[i + 1] / 255.0, pr = pixels[i + 2] / 255.0;
                double max = Math.Max(pr, Math.Max(pg, pb));
                double min = Math.Min(pr, Math.Min(pg, pb));
                double saturation = max == 0 ? 0 : (max - min) / max;

                double weight = saturation * saturation * max + 0.02;
                r += pr * weight; g += pg * weight; b += pb * weight;
                total += weight;
            }

            var (hue, sat, val) = ToHsv(r / total, g / total, b / total);

            // Clamp saturation and brightness to a range that looks good in the UI
            sat = Math.Clamp(sat * 1.2, 0.35, 0.8);
            val = Math.Clamp(val * 1.3, 0.55, 0.85);

            return FromHsv(hue, sat, val);
        }
        catch
        {
            return null;
        }
    }

    private static (double h, double s, double v) ToHsv(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d = max - min;

        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
        }
        if (h < 0) h += 360;

        return (h, max == 0 ? 0 : d / max, max);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;

        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
