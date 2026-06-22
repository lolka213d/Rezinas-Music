using Harmony.Data;
using Harmony.Models;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>
/// Shared logic for turning a transient track (e.g. a search result) into a
/// persisted row, reusing an existing row when the same Source + SourceId was
/// already saved, and upserting the related Artist/Album records.
/// </summary>
internal static class TrackPersistence
{
    /// <summary>
    /// Returns the persisted <see cref="Track"/> for the given transient track,
    /// creating it (and its artist/album) if necessary.
    /// </summary>
    public static async Task<Track> EnsureAsync(AppDbContext db, Track track)
    {
        var existing = await db.Tracks
            .FirstOrDefaultAsync(t => t.Source == track.Source && t.SourceId == track.SourceId);
        if (existing != null)
            return existing;

        // Upsert artist by name.
        Artist? artist = null;
        if (!string.IsNullOrWhiteSpace(track.ArtistName))
        {
            artist = await db.Artists.FirstOrDefaultAsync(a => a.Name == track.ArtistName);
            if (artist == null)
            {
                artist = new Artist { Name = track.ArtistName, ImageUrl = track.ThumbnailUrl };
                db.Artists.Add(artist);
            }
        }

        // Upsert album by name.
        Album? album = null;
        if (!string.IsNullOrWhiteSpace(track.AlbumName))
        {
            album = await db.Albums.FirstOrDefaultAsync(a => a.Name == track.AlbumName);
            if (album == null)
            {
                album = new Album
                {
                    Name = track.AlbumName!,
                    ArtistName = track.ArtistName,
                    ImageUrl = track.ThumbnailUrl
                };
                db.Albums.Add(album);
            }
        }

        var entity = new Track
        {
            Title = track.Title,
            ArtistName = track.ArtistName,
            AlbumName = track.AlbumName,
            DurationSeconds = track.DurationSeconds,
            Source = track.Source,
            SourceId = track.SourceId,
            StreamUrl = track.StreamUrl,
            ThumbnailUrl = track.ThumbnailUrl,
            Artist = artist,
            Album = album
        };
        db.Tracks.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }
}
