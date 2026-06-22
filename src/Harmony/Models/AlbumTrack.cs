namespace Harmony.Models;

/// <summary>Join row: track membership in a user-created album.</summary>
public class AlbumTrack
{
    public int Id { get; set; }
    public int AlbumId { get; set; }
    public Album? Album { get; set; }
    public int TrackId { get; set; }
    public Track? Track { get; set; }
    public int Position { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
