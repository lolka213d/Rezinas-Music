using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

/// <summary>Aurora Home: genre picks, daily mix, and personalized rows from Deezer + library.</summary>
public partial class HomeViewModel : ObservableObject
{
    private const int TracksPerSection = 40;
    private const int AlbumsPerSection = 24;

    private readonly DeezerHomeService _deezerHome;
    private readonly SampleMusicProvider _sample;
    private readonly PlayerViewModel _player;
    private readonly ILibraryService _library;
    private readonly IFavoritesService _favorites;
    private readonly IHistoryService _history;
    private readonly IPlaylistService _playlists;
    private readonly NavigationService _navigation;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _loc;
    private readonly HomeFeedCache _feedCache;
    private readonly DailyMixCache _dailyMixCache;
    private readonly SmartPlaylistService _smartPlaylists;

    private List<Track> _chartTracks = new();
    private List<Track> _recentTracks = new();
    private List<Track> _favoriteTracks = new();
    private List<HomeAlbumCard> _allAlbums = new();
    private string _loadedFeedLanguage = string.Empty;

    public HomeViewModel(
        DeezerHomeService deezerHome,
        SampleMusicProvider sample,
        PlayerViewModel player,
        ILibraryService library,
        IFavoritesService favorites,
        IHistoryService history,
        IPlaylistService playlists,
        NavigationService navigation,
        ISettingsService settings,
        ILocalizationService localization,
        HomeFeedCache feedCache,
        DailyMixCache dailyMixCache,
        SmartPlaylistService smartPlaylists)
    {
        _deezerHome = deezerHome;
        _sample = sample;
        _player = player;
        _library = library;
        _favorites = favorites;
        _history = history;
        _playlists = playlists;
        _navigation = navigation;
        _settings = settings;
        _loc = localization;
        _feedCache = feedCache;
        _dailyMixCache = dailyMixCache;
        _smartPlaylists = smartPlaylists;
        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerViewModel.CurrentTrack) or nameof(PlayerViewModel.PositionSeconds)
                or nameof(PlayerViewModel.DurationSeconds) or nameof(PlayerViewModel.IsPlaying))
                UpdateContinueListening();
        };
        _settings.SettingsChanged += (_, _) => _ = OnSettingsChangedAsync();

        UserDisplayName = _settings.Current.UserName;

        HomeTabs.Clear();
        HomeTabs.Add(new HomeTabChip("discover", _loc.T("home.tabDiscover")));
        HomeTabs.Add(new HomeTabChip("foryou", _loc.T("home.tabForYou")));
        SelectedTab = HomeTabs[0];
        _loc.LanguageChanged += (_, _) => RefreshHomeTabLabels();
    }

    public ILocalizationService Loc => _loc;
    public string LoadingMoreLabel => _loc.T("home.loadingMore");

    private void RefreshHomeTabLabels()
    {
        if (HomeTabs.Count < 2) return;
        var id = SelectedTab?.Id ?? "discover";
        HomeTabs[0] = new HomeTabChip("discover", _loc.T("home.tabDiscover"));
        HomeTabs[1] = new HomeTabChip("foryou", _loc.T("home.tabForYou"));
        SelectedTab = HomeTabs.FirstOrDefault(t => t.Id == id) ?? HomeTabs[0];
        OnPropertyChanged(nameof(HomeTabs));
        OnPropertyChanged(nameof(LoadingMoreLabel));
    }

    public ObservableCollection<HomeTabChip> HomeTabs { get; } = new();

    public PlayerViewModel Player => _player;

    public string FeaturedTodayLabel => _loc.T("home.featuredToday");
    public string PlayNowLabel => _loc.T("home.playNow");
    public string ShuffleLabel => _loc.T("home.shuffle");
    public string QuickAccessLabel => _loc.T("home.quickAccess");
    public string ContinueListeningLabel => _loc.T("home.continueListening");
    public string RecentlyPlayedLabel => _loc.T("home.recentlyPlayed");
    public string MadeForYouLabel => _loc.T("home.madeForYou");

    public bool IsDiscoverTab => SelectedTab?.Id is "discover" or null;
    public bool IsForYouTab => SelectedTab?.Id == "foryou";

    public ObservableCollection<HomeQuickTile> QuickTiles { get; } = new();
    public ObservableCollection<Track> RecentCards { get; } = new();
    public ObservableCollection<HomeMixCard> RecommendationMixes { get; } = new();
    public ObservableCollection<HomeTrackSection> ChartSections { get; } = new();
    public ObservableCollection<HomeTrackSection> RecentSections { get; } = new();
    public ObservableCollection<HomeTrackSection> FavoriteSections { get; } = new();
    public ObservableCollection<HomeTrackSection> GenreSections { get; } = new();
    public ObservableCollection<HomeAlbumSection> AlbumSections { get; } = new();
    public ObservableCollection<Track> DiscoverTracks { get; } = new();
    public ObservableCollection<HomeQuickTile> BrowseShortcuts { get; } = new();
    public ObservableCollection<HomePlaylistCard> RecommendedPlaylists { get; } = new();

    public string DiscoverTitle => _loc.T("home.tabDiscover");
    public string BrowseSubtitle => _loc.T("home.browseSubtitle");
    public string RecommendedPlaylistsLabel => _loc.T("home.recommendedPlaylists");
    public bool HasBrowseShortcuts => BrowseShortcuts.Count > 0;
    public bool HasRecommendedPlaylists => RecommendedPlaylists.Count > 0;

    [ObservableProperty] private string _greeting = string.Empty;
    [ObservableProperty] private string _userDisplayName = "Guest";
    [ObservableProperty] private string _greetingSubtitle = string.Empty;
    [ObservableProperty] private Track? _featuredTrack;
    [ObservableProperty] private Track? _continueTrack;
    [ObservableProperty] private double _continueProgress;
    [ObservableProperty] private HomeTabChip? _selectedTab;
    [ObservableProperty] private HomeMixCard? _dailyMix;
    [ObservableProperty] private bool _hasDailyMix;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private bool _hasFeatured;
    [ObservableProperty] private bool _hasContinue;
    [ObservableProperty] private bool _hasRecentCards;
    [ObservableProperty] private bool _hasRecommendations;
    [ObservableProperty] private bool _hasRecentSections;
    [ObservableProperty] private bool _hasFavoriteSections;
    [ObservableProperty] private bool _hasChartSections;
    [ObservableProperty] private bool _hasAlbumSections;
    [ObservableProperty] private bool _hasGenreSections;

    private CancellationTokenSource? _loadCts;

    public async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        Greeting = DateTime.Now.Hour switch
        {
            < 12 => _loc.T("home.greetingMorning"),
            < 18 => _loc.T("home.greetingAfternoon"),
            _ => _loc.T("home.greetingEvening")
        };
        GreetingSubtitle = _loc.T("home.greetingSubtitle");

        try
        {
            var lang = _settings.Current.Language;
            _loadedFeedLanguage = lang;
            var cachedTask = _feedCache.ReadAsync(lang);
            var quickTask = Task.CompletedTask;
            var recentTask = _history.GetRecentTracksAsync(24);
            var favoritesTask = _favorites.GetFavoritesAsync(60);

            await Task.WhenAll(cachedTask, quickTask, recentTask, favoritesTask);
            if (token.IsCancellationRequested) return;

            _recentTracks = (await recentTask).ToList();
            _favoriteTracks = (await favoritesTask).DistinctBy(t => t.Source + t.SourceId).ToList();

            var cached = await cachedTask;
            if (cached is { Tracks.Count: > 0 })
            {
                _chartTracks = cached.Tracks;
                _allAlbums = HomeFeedCache.ToAlbumCards(cached.Albums);
            }
            else
            {
                _chartTracks = _sample.GetCatalog().Take(36).ToList();
                _allAlbums = [];
            }

            ApplyTabContentAsync();

            _ = LoadGenreSectionsAsync(lang, token);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }

        if (!token.IsCancellationRequested)
            _ = RefreshChartsFromNetworkAsync(token);
    }

    private async Task RefreshChartsFromNetworkAsync(CancellationToken parentToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        var token = cts.Token;

        try
        {
            var lang = _settings.Current.Language;
            var tracksTask = _deezerHome.GetRegionalChartTracksAsync(lang, 1, 40, token);
            var albumsTask = _deezerHome.GetRegionalChartAlbumsAsync(lang, 1, 24, token);
            await Task.WhenAll(tracksTask, albumsTask);
            if (token.IsCancellationRequested) return;

            var tracks = (await tracksTask).ToList();
            if (tracks.Count == 0) return;

            _chartTracks = tracks;
            _allAlbums = await BuildAlbumsFromFavoritesAsync((await albumsTask).ToList(), _favoriteTracks);
            await _feedCache.SaveAsync(lang, _chartTracks, _allAlbums);
            ApplyTabContentAsync();

            _ = LoadGenreSectionsAsync(lang, token);

            _ = LoadMoreInBackgroundAsync(token);
        }
        catch (OperationCanceledException) { }
    }

    private async Task LoadMoreInBackgroundAsync(CancellationToken parentToken)
    {
        IsLoadingMore = true;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            var token = cts.Token;

            var tracksTask = _deezerHome.GetRegionalChartTracksAsync(_settings.Current.Language, 2, 80, token);
            var albumsTask = _deezerHome.GetRegionalChartAlbumsAsync(_settings.Current.Language, 1, 50, token);
            await Task.WhenAll(tracksTask, albumsTask);
            if (token.IsCancellationRequested) return;

            var tracks = (await tracksTask).ToList();
            if (tracks.Count > 0)
                _chartTracks = tracks;

            var albums = (await albumsTask).ToList();
            if (albums.Count > 0)
                _allAlbums = await BuildAlbumsFromFavoritesAsync(albums, _favoriteTracks);

            ApplyTabContentAsync();

            _ = LoadGenreSectionsAsync(_settings.Current.Language, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private async Task OnSettingsChangedAsync()
    {
        UserDisplayName = _settings.Current.UserName;
        var lang = _settings.Current.Language;
        if (!string.Equals(lang, _loadedFeedLanguage, StringComparison.OrdinalIgnoreCase))
        {
            _loadedFeedLanguage = lang;
            await LoadAsync();
            return;
        }

        ApplyTabContentAsync();
    }

    partial void OnSelectedTabChanged(HomeTabChip? value)
    {
        ApplyTabContentAsync();
        OnPropertyChanged(nameof(IsDiscoverTab));
        OnPropertyChanged(nameof(IsForYouTab));
    }

    [RelayCommand]
    private void SelectTab(HomeTabChip tab) => SelectedTab = tab;

    private void ApplyTabContentAsync() => _ = ApplyTabContentCoreAsync();

    private async Task ApplyTabContentCoreAsync()
    {
        ChartSections.Clear();
        RecentSections.Clear();
        FavoriteSections.Clear();
        AlbumSections.Clear();

        var tab = SelectedTab?.Id ?? "discover";

        switch (tab)
        {
            case "foryou":
                BuildTrackSections(RecentSections, _recentTracks, _loc.T("nav.history"));
                BuildTrackSections(FavoriteSections, _favoriteTracks, _loc.T("nav.favorites"));
                BuildRecommendations();
                _ = LoadSmartPlaylistsAsync();
                break;
            default:
                BuildTrackSections(ChartSections, _chartTracks.Take(50).ToList(), _loc.T("home.trending"));
                if (_settings.Current.ShowHomeAlbums)
                    BuildAlbumSections(AlbumSections, _allAlbums.Take(AlbumsPerSection).ToList());
                BuildRecommendations();
                break;
        }

        HasChartSections = ChartSections.Count > 0;
        HasRecentSections = RecentSections.Count > 0;
        HasFavoriteSections = FavoriteSections.Count > 0;
        HasAlbumSections = AlbumSections.Count > 0;
        HasGenreSections = GenreSections.Count > 0;

        DiscoverTracks.Clear();
        BuildBrowseShortcuts();
        OnPropertyChanged(nameof(HasBrowseShortcuts));
        _ = LoadRecommendedPlaylistsAsync();

        await UpdateDashboardAsync();
    }

    private async Task LoadRecommendedPlaylistsAsync()
    {
        try
        {
            RecommendedPlaylists.Clear();
            foreach (var p in (await _playlists.GetPlaylistsAsync()).Take(10))
            {
                IReadOnlyList<string?> thumbs = Array.Empty<string?>();
                try
                {
                    var tracks = await _playlists.GetTracksAsync(p.Id);
                    thumbs = tracks
                        .Select(t => t.ThumbnailUrl)
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .Take(4)
                        .Cast<string?>()
                        .ToList();
                }
                catch { /* cover is optional */ }

                RecommendedPlaylists.Add(new HomePlaylistCard
                {
                    Playlist = p,
                    Thumbnails = thumbs
                });
            }
            OnPropertyChanged(nameof(HasRecommendedPlaylists));
        }
        catch { /* ignore */ }
    }

    private async Task LoadGenreSectionsAsync(string lang, CancellationToken token)
    {
        try
        {
            GenreSections.Clear();
            foreach (var (genreId, key) in GenreChartMap.ForLanguage(lang))
            {
                if (token.IsCancellationRequested) return;
                var tracks = await _deezerHome.GetGenreRadioTracksAsync(genreId, 18, token);
                if (tracks.Count == 0) continue;

                var section = new HomeTrackSection { Title = _loc.T(key) };
                foreach (var t in tracks)
                    section.Tracks.Add(t);
                GenreSections.Add(section);
            }

            HasGenreSections = GenreSections.Count > 0;
        }
        catch (OperationCanceledException) { }
    }

    private async Task UpdateDashboardAsync()
    {
        FeaturedTrack = _player.CurrentTrack ?? _chartTracks.FirstOrDefault() ?? _recentTracks.FirstOrDefault();
        HasFeatured = FeaturedTrack != null;
        OnPropertyChanged(nameof(ShowFeaturedSpotlight));

        RecentCards.Clear();
        foreach (var t in _recentTracks.Take(12))
            RecentCards.Add(t);
        HasRecentCards = RecentCards.Count > 0;

        BuildRecommendations();
        await BuildDailyMixAsync();
        UpdateContinueListening();
    }

    private async Task LoadSmartPlaylistsAsync()
    {
        try
        {
            foreach (var smart in await _smartPlaylists.BuildAsync())
            {
                if (smart.Tracks.Count < 3) continue;
                RecommendationMixes.Add(new HomeMixCard
                {
                    Title = _loc.T(smart.TitleKey),
                    Subtitle = _loc.T(smart.SubtitleKey),
                    SeedTrack = smart.Tracks[0],
                    Tracks = smart.Tracks.ToList(),
                    AccentColor = "#6366F1"
                });
            }
            HasRecommendations = RecommendationMixes.Count > 0;
        }
        catch { /* optional */ }
    }

    private async Task BuildDailyMixAsync()
    {
        var pool = _recentTracks
            .Concat(_favoriteTracks)
            .DistinctBy(t => $"{t.Source}:{t.SourceId}")
            .ToList();

        if (pool.Count < 5)
        {
            DailyMix = null;
            HasDailyMix = false;
            return;
        }

        var dayKey = RadioDailySeed.TodayKey;
        var cached = await _dailyMixCache.ReadAsync(dayKey).ConfigureAwait(false);
        List<Track> tracks;
        if (cached is { Tracks.Count: > 0 })
        {
            tracks = cached.Tracks;
        }
        else
        {
            tracks = RadioDailySeed.ShuffleForDay(pool, "daily-mix", dayKey).Take(24).ToList();
            await _dailyMixCache.SaveAsync(dayKey, tracks).ConfigureAwait(false);
        }

        DailyMix = new HomeMixCard
        {
            Title = _loc.T("home.dailyMix"),
            Subtitle = string.Format(_loc.T("home.dailyMixTracks"), tracks.Count),
            SeedTrack = tracks[0],
            Tracks = tracks,
            AccentColor = "#8B5CF6"
        };
        HasDailyMix = true;
    }

    private void UpdateContinueListening()
    {
        if (_player.CurrentTrack is { } cur && _player.DurationSeconds > 0
            && _player.PositionSeconds > 12
            && _player.PositionSeconds < _player.DurationSeconds - 8)
        {
            ContinueTrack = cur;
            ContinueProgress = _player.PositionSeconds / _player.DurationSeconds;
            HasContinue = true;
            OnPropertyChanged(nameof(ShowFeaturedSpotlight));
            return;
        }

        var s = _settings.Current;
        if (s.LastPlayedSource is int rawSource
            && Enum.IsDefined(typeof(MusicSource), rawSource)
            && !string.IsNullOrWhiteSpace(s.LastPlayedSourceId)
            && s.LastPlayedPositionSeconds > 12)
        {
            var saved = _recentTracks.FirstOrDefault(t =>
                t.Source == (MusicSource)rawSource && t.SourceId == s.LastPlayedSourceId);
            if (saved != null)
            {
                ContinueTrack = saved;
                ContinueProgress = saved.DurationSeconds > 0
                    ? Math.Clamp(s.LastPlayedPositionSeconds / saved.DurationSeconds, 0, 1)
                    : 0;
                HasContinue = true;
                OnPropertyChanged(nameof(ShowFeaturedSpotlight));
                return;
            }
        }

        ContinueTrack = null;
        ContinueProgress = 0;
        HasContinue = false;
        OnPropertyChanged(nameof(ShowFeaturedSpotlight));
    }

    /// <summary>Featured hero is hidden when continue-listening is shown to avoid duplicate single-track rows.</summary>
    public bool ShowFeaturedSpotlight => HasFeatured && !HasContinue;

    private void BuildRecommendations()
    {
        RecommendationMixes.Clear();
        if (_chartTracks.Count < 6) { HasRecommendations = false; return; }

        var mixes = new[]
        {
            ("home.mixChill", "home.mixChillSub", "#7C3AED", 0),
            ("home.mixFocus", "home.mixFocusSub", "#38BDF8", 5),
            ("home.mixNight", "home.mixNightSub", "#22C55E", 10),
        };

        foreach (var (titleKey, subtitleKey, color, offset) in mixes)
        {
            if (offset + 5 > _chartTracks.Count) continue;
            var chunk = _chartTracks.Skip(offset).Take(20).ToList();
            RecommendationMixes.Add(new HomeMixCard
            {
                Title = _loc.T(titleKey),
                Subtitle = _loc.T(subtitleKey),
                SeedTrack = chunk[0],
                Tracks = chunk,
                AccentColor = color
            });
        }

        HasRecommendations = RecommendationMixes.Count > 0;
    }

    private static void BuildTrackSections(
        ObservableCollection<HomeTrackSection> target, IReadOnlyList<Track> tracks, string prefix)
    {
        if (tracks.Count == 0) return;

        var sectionCount = (tracks.Count + TracksPerSection - 1) / TracksPerSection;
        for (var i = 0; i < sectionCount; i++)
        {
            var start = i * TracksPerSection;
            var chunk = tracks.Skip(start).Take(TracksPerSection).ToList();
            if (chunk.Count == 0) continue;

            var end = start + chunk.Count;
            var section = new HomeTrackSection
            {
                Title = sectionCount == 1 ? prefix : $"{prefix} · {start + 1}–{end}"
            };
            foreach (var t in chunk)
                section.Tracks.Add(t);
            target.Add(section);
        }
    }

    private static void BuildAlbumSections(ObservableCollection<HomeAlbumSection> target, IReadOnlyList<HomeAlbumCard> albums)
    {
        if (albums.Count == 0) return;

        var sectionCount = (albums.Count + AlbumsPerSection - 1) / AlbumsPerSection;
        for (var i = 0; i < sectionCount; i++)
        {
            var start = i * AlbumsPerSection;
            var chunk = albums.Skip(start).Take(AlbumsPerSection).ToList();
            if (chunk.Count == 0) continue;

            var end = start + chunk.Count;
            var section = new HomeAlbumSection
            {
                Title = sectionCount == 1 ? "Popular albums" : $"Popular albums · {start + 1}–{end}"
            };
            foreach (var a in chunk)
                section.Albums.Add(a);
            target.Add(section);
        }
    }

    private void BuildBrowseShortcuts()
    {
        BrowseShortcuts.Clear();
        BrowseShortcuts.Add(new HomeQuickTile
        {
            Title = _loc.T("nav.radio"),
            Subtitle = _loc.T("radio.subtitle"),
            TargetPage = AppPage.Radio
        });
        BrowseShortcuts.Add(new HomeQuickTile
        {
            Title = _loc.T("home.browsePlaylists"),
            Subtitle = _loc.T("library.playlistsSubtitle"),
            TargetPage = AppPage.Playlists
        });
        BrowseShortcuts.Add(new HomeQuickTile
        {
            Title = _loc.T("home.browseFavorites"),
            Subtitle = _loc.T("favorites.subtitle"),
            TargetPage = AppPage.Favorites,
            IsLikedSongs = true
        });
        BrowseShortcuts.Add(new HomeQuickTile
        {
            Title = _loc.T("home.browseHistory"),
            Subtitle = _loc.T("library.historySubtitle"),
            TargetPage = AppPage.History
        });
        BrowseShortcuts.Add(new HomeQuickTile
        {
            Title = _loc.T("nav.library"),
            Subtitle = _loc.T("library.subtitle"),
            TargetPage = AppPage.Library
        });
    }

    private async Task BuildQuickTilesAsync()
    {
        QuickTiles.Clear();

        QuickTiles.Add(new HomeQuickTile
        {
            Title = "Liked Songs",
            Subtitle = "Your favorites",
            IsLikedSongs = true,
            TargetPage = AppPage.Favorites
        });

        var playlists = await _playlists.GetPlaylistsAsync();
        foreach (var p in playlists.Take(2))
        {
            QuickTiles.Add(new HomeQuickTile
            {
                Title = p.Name,
                Subtitle = "Playlist",
                PlaylistId = p.Id
            });
        }

        QuickTiles.Add(new HomeQuickTile
        {
            Title = "Recently Played",
            Subtitle = "Pick up where you left off",
            TargetPage = AppPage.History
        });

        QuickTiles.Add(new HomeQuickTile
        {
            Title = _loc.T("home.browseAlbums"),
            Subtitle = _loc.T("nav.albums"),
            TargetPage = AppPage.Albums
        });

        QuickTiles.Add(new HomeQuickTile
        {
            Title = "Your Library",
            Subtitle = "All saved music",
            TargetPage = AppPage.Library
        });
    }

    private Task<List<HomeAlbumCard>> BuildAlbumsFromFavoritesAsync(
        List<HomeAlbumCard> chartAlbums, IReadOnlyList<Track>? favorites = null)
    {
        favorites ??= Array.Empty<Track>();
        var byAlbum = favorites
            .Where(t => !string.IsNullOrWhiteSpace(t.AlbumName))
            .GroupBy(t => t.AlbumName!)
            .Select(g => new HomeAlbumCard
            {
                Title = g.Key,
                ArtistName = g.First().ArtistName,
                ThumbnailUrl = g.First().ThumbnailUrl,
                PlayTrack = g.First()
            })
            .ToList();

        var merged = byAlbum
            .Concat(chartAlbums)
            .DistinctBy(a => !string.IsNullOrWhiteSpace(a.SourceId) ? $"deezer:{a.SourceId}" : $"{a.Title}|{a.ArtistName}")
            .ToList();
        return Task.FromResult(merged);
    }

    [RelayCommand]
    private void OpenBrowseShortcut(HomeQuickTile tile) => _navigation.Navigate(tile.TargetPage ?? AppPage.Home);

    [RelayCommand]
    private async Task OpenQuickTile(HomeQuickTile tile)
    {
        if (tile.TargetPage is AppPage page)
        {
            _navigation.Navigate(page);
            return;
        }

        if (tile.PlaylistId is int pid)
        {
            var tracks = await _playlists.GetTracksAsync(pid);
            if (tracks.Count > 0)
                await _player.PlayQueueAsync(tracks, tracks[0]);
        }
    }

    [RelayCommand]
    private void OpenSearch() => _navigation.Navigate(AppPage.Search);

    [RelayCommand]
    private void SelectPlaylistFromHome(HomePlaylistCard card) =>
        _navigation.OpenPlaylist(card.Playlist);

    [RelayCommand]
    private void OpenSettings() => _navigation.Navigate(AppPage.Settings);

    [RelayCommand]
    private async Task PlayFeatured()
    {
        if (FeaturedTrack == null) return;
        await PlaySectionTrack(FeaturedTrack);
    }

    [RelayCommand]
    private async Task ShuffleFeatured()
    {
        if (_chartTracks.Count == 0) return;
        var shuffled = _chartTracks.OrderBy(_ => Random.Shared.Next()).Take(25).ToList();
        _navigation.OpenAlbum(AlbumNavigationContext.FromTrackList("Featured mix", shuffled, shuffled[0]));
        await _player.PlayQueueAsync(shuffled, shuffled[0]);
    }

    [RelayCommand]
    private async Task ContinueListening()
    {
        if (ContinueTrack == null) return;
        await PlaySectionTrack(ContinueTrack);
    }

    [RelayCommand]
    private Task FindSimilar(Track track) => _player.PlaySimilarCommand.ExecuteAsync(track);

    [RelayCommand]
    private async Task PlayMix(HomeMixCard mix)
    {
        var tracks = mix.Tracks.ToList();
        _navigation.OpenAlbum(AlbumNavigationContext.FromTrackList(mix.Title, tracks, mix.SeedTrack));
        await _player.PlayQueueAsync(tracks, mix.SeedTrack);
    }

    [RelayCommand]
    private async Task OpenRecent(Track track)
    {
        if (!string.IsNullOrWhiteSpace(track.AlbumName))
        {
            _navigation.OpenAlbum(new AlbumNavigationContext
            {
                Title = track.AlbumName,
                ArtistName = track.ArtistName,
                ThumbnailUrl = track.ThumbnailUrl,
                FallbackTrack = track
            });
            await _player.PlayQueueAsync(new[] { track }, track);
            return;
        }

        var (title, queue) = FindSectionForTrack(track);
        if (queue.Count == 0)
            queue = RecentSections.SelectMany(s => s.Tracks).ToList();

        if (queue.Count == 0)
            queue = new List<Track> { track };

        _navigation.OpenAlbum(AlbumNavigationContext.FromTrackList(
            title == track.Title ? "Recently played" : title, queue, track));
        await _player.PlayQueueAsync(queue, track);
    }

    [RelayCommand]
    private async Task PlaySectionTrack(Track track)
    {
        if (track == null) return;
        var (title, queue) = FindSectionForTrack(track);
        if (queue.Count == 0)
            queue = new List<Track> { track };
        _navigation.OpenAlbum(AlbumNavigationContext.FromTrackList(title, queue, track));
        await _player.PlayQueueAsync(queue, track);
    }

    private (string Title, List<Track> Queue) FindSectionForTrack(Track track)
    {
        var section = ChartSections.FirstOrDefault(s => s.Tracks.Contains(track))
            ?? RecentSections.FirstOrDefault(s => s.Tracks.Contains(track))
            ?? FavoriteSections.FirstOrDefault(s => s.Tracks.Contains(track));

        if (section != null)
            return (section.Title, section.Tracks.ToList());

        return (track.Title, new List<Track> { track });
    }

    [RelayCommand]
    private Task OpenAlbum(HomeAlbumCard album) =>
        AlbumPlaybackHelper.OpenAndPlayAsync(album, _deezerHome, _navigation, _player);

    [RelayCommand]
    private Task AddToLibrary(Track track) => _library.AddToLibraryAsync(track);

    [RelayCommand]
    private Task Like(Track track) => _favorites.ToggleAsync(track);
}
