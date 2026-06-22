using Harmony.Models;

namespace Harmony.Services;

/// <summary>Deterministic per-day seeds so «My Wave» refreshes once every 24 hours.</summary>
public static class RadioDailySeed
{
    public static int TodayKey => DateOnly.FromDateTime(DateTime.Now).DayNumber;

    public static Random CreateRandom(string stationId, int dayKey, int salt = 0)
    {
        var seed = HashCode.Combine(stationId ?? "", dayKey, salt);
        return new Random(seed);
    }

    public static List<Track> ShuffleForDay(IReadOnlyList<Track> tracks, string stationId, int dayKey)
    {
        if (tracks.Count <= 1) return tracks.ToList();

        var rng = CreateRandom(stationId, dayKey);
        return tracks.OrderBy(_ => rng.Next()).ToList();
    }
}
