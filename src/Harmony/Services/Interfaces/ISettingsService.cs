using Harmony.Models;

namespace Harmony.Services.Interfaces;

/// <summary>Loads and saves the single <see cref="UserSettings"/> row.</summary>
public interface ISettingsService
{
    /// <summary>Cached current settings (loaded at startup).</summary>
    UserSettings Current { get; }

    /// <summary>Reload settings from the database.</summary>
    Task<UserSettings> LoadAsync();

    /// <summary>Persist the provided settings and update the cache.</summary>
    Task SaveAsync(UserSettings settings);

    /// <summary>Delete cached files from disk.</summary>
    Task ClearCacheAsync();

    /// <summary>Persist last-played track for the player bar (no SettingsChanged).</summary>
    Task SaveLastPlaybackAsync(MusicSource? source, string? sourceId, double positionSeconds);

    /// <summary>Raised after settings are saved.</summary>
    event EventHandler? SettingsChanged;
}
