using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Localization;
using Harmony.Services.Interfaces;

namespace Harmony.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly HomeViewModel _home;
    private readonly RadioViewModel _radio;
    private readonly SearchViewModel _search;
    private readonly LibraryViewModel _library;
    private readonly CollectionsViewModel _collections;
    private readonly FavoritesViewModel _favorites;
    private readonly HistoryViewModel _history;
    private readonly AlbumDetailViewModel _albumDetail;
    private readonly ArtistDetailViewModel _artistDetail;
    private readonly SettingsViewModel _settings;
    private readonly ProfileViewModel _profile;
    private readonly ISettingsService _settingsService;
    private readonly NavigationService _navigation;
    private readonly ILocalizationService _loc;
    private bool _suppressNavFromSelection;

    public MainViewModel(
        PlayerViewModel player,
        NowPlayingViewModel nowPlaying,
        LyricsViewModel lyrics,
        HomeViewModel home,
        RadioViewModel radio,
        SearchViewModel search,
        LibraryViewModel library,
        CollectionsViewModel collections,
        FavoritesViewModel favorites,
        HistoryViewModel history,
        AlbumDetailViewModel albumDetail,
        ArtistDetailViewModel artistDetail,
        SettingsViewModel settings,
        ProfileViewModel profile,
        ISettingsService settingsService,
        NavigationService navigation,
        ILocalizationService localization)
    {
        Player = player;
        NowPlaying = nowPlaying;
        Lyrics = lyrics;
        _home = home;
        _radio = radio;
        _search = search;
        _library = library;
        _collections = collections;
        _favorites = favorites;
        _history = history;
        _albumDetail = albumDetail;
        _artistDetail = artistDetail;
        _settings = settings;
        _profile = profile;
        _settingsService = settingsService;
        _navigation = navigation;
        _loc = localization;
        _navigation.NavigateRequested += OnNavigateRequested;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new(AppPage.Home, "nav.home", "F1 M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"),
            new(AppPage.Radio, "nav.radio", "F1 M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z"),
            new(AppPage.Search, "nav.search", "F1 M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"),
            new(AppPage.Library, "nav.library", "F1 M4 10v2h12v-2H4zm0-4v2h12V6H4zm0 8v2h8v-2H4zm14-4v8.17c-.31-.11-.65-.17-1-.17-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z"),
            new(AppPage.Playlists, "nav.playlists", "F1 M15 6H3v2h12V6zm0 4H3v2h12v-2zM3 16h8v-2H3v2zM17 6v8.18c-.31-.11-.65-.18-1-.18-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z"),
            new(AppPage.Favorites, "nav.favorites", "F1 M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"),
            new(AppPage.History, "nav.history", "F1 M13 3c-4.97 0-9 4.03-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42C8.27 19.99 10.51 21 13 21c4.97 0 9-4.03 9-9s-4.03-9-9-9zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"),
            new(AppPage.Settings, "nav.settings", "F1 M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z"),
        };

        _loc.LanguageChanged += (_, _) => RefreshNavLabels();

        ApplyProfile(_settingsService.Current);
        _settingsService.SettingsChanged += (_, _) => ApplyProfile(_settingsService.Current);

        _navigation.ResetTo(AppPage.Home);
        SelectedNavigation = NavigationItems[0];
    }

    [ObservableProperty] private bool _isStartupLoading = true;

    public string SplashTitle => AppBranding.Name;
    public string SplashSubtitle => _loc.T("common.loading");
    public string SplashVersion => $"v{UpdateCheckService.CurrentVersion}";

    public async Task StartInitialLoadAsync()
    {
        try
        {
            _ = Player.RestoreLastPlaybackAsync();
            _ = _home.LoadAsync();
            await Task.Delay(500);
        }
        finally
        {
            IsStartupLoading = false;
        }
    }

    public PlayerViewModel Player { get; }
    public SettingsViewModel Settings => _settings;
    public LyricsViewModel Lyrics { get; }
    public NowPlayingViewModel NowPlaying { get; }
    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public ILocalizationService Loc => _loc;

    [ObservableProperty] private ObservableObject? _currentPage;
    [ObservableProperty] private NavigationItem? _selectedNavigation;
    [ObservableProperty] private string _userName = "Guest";
    [ObservableProperty] private string? _avatarPath;
    [ObservableProperty] private bool _isNowPlayingOpen;

    [RelayCommand] private void OpenProfileHome() => OnNavigateRequested(AppPage.Home);
    [RelayCommand] private void OpenProfile() => OnNavigateRequested(AppPage.Profile);
    [RelayCommand] private void OpenProfileSearch() => OnNavigateRequested(AppPage.Search);
    [RelayCommand] private void OpenProfileSettings() => OpenSettingsTab(SettingsTab.Profile);
    [RelayCommand] private void OpenProfileLibrary() => OnNavigateRequested(AppPage.Library);
    [RelayCommand] private void OpenProfileFavorites() => OnNavigateRequested(AppPage.Favorites);
    [RelayCommand] private void OpenProfileHistory() => OnNavigateRequested(AppPage.History);
    [RelayCommand] private void OpenProfilePlaylists() => OnNavigateRequested(AppPage.Playlists);
    [RelayCommand] private void OpenProfileLook() => OpenSettingsTab(SettingsTab.Look);
    [RelayCommand] private void OpenProfilePlayback() => OpenSettingsTab(SettingsTab.Playback);
    [RelayCommand] private async Task OpenProfileLyrics() => await Lyrics.OpenAsync();

    private void OpenSettingsTab(SettingsTab tab)
    {
        _settings.ShowTab(tab);
        Navigate(AppPage.Settings);
        var item = NavigationItems.FirstOrDefault(n => n.Page == AppPage.Settings);
        if (item != null)
            SelectedNavigation = item;
    }

    private void ApplyProfile(UserSettings s)
    {
        UserName = s.UserName;
        AvatarPath = s.AvatarPath;
        ThemeService.Apply(s.Theme);
        _loc.SetLanguage(s.Language);
        RefreshNavLabels();
    }

    private void RefreshNavLabels()
    {
        foreach (var item in NavigationItems)
            item.Label = _loc.NavLabel(item.Page);
    }

    partial void OnSelectedNavigationChanged(NavigationItem? value)
    {
        if (value == null || _suppressNavFromSelection) return;
        Navigate(value.Page);
    }

    private void OnNavigateRequested(AppPage page)
    {
        if (page is not (AppPage.AlbumDetail or AppPage.ArtistDetail))
        {
            var item = NavigationItems.FirstOrDefault(n => n.Page == page);
            if (item != null && !ReferenceEquals(SelectedNavigation, item))
            {
                _suppressNavFromSelection = true;
                SelectedNavigation = item;
                _suppressNavFromSelection = false;
            }
        }

        Navigate(page);
    }

    private void Navigate(AppPage page)
    {
        switch (page)
        {
            case AppPage.Home:
                if (_home.ChartSections.Count == 0 && !_home.IsLoading)
                    _ = _home.LoadAsync();
                CurrentPage = _home;
                break;
            case AppPage.Radio:
                _ = _radio.InitializeAsync();
                CurrentPage = _radio;
                break;
            case AppPage.Search:
                _ = _search.LoadHistoryAsync();
                CurrentPage = _search;
                break;
            case AppPage.Library:
            case AppPage.Albums:
            case AppPage.Collections:
                _ = _library.LoadAsync();
                CurrentPage = _library;
                break;
            case AppPage.Playlists:
                _collections.SelectedKind = CollectionKind.Playlists;
                _ = _collections.OpenAfterNavigationAsync(_navigation.ConsumePendingPlaylistId());
                CurrentPage = _collections;
                break;
            case AppPage.Favorites:
                _ = _favorites.LoadAsync();
                CurrentPage = _favorites;
                break;
            case AppPage.History:
                _ = _history.LoadAsync();
                CurrentPage = _history;
                break;
            case AppPage.AlbumDetail:
                if (_navigation.PendingAlbum != null)
                    _ = _albumDetail.LoadAsync(_navigation.PendingAlbum);
                CurrentPage = _albumDetail;
                break;
            case AppPage.ArtistDetail:
                if (_navigation.PendingArtist != null)
                    _ = _artistDetail.LoadAsync(_navigation.PendingArtist);
                CurrentPage = _artistDetail;
                break;
            case AppPage.Settings:
                _settings.Load();
                CurrentPage = _settings;
                break;
            case AppPage.Profile:
                _ = _profile.LoadAsync();
                CurrentPage = _profile;
                break;
        }
    }
}
