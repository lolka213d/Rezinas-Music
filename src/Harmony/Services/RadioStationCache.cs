using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harmony.Data;
using Harmony.Models;

namespace Harmony.Services;

/// <summary>Persists daily «My Wave» station playlists until the local calendar day changes.</summary>
public sealed class RadioStationCache
{
    private static string CacheFileFor(string stationId) =>
        Path.Combine(AppPaths.CacheFolder, $"radio-v2-{stationId}.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public async Task<RadioStationSnapshot?> ReadAsync(string stationId, int dayKey)
    {
        try
        {
            var path = CacheFileFor(stationId);
            if (!File.Exists(path)) return null;

            await using var stream = File.OpenRead(path);
            var snap = await JsonSerializer.DeserializeAsync<RadioStationSnapshot>(stream, JsonOptions);
            if (snap == null || snap.DayKey != dayKey || snap.Tracks.Count == 0) return null;
            return snap;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(string stationId, int dayKey, int startIndex, IReadOnlyList<Track> tracks)
    {
        try
        {
            var path = CacheFileFor(stationId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var snap = new RadioStationSnapshot
            {
                StationId = stationId,
                DayKey = dayKey,
                StartIndex = startIndex,
                SavedAtUtc = DateTime.UtcNow,
                Tracks = tracks.ToList()
            };
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, snap, JsonOptions);
        }
        catch
        {
            // Non-critical.
        }
    }

    public sealed class RadioStationSnapshot
    {
        public string StationId { get; set; } = string.Empty;
        public int DayKey { get; set; }
        public int StartIndex { get; set; }
        public DateTime SavedAtUtc { get; set; }
        public List<Track> Tracks { get; set; } = [];
    }
}
