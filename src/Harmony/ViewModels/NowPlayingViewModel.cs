using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;
using Harmony.Views;

namespace Harmony.ViewModels;

public enum NowPlayingTab
{
    NowPlaying,
    Queue,
    Lyrics
}

/// <summary>
/// Backs the right-hand context panel: now playing, queue, lyrics.
/// </summary>
public partial class NowPlayingViewModel : ObservableObject
{
    private readonly LyricsViewModel _lyrics;
    private readonly DeezerHomeService _deezer;
    private readonly NavigationService _navigation;
    private readonly IPlaylistService _playlists;
    private readonly ILocalizationService _loc;
    private CancellationTokenSource? _contextCts;

    public NowPlayingViewModel(
        PlayerViewModel player,
        LyricsViewModel lyrics,
        DeezerHomeService deezer,
        NavigationService navigation,
        IPlaylistService playlists,
        ILocalizationService localization)
    {
        Player = player;
        _lyrics = lyrics;
        _deezer = deezer;
        _navigation = navigation;
        _playlists = playlists;
        _loc = localization;
        Player.PropertyChanged += OnPlayerPropertyChanged;
        Player.UpNext.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasQueueItems));
            OnPropertyChanged(nameof(IsQueueEmpty));
        };

        if (Player.CurrentTrack != null)
            _ = LoadContextAsync();
    }

    public PlayerViewModel Player { get; }

    public ObservableCollection<TrackCreditLine> Credits { get; } = new();

    [ObservableProperty] private NowPlayingTab _selectedTab = NowPlayingTab.NowPlaying;
    [ObservableProperty] private string _primaryArtistName = string.Empty;
    [ObservableProperty] private string? _artistImageUrl;
    [ObservableProperty] private string _fansLine = string.Empty;
    [ObservableProperty] private string? _artistBio;
    [ObservableProperty] private string _trackSpotifyUrl = string.Empty;
    [ObservableProperty] private string _primaryArtistSpotifyUrl = string.Empty;
    [ObservableProperty] private string? _deezerArtistId;
    [ObservableProperty] private bool _isLoadingContext;

    public bool IsNowPlayingTab => SelectedTab == NowPlayingTab.NowPlaying;
    public bool IsQueueTab => SelectedTab == NowPlayingTab.Queue;
    public bool IsLyricsTab => SelectedTab == NowPlayingTab.Lyrics;
    public bool HasTrack => Player.CurrentTrack != null;
    public bool HasQueueItems => Player.UpNext.Count > 0;
    public bool IsQueueEmpty => !HasQueueItems;

    public string AboutArtistLabel => _loc.Language == "ru" ? "Об исполнителе" : "About the artist";
    public string CreditsLabel => _loc.Language == "ru" ? "Участники" : "Credits";
    public string ShowAllCreditsLabel => _loc.Language == "ru" ? "Показать все" : "Show all";
    public string OpenSpotifyLabel => _loc.Language == "ru" ? "Spotify" : "Spotify";
    public string QueueEmptyTitle => _loc.Language == "ru" ? "Очередь пуста" : "Your queue is empty";
    public string SearchNewLabel => _loc.Language == "ru" ? "Найти что-нибудь новое" : "Search for something new";
    public string FollowSpotifyLabel => _loc.Language == "ru" ? "Открыть в Spotify" : "Open in Spotify";

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.CurrentTrack))
        {
            SelectedTab = NowPlayingTab.NowPlaying;
            _ = LoadContextAsync();
            OnPropertyChanged(nameof(HasTrack));
        }

        if (e.PropertyName is nameof(PlayerViewModel.UpNext) or nameof(PlayerViewModel.QueueCount))
        {
            OnPropertyChanged(nameof(HasQueueItems));
            OnPropertyChanged(nameof(IsQueueEmpty));
        }
    }

    private async Task LoadContextAsync()
    {
        _contextCts?.Cancel();
        _contextCts?.Dispose();
        _contextCts = new CancellationTokenSource();
        var ct = _contextCts.Token;

        var track = Player.CurrentTrack;
        if (track == null)
        {
            ClearContext();
            return;
        }

        IsLoadingContext = true;
        try
        {
            var ctx = await _deezer.GetTrackContextAsync(track, ct);
            if (ct.IsCancellationRequested) return;

            PrimaryArtistName = ctx.PrimaryArtistName;
            DeezerArtistId = ctx.DeezerArtistId;
            ArtistImageUrl = ctx.ArtistImageUrl;
            FansLine = ctx.FansLine;
            ArtistBio = ctx.ArtistBio;
            TrackSpotifyUrl = ctx.TrackSpotifyUrl;
            PrimaryArtistSpotifyUrl = ctx.PrimaryArtistSpotifyUrl;

            Credits.Clear();
            foreach (var line in ctx.Credits)
                Credits.Add(line);
        }
        catch
        {
            if (!ct.IsCancellationRequested)
                ClearContext();
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsLoadingContext = false;
        }
    }

    private void ClearContext()
    {
        PrimaryArtistName = string.Empty;
        DeezerArtistId = null;
        ArtistImageUrl = null;
        FansLine = string.Empty;
        ArtistBio = null;
        TrackSpotifyUrl = string.Empty;
        PrimaryArtistSpotifyUrl = string.Empty;
        Credits.Clear();
        IsLoadingContext = false;
    }

    partial void OnSelectedTabChanged(NowPlayingTab value)
    {
        OnPropertyChanged(nameof(IsNowPlayingTab));
        OnPropertyChanged(nameof(IsQueueTab));
        OnPropertyChanged(nameof(IsLyricsTab));
    }

    [RelayCommand]
    private void SelectNowPlaying() => SelectedTab = NowPlayingTab.NowPlaying;

    [RelayCommand]
    private void SelectQueue() => SelectedTab = NowPlayingTab.Queue;

    [RelayCommand]
    private void SelectLyrics() => SelectedTab = NowPlayingTab.Lyrics;

    [RelayCommand]
    private async Task OpenFullLyrics()
    {
        SelectedTab = NowPlayingTab.Lyrics;
        await _lyrics.OpenAsync();
    }

    [RelayCommand]
    private void OpenSpotifyUrl(string? url) => OpenBrowser(url);

    [RelayCommand]
    private void OpenTrackOnSpotify() => OpenBrowser(TrackSpotifyUrl);

    [RelayCommand]
    private void OpenPrimaryArtistOnSpotify() => OpenBrowser(PrimaryArtistSpotifyUrl);

    [RelayCommand]
    private void OpenCreditOnSpotify(TrackCreditLine? credit)
    {
        if (credit != null)
            OpenBrowser(credit.SpotifyUrl);
    }

    [RelayCommand]
    private void OpenArtistPage()
    {
        if (string.IsNullOrWhiteSpace(PrimaryArtistName)) return;
        _navigation.OpenArtist(new ArtistNavigationContext
        {
            DeezerArtistId = DeezerArtistId,
            Name = PrimaryArtistName,
            ThumbnailUrl = ArtistImageUrl
        });
    }

    [RelayCommand]
    private async Task AddCurrentToPlaylist()
    {
        var track = Player.CurrentTrack;
        var owner = Application.Current.MainWindow as Window;
        if (track == null || owner == null) return;
        await AddToPlaylistDialog.ShowAsync(_playlists, track, owner);
    }

    [RelayCommand]
    private void GoSearch() => _navigation.Navigate(AppPage.Search);

    private static void OpenBrowser(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }
}
