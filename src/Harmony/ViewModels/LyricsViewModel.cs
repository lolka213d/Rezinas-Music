using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Models;
using Harmony.Services.Interfaces;

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
    private CancellationTokenSource? _cts;
    private int _loadGeneration;
    private IReadOnlyList<LyricLineViewModel> _lineList = Array.Empty<LyricLineViewModel>();
    private double _syncScale = 1.0;

    public LyricsViewModel(ILyricsService lyricsService, ISettingsService settings, PlayerViewModel player)
    {
        _lyricsService = lyricsService;
        _settings = settings;
        _player = player;
        _player.PropertyChanged += OnPlayerChanged;
    }

    public ObservableCollection<LyricLineViewModel> Lines { get; } = new();

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSynced;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _activeLineIndex = -1;

    public bool ShowEmptyMessage => !IsLoading && Lines.Count == 0 && !string.IsNullOrWhiteSpace(StatusMessage);

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
                _ = PreloadLyricsAsync(_player.CurrentTrack);
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
        if (!value)
        {
            _cts?.Cancel();
            IsLoading = false;
            return;
        }

        if (_player.CurrentTrack == null)
            StatusMessage = "Нет активного трека.";
    }

    private async Task LoadLyricsAsync()
    {
        var track = _player.CurrentTrack;
        if (track == null)
        {
            Lines.Clear();
            StatusMessage = "Нет активного трека.";
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
            var duration = EffectiveDuration();
            var data = await _lyricsService.GetLyricsAsync(
                track.ArtistName, track.Title, duration, token);

            if (token.IsCancellationRequested) return;

            if (data == null || data.Lines.Count == 0)
            {
                StatusMessage = "Текст песни не найден.";
                IsSynced = false;
                OnPropertyChanged(nameof(ShowEmptyMessage));
                return;
            }

            IsSynced = data.IsSynced;
            foreach (var line in data.Lines)
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

    private double EffectiveDuration()
    {
        if (_player.DurationSeconds > 0) return _player.DurationSeconds;
        return _player.CurrentTrack?.DurationSeconds ?? 0;
    }

    /// <summary>
    /// When the real track length differs from LRC timestamps (YouTube vs Deezer preview),
    /// scale lyric times to match actual playback.
    /// </summary>
    private void RecalculateSyncScale()
    {
        if (!IsSynced || _lineList.Count == 0)
        {
            _syncScale = 1.0;
            return;
        }

        var duration = EffectiveDuration();
        var lastLrc = _lineList[^1].StartSeconds;
        if (duration > 30 && lastLrc > 15 && Math.Abs(lastLrc - duration) > 8)
            _syncScale = duration / lastLrc;
        else
            _syncScale = 1.0;
    }

    private void UpdateActiveLine(double positionSeconds)
    {
        if (!IsOpen || _lineList.Count == 0) return;

        var adjusted = positionSeconds / _syncScale + _settings.Current.LyricsOffsetSeconds;
        var idx = -1;
        for (var i = _lineList.Count - 1; i >= 0; i--)
        {
            if (adjusted >= _lineList[i].StartSeconds - 0.08)
            {
                idx = i;
                break;
            }
        }

        if (idx == ActiveLineIndex) return;

        for (var i = 0; i < _lineList.Count; i++)
        {
            _lineList[i].IsActive = i == idx;
            _lineList[i].IsPast = i < idx;
        }

        ActiveLineIndex = idx;
        if (idx >= 0)
            ActiveLineChanged?.Invoke(this, idx);
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyMessage));
    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(ShowEmptyMessage));

    private Task PreloadLyricsAsync(Track track) =>
        _lyricsService.GetLyricsAsync(track.ArtistName, track.Title, track.DurationSeconds);
}
