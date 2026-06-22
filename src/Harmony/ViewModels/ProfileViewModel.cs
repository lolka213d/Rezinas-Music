using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;

namespace Harmony.ViewModels;

public sealed class ProfileAlbumCard
{
    public required string Title { get; init; }
    public string ArtistName { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public int TrackCount { get; init; }
    public Album? UserAlbum { get; init; }
    public IReadOnlyList<Track>? LibraryTracks { get; init; }

    public string Subtitle => TrackCount > 0 ? FormatTracks(TrackCount) : ArtistName;

    private static string FormatTracks(int count) => count switch
    {
        1 => "1 трек",
        >= 2 and <= 4 => $"{count} трека",
        _ => $"{count} треков"
    };

    public static ProfileAlbumCard FromUserAlbum(Album album) => new()
    {
        Title = album.Name,
        ArtistName = album.ArtistName ?? "Various artists",
        ThumbnailUrl = album.ImageUrl,
        TrackCount = album.TrackCount,
        UserAlbum = album
    };
}

public sealed class ProfilePlaylistCard
{
    public required Playlist Playlist { get; init; }
    public string Title => Playlist.Name;
    public int TrackCount => Playlist.TrackCount;

    public string Subtitle => TrackCount switch
    {
        1 => "1 трек",
        >= 2 and <= 4 => $"{TrackCount} трека",
        _ => $"{TrackCount} треков"
    };

    public static ProfilePlaylistCard From(Playlist playlist) => new() { Playlist = playlist };
}

/// <summary>User profile: install date and albums.</summary>
public partial class ProfileViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IAlbumService _albums;
    private readonly IPlaylistService _playlists;
    private readonly ILibraryService _library;
    private readonly NavigationService _navigation;

    public ProfileViewModel(
        ISettingsService settings,
        IAlbumService albums,
        IPlaylistService playlists,
        ILibraryService library,
        NavigationService navigation)
    {
        _settings = settings;
        _navigation = navigation;
        _albums = albums;
        _playlists = playlists;
        _library = library;
        _settings.SettingsChanged += (_, _) => ApplyProfile();
    }

    public ObservableCollection<ProfileAlbumCard> Albums { get; } = new();
    public ObservableCollection<ProfilePlaylistCard> Playlists { get; } = new();

    [ObservableProperty] private string _userName = "Guest";
    [ObservableProperty] private string? _avatarPath;
    [ObservableProperty] private string _installedDisplay = "—";
    [ObservableProperty] private bool _hasAlbums;
    [ObservableProperty] private bool _hasPlaylists;
    [ObservableProperty] private bool _isLoading;

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            ApplyProfile();
            await LoadPlaylistsAsync();
            await LoadAlbumsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyProfile()
    {
        var s = _settings.Current;
        UserName = s.UserName;
        AvatarPath = s.AvatarPath;
        InstalledDisplay = s.InstalledAt.HasValue
            ? s.InstalledAt.Value.ToLocalTime().ToString("d MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture)
            : "—";
    }

    private async Task LoadPlaylistsAsync()
    {
        Playlists.Clear();
        foreach (var playlist in await _playlists.GetPlaylistsAsync())
            Playlists.Add(ProfilePlaylistCard.From(playlist));
        HasPlaylists = Playlists.Count > 0;
    }

    private async Task LoadAlbumsAsync()
    {
        Albums.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var album in await _albums.GetUserAlbumsAsync())
        {
            var card = ProfileAlbumCard.FromUserAlbum(album);
            Albums.Add(card);
            seen.Add(card.Title);
        }

        var library = await _library.GetLibraryAsync();
        foreach (var group in library
                     .Where(t => !string.IsNullOrWhiteSpace(t.AlbumName))
                     .GroupBy(t => t.AlbumName!, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(g => g.Max(t => t.AddedToLibraryAt ?? DateTime.MinValue))
                     .Take(32))
        {
            if (seen.Contains(group.Key)) continue;
            var tracks = group.ToList();
            Albums.Add(new ProfileAlbumCard
            {
                Title = group.Key,
                ArtistName = tracks.Select(t => t.ArtistName).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? "",
                ThumbnailUrl = tracks.Select(t => t.ThumbnailUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)),
                TrackCount = tracks.Count,
                LibraryTracks = tracks
            });
            seen.Add(group.Key);
        }

        HasAlbums = Albums.Count > 0;
    }

    [RelayCommand]
    private void OpenPlaylist(ProfilePlaylistCard card) => _navigation.OpenPlaylist(card.Playlist);

    [RelayCommand]
    private async Task OpenAlbum(ProfileAlbumCard card)
    {
        if (card.UserAlbum != null)
        {
            _navigation.OpenAlbum(AlbumNavigationContext.FromUserAlbum(card.UserAlbum));
            return;
        }

        if (card.LibraryTracks is { Count: > 0 } tracks)
        {
            var focus = tracks[0];
            _navigation.OpenAlbum(AlbumNavigationContext.FromTrackList(card.Title, tracks, focus));
            return;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenSettings() => _navigation.Navigate(AppPage.Settings);
}
