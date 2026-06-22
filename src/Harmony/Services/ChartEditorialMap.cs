namespace Harmony.Services;

/// <summary>Maps UI language codes to official Deezer Charts regional playlist ids.</summary>
public static class ChartEditorialMap
{
    public const long WorldwidePlaylistId = 3155776842L;

    public static long GetPlaylistId(string language) =>
        GetPlaylistIds(language).First();

    public static IReadOnlyList<long> GetPlaylistIds(string language) =>
        (language ?? "en").ToLowerInvariant() switch
        {
            "ru" =>
            [
                11855515681L, // RUSSIAN HITS | TikToK
                1116189381L,  // Top Russia
            ],
            "uk" => [1362526495L],
            "de" => [1111143121L],
            "fr" => [1109890291L],
            "es" => [1116190041L],
            "it" => [1116187241L],
            "pt" => [1362519755L],
            "pl" => [1266972311L],
            "ja" => [1362508955L],
            _ => [WorldwidePlaylistId]
        };
}
