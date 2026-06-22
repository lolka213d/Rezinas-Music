using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

public sealed class AlbumService : IAlbumService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AlbumService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<Album>> GetUserAlbumsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Albums
            .Where(a => a.IsUserCreated)
            .Include(a => a.Items)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<Album?> GetUserAlbumAsync(int albumId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Albums
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == albumId && a.IsUserCreated);
    }

    public async Task<Album> CreateAsync(string name, string? artistName = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var album = new Album
        {
            Name = string.IsNullOrWhiteSpace(name) ? "New album" : name.Trim(),
            ArtistName = string.IsNullOrWhiteSpace(artistName) ? "Various artists" : artistName.Trim(),
            IsUserCreated = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        return album;
    }

    public async Task RenameAsync(int albumId, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var album = await db.Albums.FindAsync(albumId);
        if (album != null && album.IsUserCreated && !string.IsNullOrWhiteSpace(newName))
        {
            album.Name = newName.Trim();
            await db.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int albumId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var album = await db.Albums.FindAsync(albumId);
        if (album != null && album.IsUserCreated)
        {
            db.Albums.Remove(album);
            await db.SaveChangesAsync();
        }
    }

    public async Task AddTrackAsync(int albumId, Track track)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var album = await db.Albums.FindAsync(albumId);
        if (album is not { IsUserCreated: true }) return;

        var entity = await TrackPersistence.EnsureAsync(db, track);

        if (string.IsNullOrWhiteSpace(album.ImageUrl) && !string.IsNullOrWhiteSpace(entity.ThumbnailUrl))
            album.ImageUrl = entity.ThumbnailUrl;

        var exists = await db.AlbumTracks.AnyAsync(at => at.AlbumId == albumId && at.TrackId == entity.Id);
        if (exists) return;

        var position = await db.AlbumTracks.CountAsync(at => at.AlbumId == albumId);
        db.AlbumTracks.Add(new AlbumTrack
        {
            AlbumId = albumId,
            TrackId = entity.Id,
            Position = position
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveTrackAsync(int albumId, int trackId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var item = await db.AlbumTracks
            .FirstOrDefaultAsync(at => at.AlbumId == albumId && at.TrackId == trackId);
        if (item != null)
        {
            db.AlbumTracks.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<Track>> GetTracksAsync(int albumId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AlbumTracks
            .Where(at => at.AlbumId == albumId)
            .OrderBy(at => at.Position)
            .Include(at => at.Track)
            .Select(at => at.Track!)
            .ToListAsync();
    }

    public async Task SetImageAsync(int albumId, string? imagePath)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var album = await db.Albums.FindAsync(albumId);
        if (album is not { IsUserCreated: true }) return;
        album.ImageUrl = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath.Trim();
        await db.SaveChangesAsync();
    }
}
