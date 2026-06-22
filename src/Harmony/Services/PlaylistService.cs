using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>SQLite-backed implementation of <see cref="IPlaylistService"/>.</summary>
public sealed class PlaylistService : IPlaylistService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PlaylistService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<Playlist>> GetPlaylistsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Playlists
            .Include(p => p.Items)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Playlist> CreateAsync(string name)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var playlist = new Playlist { Name = string.IsNullOrWhiteSpace(name) ? "New playlist" : name.Trim() };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();
        return playlist;
    }

    public async Task RenameAsync(int playlistId, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var playlist = await db.Playlists.FindAsync(playlistId);
        if (playlist != null && !string.IsNullOrWhiteSpace(newName))
        {
            playlist.Name = newName.Trim();
            await db.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int playlistId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var playlist = await db.Playlists.FindAsync(playlistId);
        if (playlist != null)
        {
            db.Playlists.Remove(playlist); // cascade removes PlaylistTracks
            await db.SaveChangesAsync();
        }
    }

    public async Task AddTrackAsync(int playlistId, Track track)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await TrackPersistence.EnsureAsync(db, track);

        var alreadyThere = await db.PlaylistTracks
            .AnyAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == entity.Id);
        if (alreadyThere) return;

        var nextPosition = await db.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .CountAsync();

        db.PlaylistTracks.Add(new PlaylistTrack
        {
            PlaylistId = playlistId,
            TrackId = entity.Id,
            Position = nextPosition
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveTrackAsync(int playlistId, int trackId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var item = await db.PlaylistTracks
            .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);
        if (item != null)
        {
            db.PlaylistTracks.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<Track>> GetTracksAsync(int playlistId)
    {
        var entries = await GetTrackEntriesAsync(playlistId);
        return entries.Select(e => e.Track).ToList();
    }

    public async Task<IReadOnlyList<PlaylistTrackEntry>> GetTrackEntriesAsync(int playlistId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.Position)
            .Include(pt => pt.Track)
            .Select(pt => new PlaylistTrackEntry
            {
                Track = pt.Track!,
                Position = pt.Position,
                AddedAt = pt.AddedAt
            })
            .ToListAsync();
    }
}
