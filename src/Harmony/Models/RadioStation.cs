namespace Harmony.Models;

public enum RadioStationKind
{
    Personal,
    Playlist,
    Genre
}

/// <summary>Internet radio station (Deezer chart / genre radio).</summary>
public sealed class RadioStation
{
    public required string Id { get; init; }
    public required string TitleKey { get; init; }
    public required string SubtitleKey { get; init; }
    public required RadioStationKind Kind { get; init; }
    public required long DeezerId { get; init; }
    public required string AccentColor { get; init; }
    public string AccentColor2 { get; init; } = "#FF38BDF8";
}
