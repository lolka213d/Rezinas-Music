namespace Harmony.Models;

/// <summary>Artist entity. Upserted (by name) whenever a track is saved.</summary>
public class Artist
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    /// <summary>Tracks attributed to this artist.</summary>
    public List<Track> Tracks { get; set; } = new();
}
