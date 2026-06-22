using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Harmony.Models;

/// <summary>
/// Represents a single playable (or informational) track. The same class is
/// used for transient search results and for persisted library items. A track
/// is only written to the database once the user adds it to the library,
/// favorites, a playlist, or plays it (history).
/// </summary>
public class Track
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    /// <summary>Denormalized artist name for fast display in lists.</summary>
    public string ArtistName { get; set; } = string.Empty;

    /// <summary>Denormalized album name (optional).</summary>
    public string? AlbumName { get; set; }

    /// <summary>Track length in seconds (0 when unknown).</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Origin of the track (YouTube, Spotify, Sample...).</summary>
    public MusicSource Source { get; set; }

    /// <summary>
    /// Provider specific identifier (e.g. YouTube video id or Spotify track id).
    /// Combined with <see cref="Source"/> it uniquely identifies a track.
    /// </summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// Direct, legally streamable audio URL or local file path. May be null for
    /// Spotify results, which are used for information/cover art only.
    /// </summary>
    public string? StreamUrl { get; set; }

    /// <summary>Cover / thumbnail image URL.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Set when the track is stored in the user's library.</summary>
    public DateTime? AddedToLibraryAt { get; set; }

    // ----- Navigation (optional relational links populated when persisted) -----

    public int? ArtistId { get; set; }
    public Artist? Artist { get; set; }

    public int? AlbumId { get; set; }
    public Album? Album { get; set; }

    /// <summary>True when this track can actually be played (has a stream URL).</summary>
    [NotMapped]
    public bool IsPlayable => !string.IsNullOrWhiteSpace(StreamUrl);

    /// <summary>Human readable duration, e.g. 3:45.</summary>
    [NotMapped]
    public string DurationDisplay =>
        DurationSeconds <= 0
            ? "--:--"
            : TimeSpan.FromSeconds(DurationSeconds).ToString(DurationSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    /// <summary>Short info line for lists: album · duration · source.</summary>
    [NotMapped]
    public string InfoLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(AlbumName)) parts.Add(AlbumName);
            if (DurationSeconds > 0) parts.Add(DurationDisplay);
            return parts.Count > 0 ? string.Join(" · ", parts) : Title;
        }
    }

    /// <summary>True when two tracks refer to the same provider item.</summary>
    public bool Matches(Track? other) =>
        other != null && Source == other.Source && SourceId == other.SourceId;
}
