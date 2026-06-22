using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>SQLite-backed implementation of <see cref="IHistoryService"/>.</summary>
public sealed class HistoryService : IHistoryService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public HistoryService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task RecordAsync(Track track)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await TrackPersistence.EnsureAsync(db, track);
        db.ListeningHistory.Add(new ListeningHistoryEntry { TrackId = entity.Id });
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Track>> GetRecentTracksAsync(int limit = 30)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.ListeningHistory.AsNoTracking()
            .Include(h => h.Track)
            .OrderByDescending(h => h.PlayedAt)
            .Take(Math.Max(limit * 4, limit))
            .ToListAsync();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Track>(limit);
        foreach (var row in rows)
        {
            if (row.Track == null) continue;
            var key = $"{row.Track.Source}|{row.Track.SourceId}";
            if (!seen.Add(key)) continue;
            result.Add(row.Track);
            if (result.Count >= limit) break;
        }
        return result;
    }

    public async Task<Track?> FindTrackAsync(MusicSource source, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return null;
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Tracks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Source == source && t.SourceId == sourceId);
    }

    public async Task<IReadOnlyList<HistoryItem>> GetHistoryAsync(int limit = 200)
    {
        await using var db = await _factory.CreateDbContextAsync();

        // Play counts per track.
        var counts = await db.ListeningHistory.AsNoTracking()
            .GroupBy(h => h.TrackId)
            .Select(g => new { TrackId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TrackId, x => x.Count);

        var rows = await db.ListeningHistory.AsNoTracking()
            .Include(h => h.Track)
            .OrderByDescending(h => h.PlayedAt)
            .Take(limit)
            .ToListAsync();

        return rows
            .Where(h => h.Track != null)
            .Select(h => new HistoryItem(
                h.Track!,
                h.PlayedAt,
                counts.TryGetValue(h.TrackId, out var c) ? c : 1))
            .ToList();
    }

    public async Task<IReadOnlyList<HistoryItem>> GetUniqueHistoryAsync(int limit = 100)
    {
        var all = await GetHistoryAsync(400);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<HistoryItem>();
        foreach (var item in all)
        {
            var key = $"{item.Track.Source}|{item.Track.SourceId}";
            if (!seen.Add(key)) continue;
            result.Add(item);
            if (result.Count >= limit) break;
        }
        return result;
    }

    public async Task ClearAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.ListeningHistory.RemoveRange(db.ListeningHistory);
        await db.SaveChangesAsync();
    }
}
