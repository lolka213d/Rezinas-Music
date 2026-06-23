using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

/// <summary>
/// Drives the bottom player bar: queue, shuffle, radio, transport, volume.
/// </summary>
public partial class PlayerViewModel : ObservableObject
{
    private readonly IAudioPlayerService _player;
    private readonly IHistoryService _history;
    private readonly IFavoritesService _favorites;
    private readonly ISettingsService _settings;
    private readonly IStreamResolverService _streams;
    private readonly DeezerHomeService _deezer;
    private readonly LastFmScrobbler _lastFm;
    private readonly RadioPlaybackContext _radioContext;
    private readonly PersonalWaveService _personalWave;
    private readonly DiscordPresenceService _discord;
    private readonly IAppLog _log;
    private readonly NavigationService _navigation;

    private readonly ILocalizationService _loc;

    private readonly List<Track> _queue = new();
    private List<Track> _savedOrder = new();
    private int _index = -1;
    private int _trackEndHandling;
    private bool _isSeeking;
    private int _playSession;
    private CancellationTokenSource? _sleepCts;
    private double _pendingSeekSeconds;
    private DateTime _lastPlaybackSaveUtc = DateTime.MinValue;

    public PlayerViewModel(
        IAudioPlayerService player,
        IHistoryService history,
        IFavoritesService favorites,
        ISettingsService settings,
        IStreamResolverService streams,
        DeezerHomeService deezer,
        LastFmScrobbler lastFm,
        RadioPlaybackContext radioContext,
        PersonalWaveService personalWave,
        DiscordPresenceService discord,
        ILocalizationService localization,
        IAppLog log,
        NavigationService navigation)
    {
        _player = player;
        _history = history;
        _favorites = favorites;
        _settings = settings;
        _streams = streams;
        _deezer = deezer;
        _lastFm = lastFm;
        _radioContext = radioContext;
        _personalWave = personalWave;
        _discord = discord;
        _loc = localization;
        _log = log;
        _navigation = navigation;
        _loc.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(RepeatModeHint));
            OnPropertyChanged(nameof(CurrentArtistDisplay));
            OnPropertyChanged(nameof(LyricsTooltip));
        };

        _volume = settings.Current.Volume;
        _player.Volume = (float)_volume;
        _player.PlaybackSpeed = (float)settings.Current.PlaybackSpeed;

        _player.PositionChanged += (_, _) =>
        {
            OnPositionChanged();
            _lastFm.OnPositionChanged(CurrentTrack, PositionSeconds, DurationSeconds);
        };
        _player.StateChanged += (_, _) => OnStateChanged();
        _player.TrackEnded += async (_, _) => await OnTrackEndedAsync();

        _settings.SettingsChanged += (_, _) =>
        {
            if (Math.Abs(Volume - _settings.Current.Volume) > 0.01)
                Volume = _settings.Current.Volume;
            _player.PlaybackSpeed = (float)_settings.Current.PlaybackSpeed;
            ResetSleepTimer();
        };
    }

    public ObservableCollection<Track> UpNext { get; } = new();

    [ObservableProperty] private Track? _currentTrack;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private double _positionSeconds;
    [ObservableProperty] private double _durationSeconds;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private bool _isCurrentFavorite;
    [ObservableProperty] private bool _isResolvingStream;
    [ObservableProperty] private RepeatMode _repeatMode;
    [ObservableProperty] private bool _isShuffle;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool IsRepeatAll => RepeatMode == RepeatMode.All;
    public bool IsRepeatOne => RepeatMode == RepeatMode.One;
    public bool IsRepeatOff => RepeatMode == RepeatMode.Off;
    public string RepeatModeHint => RepeatMode switch
    {
        RepeatMode.All => _loc.T("player.repeatAll"),
        RepeatMode.One => _loc.T("player.repeatOne"),
        _ => _loc.T("player.repeatOff")
    };
    public bool HasTrack => CurrentTrack != null;
    public int QueueCount => _queue.Count;
    public IReadOnlyList<Track> AllQueue => _queue;

    public string CurrentArtistDisplay =>
        string.IsNullOrWhiteSpace(CurrentTrack?.ArtistName)
            ? _loc.T("player.selectTrack")
            : CurrentTrack!.ArtistName;

    public string LyricsTooltip => _loc.T("album.lyricsTitle");
    public string PositionDisplay => Format(PositionSeconds);
    public string DurationDisplay => Format(DurationSeconds);
    public double ProgressMaximum => Math.Max(1, DurationSeconds);

    public async Task PlayQueueAsync(IEnumerable<Track> tracks, Track start, RadioStation? radioStation = null)
    {
        if (start == null) return;

        _radioContext.ActiveStation = radioStation;
        _radioContext.ResetDailyExtension();

        _pendingSeekSeconds = 0;
        Interlocked.Increment(ref _playSession);
        _queue.Clear();
        _queue.AddRange(tracks.Where(t => t != null)!);
        if (_queue.Count == 0)
        {
            _queue.Add(start);
        }
        _savedOrder = _queue.ToList();

        _index = _queue.FindIndex(t =>
            t != null && t.Source == start.Source && t.SourceId == start.SourceId);
        if (_index < 0)
        {
            _queue.Insert(0, start);
            _savedOrder = _queue.ToList();
            _index = 0;
        }

        StatusMessage = string.Empty;
        RefreshUpNext();
        PrefetchStream(_queue[_index]);
        if (_index + 1 < _queue.Count)
            PrefetchStream(_queue[_index + 1]);
        await PlayCurrentAsync();
    }

    public Task PlaySingleAsync(Track track) => PlayQueueAsync(new[] { track }, track);

    /// <summary>Jump to a track already in the active queue without rebuilding or reshuffling it.</summary>
    public Task JumpToQueueTrackAsync(Track track)
    {
        if (track == null) return Task.CompletedTask;
        var idx = _queue.FindIndex(t => t != null && t.Matches(track));
        if (idx < 0)
            return PlayQueueAsync(new[] { track }, track);

        Interlocked.Increment(ref _playSession);
        _index = idx;
        return PlayCurrentAsync();
    }

    /// <summary>Restore last track in the player bar without starting playback.</summary>
    public async Task RestoreLastPlaybackAsync()
    {
        try
        {
            Track? track = null;
            var s = _settings.Current;
            if (s.LastPlayedSource is int rawSource
                && Enum.IsDefined(typeof(MusicSource), rawSource)
                && !string.IsNullOrWhiteSpace(s.LastPlayedSourceId))
            {
                track = await _history.FindTrackAsync((MusicSource)rawSource, s.LastPlayedSourceId);
            }

            if (track == null)
            {
                var recent = await _history.GetRecentTracksAsync(1);
                track = recent.FirstOrDefault();
            }

            if (track == null) return;

            var savedPos = Math.Max(0, s.LastPlayedPositionSeconds);
            if (track.DurationSeconds > 0)
                savedPos = Math.Min(savedPos, Math.Max(0, track.DurationSeconds - 1));

            CurrentTrack = CloneTrack(track);
            DurationSeconds = track.DurationSeconds > 0 ? track.DurationSeconds : savedPos;
            PositionSeconds = savedPos;
            _pendingSeekSeconds = savedPos;

            _queue.Clear();
            _queue.Add(track);
            _savedOrder = new List<Track> { track };
            _index = 0;
            RefreshUpNext();
            OnPropertyChanged(nameof(HasTrack));
            OnPropertyChanged(nameof(QueueCount));

            IsCurrentFavorite = await _favorites.IsFavoriteAsync(track.Source, track.SourceId);
            _log.Info($"Restored last track: {track.ArtistName} — {track.Title} @ {savedPos:F0}s");
        }
        catch (Exception ex)
        {
            _log.Warning($"Could not restore last playback: {ex.Message}");
        }
    }

    /// <summary>Persist last track + position (called on app exit).</summary>
    public void PersistPlaybackState()
    {
        if (CurrentTrack == null) return;
        try
        {
            _settings.SaveLastPlaybackAsync(
                CurrentTrack.Source,
                CurrentTrack.SourceId,
                PositionSeconds).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _log.Warning($"Failed to persist playback state: {ex.Message}");
        }
    }

    public void AddToQueue(Track track)
    {
        _queue.Add(track);
        _savedOrder.Add(track);
        RefreshUpNext();
        OnPropertyChanged(nameof(QueueCount));
    }

    [RelayCommand]
    private void RemoveFromQueue(Track track)
    {
        if (track == null) return;
        var idx = _queue.FindIndex(t =>
            t != null && t.Source == track.Source && t.SourceId == track.SourceId);
        if (idx < 0) return;
        _queue.RemoveAt(idx);
        _savedOrder.RemoveAll(t =>
            t != null && t.Source == track.Source && t.SourceId == track.SourceId);
        if (idx < _index) _index--;
        else if (idx == _index && _index >= _queue.Count)
            _index = Math.Max(0, _queue.Count - 1);
        RefreshUpNext();
        OnPropertyChanged(nameof(QueueCount));
    }

    [RelayCommand]
    private async Task PlayFromQueue(Track track)
    {
        Interlocked.Increment(ref _playSession);
        if (track == null) return;
        _index = _queue.FindIndex(t =>
            t != null && t.Source == track.Source && t.SourceId == track.SourceId);
        if (_index >= 0)
            await PlayCurrentAsync();
    }

    private async Task PlayCurrentAsync()
    {
        if (_index < 0 || _index >= _queue.Count) return;
        var session = Volatile.Read(ref _playSession);
        var source = _queue[_index];
        var restoreSeek = ConsumeRestoreSeekIfMatching(source);

        PositionSeconds = 0;
        DurationSeconds = source.DurationSeconds > 0 ? source.DurationSeconds : 0;

        CurrentTrack = CloneTrack(source);
        OnPropertyChanged(nameof(HasTrack));

        IsResolvingStream = true;
        StatusMessage = $"Loading {source.Title}…";
        try
        {
            _log.Info($"Play requested: {source.ArtistName} — {source.Title}");
            var streamUrl = await _streams.ResolveFullStreamAsync(source);
            if (session != Volatile.Read(ref _playSession)) return;

            if (string.IsNullOrWhiteSpace(streamUrl))
            {
                StatusMessage = $"Could not play «{source.Title}». Check logs.";
                _log.Warning($"No stream URL for '{source.Title}'");
                return;
            }

            source.StreamUrl = streamUrl;
            _queue[_index].StreamUrl = streamUrl;

            if (streamUrl.Contains("preview", StringComparison.OrdinalIgnoreCase))
                StatusMessage = $"Preview only (30s) for «{source.Title}»";
            else
                StatusMessage = string.Empty;

            var track = CloneTrack(source);
            track.StreamUrl = streamUrl;

            CurrentTrack = track;
            OnPropertyChanged(nameof(HasTrack));
            RefreshUpNext();

            if (session != Volatile.Read(ref _playSession)) return;

            _player.PlaybackSpeed = (float)_settings.Current.PlaybackSpeed;
            var crossfade = EffectiveCrossfadeMs();
            await _player.PlayAsync(track, crossfade);
            if (session != Volatile.Read(ref _playSession)) return;

            if (restoreSeek > 0.5)
            {
                if (track.DurationSeconds > 0)
                    restoreSeek = Math.Min(restoreSeek, Math.Max(0, track.DurationSeconds - 0.5));
                _player.Seek(TimeSpan.FromSeconds(restoreSeek));
                PositionSeconds = restoreSeek;
            }

            _ = SaveLastPlaybackAsync(track.Source, track.SourceId, PositionSeconds);

            _lastFm.OnTrackStarted(track);
            _discord.Update(track, true);
            ResetSleepTimer();

            _ = RecordPlaybackMetaAsync(track);

            if (_index + 1 < _queue.Count)
                PrefetchStream(_queue[_index + 1]);

            if (IsRadioActive && _index >= _queue.Count - 3)
                _ = PrefetchRadioTracksAsync();
        }
        catch (Exception ex)
        {
            if (session == Volatile.Read(ref _playSession))
            {
                StatusMessage = $"Playback error: {ex.Message}";
                _log.Error($"Failed to play '{source.Title}'", ex);
            }
        }
        finally
        {
            if (session == Volatile.Read(ref _playSession))
                IsResolvingStream = false;
        }
    }

    private double ConsumeRestoreSeekIfMatching(Track source)
    {
        if (_pendingSeekSeconds <= 0.5) return 0;

        var s = _settings.Current;
        var matches = s.LastPlayedSource is int rawSource
                      && rawSource == (int)source.Source
                      && string.Equals(s.LastPlayedSourceId, source.SourceId, StringComparison.Ordinal);

        var seek = matches ? _pendingSeekSeconds : 0;
        _pendingSeekSeconds = 0;
        return seek;
    }

    private void RefreshUpNext()
    {
        UpNext.Clear();
        for (var i = _index + 1; i < _queue.Count; i++)
            UpNext.Add(_queue[i]);
        OnPropertyChanged(nameof(UpNext));
    }

    private void PrefetchStream(Track track)
    {
        _ = Task.Run(async () =>
        {
            try { await _streams.ResolveFullStreamAsync(track); }
            catch { /* prefetch is best-effort */ }
        });
    }

    private async Task RecordPlaybackMetaAsync(Track track)
    {
        try
        {
            await _history.RecordAsync(track);
            IsCurrentFavorite = await _favorites.IsFavoriteAsync(track.Source, track.SourceId);
        }
        catch (Exception ex)
        {
            _log.Warning($"Could not record playback meta: {ex.Message}");
        }
    }

    private static Track CloneTrack(Track t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        ArtistName = t.ArtistName,
        AlbumName = t.AlbumName,
        DurationSeconds = t.DurationSeconds,
        Source = t.Source,
        SourceId = t.SourceId,
        StreamUrl = t.StreamUrl,
        ThumbnailUrl = t.ThumbnailUrl,
        AddedToLibraryAt = t.AddedToLibraryAt,
        ArtistId = t.ArtistId,
        AlbumId = t.AlbumId
    };

    [RelayCommand]
    private void Stop()
    {
        _lastFm.OnTrackStopped();
        _player.Stop();
        IsPlaying = false;
        if (CurrentTrack != null)
            _ = SaveLastPlaybackAsync(CurrentTrack.Source, CurrentTrack.SourceId, PositionSeconds);
    }

    [RelayCommand]
    private void PlayPause()
    {
        switch (_player.State)
        {
            case PlaybackState.Playing:
                _player.Pause();
                break;
            case PlaybackState.Paused:
                _player.Resume();
                break;
            case PlaybackState.Stopped when CurrentTrack != null:
                if (_queue.Count == 0)
                {
                    _queue.Add(CurrentTrack);
                    _savedOrder = new List<Track> { CurrentTrack };
                    _index = 0;
                }
                _ = PlayCurrentAsync();
                break;
        }
    }

    [RelayCommand]
    private void ToggleRepeat()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off
        };
    }

    [RelayCommand]
    private async Task ToggleShuffleAsync()
    {
        IsShuffle = !IsShuffle;
        if (IsShuffle)
            ShuffleQueueInPlace(keepCurrent: true);
        else
            RestoreQueueOrder();

        RefreshUpNext();
        if (CurrentTrack != null && !IsPlaying && _player.State == PlaybackState.Stopped)
            await PlayCurrentAsync();
    }

    private void ShuffleQueueInPlace(bool keepCurrent)
    {
        if (_queue.Count <= 1) return;
        var current = keepCurrent && _index >= 0 && _index < _queue.Count ? _queue[_index] : null;
        var rest = _queue.Where(t => current == null || t.Source != current.Source || t.SourceId != current.SourceId).OrderBy(_ => Random.Shared.Next()).ToList();
        _queue.Clear();
        if (current != null)
        {
            _queue.Add(current);
            _queue.AddRange(rest);
            _index = 0;
        }
        else
        {
            _queue.AddRange(rest);
            _index = 0;
        }
    }

    private void RestoreQueueOrder()
    {
        if (_savedOrder.Count == 0) return;
        var current = _index >= 0 && _index < _queue.Count ? _queue[_index] : CurrentTrack;
        _queue.Clear();
        _queue.AddRange(_savedOrder);
        if (current != null)
            _index = _queue.FindIndex(t => t.Source == current.Source && t.SourceId == current.SourceId);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (_queue.Count == 0) return;
        Interlocked.Increment(ref _playSession);

        if (TryAdvanceIndex())
        {
            await PlayCurrentAsync();
            return;
        }

        if (ShouldExtendQueueAfterEnd())
            await ExtendRadioAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        if (_queue.Count == 0) return;
        if (_player.Position.TotalSeconds > 3)
        {
            _player.Seek(TimeSpan.Zero);
            return;
        }

        Interlocked.Increment(ref _playSession);
        if (_index <= 0 && RepeatMode != RepeatMode.All)
            return;

        _index = RepeatMode == RepeatMode.All
            ? (_index - 1 + _queue.Count) % _queue.Count
            : _index - 1;
        await PlayCurrentAsync();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (CurrentTrack == null) return;
        IsCurrentFavorite = await _favorites.ToggleAsync(CurrentTrack);
    }

    private async Task OnTrackEndedAsync()
    {
        if (_queue.Count == 0) return;
        if (Interlocked.CompareExchange(ref _trackEndHandling, 1, 0) != 0) return;

        try
        {
            switch (RepeatMode)
            {
                case RepeatMode.One:
                    await PlayCurrentAsync();
                    return;
                case RepeatMode.All:
                    _index = (_index + 1) % _queue.Count;
                    await PlayCurrentAsync();
                    return;
            }

            if (IsRadioActive)
            {
                if (TryAdvanceIndex())
                    await PlayCurrentAsync();
                else
                    await ExtendRadioAsync();
                return;
            }

            if (TryAdvanceIndex())
                await PlayCurrentAsync();
            else if (ShouldExtendQueueAfterEnd())
                await ExtendRadioAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _trackEndHandling, 0);
        }
    }

    /// <summary>Move to the next queue index. Returns false when repeat is off and the queue has ended.</summary>
    private bool TryAdvanceIndex()
    {
        if (_queue.Count == 0) return false;

        if (_index < _queue.Count - 1)
        {
            _index++;
            return true;
        }

        if (RepeatMode == RepeatMode.All)
        {
            _index = 0;
            return true;
        }

        return false;
    }

    private bool ShouldExtendQueueAfterEnd() =>
        IsRadioActive || _settings.Current.RadioEnabled;

    private bool IsRadioActive =>
        _radioContext.ActiveStation != null && _settings.Current.RadioEnabled;

    private async Task ExtendRadioAsync()
    {
        try
        {
            var extras = await FetchRadioExtrasAsync(5);
            if (extras.Count == 0) return;

            _queue.AddRange(extras);
            _savedOrder.AddRange(extras);
            _radioContext.NotifyTracksAppended(extras);
            RefreshUpNext();
            _index++;
            StatusMessage = _loc.T("radio.extending");
            await PlayCurrentAsync();
        }
        catch (Exception ex)
        {
            _log.Error("Radio extend failed", ex);
        }
    }

    private async Task PrefetchRadioTracksAsync()
    {
        try
        {
            if (!IsRadioActive || _index < _queue.Count - 3) return;

            var extras = await FetchRadioExtrasAsync(8);
            if (extras.Count == 0) return;

            _queue.AddRange(extras);
            _savedOrder.AddRange(extras);
            _radioContext.NotifyTracksAppended(extras);
            RefreshUpNext();
        }
        catch (Exception ex)
        {
            _log.Warning($"Radio prefetch failed: {ex.Message}");
        }
    }

    private async Task<List<Track>> FetchRadioExtrasAsync(int take)
    {
        var station = _radioContext.ActiveStation;
        var dayKey = RadioDailySeed.TodayKey;
        var batch = _radioContext.NextExtendBatch();
        var rng = RadioDailySeed.CreateRandom(station?.Id ?? "chart", dayKey, batch);
        var startIndex = rng.Next(0, 40);

        IReadOnlyList<Track> rec = station is { Kind: RadioStationKind.Personal }
            ? await _personalWave.GetDailyTracksAsync(40)
            : station is { } active
                ? await _deezer.GetRadioStationTracksAsync(active, 40, startIndex)
                : await _deezer.GetChartTracksAsync();

        var extras = rec
            .Where(t => !_queue.Any(q => q != null && q.Source == t.Source && q.SourceId == t.SourceId))
            .OrderBy(_ => rng.Next())
            .Take(take)
            .ToList();

        if (extras.Count == 0 && rec.Count > 0)
        {
            extras = rec
                .OrderBy(_ => rng.Next())
                .Take(take)
                .ToList();
        }

        return extras;
    }

    public void SetVolumeFromClick(double normalizedPosition) =>
        Volume = Math.Clamp(normalizedPosition, 0, 1);

    [RelayCommand]
    private async Task PlaySimilarAsync(Track? track = null)
    {
        var seed = track ?? CurrentTrack;
        if (seed == null) return;
        try
        {
            var chart = await _deezer.GetChartTracksAsync(1);
            var similar = chart
                .Where(t => !t.Matches(seed))
                .Where(t => t.ArtistName == seed.ArtistName
                    || string.Equals(t.AlbumName, seed.AlbumName, StringComparison.OrdinalIgnoreCase))
                .Take(12)
                .ToList();
            if (similar.Count == 0)
                similar = chart.Where(t => !t.Matches(seed)).Take(12).ToList();
            if (similar.Count == 0) return;
            await PlayQueueAsync(similar, similar[0]);
        }
        catch (Exception ex)
        {
            _log.Error("Find similar failed", ex);
        }
    }

    private void ResetSleepTimer()
    {
        _sleepCts?.Cancel();
        var mins = _settings.Current.SleepTimerMinutes;
        if (mins <= 0) return;
        _sleepCts = new CancellationTokenSource();
        _ = RunSleepTimerAsync(mins, _sleepCts.Token);
    }

    private async Task RunSleepTimerAsync(int minutes, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(minutes), token);
            Stop();
            StatusMessage = "Sleep timer stopped playback.";
        }
        catch (OperationCanceledException) { }
    }

    public void BeginSeek() => _isSeeking = true;

    public void EndSeek(double seconds)
    {
        _isSeeking = false;
        if (DurationSeconds > 0)
            seconds = Math.Clamp(seconds, 0, DurationSeconds);
        _player.Seek(TimeSpan.FromSeconds(seconds));
    }

    private void OnPositionChanged()
    {
        DurationSeconds = _player.Duration.TotalSeconds;
        if (!_isSeeking)
            PositionSeconds = _player.Position.TotalSeconds;

        if (CurrentTrack != null && _player.State == PlaybackState.Playing)
            MaybeSavePlaybackPosition();
    }

    private void OnStateChanged()
    {
        IsPlaying = _player.State == PlaybackState.Playing;
        _discord.Update(CurrentTrack, IsPlaying);
        if (CurrentTrack != null && _player.State is PlaybackState.Paused or PlaybackState.Stopped)
            _ = SaveLastPlaybackAsync(CurrentTrack.Source, CurrentTrack.SourceId, PositionSeconds);
    }

    private void MaybeSavePlaybackPosition()
    {
        if (DateTime.UtcNow - _lastPlaybackSaveUtc < TimeSpan.FromSeconds(5)) return;
        _lastPlaybackSaveUtc = DateTime.UtcNow;
        var track = CurrentTrack!;
        _ = SaveLastPlaybackAsync(track.Source, track.SourceId, PositionSeconds);
    }

    private async Task SaveLastPlaybackAsync(MusicSource source, string sourceId, double position)
    {
        try
        {
            await _settings.SaveLastPlaybackAsync(source, sourceId, position);
        }
        catch (Exception ex)
        {
            _log.Warning($"Failed to save last playback: {ex.Message}");
        }
    }

    partial void OnVolumeChanged(double value)
    {
        _player.Volume = (float)value;
        _settings.Current.Volume = value;
    }

    partial void OnCurrentTrackChanged(Track? value)
    {
        OnPropertyChanged(nameof(HasTrack));
        OnPropertyChanged(nameof(CurrentArtistDisplay));
    }

    partial void OnPositionSecondsChanged(double value) => OnPropertyChanged(nameof(PositionDisplay));
    partial void OnDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(DurationDisplay));
        OnPropertyChanged(nameof(ProgressMaximum));
    }

    partial void OnRepeatModeChanged(RepeatMode value)
    {
        OnPropertyChanged(nameof(IsRepeatAll));
        OnPropertyChanged(nameof(IsRepeatOne));
        OnPropertyChanged(nameof(IsRepeatOff));
        OnPropertyChanged(nameof(RepeatModeHint));
    }

    partial void OnIsShuffleChanged(bool value) => OnPropertyChanged(nameof(IsShuffle));

    [RelayCommand]
    private void OpenNowPlaying()
    {
        if (CurrentTrack == null) return;

        var queue = _queue.Count > 0 ? _queue.ToList() : new List<Track> { CurrentTrack };
        var title = !string.IsNullOrWhiteSpace(CurrentTrack.AlbumName)
            ? CurrentTrack.AlbumName
            : CurrentTrack.Title;

        _navigation.OpenAlbum(AlbumNavigationContext.FromTrackList(title, queue, CurrentTrack));
    }

    public void MoveUpNext(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex) return;
        var fromQueue = _index + 1 + fromIndex;
        var toQueue = _index + 1 + toIndex;
        if (fromQueue < 0 || fromQueue >= _queue.Count || toQueue < 0 || toQueue >= _queue.Count) return;

        var item = _queue[fromQueue];
        _queue.RemoveAt(fromQueue);
        _queue.Insert(toQueue, item);

        var savedIdx = _savedOrder.FindIndex(t => t.Matches(item));
        if (savedIdx >= 0)
        {
            _savedOrder.RemoveAt(savedIdx);
            var targetSaved = Math.Min(toQueue, _savedOrder.Count);
            _savedOrder.Insert(targetSaved, item);
        }

        RefreshUpNext();
    }

    private int EffectiveCrossfadeMs()
    {
        var ms = _settings.Current.CrossfadeMs;
        if (_settings.Current.GaplessPlayback && _queue.Count > 1)
            ms = Math.Max(ms, 450);
        return ms;
    }

    private static string Format(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds)) return "0:00";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }
}
