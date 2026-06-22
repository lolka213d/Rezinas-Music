using Harmony.Models;

namespace Harmony.Config;

/// <summary>
/// Built-in API keys used for all installations. Fill before distributing the app.
/// End users never see or edit these in Settings.
/// </summary>
public static class BuiltInApiCredentials
{
    // Set your keys here (or leave null to use Deezer-only / YouTube without key).
    public const string? YouTubeApiKey = null;
    public const string? SpotifyClientId = "";
    public const string? SpotifyClientSecret = "";
    public const string? SoundCloudClientId = null;

    public static void Apply(UserSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(YouTubeApiKey))
            settings.YouTubeApiKey = YouTubeApiKey;
        if (!string.IsNullOrWhiteSpace(SpotifyClientId))
            settings.SpotifyClientId = SpotifyClientId;
        if (!string.IsNullOrWhiteSpace(SpotifyClientSecret))
            settings.SpotifyClientSecret = SpotifyClientSecret;
        if (!string.IsNullOrWhiteSpace(SoundCloudClientId))
            settings.SoundCloudClientId = SoundCloudClientId;
    }
}
