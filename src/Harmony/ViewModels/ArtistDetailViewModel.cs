using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

public partial class ArtistDetailViewModel : ObservableObject
{
    private readonly DeezerHomeService _deezer;
    private readonly PlayerViewModel _player;
    private readonly NavigationService _navigation;
    private readonly ILocalizationService _loc;

    public ArtistDetailViewModel(
        DeezerHomeService deezer,
        PlayerViewModel player,
        NavigationService navigation,
        ILocalizationService localization)
    {
        _deezer = deezer;
        _player = player;
        _navigation = navigation;
        _loc = localization;
    }

    public ILocalizationService Loc => _loc;

    public ObservableCollection<Track> TopTracks { get; } = new();
    public ObservableCollection<HomeAlbumCard> Albums { get; } = new();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _thumbnailUrl;
    [ObservableProperty] private string _metaLine = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasContent;

    public async Task LoadAsync(ArtistNavigationContext context)
    {
        Name = context.Name;
        ThumbnailUrl = context.ThumbnailUrl;
        IsLoading = true;
        TopTracks.Clear();
        Albums.Clear();
        HasContent = false;

        try
        {
            ArtistPageData? data = null;
            if (!string.IsNullOrWhiteSpace(context.DeezerArtistId))
                data = await _deezer.GetArtistPageAsync(context.DeezerArtistId);
            else
            {
                var id = await _deezer.FindArtistIdAsync(context.Name);
                if (!string.IsNullOrWhiteSpace(id))
                    data = await _deezer.GetArtistPageAsync(id);
            }

            if (data != null)
            {
                Name = data.Name;
                ThumbnailUrl ??= data.PictureUrl;
                MetaLine = data.Fans > 0 ? string.Format(_loc.T("artist.fansCount"), data.Fans) : Name;
                foreach (var t in data.TopTracks) TopTracks.Add(t);
                foreach (var a in data.Albums) Albums.Add(a);
            }

            HasContent = TopTracks.Count > 0 || Albums.Count > 0;
            if (!HasContent)
                MetaLine = _loc.T("artist.notFound");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task Play() => TopTracks.Count > 0
        ? _player.PlayQueueAsync(TopTracks, TopTracks[0])
        : Task.CompletedTask;

    [RelayCommand]
    private Task PlayTrack(Track track) => _player.PlayQueueAsync(TopTracks, track);

    [RelayCommand]
    private Task OpenAlbum(HomeAlbumCard album) =>
        AlbumPlaybackHelper.OpenAndPlayAsync(album, _deezer, _navigation, _player);

    [RelayCommand]
    private void GoBack() => _navigation.GoBack();
}
