using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Harmony.Helpers;

public static class VisualTreeEffectHelper
{
    public static void StripDropShadows(DependencyObject root)
    {
        if (root is UIElement el && el.Effect is DropShadowEffect)
            el.Effect = null;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            StripDropShadows(VisualTreeHelper.GetChild(root, i));
    }

    public static void EnableBitmapCache(DependencyObject root, int depth = 0)
    {
        if (depth > 6) return;

        if (root is UIElement el)
        {
            RenderOptions.SetBitmapScalingMode(el, BitmapScalingMode.LowQuality);
            if (el is Panel or Border { IsHitTestVisible: false })
                el.CacheMode = new BitmapCache(1.0);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            EnableBitmapCache(VisualTreeHelper.GetChild(root, i), depth + 1);
    }
}
