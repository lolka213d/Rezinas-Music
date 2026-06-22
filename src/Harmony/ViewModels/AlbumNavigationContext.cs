using Harmony.Models;

namespace Harmony.ViewModels;

/// <summary>Payload for opening the album detail page.</summary>
public sealed class AlbumNavigationContext
{
    /// <summary>User album in SQLite.</summary>
    public int? UserAlbumId { get; init; }

    /// <summary>Deezer album id for remote catalog albums.</summary>
    public string? DeezerAlbumId { get; init; }

    public string Title { get; init; } = string.Empty;
    public string ArtistName { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public int? Year { get; init; }

    /// <summary>When no Deezer id (e.g. album from favorites), show this single track.</summary>
    public Track? FallbackTrack { get; init; }

    /// <summary>Pre-built track list (chart section, recently played, etc.).</summary>
    public IReadOnlyList<Track>? PreloadedTracks { get; init; }

    /// <summary>Track to highlight when opening a preloaded list.</summary>
    public Track? InitialTrack { get; init; }

    public static AlbumNavigationContext FromTrackList(string title, IReadOnlyList<Track> tracks, Track focus) => new()
    {
        Title = title,
        ArtistName = focus.ArtistName,
        ThumbnailUrl = focus.ThumbnailUrl ?? tracks.FirstOrDefault()?.ThumbnailUrl,
        PreloadedTracks = tracks,
        InitialTrack = focus
    };
    public static AlbumNavigationContext FromCard(HomeAlbumCard card) => new()
    {
        DeezerAlbumId = card.SourceId,
        Title = card.Title,
        ArtistName = card.ArtistName,
        ThumbnailUrl = card.ThumbnailUrl,
        FallbackTrack = string.IsNullOrWhiteSpace(card.SourceId) ? card.PlayTrack : null
    };

    public static AlbumNavigationContext FromUserAlbum(Models.Album album) => new()
    {
        UserAlbumId = album.Id,
        Title = album.Name,
        ArtistName = album.ArtistName ?? "Various artists",
        ThumbnailUrl = album.ImageUrl,
        Year = album.Year
    };
}
