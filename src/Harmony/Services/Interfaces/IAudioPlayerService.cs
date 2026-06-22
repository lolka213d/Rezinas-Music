using Harmony.Models;

namespace Harmony.Services.Interfaces;

/// <summary>Playback states reported by the audio engine.</summary>
public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}

/// <summary>
/// Audio engine abstraction. The default implementation uses NAudio and can
/// play local files as well as remote HTTP(S) streams.
/// </summary>
public interface IAudioPlayerService
{
    /// <summary>Track currently loaded into the engine (null when stopped).</summary>
    Track? CurrentTrack { get; }

    PlaybackState State { get; }

    /// <summary>Total length of the loaded track.</summary>
    TimeSpan Duration { get; }

    /// <summary>Current playback position.</summary>
    TimeSpan Position { get; set; }

    /// <summary>Linear volume 0.0 - 1.0 (app output only — does not change Windows mixer).</summary>
    float Volume { get; set; }

    /// <summary>Playback speed multiplier 0.5–2.0.</summary>
    float PlaybackSpeed { get; set; }

    event EventHandler? PositionChanged;

    /// <summary>Raised when the playback state changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Raised when the current track plays to its natural end.</summary>
    event EventHandler? TrackEnded;

    /// <summary>Load <paramref name="track"/> and start playing it.</summary>
    Task PlayAsync(Track track, int crossfadeMs = 0);

    void Pause();
    void Resume();
    void Stop();

    /// <summary>Seek to the given absolute position.</summary>
    void Seek(TimeSpan position);
}
