using System.Collections.ObjectModel;

using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using Harmony.Models;

using Harmony.Services;

using Harmony.Services.Interfaces;

using Harmony.Services.Localization;



namespace Harmony.ViewModels;



public enum SearchTab { All, Tracks, Albums, Artists }

public enum SearchSourceFilter { All, Deezer, Local }

public enum SearchDurationFilter { Any, Short, Medium, Long }



/// <summary>Search with tabs, dedup, live search.</summary>

public partial class SearchViewModel : ObservableObject

{

    private readonly IEnumerable<IMusicSearchService> _providers;

    private readonly DeezerHomeService _deezer;

    private readonly ISettingsService _settings;

    private readonly ISearchHistoryService _searchHistory;

    private readonly PlayerViewModel _player;

    private readonly ILibraryService _library;

    private readonly IFavoritesService _favorites;

    private readonly IPlaylistService _playlists;

    private readonly NavigationService _navigation;

    private readonly ILocalizationService _loc;

    private CancellationTokenSource? _cts;

    private CancellationTokenSource? _debounce;



    private List<Track> _allTracks = new();

    private List<HomeAlbumCard> _allAlbums = new();

    private List<SearchArtistCard> _allArtists = new();



    public SearchViewModel(

        IEnumerable<IMusicSearchService> providers,

        DeezerHomeService deezer,

        ISettingsService settings,

        ISearchHistoryService searchHistory,

        PlayerViewModel player,

        ILibraryService library,

        IFavoritesService favorites,

        IPlaylistService playlists,

        NavigationService navigation,

        ILocalizationService localization)

    {

        _providers = providers;

        _deezer = deezer;

        _settings = settings;

        _searchHistory = searchHistory;

        _player = player;

        _library = library;

        _favorites = favorites;

        _playlists = playlists;

        _navigation = navigation;

        _loc = localization;

        _loc.LanguageChanged += (_, _) => RefreshLocalizedText();

        StatusMessage = string.Empty;

    }



    public ILocalizationService Loc => _loc;



    public ObservableCollection<Track> Results { get; } = new();

    public ObservableCollection<HomeAlbumCard> AlbumResults { get; } = new();

    public ObservableCollection<SearchArtistCard> ArtistResults { get; } = new();

    public ObservableCollection<string> RecentQueries { get; } = new();

    public SearchTab[] Tabs { get; } = Enum.GetValues<SearchTab>();



    [ObservableProperty] private string _query = string.Empty;

    [ObservableProperty] private SearchTab _selectedTab = SearchTab.All;

    [ObservableProperty] private bool _isSearching;

    [ObservableProperty] private bool _hasAlbums;

    [ObservableProperty] private bool _hasArtists;

    [ObservableProperty] private bool _hasTracks;

    [ObservableProperty] private bool _hasRecentQueries;
    [ObservableProperty] private Track? _featuredTrack;

    [ObservableProperty] private SearchSourceFilter _sourceFilter = SearchSourceFilter.All;

    [ObservableProperty] private SearchDurationFilter _durationFilter = SearchDurationFilter.Any;

    [ObservableProperty] private bool _hasSearched;

    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<string> Suggestions { get; } = new();

    public bool ShowResultsPage => HasSearched;
    public bool ShowIdleSearch => !HasSearched;
    public bool ShowSuggestions => !HasSearched && Suggestions.Count > 0 && !string.IsNullOrWhiteSpace(Query);
    public string SearchPlaceholder => _loc.T("search.placeholderHarmony");
    public string ResultsTitle => _loc.T("search.resultsFor");
    public string ResultsSubtitle => string.IsNullOrWhiteSpace(Query) ? string.Empty : string.Format(_loc.T("search.forQuery"), Query);



    public string RecentSearchesLabel => _loc.T("search.recent");



    private void RefreshLocalizedText()

    {

        OnPropertyChanged(nameof(TabAllLabel));

        OnPropertyChanged(nameof(TabTracksLabel));

        OnPropertyChanged(nameof(TabAlbumsLabel));

        OnPropertyChanged(nameof(TabArtistsLabel));

        OnPropertyChanged(nameof(RecentSearchesLabel));

    }



    public string TabAllLabel => _loc.SearchTabLabel(SearchTab.All);

    public string TabTracksLabel => _loc.SearchTabLabel(SearchTab.Tracks);

    public string TabAlbumsLabel => _loc.SearchTabLabel(SearchTab.Albums);

    public string TabArtistsLabel => _loc.SearchTabLabel(SearchTab.Artists);

