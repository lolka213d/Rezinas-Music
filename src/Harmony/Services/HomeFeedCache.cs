using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harmony.Data;
using Harmony.Models;
using Harmony.ViewModels;

namespace Harmony.Services;

/// <summary>Persists the last successful Deezer home chart payload for instant cold start.</summary>
public sealed class HomeFeedCache
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);

    private static string CacheFileFor(string language) =>
        Path.Combine(AppPaths.CacheFolder, $"home-feed-{(language ?? "en").ToLowerInvariant()}.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public async Task<HomeFeedSnapshot?> ReadAsync(string language)
    {
        try
        {
            var cacheFile = CacheFileFor(language);
            if (!File.Exists(cacheFile)) return null;
            await using var stream = File.OpenRead(cacheFile);
            var snap = await JsonSerializer.DeserializeAsync<HomeFeedSnapshot>(stream, JsonOptions);
            if (snap == null || snap.SavedAtUtc == default) return null;
            if (DateTime.UtcNow - snap.SavedAtUtc > MaxAge) return null;
            if (!string.Equals(snap.Language, language, StringComparison.OrdinalIgnoreCase)) return null;
            return snap;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(string language, IReadOnlyList<Track> tracks, IReadOnlyList<HomeAlbumCard> albums)
    {
        try
        {
            var cacheFile = CacheFileFor(language);
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
            var snap = new HomeFeedSnapshot
            {
                Language = language,
                SavedAtUtc = DateTime.UtcNow,
                Tracks = tracks.ToList(),
                Albums = albums.Select(a => new CachedAlbumCard
                {
                    Title = a.Title,
                    ArtistName = a.ArtistName,
                    ThumbnailUrl = a.ThumbnailUrl,
                    SourceId = a.SourceId
                }).ToList()
            };
            await using var stream = File.Create(cacheFile);
            await JsonSerializer.SerializeAsync(stream, snap, JsonOptions);
        }
        catch
        {
            // Non-critical.
        }
    }

    public sealed class HomeFeedSnapshot
    {
        public string Language { get; set; } = "en";
        public DateTime SavedAtUtc { get; set; }
        public List<Track> Tracks { get; set; } = [];
        public List<CachedAlbumCard> Albums { get; set; } = [];
    }

    public sealed class CachedAlbumCard
    {
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string? SourceId { get; set; }
    }

    public static List<HomeAlbumCard> ToAlbumCards(IReadOnlyList<CachedAlbumCard> cards) =>
        cards.Select(c => new HomeAlbumCard
        {
            Title = c.Title,
            ArtistName = c.ArtistName,
            ThumbnailUrl = c.ThumbnailUrl,
            SourceId = c.SourceId
        }).ToList();
}
