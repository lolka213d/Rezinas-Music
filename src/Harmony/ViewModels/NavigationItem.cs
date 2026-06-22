using CommunityToolkit.Mvvm.ComponentModel;

namespace Harmony.ViewModels;

/// <summary>The navigable sections of the app.</summary>
public enum AppPage
{
    Home,
    Radio,
    Search,
    Library,
    Albums,
    Collections,
    AlbumDetail,
    ArtistDetail,
    History,
    Favorites,
    Playlists,
    Profile,
    Settings
}

/// <summary>A single entry in the sidebar navigation list.</summary>
public partial class NavigationItem : ObservableObject
{
    public NavigationItem(AppPage page, string labelKey, string iconData)
    {
        Page = page;
        LabelKey = labelKey;
        IconData = iconData;
    }

    public AppPage Page { get; }
    public string LabelKey { get; }

    [ObservableProperty] private string _label = string.Empty;

    /// <summary>Vector path geometry (mini-language) shown as the sidebar icon.</summary>
    public string IconData { get; }
}
