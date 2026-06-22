using Harmony.Models;



namespace Harmony.Services;



/// <summary>Merges duplicate tracks from multiple search providers.</summary>

public static class TrackDedup

{

    public static IEnumerable<Track> Merge(IEnumerable<Track> tracks)

    {

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in tracks)

        {

            var key = $"{Norm(t.ArtistName)}|{Norm(t.Title)}";

            if (!seen.Add(key)) continue;

            yield return t;

        }

    }



    private static string Norm(string s) => s.Trim().ToLowerInvariant();

}


