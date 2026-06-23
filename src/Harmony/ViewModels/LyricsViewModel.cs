using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

/// <summary>
/// Full-screen Spotify-style lyrics overlay: synced lines highlight and
/// scroll with playback position.
/// </summary>
public partial class LyricsViewModel : ObservableObject
{
    private readonly ILyricsService _lyricsService;
    private readonly ISettingsService _settings;
    private readonly PlayerViewModel _player;
    private readonly ILocalizationService _loc;
    private readonly UiPerformanceService _uiPerf;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _paletteCts;
    private int _loadGeneration;
    private IReadOnlyList<LyricLineViewModel> _lineList = Array.Empty<LyricLineViewModel>();
    private double _syncScale = 1.0;
    private bool _lyricsAreSynced;
    private double _lyricsMetadataDuration;

    public LyricsViewModel(
        ILyricsService lyricsService,
        ISettingsService settings,
        PlayerViewModel player,
        ILocalizationService localization,
        UiPerformanceService uiPerf)
    {
        _lyricsService = lyricsService;
        _settings = settings;
        _player = player;
        _loc = localization;
        _uiPerf = uiPerf;
        _loc.LanguageChanged += (_, _) => RefreshLocalizedLabels();
        _player.PropertyChanged += OnPlayerChanged;
    }

    public ObservableCollection<LyricLineViewModel> Lines { get; } = new();

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSynced;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _activeLineIndex = -1;
    [ObservableProperty] private string _backgroundColor = CoverPaletteExtractor.Default.Background;
    [ObservableProperty] private string _accentLeftColor = CoverPaletteExtractor.Default.AccentLeft;
    [ObservableProperty] private string _accentRightColor = CoverPaletteExtractor.Default.AccentRight;
    [ObservableProperty] private string _activeTextColor = CoverPaletteExtractor.Default.ActiveText;
    [ObservableProperty] private string _inactiveTextColor = CoverPaletteExtractor.Default.InactiveText;
    [ObservableProperty] private string _pastTextColor = CoverPaletteExtractor.Default.PastText;
    [ObservableProperty] private bool _isLightTheme;

    public bool ShowEmptyMessage => !IsLoading && Lines.Count == 0 && !string.IsNullOrWhiteSpace(StatusMessage);
    public string LoadingLabel => _loc.T("lyrics.loading");
    public string CloseTooltip => _loc.T("lyrics.closeTooltip");

