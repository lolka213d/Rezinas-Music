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
    public const string? SpotifyClientId = "8b8d7d08b86747408724f3def324e13e";
    public const string? SpotifyClientSecret = "23f8adb2445b43798dc078b4b4c24849";
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