    public string FilterAllSourcesLabel => _loc.T("search.filterAll");
    public string FilterDeezerLabel => _loc.T("search.filterDeezer");
    public string FilterLocalLabel => _loc.T("search.filterLocal");
    public string FilterAnyDurationLabel => _loc.T("search.filterAnyDuration");
    public string FilterShortLabel => _loc.T("search.filterShort");
    public string FilterMediumLabel => _loc.T("search.filterMedium");
    public string FilterLongLabel => _loc.T("search.filterLong");
    public bool HasFeaturedTrack => FeaturedTrack != null;
    public string FeaturedSubtitle => FeaturedTrack == null
        ? string.Empty
        : $"Трек • {FeaturedTrack.ArtistName}";
    public string TopResultLabel => _loc.T("search.topResult");
    public string TrackTypeLabel => _loc.T("search.typeTrack");

    public SearchSourceFilter[] SourceFilters { get; } = Enum.GetValues<SearchSourceFilter>();
    public SearchDurationFilter[] DurationFilters { get; } = Enum.GetValues<SearchDurationFilter>();



    public async Task LoadHistoryAsync()
    {
        await _searchHistory.ClearAsync();
        RecentQueries.Clear();
        HasRecentQueries = false;
    }



    [RelayCommand]

    private void SelectTab(SearchTab tab) => SelectedTab = tab;



    [RelayCommand]

    private async Task SearchFromHistory(string query)

    {

        Query = query;

        await SearchAsync();

    }



    [RelayCommand]

    private async Task ClearSearchHistory()

    {

        await _searchHistory.ClearAsync();

        await LoadHistoryAsync();

    }



