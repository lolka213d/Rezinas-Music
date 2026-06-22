using Harmony.Models;

namespace Harmony.Services;

/// <summary>Curated radio stations by language and genre (Deezer Charts playlists + genre radio).</summary>
public static class RadioStationCatalog
{
    public static IReadOnlyList<RadioStation> All { get; } =
    [
        Station("personal", RadioStationKind.Personal, 0, "radio.station.personal", "radio.station.personal.sub", "#FF7C3AED", "#FFEC4899"),
        Station("ru", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("ru"), "radio.station.ru", "radio.station.ru.sub", "#FF8B5CF6", "#FF6366F1"),
        Station("en", RadioStationKind.Playlist, ChartEditorialMap.WorldwidePlaylistId, "radio.station.en", "radio.station.en.sub", "#FF38BDF8", "#FF0EA5E9"),
        Station("uk", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("uk"), "radio.station.uk", "radio.station.uk.sub", "#FFF59E0B", "#FFF97316"),
        Station("de", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("de"), "radio.station.de", "radio.station.de.sub", "#FF22C55E", "#FF14B8A6"),
        Station("fr", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("fr"), "radio.station.fr", "radio.station.fr.sub", "#FFEC4899", "#FFA855F7"),
        Station("es", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("es"), "radio.station.es", "radio.station.es.sub", "#FFEF4444", "#FFF97316"),
        Station("it", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("it"), "radio.station.it", "radio.station.it.sub", "#FF10B981", "#FF06B6D4"),
        Station("pt", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("pt"), "radio.station.pt", "radio.station.pt.sub", "#FF84CC16", "#FF22C55E"),
        Station("pl", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("pl"), "radio.station.pl", "radio.station.pl.sub", "#FF3B82F6", "#FF6366F1"),
        Station("ja", RadioStationKind.Playlist, ChartEditorialMap.GetPlaylistId("ja"), "radio.station.ja", "radio.station.ja.sub", "#FFF472B6", "#FF8B5CF6"),
        Station("pop", RadioStationKind.Genre, 132, "home.genrePop", "radio.station.genre.sub", "#FF8B5CF6", "#FFEC4899"),
        Station("rock", RadioStationKind.Genre, 152, "home.genreRock", "radio.station.genre.sub", "#FF64748B", "#FF475569"),
        Station("hiphop", RadioStationKind.Genre, 116, "home.genreHipHop", "radio.station.genre.sub", "#FF1E293B", "#FF6366F1"),
        Station("electronic", RadioStationKind.Genre, 85, "home.genreElectronic", "radio.station.genre.sub", "#FF06B6D4", "#FF8B5CF6"),
        Station("jazz", RadioStationKind.Genre, 466, "home.genreJazz", "radio.station.genre.sub", "#FFF59E0B", "#FF78350F"),
    ];

    public static RadioStation? Find(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : All.FirstOrDefault(s => s.Id == id);

    public static RadioStation DefaultForLanguage(string language) =>
        Find("personal") ?? Find((language ?? "en").ToLowerInvariant() switch
        {
            "ru" => "ru",
            "uk" => "uk",
            "de" => "de",
            "fr" => "fr",
            "es" => "es",
            "it" => "it",
            "pt" => "pt",
            "pl" => "pl",
            "ja" => "ja",
            _ => "en"
        }) ?? All[1];

    private static RadioStation Station(
        string id, RadioStationKind kind, long deezerId,
        string titleKey, string subtitleKey, string accent, string accent2) => new()
    {
        Id = id,
        Kind = kind,
        DeezerId = deezerId,
        TitleKey = titleKey,
        SubtitleKey = subtitleKey,
        AccentColor = accent,
        AccentColor2 = accent2
    };
}
