using Harmony.Models;

namespace Harmony.Services;

/// <summary>Keeps language radio stations focused on regional tracks.</summary>
public static class RegionalTrackFilter
{
    public static IReadOnlyList<Track> Apply(string stationId, IEnumerable<Track> tracks, int limit)
    {
        IEnumerable<Track> filtered = stationId switch
        {
            "ru" => tracks.Where(IsCyrillicTrack),
            "uk" => tracks.Where(IsCyrillicTrack),
            _ => tracks
        };

        return filtered.Take(limit).ToList();
    }

    private static bool IsCyrillicTrack(Track track) =>
        ContainsCyrillic(track.Title) || ContainsCyrillic(track.ArtistName);

    private static bool ContainsCyrillic(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Any(c => c is >= '\u0400' and <= '\u04FF');
}