    /// <summary>Raised when the active line changes so the view can scroll.</summary>
    public event EventHandler<int>? ActiveLineChanged;

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsOpen)
        {
            IsOpen = false;
            return;
        }

        await OpenAsync();
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    /// <summary>Open the lyrics overlay and load text for the current track.</summary>
    public async Task OpenAsync()
    {
        IsOpen = true;
        await LoadLyricsAsync();
    }

    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.CurrentTrack))
        {
            if (_player.CurrentTrack != null)
            {
                _ = PreloadLyricsAsync(_player.CurrentTrack);
                _ = UpdatePaletteAsync(_player.CurrentTrack);
            }

            if (IsOpen)
                _ = LoadLyricsAsync();
            return;
        }

        if (e.PropertyName is nameof(PlayerViewModel.PositionSeconds) or nameof(PlayerViewModel.DurationSeconds))
        {
            if (!IsOpen) return;

            if (e.PropertyName == nameof(PlayerViewModel.DurationSeconds))
                RecalculateSyncScale();

            UpdateActiveLine(_player.PositionSeconds);
        }
    }

    partial void OnIsOpenChanged(bool value)
    {
        _uiPerf.LyricsOpen = value;
        if (!value)
        {
            _cts?.Cancel();
            _paletteCts?.Cancel();
            IsLoading = false;
            return;
        }

        if (_player.CurrentTrack != null)
            _ = UpdatePaletteAsync(_player.CurrentTrack);

        if (_player.CurrentTrack == null)
            StatusMessage = _loc.T("lyrics.noActiveTrack");
    }

    private async Task LoadLyricsAsync()
    {
        var track = _player.CurrentTrack;
        if (track == null)
        {
            Lines.Clear();
            StatusMessage = _loc.T("lyrics.noActiveTrack");
            IsLoading = false;
            OnPropertyChanged(nameof(ShowEmptyMessage));
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var generation = Interlocked.Increment(ref _loadGeneration);

        IsLoading = true;
        Lines.Clear();
        OnPropertyChanged(nameof(ShowEmptyMessage));
        StatusMessage = string.Empty;
        ActiveLineIndex = -1;
        _syncScale = 1.0;

        try
        {
            var metadataDuration = EffectiveDuration(track);
            var data = await _lyricsService.GetLyricsAsync(
                track.ArtistName, track.Title, metadataDuration, token);

            if (token.IsCancellationRequested) return;

            if (data == null || data.Lines.Count == 0)
            {
                StatusMessage = _loc.T("lyrics.notFoundLong");
                IsSynced = false;
                OnPropertyChanged(nameof(ShowEmptyMessage));
                return;
            }

            var playbackDuration = _player.DurationSeconds > 0 ? _player.DurationSeconds : metadataDuration;
            var prepared = LyricSyncHelper.Prepare(data, metadataDuration, playbackDuration);
            _lyricsAreSynced = prepared.IsSynced;
            _lyricsMetadataDuration = metadataDuration;
            _syncScale = prepared.SyncScale;
            IsSynced = prepared.IsSynced;

            foreach (var line in prepared.Lines)
                Lines.Add(new LyricLineViewModel(line.Text, line.StartSeconds));

            _lineList = Lines.ToList();
            OnPropertyChanged(nameof(ShowEmptyMessage));
            RecalculateSyncScale();
            UpdateActiveLine(_player.PositionSeconds);
        }
        finally
        {
            if (generation == Volatile.Read(ref _loadGeneration))
                IsLoading = false;
        }
    }

    private double EffectiveDuration(Track? track = null)
    {
        track ??= _player.CurrentTrack;
        if (_player.DurationSeconds > 0) return _player.DurationSeconds;
        return track?.DurationSeconds ?? 0;
    }

    private void RecalculateSyncScale()
    {
        _syncScale = LyricSyncHelper.RecalculateScale(
            _lyricsAreSynced,
            _lyricsMetadataDuration,
            EffectiveDuration());
    }

    private void UpdateActiveLine(double positionSeconds)
    {
        if (!IsOpen || _lineList.Count == 0) return;

        var idx = LyricSyncHelper.FindActiveIndex(
            _lineList,
            positionSeconds,
            _syncScale,
            _settings.Current.LyricsOffsetSeconds);

        if (idx == ActiveLineIndex) return;

        for (var i = 0; i < _lineList.Count; i++)
        {
            var dist = idx < 0 ? Math.Abs(i) : Math.Abs(i - idx);
            _lineList[i].IsActive = i == idx;
            _lineList[i].IsPast = idx >= 0 && i < idx;
            _lineList[i].LineOpacity = i == idx ? 1.0 : Math.Max(0.16, 0.72 - dist * 0.14);
            _lineList[i].FontSize = i == idx ? 38 : Math.Max(24, 34 - dist * 2.5);
        }

        ActiveLineIndex = idx;
        if (idx >= 0)
            ActiveLineChanged?.Invoke(this, idx);
    }

    private void RefreshLocalizedLabels()
    {
        OnPropertyChanged(nameof(LoadingLabel));
        OnPropertyChanged(nameof(CloseTooltip));
        if (_player.CurrentTrack == null && IsOpen)
            StatusMessage = _loc.T("lyrics.noActiveTrack");
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyMessage));
    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(ShowEmptyMessage));

    private Task PreloadLyricsAsync(Track track) =>
        _lyricsService.GetLyricsAsync(track.ArtistName, track.Title, track.DurationSeconds);

    private async Task UpdatePaletteAsync(Track track)
    {
        _paletteCts?.Cancel();
        _paletteCts = new CancellationTokenSource();
        var token = _paletteCts.Token;

        try
        {
            var palette = await CoverPaletteExtractor.ExtractAsync(track.ThumbnailUrl, token);
            if (token.IsCancellationRequested) return;

            BackgroundColor = palette.Background;
            AccentLeftColor = palette.AccentLeft;
            AccentRightColor = palette.AccentRight;
            ActiveTextColor = palette.ActiveText;
            InactiveTextColor = palette.InactiveText;
            PastTextColor = palette.PastText;
            IsLightTheme = palette.IsLight;
        }
        catch
        {
            if (token.IsCancellationRequested) return;
            ApplyDefaultPalette();
        }
    }

    private void ApplyDefaultPalette()
    {
        var palette = CoverPaletteExtractor.Default;
        BackgroundColor = palette.Background;
        AccentLeftColor = palette.AccentLeft;
        AccentRightColor = palette.AccentRight;
        ActiveTextColor = palette.ActiveText;
        InactiveTextColor = palette.InactiveText;
        PastTextColor = palette.PastText;
        IsLightTheme = palette.IsLight;
    }
}