    partial void OnQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            HasSearched = false;
            Suggestions.Clear();
            OnPropertyChanged(nameof(ShowSuggestions));
            OnPropertyChanged(nameof(ShowResultsPage));
            OnPropertyChanged(nameof(ShowIdleSearch));
            return;
        }

        UpdateSuggestions();

        _debounce?.Cancel();
        if (value.Trim().Length < 2) return;

        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        _ = DebouncedSearchAsync(token);
    }

    private void UpdateSuggestions()
    {
        Suggestions.Clear();
        var q = Query.Trim();
        if (q.Length < 1) return;

        if (!Suggestions.Contains(q))
            Suggestions.Add(q);

        OnPropertyChanged(nameof(ShowSuggestions));
    }

    [RelayCommand]
    private void ClearQuery()
    {
        Query = string.Empty;
        HasSearched = false;
    }

    [RelayCommand]
    private void BackFromResults()
    {
        HasSearched = false;
        Results.Clear();
        AlbumResults.Clear();
        ArtistResults.Clear();
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task PickSuggestion(string suggestion)
    {
        Query = suggestion;
        await SearchAsync();
    }

    [RelayCommand]
    private async Task RemoveHistoryItem(string query)
    {
        await _searchHistory.RemoveAsync(query);
        await LoadHistoryAsync();
        UpdateSuggestions();
    }



    private async Task DebouncedSearchAsync(CancellationToken token)

    {

        try

        {

            await Task.Delay(450, token);

            await SearchAsync();

        }

        catch (OperationCanceledException) { }

    }



    partial void OnSelectedTabChanged(SearchTab value) => ApplyTabFilter();

    partial void OnSourceFilterChanged(SearchSourceFilter value) => ApplyTabFilter();

    partial void OnDurationFilterChanged(SearchDurationFilter value) => ApplyTabFilter();

    [RelayCommand]
    private void SelectSourceFilter(SearchSourceFilter filter) => SourceFilter = filter;

    [RelayCommand]
    private void SelectDurationFilter(SearchDurationFilter filter) => DurationFilter = filter;

    [RelayCommand]
    private Task FindSimilar(Track track) => _player.PlaySimilarCommand.ExecuteAsync(track);



    [RelayCommand]

    private async Task SearchAsync()

    {

        if (string.IsNullOrWhiteSpace(Query)) return;



        _cts?.Cancel();

        _cts = new CancellationTokenSource();

        var token = _cts.Token;



        await _settings.LoadAsync();

        IsSearching = true;

        StatusMessage = _loc.T("search.searching");



        try

        {

            var available = _providers.Where(p => p.IsAvailable).ToList();

            if (available.Count == 0)

            {

                StatusMessage = _loc.T("search.noSources");

                return;

            }



            var trackTasks = available.Select(p => SearchProviderAsync(p, Query, token)).ToList();

            var albumTask = _deezer.SearchAlbumsAsync(Query, token);

            var artistTask = _deezer.SearchArtistsAsync(Query, token);

            await Task.WhenAll(trackTasks.Cast<Task>().Append(albumTask).Append(artistTask));



            if (token.IsCancellationRequested) return;



            _allTracks = TrackDedup.Merge((await Task.WhenAll(trackTasks)).SelectMany(r => r.Tracks)).ToList();

            _allAlbums = (await albumTask).ToList();

            _allArtists = (await artistTask).ToList();



            ApplyTabFilter();

            await _searchHistory.ClearAsync();
            RecentQueries.Clear();
            HasRecentQueries = false;
            Suggestions.Clear();
            OnPropertyChanged(nameof(ShowSuggestions));

            StatusMessage = string.Format(_loc.T("search.found"), _allTracks.Count, _allAlbums.Count, _allArtists.Count);
            HasSearched = true;
            OnPropertyChanged(nameof(ShowResultsPage));
            OnPropertyChanged(nameof(ShowIdleSearch));
            OnPropertyChanged(nameof(ResultsSubtitle));

        }

        finally

        {

            IsSearching = false;

        }

    }



    private void ApplyTabFilter()

    {

        Results.Clear();

        AlbumResults.Clear();

        ArtistResults.Clear();

        var tracks = FilterTracks(_allTracks);



        switch (SelectedTab)

        {

            case SearchTab.Tracks:

                foreach (var t in tracks) Results.Add(t);

                break;

            case SearchTab.Albums:

                foreach (var a in _allAlbums) AlbumResults.Add(a);

                break;

            case SearchTab.Artists:

                foreach (var a in _allArtists) ArtistResults.Add(a);

                break;

            default:

                foreach (var t in tracks) Results.Add(t);

                foreach (var a in _allAlbums) AlbumResults.Add(a);

                foreach (var a in _allArtists) ArtistResults.Add(a);

                break;

        }



        HasTracks = Results.Count > 0;

        HasAlbums = AlbumResults.Count > 0;

        HasArtists = ArtistResults.Count > 0;

        FeaturedTrack = SelectedTab is SearchTab.Albums or SearchTab.Artists
            ? null
            : tracks.FirstOrDefault();
        OnPropertyChanged(nameof(HasFeaturedTrack));
        OnPropertyChanged(nameof(FeaturedSubtitle));
    }

    private IEnumerable<Track> FilterTracks(IEnumerable<Track> tracks)
    {
        var filtered = tracks;
        filtered = SourceFilter switch
        {
            SearchSourceFilter.Deezer => filtered.Where(t => t.Source == MusicSource.Deezer),
            SearchSourceFilter.Local => filtered.Where(t => t.Source == MusicSource.Local),
            _ => filtered
        };
        filtered = DurationFilter switch
        {
            SearchDurationFilter.Short => filtered.Where(t => t.DurationSeconds > 0 && t.DurationSeconds < 180),
            SearchDurationFilter.Medium => filtered.Where(t => t.DurationSeconds >= 180 && t.DurationSeconds <= 360),
            SearchDurationFilter.Long => filtered.Where(t => t.DurationSeconds > 360),
            _ => filtered
        };
        return filtered;
    }



    private async Task<(IMusicSearchService Provider, IReadOnlyList<Track> Tracks, string? Error)> SearchProviderAsync(

        IMusicSearchService provider, string query, CancellationToken token)

    {

        try

        {

            var tracks = await provider.SearchAsync(query, token);

            return (provider, tracks, provider.LastError);

        }

        catch (Exception ex)

        {

            return (provider, Array.Empty<Track>(), ex.Message);

        }

    }



    [RelayCommand]
    private Task Play(Track track)
    {
        var queue = Results.Count > 0 ? Results.ToList() : _allTracks;
        return _player.PlayQueueAsync(queue, track);
    }

    [RelayCommand]
    private Task PlayFeatured()
    {
        if (FeaturedTrack == null) return Task.CompletedTask;
        var queue = Results.Count > 0 ? Results.ToList() : _allTracks.ToList();
        return _player.PlayQueueAsync(queue, FeaturedTrack);
    }



    [RelayCommand]

    private Task OpenAlbum(HomeAlbumCard album) =>

        AlbumPlaybackHelper.OpenAndPlayAsync(album, _deezer, _navigation, _player);



    [RelayCommand]

    private void OpenArtist(SearchArtistCard artist) =>

        _navigation.OpenArtist(ArtistNavigationContext.FromCard(artist));



    [RelayCommand]

    private Task AddToLibrary(Track track) => _library.AddToLibraryAsync(track);



    [RelayCommand]

    private Task Like(Track track) => _favorites.ToggleAsync(track);



    [RelayCommand]

    private async Task AddToPlaylist(Track track)

    {

        var owner = Application.Current.MainWindow;

        if (owner != null)

            await Views.AddToPlaylistDialog.ShowAsync(_playlists, track, owner);

    }

}


