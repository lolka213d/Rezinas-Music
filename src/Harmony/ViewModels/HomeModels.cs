using System.Collections.ObjectModel;
using Harmony.Models;

namespace Harmony.ViewModels;

/// <summary>Quick-access tile on the Home screen (Liked Songs, playlist, library…).</summary>
public sealed class HomeQuickTile
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsLikedSongs { get; init; }
    public bool IsRecent => TargetPage == AppPage.History;
    public bool IsLibrary => TargetPage == AppPage.Library;
    public bool IsRadio => TargetPage == AppPage.Radio;
    public bool IsAlbums => TargetPage is AppPage.Albums or AppPage.Collections;
    public bool IsFavorites => TargetPage == AppPage.Favorites;
    public bool IsPlaylistsNav => TargetPage == AppPage.Playlists;
    public bool IsPlaylist => PlaylistId != null || IsPlaylistsNav;
    public AppPage? TargetPage { get; init; }
    public int? PlaylistId { get; init; }
}

/// <summary>Album card for horizontal rows on Home / Search.</summary>
public sealed class HomeAlbumCard
{
    public string Title { get; init; } = string.Empty;
    public string ArtistName { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? SourceId { get; init; }
    public Track? PlayTrack { get; init; }
}

/// <summary>Artist card for search results.</summary>
public sealed class SearchArtistCard
{
    public string Name { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? SourceId { get; init; }
    public Track? PlayTrack { get; init; }
}

/// <summary>Filter chip on Home (All / Music …).</summary>
public sealed class HomeFilterChip
{
    public HomeFilterChip(string id, string label, bool isEnabled = true)
    {
        Id = id;
        Label = label;
        IsEnabled = isEnabled;
    }

    public string Id { get; }
    public string Label { get; }
    public bool IsEnabled { get; }
}

/// <summary>Home feed tab (Discover / Albums / For you).</summary>
public sealed class HomeTabChip
{
    public HomeTabChip(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
}

/// <summary>Grouped track row on Home (e.g. Top 1–25).</summary>
public sealed class HomeTrackSection
{
    public string Title { get; init; } = string.Empty;
    public ObservableCollection<Track> Tracks { get; } = new();
}

/// <summary>Grouped album row on Home.</summary>
public sealed class HomeAlbumSection
{
    public string Title { get; init; } = string.Empty;
    public ObservableCollection<HomeAlbumCard> Albums { get; } = new();
}

/// <summary>Recommendation / mood mix card on Home.</summary>
public sealed class HomeMixCard
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public Track SeedTrack { get; init; } = null!;
    public IReadOnlyList<Track> Tracks { get; init; } = Array.Empty<Track>();
    public string AccentColor { get; init; } = "#7C3AED";
}
