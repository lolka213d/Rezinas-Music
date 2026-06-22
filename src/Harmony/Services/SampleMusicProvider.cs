using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>
/// A built-in catalog of royalty-free tracks (SoundHelix, free to use for
/// testing). This makes the MVP fully playable without any API keys and gives
/// the Home screen something to display. All tracks expose a real, legal,
/// directly streamable MP3 URL.
/// </summary>
public sealed class SampleMusicProvider : IMusicSearchService
{
    private static readonly IReadOnlyList<Track> Catalog = BuildCatalog();

    public MusicSource Source => MusicSource.Sample;
    public string DisplayName => "Demo catalog";
    public bool IsAvailable => true;
    public string? LastError { get; private set; }

    public Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(Catalog);

        var q = query.Trim();
        IReadOnlyList<Track> result = Catalog
            .Where(t => t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || t.ArtistName.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || (t.AlbumName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        return Task.FromResult(result);
    }

    /// <summary>Returns the full demo catalog (used by the Home screen).</summary>
    public IReadOnlyList<Track> GetCatalog() => Catalog;

    private static IReadOnlyList<Track> BuildCatalog()
    {
        var artists = new[] { "Helix Ensemble", "Nightdrive", "Aurora Keys", "Lo-Fi Cats", "The Synths" };
        var albums = new[] { "Open Frequencies", "Midnight Tape", "Glass Horizons", "Slow Hours", "Voltage" };

        var list = new List<Track>();
        for (var i = 1; i <= 12; i++)
        {
            list.Add(new Track
            {
                Title = $"Song {i}",
                ArtistName = artists[(i - 1) % artists.Length],
                AlbumName = albums[(i - 1) % albums.Length],
                DurationSeconds = 0, // filled in by the player once loaded
                Source = MusicSource.Sample,
                SourceId = $"soundhelix-{i}",
                StreamUrl = $"https://www.soundhelix.com/examples/mp3/SoundHelix-Song-{i}.mp3",
                ThumbnailUrl = null
            });
        }
        return list;
    }
}
