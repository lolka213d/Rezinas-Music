using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

public sealed record SmartPlaylist(string Id, string TitleKey, string SubtitleKey, IReadOnlyList<Track> Tracks);

/// <summary>Auto-generated playlists from library + history.</summary>
public sealed class SmartPlaylistService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILibraryService _library;

    public SmartPlaylistService(IDbContextFactory<AppDbContext> factory, ILibraryService library)
    {
        _factory = factory;
        _library = library;
    }

    public async Task<IReadOnlyList<SmartPlaylist>> BuildAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var library = (await _library.GetLibraryAsync()).ToList();
        var historyKeys = await db.ListeningHistory
            .Include(h => h.Track)
            .AsNoTracking()
            .Select(h => new { h.Track!.Source, h.Track.SourceId, h.PlayedAt })
            .ToListAsync(ct);

        var playedKeys = historyKeys
            .Select(h => FavoriteLookup.Key(h.Source, h.SourceId))
            .ToHashSet(StringComparer.Ordinal);

        var year = DateTime.UtcNow.Year;
        var fromYear = library
            .Where(t => historyKeys.Any(h =>
                h.Source == t.Source && h.SourceId == t.SourceId && h.PlayedAt.Year == year))
            .DistinctBy(t => FavoriteLookup.Key(t))
            .Take(48)
            .ToList();

        if (fromYear.Count < 3)
        {
            fromYear = library
                .Where(t => t.AddedToLibraryAt?.Year == year)
                .Take(48)
                .ToList();
        }

        var neverPlayed = library
            .Where(t => !playedKeys.Contains(FavoriteLookup.Key(t)))
            .Take(48)
            .ToList();

        return
        [
            new SmartPlaylist("from-year", "smart.fromYear", "smart.fromYearSub", fromYear),
            new SmartPlaylist("never-played", "smart.neverPlayed", "smart.neverPlayedSub", neverPlayed)
        ];
    }
}
