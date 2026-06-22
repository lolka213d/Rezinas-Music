using Harmony.Models;

namespace Harmony.Config;

/// <summary>
/// Optional built-in API keys for search providers.
/// Leave null in the public repo; set locally or via user Settings if needed.
/// </summary>
public static class BuiltInApiCredentials
{
    public const string? YouTubeApiKey = null;
    public const string? SpotifyClientId = null;
    public const string? SpotifyClientSecret = null;
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
