using Harmony.Data;
using Harmony.Models;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>Lightweight listening statistics from history.</summary>
public sealed class ListeningStatsService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ListeningStatsService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public Task<ListeningStats> GetWeeklyAsync(CancellationToken ct = default) =>
        GetAsync(StatsPeriod.Week, ct);

    public async Task<ListeningStats> GetAsync(StatsPeriod period, CancellationToken ct = default)
    {
        var since = period switch
        {
            StatsPeriod.Month => DateTime.UtcNow.AddDays(-30),
            StatsPeriod.Year => DateTime.UtcNow.AddDays(-365),
            _ => DateTime.UtcNow.AddDays(-7)
        };

        await using var db = await _factory.CreateDbContextAsync(ct);
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

        var topTracks = entries
            .Where(e => e.Track != null)
            .GroupBy(e => e.Track!)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => new TrackPlayStat(g.Key.Title, g.Key.ArtistName, g.Count()))
            .ToList();

        return new ListeningStats(entries.Count, totalSeconds, topArtists, topTracks, period);
    }
}

public sealed record ListeningStats(
    int PlayCount,
    int TotalSeconds,
    IReadOnlyList<ArtistPlayStat> TopArtists,
    IReadOnlyList<TrackPlayStat> TopTracks,
    StatsPeriod Period);

public sealed record ArtistPlayStat(string ArtistName, int PlayCount);

public sealed record TrackPlayStat(string Title, string ArtistName, int PlayCount);
