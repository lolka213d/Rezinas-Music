using Harmony.Models;

namespace Harmony.Helpers;

/// <summary>Deep links and search URLs for Spotify (opens in browser).</summary>
public static class SpotifyLinks
{
    public static string Artist(string name) =>
        $"https://open.spotify.com/search/{Uri.EscapeDataString(name.Trim())}";

    public static string Track(Track track)
    {
        if (track.Source == MusicSource.Spotify && !string.IsNullOrWhiteSpace(track.SourceId))
            return $"https://open.spotify.com/track/{track.SourceId}";

        var q = $"{track.ArtistName} {track.Title}".Trim();
        return $"https://open.spotify.com/search/{Uri.EscapeDataString(q)}";
    }
}
