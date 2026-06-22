namespace Harmony.Models;

/// <summary>
/// Identifies where a track originates from. New providers (e.g. SoundCloud)
/// can be added here without breaking existing data because we persist the
/// enum as an integer.
/// </summary>
public enum MusicSource
{
    /// <summary>Bundled royalty-free demo catalog used by the MVP.</summary>
    Sample = 0,

    /// <summary>Result coming from the YouTube Data API.</summary>
    YouTube = 1,

    /// <summary>Metadata coming from the Spotify Web API (info/cover only).</summary>
    Spotify = 2,

    /// <summary>SoundCloud (official API, requires client_id).</summary>
    SoundCloud = 3,

    /// <summary>A local audio file added by the user.</summary>
    Local = 4,

    /// <summary>Deezer public API (free, no key required).</summary>
    Deezer = 5
}
