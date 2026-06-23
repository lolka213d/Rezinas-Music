using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Harmony.Data;

namespace Harmony.Helpers;

/// <summary>Builds a warm Spotify-style palette from album art.</summary>
public static class CoverPaletteExtractor
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public sealed record Palette(
        string Background,
        string AccentLeft,
        string AccentRight,
        string ActiveText,
        string InactiveText,
        string PastText,
        bool IsLight);

    public static Palette Default { get; } = new(
        Background: "#FF14100C",
        AccentLeft: "#66E87A3A",
        AccentRight: "#6638A878",
        ActiveText: "#FFFFFFFF",
        InactiveText: "#88FFFFFF",
        PastText: "#55FFFFFF",
        IsLight: false);

    public static async Task<Palette> ExtractAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Default;

        try
        {
            var bytes = await ReadBytesAsync(url, ct);
            if (bytes.Length == 0)
                return Default;

            return ExtractFromBytes(bytes);
        }
        catch
        {
            return Default;
        }
    }

    private static async Task<byte[]> ReadBytesAsync(string url, CancellationToken ct)
    {
        if (File.Exists(url))
            return await File.ReadAllBytesAsync(url, ct);

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
            return await File.ReadAllBytesAsync(uri.LocalPath, ct);

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"48|{url.Trim()}"))).ToLowerInvariant();
        var disk = Path.Combine(AppPaths.CacheFolder, "img", hash[..2], hash + ".bin");
        if (File.Exists(disk))
            return await File.ReadAllBytesAsync(disk, ct);

        return await Http.GetByteArrayAsync(url, ct);
    }

    private static Palette ExtractFromBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = stream;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.DecodePixelWidth = 56;
        bmp.DecodePixelHeight = 56;
        bmp.EndInit();
        bmp.Freeze();

        var (r, g, b, samples) = SampleDominant(bmp);
        if (samples < 4)
            return Default;

        var primary = Color.FromRgb(r, g, b);
        var warm = Blend(primary, Color.FromRgb(232, 122, 58), 0.42);
        var cool = Blend(primary, Color.FromRgb(56, 168, 120), 0.42);
        var luminance = Luminance(primary);

        if (luminance > 0.58)
        {
            var bg = Blend(
                Desaturate(Blend(warm, cool, 0.5), 0.25),
                Color.FromRgb(255, 244, 232),
                0.55);
            bg = ClampBrightness(bg, 0.72, 0.92);

            return new Palette(
                Background: ToHex(bg),
                AccentLeft: ToHex(WithAlpha(warm, 0.55)),
                AccentRight: ToHex(WithAlpha(cool, 0.5)),
                ActiveText: "#FF1A1410",
                InactiveText: "#991A1410",
                PastText: "#661A1410",
                IsLight: true);
        }

        var darkBg = Darken(Desaturate(Blend(warm, cool, 0.45), 0.35), 0.78);
        return new Palette(
            Background: ToHex(darkBg),
            AccentLeft: ToHex(WithAlpha(warm, 0.42)),
            AccentRight: ToHex(WithAlpha(cool, 0.38)),
            ActiveText: "#FFFFFFFF",
            InactiveText: "#88FFFFFF",
            PastText: "#55FFFFFF",
            IsLight: false);
    }

    private static (byte r, byte g, byte b, int samples) SampleDominant(BitmapSource bmp)
    {
        var width = bmp.PixelWidth;
        var height = bmp.PixelHeight;
        if (width <= 0 || height <= 0)
            return (0, 0, 0, 0);

        var stride = width * 4;
        var pixels = new byte[height * stride];
        bmp.CopyPixels(pixels, stride, 0);

        double sumR, sumG, sumB, weightSum;
        sumR = sumG = sumB = weightSum = 0;
        var count = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * stride + x * 4;
                var b = pixels[i];
                var g = pixels[i + 1];
                var r = pixels[i + 2];

                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                var sat = max == 0 ? 0.0 : (max - min) / (double)max;
                if (max < 24 || max > 245 || sat < 0.12)
                    continue;

                var w = sat * sat;
                sumR += r * w;
                sumG += g * w;
                sumB += b * w;
                weightSum += w;
                count++;
            }
        }

        if (weightSum < 0.01)
            return (210, 120, 70, 0);

        return (
            (byte)Math.Clamp(sumR / weightSum, 0, 255),
            (byte)Math.Clamp(sumG / weightSum, 0, 255),
            (byte)Math.Clamp(sumB / weightSum, 0, 255),
            count);
    }

    private static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color Desaturate(Color c, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var gray = (byte)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
        return Blend(c, Color.FromRgb(gray, gray, gray), amount);
    }

    private static Color Darken(Color c, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)(c.R * (1 - amount)),
            (byte)(c.G * (1 - amount)),
            (byte)(c.B * (1 - amount)));
    }

    private static Color ClampBrightness(Color c, double minLum, double maxLum)
    {
        var lum = Luminance(c);
        if (lum >= minLum && lum <= maxLum)
            return c;

        var target = lum < minLum ? minLum : maxLum;
        var factor = target / Math.Max(lum, 0.01);
        return Color.FromRgb(
            (byte)Math.Clamp(c.R * factor, 0, 255),
            (byte)Math.Clamp(c.G * factor, 0, 255),
            (byte)Math.Clamp(c.B * factor, 0, 255));
    }

    private static Color WithAlpha(Color c, double alpha) =>
        Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B);

    private static double Luminance(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
