using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Config;

internal static class SpotifyCredentials
{
    internal static (string? ClientId, string? ClientSecret) Resolve(ISettingsService settings)
    {
        var id = FirstNonEmpty(SpotifyAppCredentials.ClientId, settings.Current.SpotifyClientId);
        var secret = FirstNonEmpty(SpotifyAppCredentials.ClientSecret, settings.Current.SpotifyClientSecret);
        return (id, secret);
    }

    internal static void ApplyBuiltIn(UserSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(SpotifyAppCredentials.ClientId))
            settings.SpotifyClientId = SpotifyAppCredentials.ClientId;
        if (!string.IsNullOrWhiteSpace(SpotifyAppCredentials.ClientSecret))
            settings.SpotifyClientSecret = SpotifyAppCredentials.ClientSecret;
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : string.IsNullOrWhiteSpace(b) ? null : b;
}
