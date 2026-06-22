namespace Harmony.Services;

/// <summary>Maps UI language codes to official Deezer Charts regional playlist ids.</summary>
public static class ChartEditorialMap
{
    public const long WorldwidePlaylistId = 3155776842L;

    public static long GetPlaylistId(string language) =>
        (language ?? "en").ToLowerInvariant() switch
        {
            "ru" => 1116189381L, // Top Russia
            "uk" => 1362526495L, // Top Ukraine
            "de" => 1111143121L, // Top Germany
            "fr" => 1109890291L, // Top France
            "es" => 1116190041L, // Top Spain
            "it" => 1116187241L, // Top Italy
            "pt" => 1362519755L, // Top Portugal
            "pl" => 1266972311L, // Top Poland
            "ja" => 1362508955L, // Top Japan
            _ => WorldwidePlaylistId
        };
}
