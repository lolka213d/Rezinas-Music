using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;
using Harmony.Views;
using Microsoft.Win32;

namespace Harmony.ViewModels;

public enum CollectionKind { Albums, Playlists }

public enum CollectionTrackSort { Title, Artist, Duration }

public enum CollectionViewMode { Compact, Rich }

/// <summary>Unified albums + playlists page.</summary>
public partial class CollectionsViewModel : ObservableObject
{
    private readonly IAlbumService _albums;
    private readonly IPlaylistService _playlists;
    private readonly ILocalImportService _import;
    private readonly PlayerViewModel _player;
    private readonly NavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly IEnumerable<IMusicSearchService> _providers;
    private readonly ISettingsService _settings;
    private readonly IFavoritesService _favorites;
    private CancellationTokenSource? _addSearchCts;
    private CancellationTokenSource? _addDebounce;
    private HashSet<string> _favoriteKeys = new(StringComparer.Ordinal);

    public CollectionsViewModel(
        IAlbumService albums,
        IPlaylistService playlists,
        ILocalImportService import,
        PlayerViewModel player,
        NavigationService navigation,
        ILocalizationService localization,
        IEnumerable<IMusicSearchService> providers,
        ISettingsService settings,
        IFavoritesService favorites)
    {
        _albums = albums;
        _playlists = playlists;
        _import = import;
        _player = player;
        _navigation = navigation;
        _loc = localization;
        _providers = providers;
        _settings = settings;
        _favorites = favorites;
        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerViewModel.CurrentTrack) or nameof(PlayerViewModel.IsPlaying))
                OnPropertyChanged(nameof(IsSelectionPlaying));
        };
        _settings.SettingsChanged += (_, _) => _ = ReloadAfterExternalSyncAsync();
    }

    private async Task ReloadAfterExternalSyncAsync()
    {
        if (SelectedKind != CollectionKind.Playlists) return;
        var selectedId = SelectedPlaylist?.Id;
        await LoadPlaylistsAsync();
        if (selectedId is int id)
        {
            SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == id);
            await LoadTracksAsync();
        }
    }

    public PlayerViewModel Player => _player;

    public ILocalizationService Loc => _loc;

    public ObservableCollection<Album> UserAlbums { get; } = new();
    public ObservableCollection<Playlist> Playlists { get; } = new();
    public ObservableCollection<PlaylistBrowseCard> PlaylistCards { get; } = new();
    public ObservableCollection<Track> Tracks { get; } = new();
    public ObservableCollection<Track> FilteredTracks { get; } = new();
    public ObservableCollection<CollectionTrackRow> FilteredTrackRows { get; } = new();
    public ObservableCollection<Track> CoverMosaicTracks { get; } = new();
    public ObservableCollection<Track> AddSearchResults { get; } = new();

    private readonly List<CollectionTrackRow> _allTrackRows = new();

    [ObservableProperty] private CollectionKind _selectedKind = CollectionKind.Albums;
    [ObservableProperty] private Album? _selectedAlbum;
    [ObservableProperty] private Playlist? _selectedPlaylist;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _addSearchQuery = string.Empty;
    [ObservableProperty] private string _trackFilterQuery = string.Empty;
    [ObservableProperty] private CollectionTrackSort _trackSort = CollectionTrackSort.Title;
    [ObservableProperty] private CollectionViewMode _viewMode = CollectionViewMode.Rich;
    [ObservableProperty] private bool _favoritesOnly;
    [ObservableProperty] private bool _isAlbumsEmpty;
    [ObservableProperty] private bool _hasAddSearchResults;
    [ObservableProperty] private bool _isSearchingToAdd;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _showAddPanel;
    [ObservableProperty] private bool _isCreateDialogOpen;

    public async Task OpenAfterNavigationAsync(int? playlistId)
    {
        SelectedKind = CollectionKind.Playlists;
        await LoadPlaylistsAsync();
        if (playlistId is int id)
            SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == id);
        else
            SelectedPlaylist = null;
    }

    [RelayCommand]
    private async Task OpenCreatePlaylistDialog()
    {
        if (IsCreateDialogOpen) return;
        var owner = Application.Current.MainWindow as Window;
        if (owner == null) return;

        IsCreateDialogOpen = true;
        try
        {
            var dialog = await Views.CreatePlaylistDialog.ShowAsync(_providers, _settings, owner);
            if (dialog == null) return;

            var pl = await _playlists.CreateAsync(dialog.PlaylistName);
            foreach (var track in dialog.SelectedTracks)
                await _playlists.AddTrackAsync(pl.Id, track);

            await LoadPlaylistsAsync();
            SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == pl.Id);
        }
        finally
        {
            IsCreateDialogOpen = false;
        }
    }

    public bool IsAlbumsMode => SelectedKind == CollectionKind.Albums;
    public bool IsPlaylistsMode => SelectedKind == CollectionKind.Playlists;
    public bool IsCompactView => ViewMode == CollectionViewMode.Compact;
    public bool IsRichView => ViewMode == CollectionViewMode.Rich;
    public bool ShowAddSearch => HasSelection && (IsPlaylistsMode || IsAlbumsMode);

    public bool IsSelectionPlaying =>
        _player.IsPlaying && _player.CurrentTrack != null &&
        Tracks.Any(t => t.Matches(_player.CurrentTrack));

    public string TabAlbumsLabel => _loc.T("nav.albums");
    public string TabPlaylistsLabel => _loc.T("nav.playlists");
    public string TracksLabel => _loc.T("common.tracks");
    public string FindSongsTitle => _loc.T("collections.findSongs");
    public string SearchAddPlaceholder => _loc.T("collections.searchAdd");
    public string PlayAllLabel => _loc.T("collections.playAll");
    public string ShuffleLabel => _loc.T("collections.shuffle");
    public string SearchInListLabel => _loc.T("collections.searchInList");
    public string SortTitleLabel => _loc.T("collections.sortTitle");
    public string SortArtistLabel => _loc.T("collections.sortArtist");
    public string SortDurationLabel => _loc.T("collections.sortDuration");
    public string FavoritesOnlyLabel => _loc.T("collections.favoritesOnly");
    public string ViewCompactLabel => _loc.T("collections.viewCompact");
    public string ViewRichLabel => _loc.T("collections.viewRich");
    public string CreateNewLabel => _loc.T("collections.createNew");
    public string CreateFirstPlaylistTitle => _loc.T("collections.createFirstTitle");
    public string CreateFirstPlaylistDesc => _loc.T("collections.createFirstDesc");
    public string DoneLabel => _loc.T("common.done");
    public string AddLabel => _loc.T("common.add");
    public string AddToPlaylistTip => _loc.T("tip.addPlaylist");
    public string RemoveFromPlaylistTip => _loc.T("tip.removePlaylist");
    public string EmptyListHint => IsAlbumsMode ? _loc.T("collections.emptyAlbums") : _loc.T("collections.emptyPlaylists");
    public string EmptyDetailTitle => IsAlbumsMode ? _loc.T("collections.pickAlbum") : _loc.T("collections.pickPlaylist");
    public string EmptyDetailHint => _loc.T("collections.pickHint");
    public bool IsListEmpty => UserAlbums.Count == 0 && Playlists.Count == 0;
    public bool IsPlaylistsEmpty => Playlists.Count == 0;
    public bool HasAnyCollections => !IsListEmpty;
    public bool ShowEmptyDetail => !HasSelection;
    public bool ShowTrackListTools => HasSelection && Tracks.Count > 0;
    public bool ShowRichColumnHeaders => IsRichView && ShowTrackListTools;
    public string AlbumColumnLabel => _loc.T("collections.albumColumn");
    public bool ShowPlaylistDetail => IsPlaylistsMode && HasSelection;
    public bool ShowPlaylistBrowse => IsPlaylistsMode && !HasSelection;
    public string PlaylistTypeLabel => _loc.T("collections.publicPlaylist");
    public string KindLabel => IsAlbumsMode ? _loc.T("nav.albums").ToUpperInvariant() : PlaylistTypeLabel.ToUpperInvariant();
    public string DateAddedColumnLabel => _loc.T("collections.dateAdded");
    public string BackToPlaylistsLabel => _loc.T("collections.backToList");
    public string OwnerMetaLine => BuildOwnerMetaLine();
    public bool ShowSingleCover => CoverMosaicTracks.Count >= 1 && CoverMosaicTracks.Count < 4;
    public bool ShowMosaicCover => CoverMosaicTracks.Count >= 4;
    public bool ShowDefaultCover => CoverMosaicTracks.Count == 0;
    public string? PrimaryCoverUrl => CoverMosaicTracks.FirstOrDefault()?.ThumbnailUrl;

    private void NotifyCoverProperties()
    {
        OnPropertyChanged(nameof(ShowSingleCover));
        OnPropertyChanged(nameof(ShowMosaicCover));
        OnPropertyChanged(nameof(ShowDefaultCover));
        OnPropertyChanged(nameof(PrimaryCoverUrl));
    }
    public string SelectionTrackCount => $"{Tracks.Count} {TracksLabel}";
    public string TrackTypeLabel => _loc.T("search.typeTrack");
    public string SelectionMetaLine => BuildSelectionMetaLine();

    partial void OnHasSelectionChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPlaylistDetail));
        OnPropertyChanged(nameof(ShowPlaylistBrowse));
        OnPropertyChanged(nameof(ShowEmptyDetail));
    }

    partial void OnSelectedKindChanged(CollectionKind value)
    {
        OnPropertyChanged(nameof(IsAlbumsMode));
        OnPropertyChanged(nameof(IsPlaylistsMode));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(ShowAddSearch));
        OnPropertyChanged(nameof(ShowPlaylistDetail));
        OnPropertyChanged(nameof(ShowPlaylistBrowse));
        NotifyEmptyState();
        NewName = string.Empty;
        StatusMessage = string.Empty;
        TrackFilterQuery = string.Empty;
        FavoritesOnly = false;
        ClearAddSearch();
        _ = LoadAsync();
    }

    partial void OnTrackFilterQueryChanged(string value) => ApplyTrackFilter();
    partial void OnTrackSortChanged(CollectionTrackSort value) => ApplyTrackFilter();
    partial void OnFavoritesOnlyChanged(bool value) => _ = ApplyTrackFilterAsync();
    partial void OnViewModeChanged(CollectionViewMode value)
    {
        OnPropertyChanged(nameof(IsCompactView));
        OnPropertyChanged(nameof(IsRichView));
        OnPropertyChanged(nameof(ShowRichColumnHeaders));
    }

    [RelayCommand]
    private void SelectKind(CollectionKind kind) => SelectedKind = kind;

    [RelayCommand]
    private void SetTrackSort(CollectionTrackSort sort) => TrackSort = sort;

    [RelayCommand]
    private void SetViewMode(CollectionViewMode mode) => ViewMode = mode;

    public async Task LoadAsync()
    {
        if (SelectedKind == CollectionKind.Albums)
            await LoadAlbumsAsync();
        else
            await LoadPlaylistsAsync();
    }

    private async Task LoadAlbumsAsync()
    {
        var selectedId = SelectedAlbum?.Id;
        UserAlbums.Clear();
        foreach (var a in await _albums.GetUserAlbumsAsync())
            UserAlbums.Add(a);

        IsAlbumsEmpty = UserAlbums.Count == 0;
        if (selectedId is > 0)
            SelectedAlbum = UserAlbums.FirstOrDefault(a => a.Id == selectedId);
        else
            SelectedAlbum = null;
        NotifyEmptyState();
    }

    private async Task LoadPlaylistsAsync()
    {
        var selectedId = SelectedPlaylist?.Id;
        Playlists.Clear();
        PlaylistCards.Clear();
        foreach (var p in await _playlists.GetPlaylistsAsync())
        {
            Playlists.Add(p);
            var thumbs = await PlaylistCoverHelper.GetThumbnailsAsync(_playlists, p.Id);
            PlaylistCards.Add(new PlaylistBrowseCard { Playlist = p, Thumbnails = thumbs });
        }

        if (selectedId is > 0)
            SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == selectedId);
        else
            SelectedPlaylist = null;
        OnPropertyChanged(nameof(IsPlaylistsEmpty));
        NotifyEmptyState();
    }

    private void NotifyEmptyState()
    {
        OnPropertyChanged(nameof(IsListEmpty));
        OnPropertyChanged(nameof(IsPlaylistsEmpty));
        OnPropertyChanged(nameof(HasAnyCollections));
        OnPropertyChanged(nameof(EmptyListHint));
        OnPropertyChanged(nameof(EmptyDetailTitle));
        OnPropertyChanged(nameof(ShowEmptyDetail));
    }

    partial void OnSelectedAlbumChanged(Album? value)
    {
        if (value == null) return;
        SelectedKind = CollectionKind.Albums;
        if (SelectedPlaylist != null) SelectedPlaylist = null;
        EditName = value.Name;
        HasSelection = true;
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(ShowAddSearch));
        NotifyEmptyState();
        ClearAddSearch();
        _ = LoadTracksAsync();
    }

    partial void OnSelectedPlaylistChanged(Playlist? value)
    {
        if (value == null)
        {
            HasSelection = false;
            OnPropertyChanged(nameof(ShowPlaylistDetail));
            OnPropertyChanged(nameof(ShowPlaylistBrowse));
            NotifyEmptyState();
            return;
        }
        SelectedKind = CollectionKind.Playlists;
        if (SelectedAlbum != null) SelectedAlbum = null;
        EditName = value.Name;
        HasSelection = true;
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(ShowAddSearch));
        NotifyEmptyState();
        _ = LoadTracksAsync();
    }

    partial void OnAddSearchQueryChanged(string value)
    {
        _addDebounce?.Cancel();
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            AddSearchResults.Clear();
            HasAddSearchResults = false;
            return;
        }

        _addDebounce = new CancellationTokenSource();
        var token = _addDebounce.Token;
        _ = DebouncedAddSearchAsync(token);
    }

    private async Task DebouncedAddSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token);
            await SearchToAddAsync(token);
        }
        catch (OperationCanceledException) { }
    }

    private void ClearAddSearch()
    {
        _addSearchCts?.Cancel();
        AddSearchQuery = string.Empty;
        AddSearchResults.Clear();
        HasAddSearchResults = false;
        IsSearchingToAdd = false;
    }

    private async Task SearchToAddAsync(CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(AddSearchQuery)) return;

        _addSearchCts?.Cancel();
        _addSearchCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var ct = _addSearchCts.Token;

        IsSearchingToAdd = true;
        try
        {
            await _settings.LoadAsync();
            var available = _providers.Where(p => p.IsAvailable).ToList();
            if (available.Count == 0) return;

            var tasks = available.Select(p => SearchProviderAsync(p, AddSearchQuery, ct)).ToList();
            var results = await Task.WhenAll(tasks);
            if (ct.IsCancellationRequested) return;

            var merged = TrackDedup.Merge(results.SelectMany(r => r.Tracks)).Take(40).ToList();
            AddSearchResults.Clear();
            foreach (var t in merged)
                AddSearchResults.Add(t);
            HasAddSearchResults = AddSearchResults.Count > 0;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsSearchingToAdd = false;
        }
    }

    private static async Task<(IMusicSearchService Provider, IReadOnlyList<Track> Tracks)> SearchProviderAsync(
        IMusicSearchService provider, string query, CancellationToken token)
    {
        try
        {
            var tracks = await provider.SearchAsync(query, token);
            return (provider, tracks);
        }
        catch
        {
            return (provider, Array.Empty<Track>());
        }
    }

    private async Task LoadTracksAsync()
    {
        Tracks.Clear();
        _allTrackRows.Clear();
        if (SelectedKind == CollectionKind.Albums && SelectedAlbum != null)
        {
            var i = 1;
            foreach (var t in await _albums.GetTracksAsync(SelectedAlbum.Id))
            {
                Tracks.Add(t);
                _allTrackRows.Add(new CollectionTrackRow
                {
                    Index = i++,
                    Track = t,
                    AddedAtDisplay = "—"
                });
            }
        }
        else if (SelectedKind == CollectionKind.Playlists && SelectedPlaylist != null)
        {
            var i = 1;
            foreach (var entry in await _playlists.GetTrackEntriesAsync(SelectedPlaylist.Id))
            {
                Tracks.Add(entry.Track);
                _allTrackRows.Add(new CollectionTrackRow
                {
                    Index = i++,
                    Track = entry.Track,
                    AddedAtDisplay = FormatRelativeDate(entry.AddedAt)
                });
            }
        }

        CoverMosaicTracks.Clear();
        foreach (var t in Tracks.Take(4))
            CoverMosaicTracks.Add(t);
        NotifyCoverProperties();

        OnPropertyChanged(nameof(SelectionTrackCount));
        OnPropertyChanged(nameof(SelectionMetaLine));
        OnPropertyChanged(nameof(OwnerMetaLine));
        OnPropertyChanged(nameof(ShowTrackListTools));
        OnPropertyChanged(nameof(ShowRichColumnHeaders));
        await ApplyTrackFilterAsync();
    }

    private async Task ApplyTrackFilterAsync()
    {
        if (FavoritesOnly)
        {
            var favs = await _favorites.GetFavoritesAsync();
            _favoriteKeys = favs.Select(t => FavoriteKey(t.Source, t.SourceId)).ToHashSet(StringComparer.Ordinal);
        }

        ApplyTrackFilter();
    }

    private void ApplyTrackFilter()
    {
        IEnumerable<CollectionTrackRow> query = _allTrackRows;

        if (!string.IsNullOrWhiteSpace(TrackFilterQuery))
        {
            var term = TrackFilterQuery.Trim();
            query = query.Where(r =>
                r.Track.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Track.ArtistName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.Track.AlbumName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (FavoritesOnly)
            query = query.Where(r => _favoriteKeys.Contains(FavoriteKey(r.Track.Source, r.Track.SourceId)));

        query = TrackSort switch
        {
            CollectionTrackSort.Artist => query.OrderBy(r => r.Track.ArtistName).ThenBy(r => r.Track.Title),
            CollectionTrackSort.Duration => query.OrderByDescending(r => r.Track.DurationSeconds).ThenBy(r => r.Track.Title),
            _ => query.OrderBy(r => r.Track.Title)
        };

        var list = query.ToList();
        FilteredTracks.Clear();
        FilteredTrackRows.Clear();
        var index = 1;
        foreach (var row in list)
        {
            FilteredTracks.Add(row.Track);
            FilteredTrackRows.Add(new CollectionTrackRow
            {
                Index = index++,
                Track = row.Track,
                AddedAtDisplay = row.AddedAtDisplay
            });
        }
    }

    private static string FavoriteKey(MusicSource source, string sourceId) => $"{source}:{sourceId}";

    private string BuildSelectionMetaLine()
    {
        if (Tracks.Count == 0) return $"0 {TracksLabel}";
        return BuildOwnerMetaLine();
    }

    private string BuildOwnerMetaLine()
    {
        var user = string.IsNullOrWhiteSpace(_settings.Current.UserName) ? "Guest" : _settings.Current.UserName;
        var duration = FormatSpotifyDuration(Tracks.Sum(t => t.DurationSeconds));
        var songs = string.Format(_loc.T("collections.songsCount"), Tracks.Count);
        return $"{user} • {songs}, {duration}";
    }

    private string FormatRelativeDate(DateTime utc)
    {
        var local = utc.ToLocalTime();
        var diff = DateTime.Now - local;
        if (diff.TotalDays < 1) return _loc.T("date.today");
        if (diff.TotalDays < 2) return _loc.T("date.yesterday");
        if (diff.TotalDays < 14) return string.Format(_loc.T("date.daysAgo"), (int)diff.TotalDays);
        return local.ToString("MMM d, yyyy");
    }

    private static string FormatSpotifyDuration(int totalSeconds)
    {
        if (totalSeconds <= 0) return "0 min";
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours} hr {ts.Minutes} min {ts.Seconds} sec";
        return $"{ts.Minutes} min {ts.Seconds} sec";
    }

    [RelayCommand]
    private void SelectPlaylist(PlaylistBrowseCard card) => SelectedPlaylist = card.Playlist;

    [RelayCommand]
    private void GoBackFromPlaylist()
    {
        SelectedPlaylist = null;
        Tracks.Clear();
        _allTrackRows.Clear();
        FilteredTracks.Clear();
        FilteredTrackRows.Clear();
        CoverMosaicTracks.Clear();
        NotifyCoverProperties();
    }

    [RelayCommand]
    private async Task Create()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;

        if (SelectedKind == CollectionKind.Albums)
        {
            var album = await _albums.CreateAsync(NewName);
            NewName = string.Empty;
            await LoadAlbumsAsync();
            SelectedAlbum = UserAlbums.FirstOrDefault(a => a.Id == album.Id);
            StatusMessage = $"\"{album.Name}\"";
        }
        else
        {
            var pl = await _playlists.CreateAsync(NewName);
            NewName = string.Empty;
            await LoadPlaylistsAsync();
            SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == pl.Id);
        }
    }

    [RelayCommand]
    private async Task Rename()
    {
        if (string.IsNullOrWhiteSpace(EditName)) return;

        if (SelectedKind == CollectionKind.Albums && SelectedAlbum != null)
        {
            await _albums.RenameAsync(SelectedAlbum.Id, EditName);
            await LoadAlbumsAsync();
            SelectedAlbum = UserAlbums.FirstOrDefault(a => a.Id == SelectedAlbum?.Id);
        }
        else if (SelectedKind == CollectionKind.Playlists && SelectedPlaylist != null)
        {
            await _playlists.RenameAsync(SelectedPlaylist.Id, EditName);
            await LoadPlaylistsAsync();
            SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == SelectedPlaylist?.Id);
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        var owner = Application.Current.MainWindow as Window;
        if (SelectedKind == CollectionKind.Albums && SelectedAlbum != null)
        {
            if (owner != null && !DarkConfirmDialog.Ask(owner,
                    string.Format(_loc.T("collections.deleteAlbumConfirm"), SelectedAlbum.Name)))
                return;
            await _albums.DeleteAsync(SelectedAlbum.Id);
            SelectedAlbum = null;
            await LoadAlbumsAsync();
        }
        else if (SelectedKind == CollectionKind.Playlists && SelectedPlaylist != null)
        {
            if (owner != null && !DarkConfirmDialog.Ask(owner,
                    string.Format(_loc.T("collections.deletePlaylistConfirm"), SelectedPlaylist.Name)))
                return;
            await _playlists.DeleteAsync(SelectedPlaylist.Id);
            SelectedPlaylist = null;
            await LoadPlaylistsAsync();
        }
    }

    [RelayCommand]
    private async Task AddTrackElsewhere(Track track)
    {
        var owner = Application.Current.MainWindow as Window;
        if (owner == null) return;
        await AddToPlaylistDialog.ShowAsync(_playlists, track, owner);
    }

    [RelayCommand]
    private void ToggleAddPanel()
    {
        ShowAddPanel = !ShowAddPanel;
        if (!ShowAddPanel)
            ClearAddSearch();
    }

    [RelayCommand]
    private async Task AddCurrentTrack()
    {
        if (SelectedKind == CollectionKind.Playlists && SelectedPlaylist == null
            || SelectedKind == CollectionKind.Albums && SelectedAlbum == null)
        {
            StatusMessage = _loc.T("collections.pickPlaylistFirst");
            return;
        }

        if (_player.CurrentTrack == null)
        {
            ShowAddPanel = true;
            StatusMessage = _loc.T("collections.nothingPlaying");
            return;
        }

        if (Tracks.Any(t => t.Matches(_player.CurrentTrack)))
        {
            StatusMessage = string.Format(_loc.T("collections.alreadyInList"), _player.CurrentTrack.Title);
            return;
        }

        if (SelectedKind == CollectionKind.Albums && SelectedAlbum != null)
        {
            await _albums.AddTrackAsync(SelectedAlbum.Id, _player.CurrentTrack);
            await LoadTracksAsync();
            await LoadAlbumsAsync();
        }
        else if (SelectedKind == CollectionKind.Playlists && SelectedPlaylist != null)
        {
            await _playlists.AddTrackAsync(SelectedPlaylist.Id, _player.CurrentTrack);
            await LoadTracksAsync();
        }

        StatusMessage = string.Format(_loc.T("collections.addedTrack"), _player.CurrentTrack.Title);
    }

    [RelayCommand]
    private async Task RemoveTrack(Track track)
    {
        if (SelectedKind == CollectionKind.Albums && SelectedAlbum != null)
        {
            await _albums.RemoveTrackAsync(SelectedAlbum.Id, track.Id);
            await LoadTracksAsync();
            await LoadAlbumsAsync();
        }
        else if (SelectedKind == CollectionKind.Playlists && SelectedPlaylist != null)
        {
            await _playlists.RemoveTrackAsync(SelectedPlaylist.Id, track.Id);
            await LoadTracksAsync();
        }
    }

    [RelayCommand]
    private async Task Play(Track? track)
    {
        if (track == null) return;
        var queue = FilteredTracks.Count > 0 ? FilteredTracks.ToList() : Tracks.ToList();
        if (queue.Count == 0) return;
        await _player.PlayQueueAsync(queue, track);
    }

    [RelayCommand]
    private async Task PlayAll()
    {
        if (Tracks.Count == 0) return;
        var queue = Tracks.ToList();
        await _player.PlayQueueAsync(queue, queue[0]);
    }

    [RelayCommand]
    private async Task ShuffleAll()
    {
        if (Tracks.Count == 0) return;
        var shuffled = Tracks.OrderBy(_ => Random.Shared.Next()).ToList();
        await _player.PlayQueueAsync(shuffled, shuffled[0]);
    }

    [RelayCommand]
    private async Task AddFromSearch(Track track)
    {
        if (SelectedKind == CollectionKind.Albums && SelectedAlbum != null)
        {
            await _albums.AddTrackAsync(SelectedAlbum.Id, track);
            await LoadTracksAsync();
            await LoadAlbumsAsync();
            StatusMessage = string.Format(_loc.T("collections.addedTrack"), track.Title);
        }
        else if (SelectedKind == CollectionKind.Playlists && SelectedPlaylist != null)
        {
            await _playlists.AddTrackAsync(SelectedPlaylist.Id, track);
            await LoadTracksAsync();
            StatusMessage = string.Format(_loc.T("collections.addedTrack"), track.Title);
        }
    }

    [RelayCommand]
    private void OpenAlbum()
    {
        if (SelectedAlbum == null) return;
        _navigation.OpenAlbum(AlbumNavigationContext.FromUserAlbum(SelectedAlbum));
    }

    [RelayCommand]
    private void PickAlbumCover()
    {
        if (SelectedAlbum == null) return;
        var path = PickImageFile();
        if (path == null) return;
        _ = SetAlbumCoverAsync(path);
    }

    private async Task SetAlbumCoverAsync(string path)
    {
        if (SelectedAlbum == null) return;
        await _albums.SetImageAsync(SelectedAlbum.Id, path);
        await LoadAlbumsAsync();
        SelectedAlbum = UserAlbums.FirstOrDefault(a => a.Id == SelectedAlbum?.Id);
    }

    [RelayCommand]
    private async Task ImportFromPc()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("common.import"),
            Filter = "Audio files|*.mp3;*.m4a;*.aac;*.wav;*.flac;*.ogg;*.wma|All files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        if (SelectedAlbum == null)
        {
            var auto = await _albums.CreateAsync($"Imported {DateTime.Now:MMM d, yyyy}");
            await LoadAlbumsAsync();
            SelectedAlbum = UserAlbums.FirstOrDefault(a => a.Id == auto.Id);
        }

        var imported = await _import.ImportFilesAsync(dialog.FileNames, SelectedAlbum?.Id);
        StatusMessage = imported.Count > 0
            ? $"+{imported.Count}"
            : string.Empty;

        if (SelectedAlbum != null)
            await LoadTracksAsync();

        await LoadAlbumsAsync();
    }

    private static string? PickImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
