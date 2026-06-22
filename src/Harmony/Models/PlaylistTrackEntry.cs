namespace Harmony.Models;

/// <summary>Playlist track with ordering and date-added metadata.</summary>
public sealed class PlaylistTrackEntry
{
    public required Track Track { get; init; }
    public int Position { get; init; }
    public DateTime AddedAt { get; init; }
}
