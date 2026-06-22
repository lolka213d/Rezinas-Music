using System.Windows;

namespace Harmony.Services;

/// <summary>Applies global UI preferences from settings.</summary>
public static class UiPreferences
{
    public static void ApplyCompactLists(bool compact)
    {
        if (Application.Current?.Resources == null) return;
        var key = compact ? "TrackListBoxCompact" : "TrackListBox";
        Application.Current.Resources["ActiveTrackListStyle"] =
            Application.Current.TryFindResource(key) ?? Application.Current.FindResource("TrackListBox");
    }
}
