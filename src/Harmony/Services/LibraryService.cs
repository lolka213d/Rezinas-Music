using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>SQLite-backed implementation of <see cref="ILibraryService"/>.</summary>
public sealed class LibraryService : ILibraryService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LibraryService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<int> EnsureTrackAsync(Track track)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await TrackPersistence.EnsureAsync(db, track);
        return entity.Id;
    }

    public async Task AddToLibraryAsync(Track track)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await TrackPersistence.EnsureAsync(db, track);
        entity.AddedToLibraryAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task RemoveFromLibraryAsync(int trackId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.Tracks.FindAsync(trackId);
        if (entity != null)
        {
            // Keep the row if it is still referenced by favorites/history/playlists;
            // simply mark it as no longer in the library.
            entity.AddedToLibraryAt = null;
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> IsInLibraryAsync(MusicSource source, string sourceId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Tracks.AnyAsync(t =>
            t.Source == source && t.SourceId == sourceId && t.AddedToLibraryAt != null);
    }

    public async Task<IReadOnlyList<Track>> GetLibraryAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Tracks
            .Where(t => t.AddedToLibraryAt != null)
            .OrderByDescending(t => t.AddedToLibraryAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetLibraryArtistsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Tracks
            .Where(t => t.AddedToLibraryAt != null && t.ArtistName != "")
            .Select(t => t.ArtistName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
    }
}
