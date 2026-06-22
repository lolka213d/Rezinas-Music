using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harmony.Data;
using Harmony.Models;

namespace Harmony.Services;

/// <summary>Persists the home «daily mix» until the local calendar day changes.</summary>
public sealed class DailyMixCache
{
    private static string CacheFile => Path.Combine(AppPaths.CacheFolder, "daily-mix.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<DailyMixSnapshot?> ReadAsync(int dayKey)
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;
            await using var stream = File.OpenRead(CacheFile);
            var snap = await JsonSerializer.DeserializeAsync<DailyMixSnapshot>(stream, JsonOptions);
            if (snap == null || snap.DayKey != dayKey || snap.Tracks.Count == 0) return null;
            return snap;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(int dayKey, IReadOnlyList<Track> tracks)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.CacheFolder);
            var snap = new DailyMixSnapshot
            {
                DayKey = dayKey,
                SavedAtUtc = DateTime.UtcNow,
                Tracks = tracks.ToList()
            };
            await using var stream = File.Create(CacheFile);
            await JsonSerializer.SerializeAsync(stream, snap, JsonOptions);
        }
        catch
        {
            // Non-critical.
        }
    }

    public sealed class DailyMixSnapshot
    {
        public int DayKey { get; set; }
        public DateTime SavedAtUtc { get; set; }
        public List<Track> Tracks { get; set; } = [];
    }
}
