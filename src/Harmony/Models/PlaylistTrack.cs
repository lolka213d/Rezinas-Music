namespace Harmony.Models;

/// <summary>
/// Join entity between <see cref="Playlist"/> and <see cref="Track"/>.
/// Keeps an explicit position so playlist ordering is preserved.
/// </summary>
public class PlaylistTrack
{
    public int Id { get; set; }

    public int PlaylistId { get; set; }
    public Playlist? Playlist { get; set; }

    public int TrackId { get; set; }
    public Track? Track { get; set; }

    /// <summary>Zero-based position of the track inside the playlist.</summary>
    public int Position { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
