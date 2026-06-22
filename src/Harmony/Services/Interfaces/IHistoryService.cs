using Harmony.Models;

namespace Harmony.Services.Interfaces;

/// <summary>A history row enriched with play-count for display.</summary>
public record HistoryItem(Track Track, DateTime PlayedAt, int PlayCount);

/// <summary>Records and queries listening history.</summary>
public interface IHistoryService
{
    /// <summary>Record that a track has just started playing.</summary>
    Task RecordAsync(Track track);

    /// <summary>Most recent history rows (newest first).</summary>
    Task<IReadOnlyList<HistoryItem>> GetHistoryAsync(int limit = 200);

    /// <summary>Distinct recent tracks for dashboards (lightweight query).</summary>
    Task<IReadOnlyList<Track>> GetRecentTracksAsync(int limit = 30);

    /// <summary>Lookup a persisted track by provider id.</summary>
    Task<Track?> FindTrackAsync(MusicSource source, string sourceId);

    /// <summary>One row per unique track (most recent play).</summary>
    Task<IReadOnlyList<HistoryItem>> GetUniqueHistoryAsync(int limit = 100);

    /// <summary>Delete the entire listening history.</summary>
    Task ClearAsync();
}
