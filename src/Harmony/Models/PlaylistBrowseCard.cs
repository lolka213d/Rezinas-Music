namespace Harmony.Models;

/// <summary>Playlist tile in browse grids with cover thumbnails.</summary>
public sealed class PlaylistBrowseCard
{
    public required Playlist Playlist { get; init; }

    public string Name => Playlist.Name;

    public int TrackCount => Playlist.TrackCount;

    public IReadOnlyList<string?> Thumbnails { get; init; } = [];

    public string? Thumbnail0 => Thumbnails.Count > 0 ? Thumbnails[0] : null;
    public string? Thumbnail1 => Thumbnails.Count > 1 ? Thumbnails[1] : null;
    public string? Thumbnail2 => Thumbnails.Count > 2 ? Thumbnails[2] : null;
    public string? Thumbnail3 => Thumbnails.Count > 3 ? Thumbnails[3] : null;
}
