using System.ComponentModel.DataAnnotations.Schema;

namespace Harmony.Models;

/// <summary>A user-created playlist.</summary>
public class Playlist
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>External provider link for synced playlists (e.g. Spotify).</summary>
    public MusicSource? ExternalSource { get; set; }

    public string? ExternalSourceId { get; set; }

    /// <summary>Ordered join rows linking this playlist to its tracks.</summary>
    public List<PlaylistTrack> Items { get; set; } = new();

    /// <summary>Number of tracks in the playlist (computed when loaded).</summary>
    [NotMapped]
    public int TrackCount => Items.Count;
}
