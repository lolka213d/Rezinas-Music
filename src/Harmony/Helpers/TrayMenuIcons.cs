using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace Harmony.Helpers;

/// <summary>Renders small tray-menu glyphs from app icon resources.</summary>
public static class TrayMenuIcons
{
    private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> ResourceKeys = new(StringComparer.Ordinal)
    {
        ["play"] = "IconPlay",
        ["pause"] = "IconPause",
        ["next"] = "IconNext",
        ["prev"] = "IconPrevious",
        ["home"] = "IconHome",
        ["search"] = "IconSearch",
        ["library"] = "IconLibrary",
        ["favorites"] = "IconHeartFilled",
        ["settings"] = "IconSettings",
        ["show"] = "IconAccount",
        ["exit"] = "IconClose",
        ["music"] = "IconNote",
    };

    public static Bitmap Get(string key)
    {
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var bmp = Render(key);
        Cache[key] = bmp;
        return bmp;
    }

    private static Geometry? ResolveGeometry(string key)
    {
        if (!ResourceKeys.TryGetValue(key, out var resourceKey))
            return null;

        if (Application.Current?.TryFindResource(resourceKey) is Geometry geometry)
            return geometry.CloneCurrentValue();

        return null;
    }

    private static Bitmap Render(string key)
    {
        const int size = 16;
        try
        {
            var geometry = ResolveGeometry(key);
            if (geometry == null)
                return new Bitmap(size, size);

            geometry.Freeze();

            var drawing = new GeometryDrawing(
                new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                null,
                geometry);
            drawing.Freeze();

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var bounds = geometry.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    return new Bitmap(size, size);

                var scale = Math.Min((size - 2) / bounds.Width, (size - 2) / bounds.Height);
                dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
                dc.PushTransform(new ScaleTransform(scale, scale));
                dc.PushTransform(new TranslateTransform((size - bounds.Width * scale) / 2, (size - bounds.Height * scale) / 2));
                dc.DrawDrawing(drawing);
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            using var tmp = new Bitmap(stream);
            return new Bitmap(tmp);
        }
        catch
        {
            return new Bitmap(size, size);
        }
    }
}
