namespace Harmony.Models;



/// <summary>Audio quality preference (used when a source supports it).</summary>

public enum AudioQuality

{

    Low = 0,

    Normal = 1,

    High = 2

}



/// <summary>Application theme.</summary>

public enum AppTheme

{

    Dark = 0,

    Light = 1

}



/// <summary>

/// Single-row table holding the local user profile and application settings.

/// API keys are stored here so the user can configure them from the UI.

/// </summary>

public class UserSettings

{

    /// <summary>Always 1 — there is exactly one local profile.</summary>

    public int Id { get; set; } = 1;



    // ----- Profile -----

    public string UserName { get; set; } = "Guest";

    public string? AvatarPath { get; set; }



    // ----- Appearance / locale -----

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public string Language { get; set; } = "en";



    // ----- Playback -----

    public AudioQuality AudioQuality { get; set; } = AudioQuality.Normal;

    public double Volume { get; set; } = 0.7;

    public double PlaybackSpeed { get; set; } = 1.0;

    public bool RadioEnabled { get; set; } = true;

    public int CrossfadeMs { get; set; }

    public double LyricsOffsetSeconds { get; set; }

    /// <summary>Auto-open the right context panel when a track starts.</summary>
    public bool OpenNowPlayingOnPlay { get; set; } = true;

    /// <summary>Respond to keyboard/media keys (play, next, prev).</summary>
    public bool MediaKeysEnabled { get; set; } = true;

    /// <summary>Show album rows on the home feed.</summary>
    public bool ShowHomeAlbums { get; set; } = true;

    /// <summary>Minimize to tray; show mini-player menu.</summary>
    public bool MiniPlayerInTray { get; set; } = true;

    /// <summary>Lower GPU use: flat UI, fewer shadow effects (good for gaming).</summary>
    public bool ReduceGpuUsage { get; set; }

    /// <summary>Denser track rows across lists.</summary>
    public bool CompactTrackLists { get; set; }

    /// <summary>Stop playback after N minutes (0 = off).</summary>
    public int SleepTimerMinutes { get; set; }

    /// <summary>Last.fm scrobbling.</summary>
    public bool LastFmEnabled { get; set; }
    public string? LastFmApiKey { get; set; }
    public string? LastFmApiSecret { get; set; }
    public string? LastFmSessionKey { get; set; }

    /// <summary>5-band EQ gain in dB (-12..+12).</summary>
    public double EqBand60 { get; set; }
    public double EqBand250 { get; set; }
    public double EqBand1k { get; set; }
    public double EqBand4k { get; set; }
    public double EqBand12k { get; set; }

    // ----- API credentials (optional; entered by the user) -----
    public string? YouTubeApiKey { get; set; }

    public string? SpotifyClientId { get; set; }

    public string? SpotifyClientSecret { get; set; }

    public string? SpotifyRefreshToken { get; set; }
    public string? SpotifyAccessToken { get; set; }
    public DateTime? SpotifyTokenExpiresUtc { get; set; }
    public string? SpotifyUserId { get; set; }
    public string? SpotifyDisplayName { get; set; }
    public DateTime? SpotifyConnectedAt { get; set; }
    public DateTime? SpotifyLastSyncUtc { get; set; }
    public bool SpotifyAutoSyncEnabled { get; set; }
    public int SpotifyAutoSyncIntervalHours { get; set; } = 24;

    public string? SoundCloudClientId { get; set; }

    /// <summary>Check GitHub releases on startup.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>User dismissed this release version.</summary>
    public string? SkippedUpdateVersion { get; set; }

    /// <summary>Last track shown in the player bar (restored on startup).</summary>
    public int? LastPlayedSource { get; set; }

    public string? LastPlayedSourceId { get; set; }

    public double LastPlayedPositionSeconds { get; set; }

    /// <summary>First launch / install timestamp (UTC).</summary>
    public DateTime? InstalledAt { get; set; }

    public bool StartWithWindows { get; set; }
    public bool DiscordPresenceEnabled { get; set; }
    public bool GaplessPlayback { get; set; }
    public bool MiniPlayerWindowEnabled { get; set; }
    public int OfflineCacheLimitMb { get; set; } = 512;
    public string? SyncFolderPath { get; set; }
    public string? LastSeenAppVersion { get; set; }

}


