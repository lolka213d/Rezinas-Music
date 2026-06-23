using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

public sealed class ProfileAlbumCard
{
    public required string Title { get; init; }
    public string ArtistName { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public int TrackCount { get; init; }
    public Album? UserAlbum { get; init; }
    public IReadOnlyList<Track>? LibraryTracks { get; init; }

    public required string Subtitle { get; init; }

    public static ProfileAlbumCard FromUserAlbum(Album album, string subtitle) => new()
    {
        Title = album.Name,
        ArtistName = album.ArtistName ?? "Various artists",
        ThumbnailUrl = album.ImageUrl,
        TrackCount = album.TrackCount,
        UserAlbum = album,
        Subtitle = subtitle
    };
}

public sealed class ProfilePlaylistCard
{
    public required Playlist Playlist { get; init; }
    public IReadOnlyList<string?> Thumbnails { get; init; } = [];

    public string Title => Playlist.Name;
    public int TrackCount => Playlist.TrackCount;
    public required string Subtitle { get; init; }

    public string? Thumbnail0 => Thumbnails.Count > 0 ? Thumbnails[0] : null;
    public string? Thumbnail1 => Thumbnails.Count > 1 ? Thumbnails[1] : null;
    public string? Thumbnail2 => Thumbnails.Count > 2 ? Thumbnails[2] : null;
    public string? Thumbnail3 => Thumbnails.Count > 3 ? Thumbnails[3] : null;

    public static ProfilePlaylistCard From(Playlist playlist, IReadOnlyList<string?> thumbnails, string subtitle) =>
        new() { Playlist = playlist, Thumbnails = thumbnails, Subtitle = subtitle };
}

/// <summary>User profile: install date and albums.</summary>
public partial class ProfileViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IAlbumService _albums;
    private readonly IPlaylistService _playlists;
    private readonly ILibraryService _library;
    private readonly NavigationService _navigation;
    private readonly ILocalizationService _loc;

    public ProfileViewModel(
        ISettingsService settings,
        IAlbumService albums,
        IPlaylistService playlists,
        ILibraryService library,
        NavigationService navigation,
        ILocalizationService localization)
    {
        _settings = settings;
        _navigation = navigation;
        _albums = albums;
        _playlists = playlists;
        _library = library;
        _loc = localization;
        _settings.SettingsChanged += (_, _) => ApplyProfile();
        _loc.LanguageChanged += (_, _) =>
        {
            RefreshCountLabels();
            _ = LoadAsync();
        };
    }

    public ILocalizationService Loc => _loc;
    public string PlaylistsCountLabel => string.Format(_loc.T("profile.countShort"), Playlists.Count);
    public string AlbumsCountLabel => string.Format(_loc.T("profile.countShort"), Albums.Count);

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
        {
            var thumbs = await PlaylistCoverHelper.GetThumbnailsAsync(_playlists, playlist.Id);
            Playlists.Add(ProfilePlaylistCard.From(playlist, thumbs, FormatTrackCount(playlist.TrackCount)));
        }
        HasPlaylists = Playlists.Count > 0;
        RefreshCountLabels();
    }

    private async Task LoadAlbumsAsync()
    {
        Albums.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var album in await _albums.GetUserAlbumsAsync())
        {
            var card = ProfileAlbumCard.FromUserAlbum(album, FormatTrackCount(album.TrackCount));
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
                LibraryTracks = tracks,
                Subtitle = FormatTrackCount(tracks.Count)
            });
            seen.Add(group.Key);
        }

        HasAlbums = Albums.Count > 0;
        RefreshCountLabels();
    }

    private void RefreshCountLabels()
    {
        OnPropertyChanged(nameof(PlaylistsCountLabel));
        OnPropertyChanged(nameof(AlbumsCountLabel));
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

    private string FormatTrackCount(int count) =>
        string.Format(_loc.T("collections.songsCount"), count);
}
