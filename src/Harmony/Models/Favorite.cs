namespace Harmony.Models;

/// <summary>A "liked" track. One row per favorited track.</summary>
public class Favorite
{
    public int Id { get; set; }

    public int TrackId { get; set; }
    public Track? Track { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
