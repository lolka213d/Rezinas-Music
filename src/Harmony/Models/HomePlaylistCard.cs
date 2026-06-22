namespace Harmony.Models;

/// <summary>Playlist row on the home screen with cover thumbnails.</summary>
public sealed class HomePlaylistCard
{
    public required Playlist Playlist { get; init; }

    public string Name => Playlist.Name;

    public int TrackCount => Playlist.TrackCount;

    public IReadOnlyList<string?> Thumbnails { get; init; } = [];

    public bool HasSingleCover => Thumbnails.Count is >= 1 and < 4;

    public bool HasMosaic => Thumbnails.Count >= 4;

    public bool HasDefaultCover => Thumbnails.Count == 0;

    public string? SingleCoverUrl => Thumbnails.FirstOrDefault();
}
