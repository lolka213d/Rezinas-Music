using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>Personal «My Wave» mix from listening history, favorites, and fresh chart tracks.</summary>
public sealed class PersonalWaveService
{
    private const double FreshRatio = 0.28;

    private readonly IHistoryService _history;
    private readonly IFavoritesService _favorites;
    private readonly DeezerHomeService _deezer;
    private readonly ISettingsService _settings;

    public PersonalWaveService(
        IHistoryService history,
        IFavoritesService favorites,
        DeezerHomeService deezer,
        ISettingsService settings)
    {
        _history = history;
        _favorites = favorites;
        _deezer = deezer;
        _settings = settings;
    }

    public async Task<IReadOnlyList<Track>> GetDailyTracksAsync(int limit = 40, CancellationToken ct = default)
    {
        var dayKey = RadioDailySeed.TodayKey;
        var rng = RadioDailySeed.CreateRandom("personal-wave", dayKey);

        var recent = (await _history.GetRecentTracksAsync(80)).ToList();
        var favorites = (await _favorites.GetFavoritesAsync(80)).ToList();
        var personalPool = recent
            .Concat(favorites)
            .DistinctBy(t => $"{t.Source}:{t.SourceId}")
            .ToList();

        var topArtists = personalPool
            .GroupBy(t => t.ArtistName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var personalTracks = personalPool
            .Where(t => topArtists.Count == 0 || topArtists.Contains(t.ArtistName))
            .OrderBy(_ => rng.Next())
            .Take(Math.Max(limit - 8, (int)(limit * (1 - FreshRatio))))
            .ToList();

        var freshTake = Math.Max(8, limit - personalTracks.Count);
        var fresh = await _deezer.GetRegionalChartTracksAsync(
            _settings.Current.Language, 1, freshTake, ct);
        var freshFiltered = fresh
            .Where(t => !personalTracks.Any(p => p.Matches(t)))
            .OrderBy(_ => rng.Next())
            .Take(freshTake)
            .ToList();

        var merged = personalTracks
            .Concat(freshFiltered)
            .DistinctBy(t => $"{t.Source}:{t.SourceId}")
            .ToList();

        if (merged.Count < 5)
        {
            var fallback = await _deezer.GetRegionalChartTracksAsync(
                _settings.Current.Language, 1, limit, ct);
            merged = fallback.Take(limit).ToList();
        }

        return RadioDailySeed.ShuffleForDay(merged, "personal-wave", dayKey).Take(limit).ToList();
    }
}
