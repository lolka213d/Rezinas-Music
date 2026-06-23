using System.IO;
using System.Windows.Threading;
using Harmony.Models;
using Harmony.Services.Interfaces;
using NAudio.Wave;
using PlaybackState = Harmony.Services.Interfaces.PlaybackState;

namespace Harmony.Services;

/// <summary>NAudio engine with optional crossfade and variable playback speed.</summary>
public sealed class NAudioPlayerService : IAudioPlayerService, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly IAppLog _log;
    private readonly ISettingsService _settings;
    private readonly UiPerformanceService _uiPerf;
    private float[] _eqBands = [0, 0, 0, 0, 0];
    private readonly object _sync = new();
    private WaveOutEvent? _output;
    private MediaFoundationReader? _reader;
    private float _volume = 0.7f;
    private float _playbackSpeed = 1f;
    private bool _suppressEndEvent;
    private int _playGeneration;

    public NAudioPlayerService(IAppLog log, ISettingsService settings, UiPerformanceService uiPerf)
    {
        _log = log;
        _settings = settings;
        _uiPerf = uiPerf;
        ApplyEqFromSettings();
        _settings.SettingsChanged += (_, _) =>
        {
            ApplyEqFromSettings();
            _uiPerf.ReduceGpuUsage = _settings.Current.ReduceGpuUsage;
        };
        _uiPerf.ReduceGpuUsage = _settings.Current.ReduceGpuUsage;
        _uiPerf.Changed += (_, _) => SyncPositionTimer();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncPositionTimer()
    {
        var ms = _uiPerf.PositionUpdateIntervalMs;
        if (ms <= 0)
        {
            _timer.Stop();
            return;
        }

        _timer.Interval = TimeSpan.FromMilliseconds(ms);
        if (State == PlaybackState.Playing)
            _timer.Start();
    }

    public Track? CurrentTrack { get; private set; }

    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public TimeSpan Position
    {
        get => _reader?.CurrentTime ?? TimeSpan.Zero;
        set => Seek(value);
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_output != null) _output.Volume = _volume;
        }
    }

    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set => _playbackSpeed = Math.Clamp(value, 0.5f, 2f);
    }

    public event EventHandler? PositionChanged;
    public event EventHandler? StateChanged;
    public event EventHandler? TrackEnded;

    public async Task PlayAsync(Track track, int crossfadeMs = 0)
    {
        if (track is null) throw new ArgumentNullException(nameof(track));
        if (string.IsNullOrWhiteSpace(track.StreamUrl))
            throw new InvalidOperationException($"Track '{track.Title}' has no playable stream URL.");

        var generation = Interlocked.Increment(ref _playGeneration);

        if (crossfadeMs > 0 && _output != null && State is PlaybackState.Playing or PlaybackState.Paused)
            await FadeVolumeAsync(0f, crossfadeMs / 2).ConfigureAwait(true);

        if (generation != Volatile.Read(ref _playGeneration)) return;

        DisposePlayback();
        CurrentTrack = track;
        var url = track.StreamUrl!;

        MediaFoundationReader? reader = null;
        try
        {
            reader = await Task.Run(() => new MediaFoundationReader(url)).ConfigureAwait(true);

            if (generation != Volatile.Read(ref _playGeneration))
            {
                reader.Dispose();
                return;
            }

            lock (_sync)
            {
                if (generation != _playGeneration)
                {
                    reader.Dispose();
                    return;
                }

                _reader = reader;
                var sample = reader.ToSampleProvider();
                if (Math.Abs(_playbackSpeed - 1f) > 0.01f)
                    sample = new SpeedSampleProvider(sample, _playbackSpeed);
                if (_eqBands.Any(b => Math.Abs(b) > 0.01f))
                    sample = new EqualizerSampleProvider(sample, _eqBands);

                _output = new WaveOutEvent();
                _output.PlaybackStopped += OnPlaybackStopped;
                _output.Init(sample.ToWaveProvider());
                _output.Volume = crossfadeMs > 0 ? 0f : _volume;
                _output.Play();
            }

            SetState(PlaybackState.Playing);
            _timer.Start();

            if (crossfadeMs > 0)
                await FadeVolumeAsync(_volume, crossfadeMs / 2).ConfigureAwait(true);

            _log.Info($"Playing: {track.ArtistName} — {track.Title} ({(url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "online" : "local")})");
        }
        catch (Exception ex)
        {
            reader?.Dispose();
            if (generation == Volatile.Read(ref _playGeneration))
            {
                DisposePlayback();
                CurrentTrack = null;
            }

            _log.Error($"Playback failed for '{track.Title}' ({url})", ex);
            throw;
        }
    }

    public void Pause()
    {
        if (_output is null || State != PlaybackState.Playing) return;
        _output.Pause();
        _timer.Stop();
        SetState(PlaybackState.Paused);
    }

    public void Resume()
    {
        if (_output is null || State != PlaybackState.Paused) return;
        _output.Play();
        _timer.Start();
        SetState(PlaybackState.Playing);
    }

    public void Stop()
    {
        Interlocked.Increment(ref _playGeneration);
        DisposePlayback();
        CurrentTrack = null;
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is null) return;
        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero
            : position > _reader.TotalTime ? _reader.TotalTime
            : position;
        _reader.CurrentTime = clamped;
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task FadeVolumeAsync(float target, int durationMs)
    {
        if (_output is null || durationMs <= 0) return;
        var start = _output.Volume;
        var steps = Math.Max(1, durationMs / 30);
        for (var i = 1; i <= steps; i++)
        {
            if (_output is null) return;
            _output.Volume = start + (target - start) * i / steps;
            await Task.Delay(30).ConfigureAwait(true);
        }
    }

    private void DisposePlayback()
    {
        _timer.Stop();

        WaveOutEvent? output;
        MediaFoundationReader? reader;

        lock (_sync)
        {
            output = _output;
            reader = _reader;
            _output = null;
            _reader = null;
        }

        if (output != null)
        {
            _suppressEndEvent = true;
            output.PlaybackStopped -= OnPlaybackStopped;
            try { output.Stop(); }
            catch { /* ignore */ }
            output.Dispose();
            _suppressEndEvent = false;
        }

        reader?.Dispose();
        SetState(PlaybackState.Stopped);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _timer.Stop();
        if (_suppressEndEvent) return;
        if (e.Exception != null)
            _log.Error("Playback stopped with error", e.Exception);
        SetState(PlaybackState.Stopped);
        TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void SetState(PlaybackState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyEqFromSettings()
    {
        var s = _settings.Current;
        _eqBands =
        [
            (float)Math.Clamp(s.EqBand60, -12, 12),
            (float)Math.Clamp(s.EqBand250, -12, 12),
            (float)Math.Clamp(s.EqBand1k, -12, 12),
            (float)Math.Clamp(s.EqBand4k, -12, 12),
            (float)Math.Clamp(s.EqBand12k, -12, 12)
        ];
    }

    public void Dispose()
    {
        Stop();
        _timer.Stop();
    }
}
