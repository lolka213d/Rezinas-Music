using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;
using Microsoft.Win32;

namespace Harmony.ViewModels;

/// <summary>Library page with artist filtering.</summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryService _library;
    private readonly ILocalImportService _import;
    private readonly PlayerViewModel _player;
    private readonly IFavoritesService _favorites;
    private readonly IPlaylistService _playlists;
    private readonly SmartPlaylistService _smart;
    private readonly ILocalizationService _loc;

    private List<Track> _allTracks = new();

    public LibraryViewModel(
        ILibraryService library,
        ILocalImportService import,
        PlayerViewModel player,
        IFavoritesService favorites,
        IPlaylistService playlists,
        SmartPlaylistService smart,
        ILocalizationService localization)
    {
        _library = library;
        _import = import;
        _player = player;
        _favorites = favorites;
        _playlists = playlists;
        _smart = smart;
        _loc = localization;
        _loc.LanguageChanged += (_, _) => _ = LoadAsync();
    }

    public ILocalizationService Loc => _loc;

    public string AllArtistsLabel => _loc.T("common.allArtists");

    public string ImportFolderLabel => _loc.T("library.importFolder");

    public ObservableCollection<Track> Tracks { get; } = new();
    public ObservableCollection<string> Artists { get; } = new();
    public ObservableCollection<SmartPlaylistCard> SmartPlaylists { get; } = new();

    [ObservableProperty] private bool _hasSmartPlaylists;

    [ObservableProperty] private string _selectedArtist = string.Empty;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _totalTracks;
    [ObservableProperty] private int _totalArtists;
    [ObservableProperty] private int _totalAlbums;
    [ObservableProperty] private string _totalDurationDisplay = "0:00";

    public string StatTracksLabel => _loc.T("library.statTracks");
    public string StatArtistsLabel => _loc.T("library.statArtists");
    public string StatAlbumsLabel => _loc.T("library.statAlbums");
    public string SongsTitleLabel => _loc.T("library.songs");
    public string SmartMixesLabel => _loc.T("library.smartMixes");
    public string PlayAllLabel => _loc.T("collections.playAll");
    public bool HasTracks => _allTracks.Count > 0;
    public string StatsLine => string.Format(_loc.T("library.statsLine"), TotalTracks, TotalArtists, TotalAlbums, TotalDurationDisplay);

    public async Task LoadAsync()
    {
        _allTracks = (await _library.GetLibraryAsync()).ToList();

        Artists.Clear();
        Artists.Add(AllArtistsLabel);
        foreach (var a in _allTracks.Select(t => t.ArtistName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
            Artists.Add(a);

        if (!Artists.Contains(SelectedArtist))
            SelectedArtist = AllArtistsLabel;

        ApplyFilter();

        SmartPlaylists.Clear();
        foreach (var mix in await _smart.BuildAsync())
        {
            if (mix.Tracks.Count == 0) continue;
            SmartPlaylists.Add(new SmartPlaylistCard
            {
                Title = _loc.T(mix.TitleKey),
                Subtitle = _loc.T(mix.SubtitleKey),
                Tracks = mix.Tracks
            });
        }
        HasSmartPlaylists = SmartPlaylists.Count > 0;
        UpdateStats();
        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(StatsLine));
    }

    private void UpdateStats()
    {
        TotalTracks = _allTracks.Count;
        TotalArtists = _allTracks.Select(t => t.ArtistName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Count();
        TotalAlbums = _allTracks.Select(t => t.AlbumName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Count();
        var secs = _allTracks.Sum(t => t.DurationSeconds);
        TotalDurationDisplay = FormatDuration(secs);
        OnPropertyChanged(nameof(StatsLine));
        OnPropertyChanged(nameof(HasTracks));
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "0:00";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    partial void OnSelectedArtistChanged(string value) => ApplyFilter();
    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Tracks.Clear();
        IEnumerable<Track> filtered = SelectedArtist == AllArtistsLabel
            ? _allTracks
            : _allTracks.Where(t => t.ArtistName == SelectedArtist);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            filtered = filtered.Where(t =>
                t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || t.ArtistName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (t.AlbumName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        foreach (var t in filtered) Tracks.Add(t);
        IsEmpty = Tracks.Count == 0;
    }

    [RelayCommand]
    private Task PlayAll()
    {
        if (_allTracks.Count == 0) return Task.CompletedTask;
        return _player.PlayQueueAsync(_allTracks, _allTracks[0]);
    }

    [RelayCommand]
    private Task ShuffleAll()
    {
        if (_allTracks.Count == 0) return Task.CompletedTask;
        var shuffled = _allTracks.OrderBy(_ => Random.Shared.Next()).ToList();
        return _player.PlayQueueAsync(shuffled, shuffled[0]);
    }

    [RelayCommand]
    private Task Play(Track track) => _player.PlayQueueAsync(Tracks, track);

    [RelayCommand]
    private async Task Remove(Track? track)
    {
        if (track == null || track.Id <= 0) return;
        await _library.RemoveFromLibraryAsync(track.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private Task Like(Track track) => _favorites.ToggleAsync(track);

    [RelayCommand]
    private async Task AddToPlaylist(Track? track)
    {
        if (track == null) return;
        var owner = Application.Current.MainWindow;
        if (owner != null)
            await Views.AddToPlaylistDialog.ShowAsync(_playlists, track, owner);
    }

    [RelayCommand]
    private Task PlaySmart(SmartPlaylistCard card) =>
        card.Tracks.Count > 0 ? _player.PlayQueueAsync(card.Tracks, card.Tracks[0]) : Task.CompletedTask;

    [RelayCommand]
    private async Task ImportFromPc()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import songs from your PC",
            Filter = "Audio files|*.mp3;*.m4a;*.aac;*.wav;*.flac;*.ogg;*.wma|All files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        var imported = await _import.ImportFilesAsync(dialog.FileNames);
        StatusMessage = imported.Count > 0
            ? $"Imported {imported.Count} song(s)."
            : "No files imported.";

        await LoadAsync();
    }

    [RelayCommand]
    private async Task ImportFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder with MP3/audio files",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var files = Directory.EnumerateFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories)
            .Where(f => LocalImportService.IsSupportedExtension(Path.GetExtension(f)))
            .ToList();
        if (files.Count == 0)
        {
            StatusMessage = "No audio files found in folder.";
            return;
        }

        var imported = await _import.ImportFilesAsync(files);
        StatusMessage = imported.Count > 0
            ? $"Imported {imported.Count} song(s) from folder."
            : "No files imported.";

        await LoadAsync();
    }
}
