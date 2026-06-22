using Harmony.Data;
using Harmony.Models;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>Lightweight listening statistics from history.</summary>
public sealed class ListeningStatsService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ListeningStatsService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<ListeningStats> GetWeeklyAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var since = DateTime.UtcNow.AddDays(-7);
        var entries = await db.ListeningHistory
            .Where(h => h.PlayedAt >= since)
            .Include(h => h.Track)
            .AsNoTracking()
            .ToListAsync(ct);

        var totalSeconds = entries.Sum(e => e.Track?.DurationSeconds ?? 0);
        var topArtists = entries
            .Where(e => e.Track != null)
            .GroupBy(e => e.Track!.ArtistName)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => new ArtistPlayStat(g.Key, g.Count()))
            .ToList();

        return new ListeningStats(entries.Count, totalSeconds, topArtists);
    }
}

public sealed record ListeningStats(int PlayCount, int TotalSeconds, IReadOnlyList<ArtistPlayStat> TopArtists);

public sealed record ArtistPlayStat(string ArtistName, int PlayCount);
