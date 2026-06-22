namespace Harmony.Models;

/// <summary>
/// One row is written every time a track starts playing, enabling the
/// "history" view as well as play-count aggregation.
/// </summary>
public class ListeningHistoryEntry
{
    public int Id { get; set; }

    public int TrackId { get; set; }
    public Track? Track { get; set; }

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}
