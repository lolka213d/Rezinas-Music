namespace Harmony.Models;

/// <summary>Album — metadata from tracks or user-created collection.</summary>
public class Album
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ArtistName { get; set; }

    public string? ImageUrl { get; set; }

    public int? Year { get; set; }

    /// <summary>True for albums the user created in the Albums page.</summary>
    public bool IsUserCreated { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>External catalog id (e.g. Deezer album id).</summary>
    public string? ExternalSourceId { get; set; }

    public MusicSource? ExternalSource { get; set; }

    /// <summary>Tracks linked via metadata FK (auto upsert).</summary>
    public List<Track> Tracks { get; set; } = new();

    /// <summary>Explicit track list for user-created albums.</summary>
    public List<AlbumTrack> Items { get; set; } = new();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TrackCount => IsUserCreated ? Items.Count : Tracks.Count;
}
