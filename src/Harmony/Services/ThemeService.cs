using System.Windows;
using Harmony.Models;

namespace Harmony.Services;

/// <summary>Applies Harmony theme: dark base styles + optional light palette overlay.</summary>
public static class ThemeService
{
    private static readonly Uri LightPaletteUri = new("Themes/LightPalette.xaml", UriKind.Relative);

    public static void Apply(AppTheme theme)
    {
        if (Application.Current == null) return;

        var merged = Application.Current.Resources.MergedDictionaries;

        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString ?? "";
            if (src.Contains("LightPalette", StringComparison.OrdinalIgnoreCase))
                merged.RemoveAt(i);
        }

        if (theme == AppTheme.Light)
            merged.Add(new ResourceDictionary { Source = LightPaletteUri });
    }
}
