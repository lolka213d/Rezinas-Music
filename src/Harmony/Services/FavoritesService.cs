using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>SQLite-backed implementation of <see cref="IFavoritesService"/>.</summary>
public sealed class FavoritesService : IFavoritesService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly FavoriteLookup _lookup;

    public FavoritesService(IDbContextFactory<AppDbContext> factory, FavoriteLookup lookup)
    {
        _factory = factory;
        _lookup = lookup;
    }
    public async Task<bool> ToggleAsync(Track track)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await TrackPersistence.EnsureAsync(db, track);

        var existing = await db.Favorites.FirstOrDefaultAsync(f => f.TrackId == entity.Id);
        if (existing != null)
        {
            db.Favorites.Remove(existing);
            await db.SaveChangesAsync();
            _lookup.ApplyToggle(track, false);
            return false;
        }

        db.Favorites.Add(new Favorite { TrackId = entity.Id });
        await db.SaveChangesAsync();
        _lookup.ApplyToggle(track, true);
        return true;
    }

    public async Task<bool> IsFavoriteAsync(MusicSource source, string sourceId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Favorites
            .Include(f => f.Track)
            .AnyAsync(f => f.Track!.Source == source && f.Track.SourceId == sourceId);
    }

    public async Task<IReadOnlyList<Track>> GetFavoritesAsync(int limit = 500)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Favorites.AsNoTracking()
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.Track!)
            .Take(Math.Max(1, limit))
            .ToListAsync();
    }

    public async Task<bool> EnsureFavoriteAsync(Track track)
    {
        if (await IsFavoriteAsync(track.Source, track.SourceId))
            return false;

        await using var db = await _factory.CreateDbContextAsync();
        var entity = await TrackPersistence.EnsureAsync(db, track);
        db.Favorites.Add(new Favorite { TrackId = entity.Id });
        await db.SaveChangesAsync();
        _lookup.ApplyToggle(track, true);
        return true;
    }

    public async Task ClearAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Favorites.RemoveRange(db.Favorites);
        await db.SaveChangesAsync();
        await _lookup.RefreshAsync();
    }
}
