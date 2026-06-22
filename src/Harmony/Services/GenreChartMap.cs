namespace Harmony.Services;

/// <summary>Deezer genre ids for home genre picks (radio → tracks).</summary>
public static class GenreChartMap
{
    public static IReadOnlyList<(int GenreId, string LocalizationKey)> ForLanguage(string language) =>
        (language ?? "en").ToLowerInvariant() switch
        {
            "ru" => Russian,
            "uk" => Ukrainian,
            _ => Default
        };

    private static readonly (int, string)[] Default =
    [
        (132, "home.genrePop"),
        (152, "home.genreRock"),
        (116, "home.genreHipHop"),
        (85, "home.genreElectronic"),
        (466, "home.genreJazz"),
    ];

    private static readonly (int, string)[] Russian =
    [
        (132, "home.genrePop"),
        (152, "home.genreRock"),
        (116, "home.genreHipHop"),
        (85, "home.genreElectronic"),
        (466, "home.genreJazz"),
    ];

    private static readonly (int, string)[] Ukrainian = Russian;
}
